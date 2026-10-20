import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { AssigneeDto, MyTaskSummary, PagedResponse, TaskDto, TaskPriority } from '../../core/api/models';
import { PageQuery, toHttpParams } from '../../core/api/paging';

export interface TaskInput {
  title: string;
  description: string | null;
  /** yyyy-MM-dd */
  dueDate: string | null;
  priority: TaskPriority;
  /** Defaults to the current user. */
  assigneeId: number | null;
  companyId: number | null;
  customerId: number | null;
  dealId: number | null;
}

/** Today in the user's time zone, as yyyy-MM-dd (the API compares due dates with it). */
export function localToday(): string {
  const now = new Date();
  const pad = (n: number) => String(n).padStart(2, '0');
  return `${now.getFullYear()}-${pad(now.getMonth() + 1)}-${pad(now.getDate())}`;
}

@Injectable({ providedIn: 'root' })
export class TasksApi {
  private readonly http = inject(HttpClient);
  private readonly url = '/api/tasks';

  /** Filters: `search`, `status` (open/done), `mine`, `assigneeId`, `companyId`, `customerId`, `dealId`, `dueFrom`, `dueTo`. */
  list(query: PageQuery): Observable<PagedResponse<TaskDto>> {
    return this.http.get<PagedResponse<TaskDto>>(this.url, { params: toHttpParams(query) });
  }

  mySummary(): Observable<MyTaskSummary> {
    return this.http.get<MyTaskSummary>(`${this.url}/my-summary`, { params: toHttpParams({ today: localToday() }) });
  }

  assignees(): Observable<AssigneeDto[]> {
    return this.http.get<AssigneeDto[]>(`${this.url}/assignees`);
  }

  create(input: TaskInput): Observable<TaskDto> {
    return this.http.post<TaskDto>(this.url, input);
  }

  update(id: number, input: TaskInput): Observable<TaskDto> {
    return this.http.put<TaskDto>(`${this.url}/${id}`, input);
  }

  complete(id: number): Observable<TaskDto> {
    return this.http.post<TaskDto>(`${this.url}/${id}/complete`, {});
  }

  reopen(id: number): Observable<TaskDto> {
    return this.http.post<TaskDto>(`${this.url}/${id}/reopen`, {});
  }

  delete(id: number): Observable<void> {
    return this.http.delete<void>(`${this.url}/${id}`);
  }
}
