import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { ActivityDto, ActivityType, PagedResponse } from '../../core/api/models';
import { PageQuery, toHttpParams } from '../../core/api/paging';

export interface ActivityInput {
  type: ActivityType;
  subject: string;
  notes: string | null;
  /** ISO date-time */
  occurredAt: string;
  durationMinutes: number | null;
  companyId: number;
  customerId: number | null;
  dealId: number | null;
}

@Injectable({ providedIn: 'root' })
export class ActivitiesApi {
  private readonly http = inject(HttpClient);
  private readonly url = '/api/activities';

  /** Filters: `search`, `type`, `companyId`, `customerId`, `dealId`, `createdBy`. Newest first by default. */
  list(query: PageQuery): Observable<PagedResponse<ActivityDto>> {
    return this.http.get<PagedResponse<ActivityDto>>(this.url, { params: toHttpParams(query) });
  }

  create(input: ActivityInput): Observable<ActivityDto> {
    return this.http.post<ActivityDto>(this.url, input);
  }

  update(id: number, input: ActivityInput): Observable<ActivityDto> {
    return this.http.put<ActivityDto>(`${this.url}/${id}`, input);
  }

  delete(id: number): Observable<void> {
    return this.http.delete<void>(`${this.url}/${id}`);
  }
}

/** Material Symbols icon per activity type. */
export const ACTIVITY_ICONS: Record<ActivityType, string> = {
  note: 'sticky_note_2',
  call: 'call',
  meeting: 'groups',
  email: 'mail',
};
