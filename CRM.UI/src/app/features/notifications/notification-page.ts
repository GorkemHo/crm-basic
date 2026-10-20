import { Component, effect, inject, signal, untracked } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { Router } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';

import { NotificationDto } from '../../core/api/models';
import { AppDatePipe } from '../../core/i18n/date.pipe';
import { AppTimeAgoPipe } from '../../core/i18n/time-ago.pipe';
import { NotifyService } from '../../core/ui/notify.service';
import { PageHeader } from '../../shared/page-header';
import { NOTIFICATION_ICONS, NotificationsService } from './notifications.service';

const PAGE_SIZE = 20;

/** All of the signed-in user's notifications. */
@Component({
  selector: 'app-notification-page',
  imports: [FormsModule, TranslocoPipe, MatButtonModule, MatButtonToggleModule, MatProgressBarModule, PageHeader, AppDatePipe, AppTimeAgoPipe],
  template: `
    <app-page-header [title]="'notifications.title' | transloco" [subtitle]="'notifications.subtitle' | transloco">
      @if (service.unread()) {
        <button matButton="tonal" (click)="markAllRead()">
          <span class="material-symbols-outlined">done_all</span> {{ 'notifications.markAllRead' | transloco }}
        </button>
      }
    </app-page-header>

    <div class="card">
      <mat-button-toggle-group class="filter" [ngModel]="unreadOnly()" (ngModelChange)="unreadOnly.set($event)" [hideSingleSelectionIndicator]="true">
        <mat-button-toggle [value]="false">{{ 'common.all' | transloco }}</mat-button-toggle>
        <mat-button-toggle [value]="true">{{ 'notifications.unread' | transloco }}</mat-button-toggle>
      </mat-button-toggle-group>

      @if (loading() && items().length === 0) {
        <mat-progress-bar mode="indeterminate" />
      } @else if (items().length === 0) {
        <p class="muted empty">{{ 'notifications.empty' | transloco }}</p>
      } @else {
        <ul class="notifications">
          @for (n of items(); track n.id) {
            <li class="notification" [class.unread]="!n.readAt">
              <button class="row" (click)="open(n)">
                <span class="icon material-symbols-outlined">{{ icons[n.type] }}</span>
                <span class="text">
                  <span>{{ service.text(n) }}</span>
                  <span class="muted when" [title]="n.createdAt | appDate: 'datetime'">{{ n.createdAt | appTimeAgo }}</span>
                </span>
                @if (!n.readAt) {
                  <span class="dot" [attr.aria-label]="'notifications.unread' | transloco"></span>
                }
              </button>
            </li>
          }
        </ul>
        @if (items().length < total()) {
          <button matButton (click)="loadMore()" [disabled]="loading()">{{ 'activities.loadMore' | transloco }}</button>
        }
      }
    </div>
  `,
  styles: `
    button .material-symbols-outlined {
      margin-right: 0.25rem;
    }
    .filter {
      margin-bottom: 1rem;
    }
    .empty {
      padding: 1rem 0;
    }
    .notifications {
      list-style: none;
      margin: 0;
      padding: 0;
    }
    .row {
      display: flex;
      align-items: center;
      gap: 1rem;
      width: 100%;
      padding: 0.75rem 0.5rem;
      border: none;
      border-bottom: 1px solid var(--mat-sys-outline-variant);
      background: none;
      color: inherit;
      font: inherit;
      text-align: left;
      cursor: pointer;
    }
    .row:hover {
      background: var(--mat-sys-surface-container);
    }
    .unread .row {
      background: var(--mat-sys-secondary-container);
    }
    .icon {
      flex: 0 0 2.25rem;
      height: 2.25rem;
      border-radius: 50%;
      display: inline-flex;
      align-items: center;
      justify-content: center;
      background: var(--mat-sys-surface-container-highest);
    }
    .text {
      flex: 1;
      display: flex;
      flex-direction: column;
    }
    .when {
      font: var(--mat-sys-body-small);
    }
    .dot {
      width: 0.6rem;
      height: 0.6rem;
      border-radius: 50%;
      background: var(--mat-sys-primary);
    }
  `,
})
export class NotificationPage {
  readonly service = inject(NotificationsService);
  private readonly notify = inject(NotifyService);
  private readonly router = inject(Router);

  readonly icons = NOTIFICATION_ICONS;
  readonly unreadOnly = signal(false);
  readonly items = signal<NotificationDto[]>([]);
  readonly total = signal(0);
  readonly loading = signal(true);
  private page = 1;

  constructor() {
    effect(() => {
      this.unreadOnly();
      untracked(() => {
        this.page = 1;
        this.fetch(false);
      });
    });
  }

  loadMore(): void {
    this.page++;
    this.fetch(true);
  }

  open(notification: NotificationDto): void {
    this.service.markRead(notification).subscribe();
    if (notification.link) void this.router.navigateByUrl(notification.link);
  }

  markAllRead(): void {
    this.service.markAllRead().subscribe({
      next: () => {
        this.page = 1;
        this.fetch(false);
      },
      error: (error) => this.notify.error(error),
    });
  }

  private fetch(append: boolean): void {
    this.loading.set(true);
    this.service.list({ unreadOnly: this.unreadOnly() || undefined, pageNumber: this.page, pageSize: PAGE_SIZE }).subscribe({
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
