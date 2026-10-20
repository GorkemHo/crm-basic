import { CdkDrag, CdkDragDrop, CdkDropList, CdkDropListGroup } from '@angular/cdk/drag-drop';
import { Component, effect, inject, signal, untracked } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatTooltipModule } from '@angular/material/tooltip';
import { Router, RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { filter } from 'rxjs';

import { BoardColumn, DealDto } from '../../core/api/models';
import { AuthStore } from '../../core/auth/auth.store';
import { Permissions } from '../../core/auth/permissions';
import { AppDatePipe } from '../../core/i18n/date.pipe';
import { AppMoneyPipe } from '../../core/i18n/money.pipe';
import { NotifyService } from '../../core/ui/notify.service';
import { PageHeader } from '../../shared/page-header';
import { CompanySelect } from '../companies/company-select';
import { DealFormDialog } from './deal-form-dialog';
import { dateOnly, isOverdue } from './deal-status';
import { DealsApi } from './deals.api';

/** Kanban board of open deals; drag a card to another column to move the deal to that stage. */
@Component({
  selector: 'app-deal-board',
  imports: [
    RouterLink, TranslocoPipe, MatButtonModule, MatProgressBarModule, MatTooltipModule,
    CdkDropListGroup, CdkDropList, CdkDrag, FormsModule, PageHeader, AppMoneyPipe, AppDatePipe, CompanySelect,
  ],
  template: `
    <app-page-header [title]="'pipeline.title' | transloco" [subtitle]="'pipeline.subtitle' | transloco">
      <a matButton="outlined" routerLink="/deals"><span class="material-symbols-outlined">table_rows</span> {{ 'pipeline.listView' | transloco }}</a>
      @if (canManageStages) {
        <a matButton="outlined" routerLink="/pipeline/stages"><span class="material-symbols-outlined">tune</span> {{ 'pipeline.manageStages' | transloco }}</a>
      }
      @if (canWrite) {
        <button matButton="filled" (click)="create()"><span class="material-symbols-outlined">add</span> {{ 'deals.new' | transloco }}</button>
      }
    </app-page-header>

    <div class="toolbar">
      <app-company-select
        class="company-filter"
        [label]="'deals.company' | transloco"
        [clearable]="true"
        [ngModel]="companyFilter()"
        (ngModelChange)="companyFilter.set($event)"
      />
    </div>

    @if (loading()) {
      <mat-progress-bar mode="indeterminate" />
    }

    <div class="board" cdkDropListGroup>
      @for (column of columns(); track column.stageId) {
        <section class="column" [attr.data-stage]="column.name">
          <header>
            <div class="column-title">
              <h2>{{ column.name }}</h2>
              <span class="count">{{ column.deals.length }}</span>
            </div>
            <span class="muted probability">{{ 'pipeline.probability' | transloco: { n: column.probability } }}</span>
            <div class="totals">
              @for (total of column.totals; track total.currency) {
                <span class="total" [matTooltip]="'pipeline.weighted' | transloco: { value: (total.weightedAmount | appMoney: total.currency) }">
                  {{ total.amount | appMoney: total.currency : true }}
                </span>
              }
            </div>
          </header>

          <div
            class="cards"
            cdkDropList
            [cdkDropListData]="column"
            [cdkDropListDisabled]="!canWrite"
            (cdkDropListDropped)="drop($event)"
          >
            @for (deal of column.deals; track deal.id) {
              <div
                class="deal-card"
                role="link"
                tabindex="0"
                cdkDrag
                [cdkDragData]="deal"
                (cdkDragStarted)="dragging = true"
                (cdkDragEnded)="dragEnded()"
                (click)="open(deal)"
                (keydown.enter)="open(deal)"
              >
                <strong>{{ deal.title }}</strong>
                <span class="muted company">{{ deal.companyName }}</span>
                <div class="meta">
                  <span class="amount">{{ deal.amount | appMoney: deal.currency }}</span>
                  @if (deal.expectedCloseDate) {
                    <span class="date" [class.overdue]="overdue(deal)">
                      <span class="material-symbols-outlined">event</span>{{ toDate(deal.expectedCloseDate) | appDate }}
                    </span>
                  }
                </div>
              </div>
            } @empty {
              <p class="muted empty">{{ 'pipeline.emptyColumn' | transloco }}</p>
            }
          </div>

          @if (canWrite) {
            <button matButton class="add" (click)="create(column.stageId)">
              <span class="material-symbols-outlined">add</span> {{ 'pipeline.addHere' | transloco }}
            </button>
          }
        </section>
      }
    </div>
  `,
  styles: `
    a .material-symbols-outlined,
    button .material-symbols-outlined {
      margin-right: 0.25rem;
    }
    .company-filter {
      display: block;
      width: min(20rem, 100%);
    }
    .board {
      display: grid;
      grid-auto-flow: column;
      grid-auto-columns: minmax(16rem, 1fr);
      gap: var(--app-gap);
      overflow-x: auto;
      padding-bottom: 0.5rem;
      align-items: start;
    }
    .column {
      background: var(--mat-sys-surface-container-low);
      border: 1px solid var(--mat-sys-outline-variant);
      border-radius: var(--app-radius);
      padding: 0.75rem;
      display: flex;
      flex-direction: column;
      gap: 0.5rem;
      min-height: 12rem;
    }
    .column-title {
      display: flex;
      align-items: center;
      gap: 0.5rem;
    }
    h2 {
      font: var(--mat-sys-title-medium);
      margin: 0;
    }
    .count {
      font: var(--mat-sys-label-medium);
      background: var(--mat-sys-surface-container-highest);
      border-radius: 1rem;
      padding: 0 0.5rem;
    }
    .probability {
      font: var(--mat-sys-label-small);
    }
    .totals {
      display: flex;
      flex-wrap: wrap;
      gap: 0.25rem 0.75rem;
      font: var(--mat-sys-label-large);
      margin-top: 0.25rem;
    }
    .cards {
      display: flex;
      flex-direction: column;
      gap: 0.5rem;
      min-height: 4rem;
      flex: 1;
    }
    .deal-card {
      display: flex;
      flex-direction: column;
      gap: 0.25rem;
      padding: 0.75rem;
      border-radius: 0.75rem;
      background: var(--mat-sys-surface);
      border: 1px solid var(--mat-sys-outline-variant);
      color: inherit;
      text-decoration: none !important;
      cursor: grab;
    }
    .deal-card:hover {
      border-color: var(--mat-sys-primary);
    }
    .company {
      font: var(--mat-sys-body-small);
    }
    .meta {
      display: flex;
      justify-content: space-between;
      align-items: center;
      gap: 0.5rem;
      margin-top: 0.25rem;
    }
    .amount {
      font: var(--mat-sys-label-large);
    }
    .date {
      display: inline-flex;
      align-items: center;
      gap: 0.15rem;
      font: var(--mat-sys-label-small);
      color: var(--mat-sys-on-surface-variant);
    }
    .date .material-symbols-outlined {
      font-size: 1rem;
      margin: 0;
    }
    .overdue {
      color: var(--mat-sys-error);
    }
    .empty {
      text-align: center;
      font: var(--mat-sys-body-small);
      padding: 1rem 0;
    }
    .add {
      align-self: flex-start;
    }
    .cdk-drag-preview {
      box-shadow: var(--mat-sys-level3);
    }
    .cdk-drag-placeholder {
      opacity: 0.4;
    }
  `,
})
export class DealBoard {
  private readonly api = inject(DealsApi);
  private readonly dialog = inject(MatDialog);
  private readonly notify = inject(NotifyService);
  private readonly router = inject(Router);

  private readonly store = inject(AuthStore);
  readonly canWrite = this.store.can(Permissions.dealsWrite);
  readonly canManageStages = this.store.can(Permissions.stagesManage);

  readonly columns = signal<BoardColumn[]>([]);
  readonly loading = signal(true);
  readonly companyFilter = signal<number | null>(null);

  readonly toDate = dateOnly;

  /** A drop also fires a click on the card; this keeps it from opening the deal. */
  dragging = false;

  constructor() {
    // First load, and again whenever the company filter changes.
    effect(() => {
      this.companyFilter();
      untracked(() => this.load());
    });
  }

  overdue(deal: DealDto): boolean {
    return isOverdue(deal.expectedCloseDate, deal.status);
  }

  open(deal: DealDto): void {
    if (this.dragging) return;
    void this.router.navigate(['/deals', deal.id]);
  }

  dragEnded(): void {
    // The click that follows the drop runs before this.
    setTimeout(() => (this.dragging = false));
  }

  create(stageId?: number): void {
    DealFormDialog.open(this.dialog, { stageId })
      .pipe(filter(Boolean))
      .subscribe(() => this.load());
  }

  drop(event: CdkDragDrop<BoardColumn, BoardColumn, DealDto>): void {
    const target = event.container.data;
    const deal = event.item.data;
    if (event.previousContainer === event.container) return;

    // Move the card right away; the reload afterwards brings back the server's totals (or undoes it on error).
    this.columns.update((columns) =>
      columns.map((c) =>
        c.stageId === target.stageId ? { ...c, deals: [...c.deals, deal] }
        : c.stageId === deal.stageId ? { ...c, deals: c.deals.filter((d) => d.id !== deal.id) }
        : c,
      ),
    );

    this.api.move(deal.id, target.stageId).subscribe({
      next: () => this.load(),
      error: (error) => {
        this.notify.error(error);
        this.load();
      },
    });
  }

  private load(): void {
    this.loading.set(true);
    this.api.board(this.companyFilter()).subscribe({
      next: (columns) => {
        this.columns.set(columns);
        this.loading.set(false);
      },
      error: (error) => {
        this.loading.set(false);
        this.notify.error(error);
      },
    });
  }
}
