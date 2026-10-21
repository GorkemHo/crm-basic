import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { toHttpParams } from '../../core/api/paging';

export interface CurrencyAmount {
  currency: string;
  count: number;
  amount: number;
}

export interface SalesReport {
  months: { year: number; month: number; won: CurrencyAmount[]; lostCount: number }[];
  won: CurrencyAmount[];
  wonCount: number;
  lostCount: number;
  /** 0–1 */
  winRate: number | null;
  averageCycleDays: number | null;
  lostReasons: { reason: string | null; count: number }[];
}

export interface PipelineStageReport {
  stageId: number;
  name: string;
  probability: number;
  totals: (CurrencyAmount & { weightedAmount: number })[];
}

export interface TypeCount {
  type: string;
  count: number;
}

export interface ActivityReport {
  months: { year: number; month: number; types: TypeCount[] }[];
  byUser: { userId: number | null; userName: string | null; total: number; types: TypeCount[] }[];
  total: number;
}

export interface TaskReport {
  byAssignee: { assigneeId: number; assigneeName: string | null; open: number; overdue: number; completed: number }[];
  open: number;
  overdue: number;
  completed: number;
}

/** yyyy-MM-dd, both ends included. */
export interface Period {
  from: string;
  to: string;
}

@Injectable({ providedIn: 'root' })
export class ReportsApi {
  private readonly http = inject(HttpClient);
  private readonly url = '/api/reports';

  sales(period: Period): Observable<SalesReport> {
    return this.http.get<SalesReport>(`${this.url}/sales`, { params: toHttpParams({ ...period }) });
  }

  pipeline(): Observable<PipelineStageReport[]> {
    return this.http.get<PipelineStageReport[]>(`${this.url}/pipeline`);
  }

  activities(period: Period): Observable<ActivityReport> {
    return this.http.get<ActivityReport>(`${this.url}/activities`, { params: toHttpParams({ ...period }) });
  }

  tasks(period: Period, today: string): Observable<TaskReport> {
    return this.http.get<TaskReport>(`${this.url}/tasks`, { params: toHttpParams({ ...period, today }) });
  }
}

/** Saves rows as a CSV file that Excel opens correctly in both languages (BOM, semicolons). */
export function downloadCsv(fileName: string, rows: (string | number | null)[][]): void {
  const escape = (value: string | number | null) => {
    const text = value === null ? '' : String(value);
    return /[";\n]/.test(text) ? `"${text.replace(/"/g, '""')}"` : text;
  };
  const csv = '﻿' + rows.map((row) => row.map(escape).join(';')).join('\r\n');

  const link = document.createElement('a');
  link.href = URL.createObjectURL(new Blob([csv], { type: 'text/csv;charset=utf-8' }));
  link.download = fileName;
  link.click();
  setTimeout(() => URL.revokeObjectURL(link.href), 1000);
}
