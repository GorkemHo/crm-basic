import { Component, OnInit, inject, input, numberAttribute, signal, viewChild } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatMenuModule } from '@angular/material/menu';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatTabsModule } from '@angular/material/tabs';
import { Router, RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { Observable, filter } from 'rxjs';

import { DealDto, StageDto } from '../../core/api/models';
import { AuthStore } from '../../core/auth/auth.store';
import { Permissions } from '../../core/auth/permissions';
import { AppDatePipe } from '../../core/i18n/date.pipe';
import { AppMoneyPipe } from '../../core/i18n/money.pipe';
import { ConfirmService } from '../../core/ui/confirm.service';
import { NotifyService } from '../../core/ui/notify.service';
import { HistoryTimeline } from '../../shared/history-timeline';
import { PageHeader } from '../../shared/page-header';
import { ActivityTimeline } from '../activities/activity-timeline';
import { DealFormDialog } from './deal-form-dialog';
import { DealStatusTag, LoseDealDialog, dateOnly, isOverdue } from './deal-status';
import { DealsApi, StagesApi } from './deals.api';

@Component({
  selector: 'app-deal-detail',
  imports: [
    RouterLink, TranslocoPipe, MatButtonModule, MatMenuModule, MatTabsModule, MatProgressBarModule,
    PageHeader, AppDatePipe, AppMoneyPipe, HistoryTimeline, DealStatusTag, ActivityTimeline,
  ],
  template: `
    @if (deal(); as d) {
      <app-page-header [title]="d.title" [subtitle]="d.companyName ?? ''" backLink="/pipeline">
        @if (canWrite) {
          @if (d.status === 'open') {
            <button matButton="outlined" [matMenuTriggerFor]="stageMenu" [disabled]="busy()">
              <span class="material-symbols-outlined">swap_horiz</span> {{ 'deals.moveTo' | transloco }}
            </button>
            <mat-menu #stageMenu="matMenu">
              @for (stage of stages(); track stage.id) {
                <button mat-menu-item [disabled]="stage.id === d.stageId" (click)="run(api.move(d.id, stage.id))">{{ stage.name }}</button>
              }
            </mat-menu>
            <button matButton="tonal" class="won" (click)="run(api.win(d.id))" [disabled]="busy()">
              <span class="material-symbols-outlined">emoji_events</span> {{ 'deals.markWon' | transloco }}
            </button>
            <button matButton="outlined" class="danger" (click)="lose(d)" [disabled]="busy()">
              <span class="material-symbols-outlined">thumb_down</span> {{ 'deals.markLost' | transloco }}
            </button>
          } @else {
            <button matButton="outlined" (click)="run(api.reopen(d.id))" [disabled]="busy()">
              <span class="material-symbols-outlined">undo</span> {{ 'deals.reopen' | transloco }}
            </button>
          }
          <button matButton="tonal" (click)="edit(d)"><span class="material-symbols-outlined">edit</span> {{ 'common.edit' | transloco }}</button>
        }
        @if (canDelete) {
          <button matButton="outlined" class="danger" (click)="remove(d)"><span class="material-symbols-outlined">delete</span> {{ 'common.delete' | transloco }}</button>
        }
      </app-page-header>

      <div class="card tabs-card">
        <mat-tab-group animationDuration="0ms">
          <mat-tab [label]="'companies.overview' | transloco">
            <dl class="details tab-body">
              <div><dt>{{ 'deals.amount' | transloco }}</dt><dd class="amount">{{ d.amount | appMoney: d.currency }}</dd></div>
              <div><dt>{{ 'common.status' | transloco }}</dt><dd><app-deal-status [status]="d.status" /></dd></div>
              <div><dt>{{ 'deals.stage' | transloco }}</dt><dd>{{ d.stageName }} <span class="muted">({{ d.probability }}%)</span></dd></div>
              <div>
                <dt>{{ 'deals.company' | transloco }}</dt>
                <dd><a [routerLink]="['/companies', d.companyId]">{{ d.companyName }}</a></dd>
              </div>
              <div>
                <dt>{{ 'deals.contact' | transloco }}</dt>
                <dd>
                  @if (d.customerId) {
                    <a [routerLink]="['/customers', d.customerId]">{{ d.customerName }}</a>
                  } @else {
                    {{ 'common.none' | transloco }}
                  }
                </dd>
              </div>
              <div>
                <dt>{{ 'deals.expectedClose' | transloco }}</dt>
                <dd [class.overdue]="overdue(d)">{{ (toDate(d.expectedCloseDate) | appDate) || ('common.none' | transloco) }}</dd>
              </div>
              @if (d.closedAt) {
                <div><dt>{{ 'deals.closedAt' | transloco }}</dt><dd>{{ d.closedAt | appDate: 'datetime' }}</dd></div>
              }
              @if (d.lostReason) {
                <div><dt>{{ 'deals.lostReason' | transloco }}</dt><dd>{{ d.lostReason }}</dd></div>
              }
              <div><dt>{{ 'common.createdAt' | transloco }}</dt><dd>{{ d.createdAt | appDate: 'datetime' }}</dd></div>
              <div><dt>{{ 'common.updatedAt' | transloco }}</dt><dd>{{ d.updatedAt | appDate: 'datetime' }}</dd></div>
            </dl>
          </mat-tab>
          @if (canReadActivities) {
            <mat-tab [label]="'activities.title' | transloco">
              <ng-template matTabContent>
                <div class="tab-body">
                  <app-activity-timeline
                    [dealId]="d.id"
                    [context]="{ company: { id: d.companyId, name: d.companyName ?? '' }, customerId: d.customerId, dealId: d.id }"
                    hide="deal"
                  />
                </div>
              </ng-template>
            </mat-tab>
          }
          <mat-tab [label]="'companies.history' | transloco">
            <ng-template matTabContent>
              <div class="tab-body"><app-history-timeline #history [url]="api.historyUrl(d.id)" /></div>
            </ng-template>
          </mat-tab>
        </mat-tab-group>
      </div>
    } @else {
      <mat-progress-bar mode="indeterminate" />
    }
  `,
  styles: `
    .tabs-card {
      padding: 0;
      overflow: hidden;
    }
    .tab-body {
      padding: var(--app-gap);
    }
    button .material-symbols-outlined {
      margin-right: 0.25rem;
    }
    .danger {
      --mat-button-outlined-label-text-color: var(--mat-sys-error);
    }
    .won {
      --mat-button-tonal-container-color: var(--app-success-container);
      --mat-button-tonal-label-text-color: var(--app-success);
    }
    .amount {
      font: var(--mat-sys-headline-small);
    }
    .overdue {
      color: var(--mat-sys-error);
    }
  `,
})
export class DealDetail implements OnInit {
  readonly api = inject(DealsApi);
  private readonly stagesApi = inject(StagesApi);
  private readonly router = inject(Router);
  private readonly dialog = inject(MatDialog);
  private readonly notify = inject(NotifyService);
  private readonly confirm = inject(ConfirmService);

  private readonly store = inject(AuthStore);
  readonly canWrite = this.store.can(Permissions.dealsWrite);
  readonly canDelete = this.store.can(Permissions.dealsDelete);
  readonly canReadActivities = this.store.can(Permissions.activitiesRead);

  /** Route parameter. */
  readonly id = input.required({ transform: numberAttribute });

  readonly deal = signal<DealDto | null>(null);
  readonly stages = signal<StageDto[]>([]);
  readonly busy = signal(false);
  private readonly history = viewChild<HistoryTimeline>('history');

  readonly toDate = dateOnly;

  ngOnInit(): void {
    this.api.get(this.id()).subscribe({
      next: (deal) => this.deal.set(deal),
      error: (error) => {
        this.notify.error(error);
        void this.router.navigate(['/pipeline']);
      },
    });

    if (this.canWrite) this.stagesApi.list().subscribe((stages) => this.stages.set(stages));
  }

  overdue(deal: DealDto): boolean {
    return isOverdue(deal.expectedCloseDate, deal.status);
  }

  /** Runs a pipeline action (move / win / reopen) and shows the updated deal. */
  run(request: Observable<DealDto>): void {
    this.busy.set(true);
    request.subscribe({
      next: (deal) => {
        this.busy.set(false);
        this.notify.success('common.saved');
        this.onSaved(deal);
      },
      error: (error) => {
        this.busy.set(false);
        this.notify.error(error);
      },
    });
  }

  lose(deal: DealDto): void {
    LoseDealDialog.open(this.dialog)
      .pipe(filter((reason): reason is string => reason !== undefined))
      .subscribe((reason) => this.run(this.api.lose(deal.id, reason || null)));
  }

  edit(deal: DealDto): void {
    DealFormDialog.open(this.dialog, { deal })
      .pipe(filter(Boolean))
      .subscribe((saved) => this.onSaved(saved));
  }

  remove(deal: DealDto): void {
    this.confirm.delete(deal.title, () =>
      this.api.delete(deal.id).subscribe({
        next: () => {
          this.notify.success('common.deleted');
          void this.router.navigate(['/pipeline']);
        },
        error: (error) => this.notify.error(error),
      }),
    );
  }

  private onSaved(deal: DealDto): void {
    this.deal.set(deal);
    this.history()?.reload();
  }
}
