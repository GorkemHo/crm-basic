import { HttpParams } from '@angular/common/http';
import { TableLazyLoadEvent } from 'primeng/table';

/** Query parameters understood by every paged list endpoint (plus endpoint-specific filters). */
export interface PageQuery {
  pageNumber?: number;
  pageSize?: number;
  sortBy?: string;
  sortDirection?: 'asc' | 'desc';
  [filter: string]: string | number | boolean | null | undefined;
}

/** Converts a PrimeNG lazy table event into paging/sorting query parameters. */
export function pageQueryFromTable(event: TableLazyLoadEvent): PageQuery {
  const rows = event.rows ?? 10;
  const sortField = Array.isArray(event.sortField) ? event.sortField[0] : event.sortField;

  return {
    pageNumber: Math.floor((event.first ?? 0) / rows) + 1,
    pageSize: rows,
    sortBy: sortField ?? undefined,
    sortDirection: sortField ? (event.sortOrder === -1 ? 'desc' : 'asc') : undefined,
  };
}

/** Builds HttpParams, skipping empty values so the API applies its defaults. */
export function toHttpParams(query: PageQuery): HttpParams {
  let params = new HttpParams();

  for (const [key, value] of Object.entries(query)) {
    if (value !== undefined && value !== null && value !== '') {
      params = params.set(key, String(value));
    }
  }

  return params;
}
