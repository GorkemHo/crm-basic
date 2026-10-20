import { Component, effect, inject, input, signal, untracked } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatMenuModule } from '@angular/material/menu';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { Subscription, filter } from 'rxjs';

import { ActivityDto } from '../../core/api/models';
import { PageQuery } from '../../core/api/paging';
import { AuthStore } from '../../core/auth/auth.store';
import { Permissions } from '../../core/auth/permissions';
import { AppDatePipe } from '../../core/i18n/date.pipe';
import { ConfirmService } from '../../core/ui/confirm.service';
import { NotifyService } from '../../core/ui/notify.service';
import { ACTIVITY_ICONS, ActivitiesApi } from './activities.api';
import { ActivityContext, ActivityFormDialog } from './activity-form-dialog';

const PAGE_SIZE = 20;

/**
 * Activities newest first, with "log activity" and per-entry edit / delete for their author (or a manager).
 * Filter by `companyId`, `customerId`, `dealId`, `type`, `search`; `context` pre-fills new entries.
 * Links to the record the timeline is shown on are left out (`hide`).
 */
@Component({
  selector: 'app-activity-timeline',
  imports: [RouterLink, TranslocoPipe, MatButtonModule, MatMenuModule, MatProgressBarModule, AppDatePipe],
  template: `
    @if (canWrite && showAdd()) {
      <div class="toolbar">
        <button matButton="filled" class="log-button" (click)="create()">
          <span class="material-symbols-outlined">add</span> {{ 'activities.new' | transloco }}
        </button>
      </div>
    }

    @if (loading() && items().length === 0) {
      <mat-progress-bar mode="indeterminate" />
    } @else if (items().length === 0) {
      <p class="muted empty">{{ 'activities.empty' | transloco }}</p>
    } @else {
      <ol class="timeline">
        @for (a of items(); track a.id) {
          <li class="activity" [attr.data-type]="a.type">
            <span class="marker" [class]="a.type"><span class="material-symbols-outlined">{{ icons[a.type] }}</span></span>
            <div class="entry">
              <div class="entry-header">
                <strong class="subject">{{ a.subject }}</strong>
                @if (isPlanned(a)) {
                  <span class="planned">{{ 'activities.planned' | transloco }}</span>
                }
                <span class="spacer"></span>
                @if (canChange(a)) {
                  <button matIconButton class="entry-menu" [matMenuTriggerFor]="menu" [attr.aria-label]="'common.actions' | transloco">
                    <span class="material-symbols-outlined">more_vert</span>
                  </button>
                  <mat-menu #menu="matMenu" xPosition="before">
                    <button mat-menu-item (click)="edit(a)">
                      <span class="material-symbols-outlined">edit</span><span>{{ 'common.edit' | transloco }}</span>
                    </button>
                    <button mat-menu-item (click)="remove(a)">
                      <span class="material-symbols-outlined">delete</span><span>{{ 'common.delete' | transloco }}</span>
                    </button>
                  </mat-menu>
                }
              </div>
              <div class="meta muted">
                <span>{{ 'activities.types.' + a.type | transloco }}</span>
                <span>{{ a.occurredAt | appDate: 'datetime' }}</span>
                @if (a.durationMinutes) {
                  <span>{{ a.durationMinutes }} {{ 'activities.minutes' | transloco }}</span>
                }
                <span>{{ a.createdByName ?? ('history.bySystem' | transloco) }}</span>
              </div>
              @if (a.notes) {
                <p class="notes">{{ a.notes }}</p>
              }
              <div class="links">
                @if (hide() !== 'company') {
                  <a [routerLink]="['/companies', a.companyId]"><span class="material-symbols-outlined">apartment</span>{{ a.companyName }}</a>
                }
                @if (a.customerId && hide() !== 'customer') {
                  <a [routerLink]="['/customers', a.customerId]"><span class="material-symbols-outlined">person</span>{{ a.customerName }}</a>
                }
                @if (a.dealId && hide() !== 'deal') {
                  <a [routerLink]="['/deals', a.dealId]"><span class="material-symbols-outlined">handshake</span>{{ a.dealTitle }}</a>
                }
              </div>
            </div>
          </li>
        }
      </ol>

      @if (items().length < total()) {
        <button matButton (click)="loadMore()" [disabled]="loading()">{{ 'activities.loadMore' | transloco }}</button>
      }
    }
  `,
  styles: `
    .toolbar {
      display: flex;
      justify-content: flex-end;
      margin-bottom: 1rem;
    }
    button .material-symbols-outlined {
      margin-right: 0.25rem;
    }
    .empty {
      padding: 1rem 0;
    }
    .timeline {
      list-style: none;
      margin: 0;
      padding: 0;
    }
    .timeline > li {
      display: flex;
      gap: 1rem;
      position: relative;
      padding-bottom: 1.5rem;
    }
    .timeline > li:not(:last-child)::before {
      content: '';
      position: absolute;
      left: 1.125rem;
      top: 2.5rem;
      bottom: 0;
      width: 2px;
      background: var(--mat-sys-outline-variant);
    }
    .marker {
      flex: 0 0 2.25rem;
      height: 2.25rem;
      border-radius: 50%;
      display: inline-flex;
      align-items: center;
      justify-content: center;
      background: var(--mat-sys-secondary-container);
      color: var(--mat-sys-on-secondary-container);
    }
    .marker.call {
      background: var(--app-success-container);
      color: var(--app-success);
    }
    .marker.meeting {
      background: var(--mat-sys-primary-container);
      color: var(--mat-sys-on-primary-container);
    }
    .marker.email {
      background: var(--mat-sys-tertiary-container);
      color: var(--mat-sys-on-tertiary-container);
    }
    .marker .material-symbols-outlined {
      font-size: 1.15rem;
    }
    .entry {
      flex: 1;
      min-width: 0;
    }
    .entry-header {
      display: flex;
      align-items: center;
      gap: 0.5rem;
      min-height: 2.25rem;
    }
    .spacer {
      flex: 1;
    }
    .planned {
      font: var(--mat-sys-label-small);
      padding: 0.1rem 0.5rem;
      border-radius: 1rem;
      background: var(--mat-sys-primary-container);
      color: var(--mat-sys-on-primary-container);
    }
    .meta {
      display: flex;
      flex-wrap: wrap;
      gap: 0 0.75rem;
      font: var(--mat-sys-body-small);
    }
    .notes {
      margin: 0.5rem 0 0;
      white-space: pre-line;
    }
    .links {
      display: flex;
      flex-wrap: wrap;
      gap: 0.25rem 1rem;
      margin-top: 0.5rem;
      font: var(--mat-sys-body-small);
    }
    .links a {
      display: inline-flex;
      align-items: center;
      gap: 0.25rem;
    }
    .links .material-symbols-outlined {
      font-size: 1rem;
    }
  `,
})
export class ActivityTimeline {
  private readonly api = inject(ActivitiesApi);
  private readonly dialog = inject(MatDialog);
  private readonly confirm = inject(ConfirmService);
  private readonly notify = inject(NotifyService);

