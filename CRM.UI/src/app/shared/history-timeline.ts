import { HttpClient } from '@angular/common/http';
import { Component, OnInit, inject, input, signal } from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';
import { ButtonModule } from 'primeng/button';
import { SkeletonModule } from 'primeng/skeleton';
import { TimelineModule } from 'primeng/timeline';

import { AuditLogDto, PagedResponse } from '../core/api/models';
import { AppDatePipe } from '../core/i18n/date.pipe';
import { NotifyService } from '../core/ui/notify.service';

/** Change history (audit log) of one record, newest first, from an `…/{id}/history` endpoint. */
@Component({
  selector: 'app-history-timeline',
  imports: [TimelineModule, SkeletonModule, ButtonModule, TranslocoPipe, AppDatePipe],
  template: `
    @if (loading() && entries().length === 0) {
      <p-skeleton height="4rem" styleClass="mb-2" />
      <p-skeleton height="4rem" />
    } @else if (entries().length === 0) {
      <p class="muted">{{ 'history.empty' | transloco }}</p>
    } @else {
      <p-timeline [value]="entries()" align="left">
        <ng-template #marker let-entry>
          <span class="marker" [class.deleted]="entry.action === 'Deleted'">
            <i [class]="entry.action === 'Deleted' ? 'pi pi-trash' : 'pi pi-pencil'"></i>
          </span>
        </ng-template>
        <ng-template #content let-entry>
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
                    <i class="pi pi-arrow-right"></i>
                    <span class="new">{{ display(change.new) }}</span>
                  } @else {
                    <span>{{ display(change.old) }}</span>
                  }
                </li>
              }
            </ul>
          </div>
        </ng-template>
      </p-timeline>

      @if (hasMore()) {
        <p-button [label]="'common.viewAll' | transloco" [text]="true" [loading]="loading()" (onClick)="loadMore()" />
      }
    }
  `,
  styles: `
    .marker {
      display: inline-flex;
      width: 2rem;
      height: 2rem;
      border-radius: 50%;
      align-items: center;
      justify-content: center;
      background: var(--p-primary-100);
      color: var(--p-primary-700);
    }
    .marker.deleted {
      background: var(--p-red-100);
      color: var(--p-red-700);
    }
    .entry {
      padding-bottom: 1rem;
    }
    .entry-header {
      display: flex;
      flex-wrap: wrap;
      gap: 0.5rem;
      align-items: baseline;
    }
    ul {
      margin: 0.5rem 0 0;
      padding-left: 1rem;
    }
    li {
      margin-bottom: 0.25rem;
    }
    .old {
      text-decoration: line-through;
      color: var(--p-text-muted-color);
    }
    .pi-arrow-right {
      font-size: 0.7rem;
      margin: 0 0.35rem;
    }
    :host ::ng-deep .p-timeline-event-opposite {
      display: none;
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
