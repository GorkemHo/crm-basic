import { DestroyRef, Signal, WritableSignal, effect, inject, signal, untracked } from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { PageEvent } from '@angular/material/paginator';
import { Sort } from '@angular/material/sort';
import { Observable, Subscription, debounceTime } from 'rxjs';

import { PagedResponse } from '../core/api/models';
import { PageQuery } from '../core/api/paging';
import { NotifyService } from '../core/ui/notify.service';

export interface PagedTableOptions<T> {
  /** Calls the list endpoint. */
  load: (query: PageQuery) => Observable<PagedResponse<T>>;
  /** Current filters; signals read here reload the table (from the first page). */
  filters?: () => PageQuery;
  /** Free-text search signal, debounced and sent as `search`. */
  search?: Signal<string>;
  pageSize?: number;
  /** Initial sort (empty = the API's default order). */
  sort?: Sort;
}

/**
 * State for a mat-table backed by a paged API endpoint. Bind:
 * `mat-table [dataSource]="table.items()"`,
 * `matSort [matSortActive]="table.sort().active" [matSortDirection]="table.sort().direction" (matSortChange)="table.onSort($event)"`,
 * `mat-paginator [length]="table.total()" [pageIndex]="table.pageIndex()" [pageSize]="table.pageSize()" (page)="table.onPage($event)"`.
 * Create it in an injection context.
 */
export class PagedTable<T> {
  readonly items = signal<T[]>([]);
  readonly total = signal(0);
  readonly loading = signal(true);
  readonly pageIndex = signal(0);
  readonly pageSize: WritableSignal<number>;
  readonly sort: WritableSignal<Sort>;
  readonly pageSizeOptions = [10, 25, 50];

  private readonly notify = inject(NotifyService);
  private readonly debouncedSearch: Signal<string>;
  private request?: Subscription;

  constructor(private readonly options: PagedTableOptions<T>) {
    this.pageSize = signal(options.pageSize ?? 10);
    this.sort = signal<Sort>(options.sort ?? { active: '', direction: '' });

    this.debouncedSearch = options.search
      ? toSignal(toObservable(options.search).pipe(debounceTime(300)), { initialValue: options.search() })
      : signal('');

    // First load, and back to page one whenever filters or the search text change.
    effect(() => {
      options.filters?.();
      this.debouncedSearch();
      untracked(() => {
        this.pageIndex.set(0);
        this.fetch();
      });
    });

    inject(DestroyRef).onDestroy(() => this.request?.unsubscribe());
  }

  onPage(event: PageEvent): void {
    this.pageIndex.set(event.pageIndex);
    this.pageSize.set(event.pageSize);
    this.fetch();
  }

  onSort(sort: Sort): void {
    this.sort.set(sort);
    this.pageIndex.set(0);
    this.fetch();
  }

  reload(): void {
    this.fetch();
  }

  private fetch(): void {
    this.request?.unsubscribe();
    this.loading.set(true);

    const sort = this.sort();
    const query: PageQuery = {
      pageNumber: this.pageIndex() + 1,
      pageSize: this.pageSize(),
      sortBy: sort.direction ? sort.active : undefined,
      sortDirection: sort.direction || undefined,
      ...(this.options.filters?.() ?? {}),
      search: this.debouncedSearch().trim() || undefined,
    };

    this.request = this.options.load(query).subscribe({
      next: (page) => {
        this.items.set(page.items);
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
