import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { CompanyDto, PagedResponse } from '../../core/api/models';
import { PageQuery, toHttpParams } from '../../core/api/paging';

export interface CompanyInput {
  companyName: string;
  taxNumber: string | null;
  isActive?: boolean;
}

@Injectable({ providedIn: 'root' })
export class CompaniesApi {
  private readonly http = inject(HttpClient);
  private readonly url = '/api/companies';

  /** Filters: `search`, `isActive`. */
  list(query: PageQuery): Observable<PagedResponse<CompanyDto>> {
    return this.http.get<PagedResponse<CompanyDto>>(this.url, { params: toHttpParams(query) });
  }

  get(id: number): Observable<CompanyDto> {
    return this.http.get<CompanyDto>(`${this.url}/${id}`);
  }

  create(input: CompanyInput): Observable<CompanyDto> {
    return this.http.post<CompanyDto>(this.url, input);
  }

  /** Partial update; an empty tax number clears it. */
  update(id: number, changes: Partial<CompanyInput>): Observable<CompanyDto> {
    return this.http.patch<CompanyDto>(`${this.url}/${id}`, changes);
  }

  delete(id: number): Observable<void> {
    return this.http.delete<void>(`${this.url}/${id}`);
  }

  historyUrl(id: number): string {
    return `${this.url}/${id}/history`;
  }
}