  private readonly store = inject(AuthStore);
  readonly canWrite = this.store.can(Permissions.activitiesWrite);
  private readonly canManage = this.store.can(Permissions.activitiesManage);

  readonly companyId = input<number | null>(null);
  readonly customerId = input<number | null>(null);
  readonly dealId = input<number | null>(null);
  readonly type = input<string | null>(null);
  readonly search = input<string>('');
  /** Pre-filled links for "log activity". */
  readonly context = input<ActivityContext>({});
  /** The record the timeline is shown on; its link is left out. */
  readonly hide = input<'company' | 'customer' | 'deal' | null>(null);
  readonly showAdd = input(true);

  readonly icons = ACTIVITY_ICONS;
  readonly items = signal<ActivityDto[]>([]);
  readonly total = signal(0);
  readonly loading = signal(true);
  private page = 1;
  private request?: Subscription;

  constructor() {
    // Reload from the first page whenever a filter changes.
    effect(() => {
      this.query();
      untracked(() => this.reload());
    });
  }

  reload(): void {
    this.page = 1;
    this.fetch(false);
  }

  loadMore(): void {
    this.page++;
    this.fetch(true);
  }

  isPlanned(activity: ActivityDto): boolean {
    return new Date(activity.occurredAt) > new Date();
  }

  canChange(activity: ActivityDto): boolean {
    return this.canWrite && (this.canManage || activity.createdBy === this.store.user()?.id);
  }

  create(): void {
    ActivityFormDialog.open(this.dialog, { context: this.context() })
      .pipe(filter(Boolean))
      .subscribe(() => this.reload());
  }

  edit(activity: ActivityDto): void {
    ActivityFormDialog.open(this.dialog, { activity })
      .pipe(filter(Boolean))
      .subscribe(() => this.reload());
  }

  remove(activity: ActivityDto): void {
    this.confirm.delete(activity.subject, () =>
      this.api.delete(activity.id).subscribe({
        next: () => {
          this.notify.success('common.deleted');
          this.reload();
        },
        error: (error) => this.notify.error(error),
      }),
    );
  }

  private query(): PageQuery {
    return {
      companyId: this.companyId(),
      customerId: this.customerId(),
      dealId: this.dealId(),
      type: this.type(),
      search: this.search().trim() || undefined,
    };
  }

  private fetch(append: boolean): void {
    this.request?.unsubscribe();
    this.loading.set(true);

    this.request = this.api.list({ ...this.query(), pageNumber: this.page, pageSize: PAGE_SIZE }).subscribe({
      next: (page) => {
        this.items.update((items) => (append ? [...items, ...page.items] : page.items));
        this.total.set(page.totalCount);
        this.loading.set(false);
      },
      error: (error) => {
        this.loading.set(false);
        this.notify.error(error);
      },
    });
  }
}
