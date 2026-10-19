import { HttpClient } from '@angular/common/http';
import { Component, OnInit, inject, input, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { TranslocoPipe } from '@jsverse/transloco';

import { AuditLogDto, PagedResponse } from '../core/api/models';
import { AppDatePipe } from '../core/i18n/date.pipe';
import { NotifyService } from '../core/ui/notify.service';

/** Change history (audit log) of one record, newest first, from an `…/{id}/history` endpoint. */
@Component({
  selector: 'app-history-timeline',
  imports: [MatButtonModule, MatProgressBarModule, TranslocoPipe, AppDatePipe],
  template: `
    @if (loading() && entries().length === 0) {
      <mat-progress-bar mode="indeterminate" />
    } @else if (entries().length === 0) {
      <p class="muted">{{ 'history.empty' | transloco }}</p>
    } @else {
      <ol class="timeline">
        @for (entry of entries(); track entry.id) {
          <li>
            <span class="marker" [class.deleted]="entry.action === 'Deleted'">
              <span class="material-symbols-outlined">{{ entry.action === 'Deleted' ? 'delete' : 'edit' }}</span>
            </span>
            <div class="entry">
              <div class="entry-header">
                <strong>{{ 'history.' + entry.action | transloco }}</strong>
                <span class="muted">
                  {{ entry.occurredAt | appDate: 'datetime' }} ·
                  {{ entry.userId ? ('history.byUser' | transloco: { id: entry.userId }) : ('history.bySystem' | transloco) }}
                </span>
              </div>
              <ul>
                @for (change of changesOf(entry); track change.field) {
                  <li>
                    <code>{{ change.field }}</code>:
                    @if (entry.action === 'Updated') {
                      <span class="old">{{ display(change.old) }}</span>
                      <span class="material-symbols-outlined arrow">arrow_forward</span>
                      <span>{{ display(change.new) }}</span>
                    } @else {
                      <span>{{ display(change.old) }}</span>
                    }
                  </li>
                }
              </ul>
            </div>
          </li>
        }
      </ol>

      @if (hasMore()) {
        <button matButton (click)="loadMore()" [disabled]="loading()">{{ 'common.viewAll' | transloco }}</button>
      }
    }
  `,
  styles: `
    .timeline {
      list-style: none;
      margin: 0;
      padding: 0;
    }
    .timeline > li {
      display: flex;
      gap: 1rem;
      position: relative;
      padding-bottom: 1.25rem;
    }
    .timeline > li:not(:last-child)::before {
      content: '';
      position: absolute;
      left: 1rem;
      top: 2.25rem;
      bottom: 0;
      width: 2px;
      background: var(--mat-sys-outline-variant);
    }
    .marker {
      flex: 0 0 2rem;
      height: 2rem;
      border-radius: 50%;
      display: inline-flex;
      align-items: center;
      justify-content: center;
      background: var(--mat-sys-primary-container);
      color: var(--mat-sys-on-primary-container);
    }
    .marker.deleted {
      background: var(--mat-sys-error-container);
      color: var(--mat-sys-on-error-container);
    }
    .marker .material-symbols-outlined {
      font-size: 1.1rem;
    }
    .entry-header {
      display: flex;
      flex-wrap: wrap;
      gap: 0.5rem;
      align-items: baseline;
      min-height: 2rem;
    }
    ul {
      margin: 0.25rem 0 0;
      padding-left: 1rem;
    }
    .old {
      text-decoration: line-through;
      color: var(--mat-sys-on-surface-variant);
    }
    .arrow {
      font-size: 0.9rem;
      margin: 0 0.25rem;
    }
  `,
})
export class HistoryTimeline implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly notify = inject(NotifyService);

  /** E.g. `/api/companies/5/history`. */
  readonly url = input.required<string>();

  readonly entries = signal<AuditLogDto[]>([]);
  readonly loading = signal(false);
  readonly hasMore = signal(false);
  private page = 0;

  ngOnInit(): void {
    this.reload();
  }

  reload(): void {
    this.page = 0;
    this.entries.set([]);
    this.loadMore();
  }

  loadMore(): void {
    this.loading.set(true);
    this.page++;

    this.http.get<PagedResponse<AuditLogDto>>(this.url(), { params: { pageNumber: this.page, pageSize: 20 } }).subscribe({
      next: (response) => {
        this.entries.update((existing) => [...existing, ...response.items]);
        this.hasMore.set(response.pageNumber < response.totalPages);
        this.loading.set(false);
      },
      error: (error) => {
        this.loading.set(false);
        this.notify.error(error);
      },
    });
  }

  changesOf(entry: AuditLogDto) {
    return Object.entries(entry.changes).map(([field, change]) => ({ field, old: change.old, new: change.new }));
  }

  display(value: unknown): string {
    if (value === null || value === undefined || value === '') return '—';
    return typeof value === 'object' ? JSON.stringify(value) : String(value);
  }
}
