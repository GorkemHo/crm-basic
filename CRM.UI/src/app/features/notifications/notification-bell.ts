import { Component, inject, signal } from '@angular/core';
import { MatBadgeModule } from '@angular/material/badge';
import { MatButtonModule } from '@angular/material/button';
import { MatMenuModule } from '@angular/material/menu';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatTooltipModule } from '@angular/material/tooltip';
import { Router, RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';

import { NotificationDto } from '../../core/api/models';
import { AppTimeAgoPipe } from '../../core/i18n/time-ago.pipe';
import { NOTIFICATION_ICONS, NotificationsService } from './notifications.service';

/** Bell in the top bar: unread count, and the latest notifications when opened. */
@Component({
  selector: 'app-notification-bell',
  imports: [RouterLink, TranslocoPipe, MatBadgeModule, MatButtonModule, MatMenuModule, MatProgressBarModule, MatTooltipModule, AppTimeAgoPipe],
  template: `
    <button
      matIconButton
      class="bell"
      [matMenuTriggerFor]="menu"
      (menuOpened)="load()"
      [matTooltip]="'notifications.title' | transloco"
      [attr.aria-label]="'notifications.title' | transloco"
    >
      <span
        class="material-symbols-outlined"
        [matBadge]="service.unread() > 99 ? '99+' : service.unread()"
        [matBadgeHidden]="!service.unread()"
        matBadgeColor="warn"
        matBadgeSize="small"
        >notifications</span
      >
    </button>

    <mat-menu #menu="matMenu" xPosition="before" class="notification-menu">
      <div class="menu-header" (click)="$event.stopPropagation()">
        <strong>{{ 'notifications.title' | transloco }}</strong>
        @if (service.unread()) {
          <button matButton class="read-all" (click)="markAllRead()">{{ 'notifications.markAllRead' | transloco }}</button>
        }
      </div>
      @if (loading()) {
        <mat-progress-bar mode="indeterminate" />
      }
      @for (n of items(); track n.id) {
        <button mat-menu-item class="item" [class.unread]="!n.readAt" (click)="open(n)">
          <span class="material-symbols-outlined">{{ icons[n.type] }}</span>
          <span class="text">
            <span class="sentence">{{ service.text(n) }}</span>
            <span class="muted when">{{ n.createdAt | appTimeAgo }}</span>
          </span>
        </button>
      } @empty {
        @if (!loading()) {
          <p class="muted empty">{{ 'notifications.empty' | transloco }}</p>
        }
      }
      <a mat-menu-item routerLink="/notifications" class="see-all">{{ 'common.viewAll' | transloco }}</a>
    </mat-menu>
  `,
  styles: `
    :host ::ng-deep .notification-menu {
      width: min(24rem, 95vw);
      max-width: none;
    }
    .menu-header {
      display: flex;
      align-items: center;
      justify-content: space-between;
      padding: 0.25rem 0.5rem 0.25rem 1rem;
    }
    .item {
      height: auto;
      min-height: 3.5rem;
      white-space: normal;
      padding-top: 0.5rem;
      padding-bottom: 0.5rem;
    }
    .item.unread {
      background: var(--mat-sys-secondary-container);
    }
    .text {
      display: flex;
      flex-direction: column;
      line-height: 1.3;
    }
    .when {
      font: var(--mat-sys-body-small);
    }
    .empty {
      padding: 1rem;
      margin: 0;
    }
    .see-all {
      justify-content: center;
      color: var(--mat-sys-primary);
    }
  `,
})
export class NotificationBell {
  readonly service = inject(NotificationsService);
  private readonly router = inject(Router);

  readonly icons = NOTIFICATION_ICONS;
  readonly items = signal<NotificationDto[]>([]);
  readonly loading = signal(false);

  load(): void {
    this.loading.set(true);
    this.service.list({ pageSize: 8 }).subscribe({
      next: (page) => {
        this.items.set(page.items);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  open(notification: NotificationDto): void {
    this.service.markRead(notification).subscribe();
    if (notification.link) void this.router.navigateByUrl(notification.link);
  }

  markAllRead(): void {
    this.service.markAllRead().subscribe(() => this.load());
  }
}
