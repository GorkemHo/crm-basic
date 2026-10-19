import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { CustomerDto, PagedResponse } from '../../core/api/models';
import { PageQuery, toHttpParams } from '../../core/api/paging';

export interface CustomerInput {
  firstName: string;
  lastName: string;
  email: string;
  phoneNumber: string | null;
  companyId: number;
  isActive?: boolean;
}

@Injectable({ providedIn: 'root' })
export class CustomersApi {
  private readonly http = inject(HttpClient);
  private readonly url = '/api/customers';

  /** Filters: `search`, `isActive`, `companyId`. */
  list(query: PageQuery): Observable<PagedResponse<CustomerDto>> {
    return this.http.get<PagedResponse<CustomerDto>>(this.url, { params: toHttpParams(query) });
  }

  get(id: number): Observable<CustomerDto> {
    return this.http.get<CustomerDto>(`${this.url}/${id}`);
  }

  create(input: CustomerInput): Observable<CustomerDto> {
    return this.http.post<CustomerDto>(this.url, input);
  }

  /** Partial update; an empty phone number clears it. */
  update(id: number, changes: Partial<CustomerInput>): Observable<CustomerDto> {
    return this.http.patch<CustomerDto>(`${this.url}/${id}`, changes);
  }

  delete(id: number): Observable<void> {
    return this.http.delete<void>(`${this.url}/${id}`);
  }

  historyUrl(id: number): string {
    return `${this.url}/${id}/history`;
  }
}
