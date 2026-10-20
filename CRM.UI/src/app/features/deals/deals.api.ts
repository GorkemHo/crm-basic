import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { BoardColumn, Currency, DealDto, PagedResponse, PipelineSummary, StageDto } from '../../core/api/models';
import { PageQuery, toHttpParams } from '../../core/api/paging';

export interface DealInput {
  title: string;
  companyId: number;
  customerId: number | null;
  amount: number;
  currency: Currency;
  /** yyyy-MM-dd */
  expectedCloseDate: string | null;
}

@Injectable({ providedIn: 'root' })
export class DealsApi {
  private readonly http = inject(HttpClient);
  private readonly url = '/api/deals';

  /** Filters: `search`, `status`, `stageId`, `companyId`, `customerId`, `currency`. */
  list(query: PageQuery): Observable<PagedResponse<DealDto>> {
    return this.http.get<PagedResponse<DealDto>>(this.url, { params: toHttpParams(query) });
  }

  board(companyId?: number | null): Observable<BoardColumn[]> {
    return this.http.get<BoardColumn[]>(`${this.url}/board`, { params: toHttpParams({ companyId }) });
  }

  summary(): Observable<PipelineSummary> {
    return this.http.get<PipelineSummary>(`${this.url}/summary`);
  }

  get(id: number): Observable<DealDto> {
    return this.http.get<DealDto>(`${this.url}/${id}`);
  }

  /** Without `stageId` the deal starts in the first stage. */
  create(input: DealInput & { stageId?: number | null }): Observable<DealDto> {
    return this.http.post<DealDto>(this.url, input);
  }

  update(id: number, input: DealInput): Observable<DealDto> {
    return this.http.put<DealDto>(`${this.url}/${id}`, input);
  }

  move(id: number, stageId: number): Observable<DealDto> {
    return this.http.post<DealDto>(`${this.url}/${id}/move`, { stageId });
  }

  win(id: number): Observable<DealDto> {
    return this.http.post<DealDto>(`${this.url}/${id}/win`, {});
  }

  lose(id: number, reason: string | null): Observable<DealDto> {
    return this.http.post<DealDto>(`${this.url}/${id}/lose`, { reason });
  }

  reopen(id: number): Observable<DealDto> {
    return this.http.post<DealDto>(`${this.url}/${id}/reopen`, {});
  }

  delete(id: number): Observable<void> {
    return this.http.delete<void>(`${this.url}/${id}`);
  }

  historyUrl(id: number): string {
    return `${this.url}/${id}/history`;
  }
}

@Injectable({ providedIn: 'root' })
export class StagesApi {
  private readonly http = inject(HttpClient);
  private readonly url = '/api/pipeline-stages';

  list(): Observable<StageDto[]> {
    return this.http.get<StageDto[]>(this.url);
  }

  create(name: string, probability: number): Observable<StageDto> {
    return this.http.post<StageDto>(this.url, { name, probability });
  }

  update(id: number, changes: { name?: string; probability?: number }): Observable<StageDto> {
    return this.http.patch<StageDto>(`${this.url}/${id}`, changes);
  }

  /** `stageIds` must list every stage, in the new order. */
  reorder(stageIds: number[]): Observable<StageDto[]> {
    return this.http.put<StageDto[]>(`${this.url}/order`, { stageIds });
  }

  delete(id: number): Observable<void> {
    return this.http.delete<void>(`${this.url}/${id}`);
  }
}
