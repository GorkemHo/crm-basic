import { HttpParams } from '@angular/common/http';

/** Query parameters understood by every paged list endpoint (plus endpoint-specific filters). */
export interface PageQuery {
  pageNumber?: number;
  pageSize?: number;
  sortBy?: string;
  sortDirection?: 'asc' | 'desc';
  [filter: string]: string | number | boolean | null | undefined;
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
