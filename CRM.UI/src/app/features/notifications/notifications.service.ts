import { HttpClient } from '@angular/common/http';
import { DestroyRef, Injectable, effect, inject, signal } from '@angular/core';
import { TranslocoService } from '@jsverse/transloco';
import { Observable, Subscription, fromEvent, interval, merge, startWith, switchMap, tap } from 'rxjs';

import { NotificationDto, PagedResponse } from '../../core/api/models';
import { PageQuery, toHttpParams } from '../../core/api/paging';
import { AuthStore } from '../../core/auth/auth.store';

/** How often the unread count is refreshed while signed in (also on returning to the tab). */
const POLL_MS = 30_000;

@Injectable({ providedIn: 'root' })
export class NotificationsService {
  private readonly http = inject(HttpClient);
  private readonly transloco = inject(TranslocoService);
  private readonly url = '/api/notifications';

  /** Unread notifications of the signed-in user. */
  readonly unread = signal(0);

  private polling?: Subscription;

  constructor() {
    const store = inject(AuthStore);

    // Poll only while someone is signed in.
    effect(() => {
      const signedIn = !!store.user();
      this.polling?.unsubscribe();
      this.polling = undefined;
      if (!signedIn) {
        this.unread.set(0);
        return;
      }

      this.polling = merge(interval(POLL_MS), fromEvent(window, 'focus'))
        .pipe(
          startWith(0),
          switchMap(() => this.http.get<{ count: number }>(`${this.url}/unread-count`)),
        )
        .subscribe({ next: (r) => this.unread.set(r.count), error: () => undefined });
    });

    inject(DestroyRef).onDestroy(() => this.polling?.unsubscribe());
  }

  /** Filters: `unreadOnly`. Newest first. */
  list(query: PageQuery): Observable<PagedResponse<NotificationDto>> {
    return this.http.get<PagedResponse<NotificationDto>>(this.url, { params: toHttpParams(query) });
  }

  markRead(notification: NotificationDto): Observable<void> {
    return this.http.post<void>(`${this.url}/${notification.id}/read`, {}).pipe(
      tap(() => {
        if (!notification.readAt) this.unread.update((n) => Math.max(0, n - 1));
      }),
    );
  }

  markAllRead(): Observable<void> {
    return this.http.post<void>(`${this.url}/read-all`, {}).pipe(tap(() => this.unread.set(0)));
  }

  /** The sentence for a notification in the active language. */
  text(notification: NotificationDto): string {
    return this.transloco.translate(`notifications.types.${notification.type}`, {
      actor: notification.actorName ?? this.transloco.translate('notifications.system'),
      subject: notification.subject,
    });
  }
}

/** Material Symbols icon per notification type. */
export const NOTIFICATION_ICONS: Record<string, string> = {
  'task.assigned': 'assignment_ind',
  'task.completed': 'task_alt',
  'deal.won': 'emoji_events',
  'deal.lost': 'thumb_down',
};
