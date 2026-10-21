import { Component, computed, effect, inject, signal, untracked } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { forkJoin } from 'rxjs';

import { ACTIVITY_TYPES } from '../../core/api/models';
import { LanguageService } from '../../core/i18n/language.service';
import { AppMoneyPipe } from '../../core/i18n/money.pipe';
import { NotifyService } from '../../core/ui/notify.service';
import { BarChart, ChartSeries } from '../../shared/charts/bar-chart';
import { PageHeader } from '../../shared/page-header';
import { localToday } from '../tasks/tasks.api';
import { ActivityReport, Period, PipelineStageReport, ReportsApi, SalesReport, TaskReport, downloadCsv } from './reports.api';

type Preset = '3m' | '6m' | '12m' | 'year' | 'custom';

interface Reports {
  sales: SalesReport;
  pipeline: PipelineStageReport[];
  activities: ActivityReport;
  tasks: TaskReport;
}

const iso = (d: Date) =>
  `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;

/** Team-wide sales, pipeline, activity and task figures for a period. */
@Component({
  selector: 'app-report-page',
  imports: [
    FormsModule, TranslocoPipe, MatButtonModule, MatButtonToggleModule, MatFormFieldModule, MatInputModule, MatProgressBarModule,
    PageHeader, BarChart, AppMoneyPipe,
  ],
  template: `
    <app-page-header [title]="'reports.title' | transloco" [subtitle]="'reports.subtitle' | transloco" />

    <div class="filters">
      <mat-button-toggle-group [ngModel]="preset()" (ngModelChange)="preset.set($event)" [hideSingleSelectionIndicator]="true" class="presets">
        <mat-button-toggle value="3m">{{ 'reports.presets.3m' | transloco }}</mat-button-toggle>
        <mat-button-toggle value="6m">{{ 'reports.presets.6m' | transloco }}</mat-button-toggle>
        <mat-button-toggle value="12m">{{ 'reports.presets.12m' | transloco }}</mat-button-toggle>
        <mat-button-toggle value="year">{{ 'reports.presets.year' | transloco }}</mat-button-toggle>
        <mat-button-toggle value="custom">{{ 'reports.presets.custom' | transloco }}</mat-button-toggle>
      </mat-button-toggle-group>
      @if (preset() === 'custom') {
        <mat-form-field>
          <mat-label>{{ 'reports.from' | transloco }}</mat-label>
          <input matInput id="reportFrom" type="date" [ngModel]="customFrom()" (ngModelChange)="customFrom.set($event)" />
        </mat-form-field>
        <mat-form-field>
          <mat-label>{{ 'reports.to' | transloco }}</mat-label>
          <input matInput id="reportTo" type="date" [ngModel]="customTo()" (ngModelChange)="customTo.set($event)" />
        </mat-form-field>
      }
    </div>

    @if (loading()) {
      <mat-progress-bar mode="indeterminate" />
    }

    @if (data(); as d) {
      <!-- Headline numbers -->
      <section class="tiles">
        <div class="card tile" data-kpi="won">
          <span class="muted">{{ 'reports.wonDeals' | transloco }}</span>
          <strong>{{ d.sales.wonCount }}</strong>
        </div>
        <div class="card tile" data-kpi="winRate">
          <span class="muted">{{ 'reports.winRate' | transloco }}</span>
          <strong>{{ d.sales.winRate === null ? '—' : percent(d.sales.winRate) }}</strong>
        </div>
        <div class="card tile" data-kpi="cycle">
          <span class="muted">{{ 'reports.averageCycle' | transloco }}</span>
          <strong>{{ d.sales.averageCycleDays === null ? '—' : ('reports.days' | transloco: { n: d.sales.averageCycleDays }) }}</strong>
        </div>
        <div class="card tile" data-kpi="lost">
          <span class="muted">{{ 'reports.lostDeals' | transloco }}</span>
          <strong>{{ d.sales.lostCount }}</strong>
        </div>
        @for (w of d.sales.won; track w.currency) {
          <div class="card tile" [attr.data-kpi]="'won-' + w.currency">
            <span class="muted">{{ 'reports.wonAmount' | transloco: { currency: w.currency } }}</span>
            <!-- Short form fits the tile; the exact amount is in the tooltip and the CSV. -->
            <strong [title]="w.amount | appMoney: w.currency">{{ w.amount | appMoney: w.currency : true }}</strong>
          </div>
        }
      </section>

      <!-- Sales over time -->
      <section class="card">
        <header>
          <h2>{{ 'reports.wonPerMonth' | transloco }}</h2>
          <button matButton (click)="exportSales(d.sales)"><span class="material-symbols-outlined">download</span> CSV</button>
        </header>
        @if (d.sales.won.length === 0) {
          <p class="muted">{{ 'reports.noWins' | transloco }}</p>
        }
        <!-- One chart per currency: amounts in different currencies are never added up. -->
        <div class="multiples">
          @for (w of d.sales.won; track w.currency) {
            <div class="multiple" [attr.data-currency]="w.currency">
              <h3>{{ w.currency }}</h3>
              <app-bar-chart
                [categories]="monthLabels(d.sales.months)"
                [series]="[{ label: w.currency, slot: 1, values: wonAmounts(d.sales, w.currency) }]"
                [format]="moneyFormat(w.currency)"
                [label]="('reports.wonPerMonth' | transloco) + ' ' + w.currency"
                [tableLabel]="'reports.showTable' | transloco"
              />
            </div>
          }
        </div>
        <h3>{{ 'reports.wonAndLost' | transloco }}</h3>
        <app-bar-chart
          [categories]="monthLabels(d.sales.months)"
          [series]="wonLostSeries(d.sales)"
          [label]="'reports.wonAndLost' | transloco"
          [tableLabel]="'reports.showTable' | transloco"
        />
      </section>

      <div class="two-columns">
        <!-- Pipeline now -->
        <section class="card">
          <header>
            <h2>{{ 'reports.pipeline' | transloco }}</h2>
            <button matButton (click)="exportPipeline(d.pipeline)"><span class="material-symbols-outlined">download</span> CSV</button>
          </header>
          <table class="report-table pipeline-table">
            <thead>
              <tr>
                <th>{{ 'deals.stage' | transloco }}</th>
                <th class="num">%</th>
                <th class="num">{{ 'reports.openAmount' | transloco }}</th>
                <th class="num">{{ 'reports.weighted' | transloco }}</th>
              </tr>
            </thead>
            <tbody>
              @for (stage of d.pipeline; track stage.stageId) {
                <tr>
                  <td>{{ stage.name }}</td>
                  <td class="num">{{ stage.probability }}</td>
                  <td class="num">
                    @for (t of stage.totals; track t.currency) {
                      <div>{{ t.amount | appMoney: t.currency }} <span class="muted">({{ t.count }})</span></div>
                    } @empty {
                      <span class="muted">—</span>
                    }
                  </td>
                  <td class="num">
                    @for (t of stage.totals; track t.currency) {
                      <div>{{ t.weightedAmount | appMoney: t.currency }}</div>
                    }
                  </td>
                </tr>
              }
            </tbody>
          </table>
        </section>

        <!-- Why deals were lost -->
        <section class="card">
          <header><h2>{{ 'reports.lostReasons' | transloco }}</h2></header>
          @for (r of d.sales.lostReasons; track r.reason) {
            <div class="reason">
              <span class="reason-label">{{ r.reason ?? ('reports.noReason' | transloco) }}</span>
              <span class="reason-bar"><span [style.width.%]="(r.count / maxReason(d.sales)) * 100"></span></span>
              <span class="num">{{ r.count }}</span>
            </div>
          } @empty {
            <p class="muted">{{ 'reports.noLosses' | transloco }}</p>
          }
        </section>
      </div>

      <!-- Activities -->
      <section class="card">
        <header>
          <h2>{{ 'reports.activities' | transloco }} ({{ d.activities.total }})</h2>
          <button matButton (click)="exportActivities(d.activities)"><span class="material-symbols-outlined">download</span> CSV</button>
        </header>
        <app-bar-chart
          [categories]="monthLabels(d.activities.months)"
          [series]="activitySeries(d.activities)"
          [stacked]="true"
          [label]="'reports.activities' | transloco"
          [tableLabel]="'reports.showTable' | transloco"
        />
        <h3>{{ 'reports.byPerson' | transloco }}</h3>
        <table class="report-table activity-table">
          <thead>
            <tr>
              <th>{{ 'reports.person' | transloco }}</th>
              @for (type of types; track type) {
                <th class="num">{{ 'activities.types.' + type | transloco }}</th>
              }
              <th class="num">{{ 'reports.total' | transloco }}</th>
            </tr>
          </thead>
          <tbody>
            @for (u of d.activities.byUser; track u.userId) {
              <tr>
                <td>{{ u.userName ?? ('history.bySystem' | transloco) }}</td>
                @for (type of types; track type) {
                  <td class="num">{{ count(u.types, type) }}</td>
                }
                <td class="num"><strong>{{ u.total }}</strong></td>
              </tr>
            } @empty {
              <tr><td class="muted" [attr.colspan]="types.length + 2">{{ 'activities.empty' | transloco }}</td></tr>
            }
          </tbody>
        </table>
      </section>

      <!-- Tasks -->
      <section class="card">
        <header>
          <h2>{{ 'reports.tasks' | transloco }}</h2>
          <button matButton (click)="exportTasks(d.tasks)"><span class="material-symbols-outlined">download</span> CSV</button>
        </header>
        <table class="report-table task-table">
          <thead>
            <tr>
              <th>{{ 'tasks.assignee' | transloco }}</th>
              <th class="num">{{ 'reports.openNow' | transloco }}</th>
              <th class="num">{{ 'reports.overdueNow' | transloco }}</th>
              <th class="num">{{ 'reports.completedInPeriod' | transloco }}</th>
            </tr>
          </thead>
          <tbody>
            @for (a of d.tasks.byAssignee; track a.assigneeId) {
              <tr>
                <td>{{ a.assigneeName }}</td>
                <td class="num">{{ a.open }}</td>
                <td class="num" [class.overdue]="a.overdue > 0">{{ a.overdue }}</td>
                <td class="num">{{ a.completed }}</td>
              </tr>
            } @empty {
              <tr><td class="muted" colspan="4">{{ 'tasks.empty' | transloco }}</td></tr>
            }
          </tbody>
          @if (d.tasks.byAssignee.length) {
            <tfoot>
              <tr>
                <th>{{ 'reports.total' | transloco }}</th>
                <th class="num">{{ d.tasks.open }}</th>
                <th class="num">{{ d.tasks.overdue }}</th>
                <th class="num">{{ d.tasks.completed }}</th>
              </tr>
            </tfoot>
          }
        </table>
      </section>
    }
  `,
  styles: `
    .filters {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: 0.5rem 1rem;
      margin-bottom: var(--app-gap);
    }
    .presets {
      flex-wrap: wrap;
    }
    section {
      margin-bottom: var(--app-gap);
    }
    section header {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: 1rem;
      margin-bottom: 0.75rem;
    }
    h2 {
      font: var(--mat-sys-title-medium);
      margin: 0;
    }
    h3 {
      font: var(--mat-sys-title-small);
      color: var(--mat-sys-on-surface-variant);
      margin: 1rem 0 0.5rem;
    }
    button .material-symbols-outlined {
      margin-right: 0.25rem;
    }
    .tiles {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(11rem, 1fr));
      gap: var(--app-gap);
    }
    .tile {
      display: flex;
      flex-direction: column;
      gap: 0.25rem;
    }
    .tile strong {
      font: var(--mat-sys-headline-small);
      font-weight: 600;
    }
    .multiples {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(18rem, 1fr));
      gap: 0 var(--app-gap);
    }
    .two-columns {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(22rem, 1fr));
      gap: 0 var(--app-gap);
    }
    .report-table {
      width: 100%;
      border-collapse: collapse;
      font: var(--mat-sys-body-medium);
    }
    .report-table th {
      text-align: left;
      font: var(--mat-sys-label-medium);
      color: var(--mat-sys-on-surface-variant);
    }
    .report-table th,
    .report-table td {
      padding: 0.5rem 0.5rem 0.5rem 0;
      border-bottom: 1px solid var(--mat-sys-outline-variant);
      vertical-align: top;
    }
    .report-table tfoot th {
      border-bottom: none;
      color: var(--mat-sys-on-surface);
    }
    .num {
      text-align: right !important;
      white-space: nowrap;
    }
    .overdue {
      color: var(--mat-sys-error);
      font-weight: 600;
    }
    .reason {
      display: grid;
      grid-template-columns: minmax(8rem, 1fr) 2fr auto;
      align-items: center;
      gap: 0.75rem;
      padding: 0.35rem 0;
    }
    .reason-bar {
      height: 0.6rem;
      background: var(--mat-sys-surface-container-highest);
      border-radius: 0.3rem;
      overflow: hidden;
    }
    .reason-bar span {
      display: block;
      height: 100%;
      background: var(--viz-2);
      border-radius: 0.3rem;
    }
  `,
})
export class ReportPage {
  private readonly api = inject(ReportsApi);
  private readonly notify = inject(NotifyService);
  private readonly language = inject(LanguageService);
  private readonly transloco = inject(TranslocoService);

  readonly types = ACTIVITY_TYPES;
  readonly preset = signal<Preset>('6m');
  readonly customFrom = signal(iso(new Date(new Date().getFullYear(), new Date().getMonth() - 2, 1)));
  readonly customTo = signal(localToday());

  readonly data = signal<Reports | null>(null);
  readonly loading = signal(true);

  readonly period = computed<Period | null>(() => {
    const now = new Date();
    const to = localToday();
    const monthsBack = (n: number) => iso(new Date(now.getFullYear(), now.getMonth() - (n - 1), 1));

    switch (this.preset()) {
      case '3m':
        return { from: monthsBack(3), to };
      case '6m':
        return { from: monthsBack(6), to };
      case '12m':
        return { from: monthsBack(12), to };
      case 'year':
        return { from: `${now.getFullYear()}-01-01`, to };
      case 'custom':
        return this.customFrom() && this.customTo() && this.customFrom() <= this.customTo()
          ? { from: this.customFrom(), to: this.customTo() }
          : null;
    }
  });

  constructor() {
    effect(() => {
      const period = this.period();
      if (period) untracked(() => this.load(period));
    });
  }

  monthLabels(months: { year: number; month: number }[]): string[] {
    const spansYears = months.length > 0 && months[0].year !== months[months.length - 1].year;
    const format = new Intl.DateTimeFormat(this.language.current(), spansYears ? { month: 'short', year: '2-digit' } : { month: 'short' });
    return months.map((m) => format.format(new Date(m.year, m.month - 1, 1)));
  }

  wonAmounts(sales: SalesReport, currency: string): number[] {
    return sales.months.map((m) => m.won.find((w) => w.currency === currency)?.amount ?? 0);
  }

  wonLostSeries(sales: SalesReport): ChartSeries[] {
    return [
      { label: this.transloco.translate('reports.won'), slot: 1, values: sales.months.map((m) => m.won.reduce((n, w) => n + w.count, 0)) },
      { label: this.transloco.translate('reports.lost'), slot: 2, values: sales.months.map((m) => m.lostCount) },
    ];
  }

  activitySeries(report: ActivityReport): ChartSeries[] {
    return this.types.map((type, index) => ({
      label: this.transloco.translate(`activities.types.${type}`),
      slot: (index + 1) as 1 | 2 | 3 | 4,
      values: report.months.map((m) => this.count(m.types, type)),
    }));
  }

  count(types: { type: string; count: number }[], type: string): number {
    return types.find((t) => t.type === type)?.count ?? 0;
  }

  maxReason(sales: SalesReport): number {
    return Math.max(1, ...sales.lostReasons.map((r) => r.count));
  }

  percent(value: number): string {
    return new Intl.NumberFormat(this.language.current(), { style: 'percent', maximumFractionDigits: 0 }).format(value);
  }

  moneyFormat(currency: string): (value: number) => string {
    return (value) =>
      new Intl.NumberFormat(this.language.current(), { style: 'currency', currency, notation: 'compact', maximumFractionDigits: 1 }).format(value);
  }

  exportSales(sales: SalesReport): void {
    const currencies = sales.won.map((w) => w.currency);
    downloadCsv(this.fileName('sales'), [
      [this.t('reports.month'), ...currencies.map((c) => `${this.t('reports.won')} ${c}`), this.t('reports.won'), this.t('reports.lost')],
      ...sales.months.map((m) => [
        `${m.year}-${String(m.month).padStart(2, '0')}`,
        ...currencies.map((c) => m.won.find((w) => w.currency === c)?.amount ?? 0),
        m.won.reduce((n, w) => n + w.count, 0),
        m.lostCount,
      ]),
    ]);
  }

  exportPipeline(pipeline: PipelineStageReport[]): void {
    downloadCsv(this.fileName('pipeline'), [
      [this.t('deals.stage'), '%', this.t('deals.currency'), this.t('reports.dealCount'), this.t('reports.openAmount'), this.t('reports.weighted')],
      ...pipeline.flatMap((s) => s.totals.map((t) => [s.name, s.probability, t.currency, t.count, t.amount, t.weightedAmount])),
    ]);
  }

  exportActivities(report: ActivityReport): void {
    downloadCsv(this.fileName('activities'), [
      [this.t('reports.person'), ...this.types.map((t) => this.t(`activities.types.${t}`)), this.t('reports.total')],
      ...report.byUser.map((u) => [u.userName ?? this.t('history.bySystem'), ...this.types.map((t) => this.count(u.types, t)), u.total]),
    ]);
  }

  exportTasks(report: TaskReport): void {
    downloadCsv(this.fileName('tasks'), [
      [this.t('tasks.assignee'), this.t('reports.openNow'), this.t('reports.overdueNow'), this.t('reports.completedInPeriod')],
      ...report.byAssignee.map((a) => [a.assigneeName, a.open, a.overdue, a.completed]),
    ]);
  }

  private t(key: string): string {
    return this.transloco.translate(key);
  }

  private fileName(report: string): string {
    const period = this.period();
    return `${report}_${period?.from}_${period?.to}.csv`;
  }

  private load(period: Period): void {
    this.loading.set(true);
    forkJoin({
      sales: this.api.sales(period),
      pipeline: this.api.pipeline(),
      activities: this.api.activities(period),
      tasks: this.api.tasks(period, localToday()),
    }).subscribe({
      next: (data) => {
        this.data.set(data);
        this.loading.set(false);
      },
      error: (error) => {
        this.loading.set(false);
        this.notify.error(error);
      },
    });
  }
}
