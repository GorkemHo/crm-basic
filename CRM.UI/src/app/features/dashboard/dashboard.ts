import { Component, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { Router, RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { Observable, catchError, filter, forkJoin, map, of } from 'rxjs';

import { ActivityDto, CompanyDto, CustomerDto, MyTaskSummary, PipelineSummary } from '../../core/api/models';
import { AuthStore } from '../../core/auth/auth.store';
import { Permissions } from '../../core/auth/permissions';
import { AppDatePipe } from '../../core/i18n/date.pipe';
import { AppMoneyPipe } from '../../core/i18n/money.pipe';
import { PageHeader } from '../../shared/page-header';
import { CompaniesApi } from '../companies/companies.api';
import { CompanyFormDialog } from '../companies/company-form-dialog';
import { CustomerFormDialog } from '../customers/customer-form-dialog';
import { CustomersApi } from '../customers/customers.api';
import { DealsApi } from '../deals/deals.api';
import { ACTIVITY_ICONS, ActivitiesApi } from '../activities/activities.api';
import { TaskList } from '../tasks/task-list';
import { TasksApi } from '../tasks/tasks.api';

interface Kpi {
  label: string;
  icon: string;
  value: number;
  link: string;
  queryParams?: Record<string, string>;
}

interface Overview {
  kpis: Kpi[];
  latestCompanies: CompanyDto[] | null;
  latestCustomers: CustomerDto[] | null;
  pipeline: PipelineSummary | null;
  latestActivities: ActivityDto[] | null;
  myTasks: MyTaskSummary | null;
}

/** Counts and latest records; each block only appears if the user may read that data. */
@Component({
  selector: 'app-dashboard',
  imports: [RouterLink, TranslocoPipe, MatButtonModule, MatProgressBarModule, PageHeader, AppDatePipe, AppMoneyPipe, TaskList],
  template: `
    <app-page-header [title]="'dashboard.welcome' | transloco: { name: firstName }" [subtitle]="'dashboard.subtitle' | transloco">
      @if (canWriteCompanies) {
        <button matButton="tonal" (click)="newCompany()"><span class="material-symbols-outlined">add_business</span> {{ 'dashboard.newCompany' | transloco }}</button>
      }
      @if (canWriteCustomers) {
        <button matButton="filled" (click)="newCustomer()"><span class="material-symbols-outlined">person_add</span> {{ 'dashboard.newCustomer' | transloco }}</button>
      }
    </app-page-header>

    @if (overview(); as o) {
      <section class="kpis">
        @for (kpi of o.kpis; track kpi.label) {
          <a class="card kpi" [routerLink]="kpi.link">
            <span class="kpi-icon material-symbols-outlined">{{ kpi.icon }}</span>
            <span class="kpi-value">{{ kpi.value }}</span>
            <span class="muted">{{ kpi.label | transloco }}</span>
          </a>
        }
      </section>

      @if (o.myTasks; as t) {
        <section class="card my-tasks">
          <header>
            <h2>{{ 'dashboard.myTasks' | transloco }}</h2>
            <div class="task-counts">
              <span class="count overdue" [class.zero]="!t.overdue">{{ 'dashboard.overdue' | transloco: { n: t.overdue } }}</span>
              <span class="count today" [class.zero]="!t.dueToday">{{ 'dashboard.dueToday' | transloco: { n: t.dueToday } }}</span>
              <span class="count">{{ 'dashboard.openTasks' | transloco: { n: t.open } }}</span>
            </div>
            <a routerLink="/tasks">{{ 'common.viewAll' | transloco }}</a>
          </header>
          <app-task-list [onlyMine]="true" [toolbar]="false" [pageSize]="5" />
        </section>
      }

      @if (o.pipeline; as p) {
        <section class="card pipeline">
          <header>
            <h2>{{ 'dashboard.pipeline' | transloco }}</h2>
            <a routerLink="/pipeline">{{ 'dashboard.openBoard' | transloco }}</a>
          </header>
          <div class="pipeline-grid">
            <div>
              <span class="muted label">{{ 'dashboard.openPipeline' | transloco }}</span>
              @for (t of p.open; track t.currency) {
                <div class="money">
                  <strong>{{ t.amount | appMoney: t.currency }}</strong>
                  <span class="muted">{{ 'dashboard.dealCount' | transloco: { n: t.count } }} · {{ 'dashboard.weighted' | transloco: { value: (t.weightedAmount | appMoney: t.currency) } }}</span>
                </div>
              } @empty {
                <p class="muted">{{ 'dashboard.nothingYet' | transloco }}</p>
              }
            </div>
            <div>
              <span class="muted label">{{ 'dashboard.wonThisMonth' | transloco }}</span>
              @for (t of p.wonThisMonth; track t.currency) {
                <div class="money won">
                  <strong>{{ t.amount | appMoney: t.currency }}</strong>
                  <span class="muted">{{ 'dashboard.dealCount' | transloco: { n: t.count } }}</span>
                </div>
              } @empty {
                <p class="muted">{{ 'dashboard.nothingYet' | transloco }}</p>
              }
              <p class="muted lost">{{ 'dashboard.lostThisMonth' | transloco: { n: p.lostThisMonth } }}</p>
            </div>
          </div>
        </section>
      }

      <section class="lists">
        @if (o.latestCompanies; as companies) {
          <div class="card">
            <header>
              <h2>{{ 'dashboard.latestCompanies' | transloco }}</h2>
              <a routerLink="/companies">{{ 'common.viewAll' | transloco }}</a>
            </header>
            @for (company of companies; track company.id) {
              <a class="row" [routerLink]="['/companies', company.id]">
                <span class="avatar"><span class="material-symbols-outlined">apartment</span></span>
                <span class="grow">
                  <strong>{{ company.companyName }}</strong>
                  <span class="muted">{{ 'companies.customerCount' | transloco }}: {{ company.customerCount }}</span>
                </span>
                <span class="muted date">{{ company.createdAt | appDate }}</span>
              </a>
            } @empty {
              <p class="muted">{{ 'dashboard.nothingYet' | transloco }}</p>
            }
          </div>
        }
        @if (o.latestActivities; as activities) {
          <div class="card recent-activities">
            <header>
              <h2>{{ 'dashboard.latestActivities' | transloco }}</h2>
              <a routerLink="/activities">{{ 'common.viewAll' | transloco }}</a>
            </header>
            @for (activity of activities; track activity.id) {
              <a class="row" [routerLink]="['/companies', activity.companyId]">
                <span class="avatar"><span class="material-symbols-outlined">{{ icons[activity.type] }}</span></span>
                <span class="grow">
                  <strong>{{ activity.subject }}</strong>
                  <span class="muted">{{ activity.companyName }}</span>
                </span>
                <span class="muted date">{{ activity.occurredAt | appDate }}</span>
              </a>
            } @empty {
              <p class="muted">{{ 'dashboard.nothingYet' | transloco }}</p>
            }
          </div>
        }
        @if (o.latestCustomers; as customers) {
          <div class="card">
            <header>
              <h2>{{ 'dashboard.latestCustomers' | transloco }}</h2>
              <a routerLink="/customers">{{ 'common.viewAll' | transloco }}</a>
            </header>
            @for (customer of customers; track customer.id) {
              <a class="row" [routerLink]="['/customers', customer.id]">
                <span class="avatar initials">{{ customer.firstName[0] }}{{ customer.lastName[0] }}</span>
                <span class="grow">
                  <strong>{{ customer.firstName }} {{ customer.lastName }}</strong>
                  <span class="muted">{{ customer.companyName }}</span>
                </span>
                <span class="muted date">{{ customer.createdAt | appDate }}</span>
              </a>
            } @empty {
              <p class="muted">{{ 'dashboard.nothingYet' | transloco }}</p>
            }
          </div>
        }
      </section>
    } @else {
      <mat-progress-bar mode="indeterminate" />
    }
  `,
  styles: `
    button .material-symbols-outlined {
      margin-right: 0.25rem;
    }
    .kpis {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(13rem, 1fr));
      gap: var(--app-gap);
      margin-bottom: var(--app-gap);
    }
    .kpi {
      display: flex;
      flex-direction: column;
      gap: 0.25rem;
      color: inherit;
      text-decoration: none !important;
      transition: border-color 0.15s, transform 0.15s;
    }
    .kpi:hover {
      border-color: var(--mat-sys-primary);
      transform: translateY(-2px);
    }
    .kpi-icon {
      width: 2.5rem;
      height: 2.5rem;
      border-radius: 0.75rem;
      display: inline-flex;
      align-items: center;
      justify-content: center;
      background: var(--mat-sys-primary-container);
      color: var(--mat-sys-on-primary-container);
      margin-bottom: 0.5rem;
    }
    .kpi-value {
      font: var(--mat-sys-display-small);
      font-weight: 600;
    }
    .lists {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(22rem, 1fr));
      gap: var(--app-gap);
    }
    .pipeline,
    .my-tasks {
      margin-bottom: var(--app-gap);
    }
    .my-tasks header {
      display: flex;
      flex-wrap: wrap;
      align-items: baseline;
      gap: 0.5rem 1rem;
      margin-bottom: 0.5rem;
    }
    .my-tasks h2 {
      flex: 0 0 auto;
    }
    .task-counts {
      flex: 1;
      display: flex;
      flex-wrap: wrap;
      gap: 0.5rem;
    }
    .count {
      font: var(--mat-sys-label-medium);
      padding: 0.1rem 0.6rem;
      border-radius: 1rem;
      background: var(--mat-sys-surface-container-highest);
    }
    .count.overdue:not(.zero) {
      background: var(--mat-sys-error-container);
      color: var(--mat-sys-on-error-container);
    }
    .count.today:not(.zero) {
      background: var(--mat-sys-primary-container);
      color: var(--mat-sys-on-primary-container);
    }
    .pipeline header {
      display: flex;
      justify-content: space-between;
      align-items: baseline;
      margin-bottom: 0.75rem;
    }
    .pipeline-grid {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(16rem, 1fr));
      gap: var(--app-gap);
    }
    .label {
      display: block;
      font: var(--mat-sys-label-large);
      margin-bottom: 0.5rem;
    }
    .money {
      display: flex;
      flex-direction: column;
      margin-bottom: 0.5rem;
    }
    .money strong {
      font: var(--mat-sys-title-large);
    }
    .money.won strong {
      color: var(--app-success);
    }
    .lost {
      margin: 0;
    }
    .lists header {
      display: flex;
      justify-content: space-between;
      align-items: baseline;
      margin-bottom: 0.75rem;
    }
    h2 {
      font: var(--mat-sys-title-medium);
    }
    .row {
      display: flex;
      align-items: center;
      gap: 0.75rem;
      padding: 0.6rem 0.5rem;
      border-radius: 0.75rem;
      color: inherit;
      text-decoration: none !important;
    }
    .row:hover {
      background: var(--mat-sys-surface-container);
    }
    .grow {
      flex: 1;
      display: flex;
      flex-direction: column;
      min-width: 0;
    }
    .grow strong {
      overflow: hidden;
      text-overflow: ellipsis;
      white-space: nowrap;
    }
    .avatar {
      flex: 0 0 2.5rem;
      height: 2.5rem;
      border-radius: 50%;
      display: inline-flex;
      align-items: center;
      justify-content: center;
      background: var(--mat-sys-secondary-container);
      color: var(--mat-sys-on-secondary-container);
      font: var(--mat-sys-label-large);
    }
    .date {
      white-space: nowrap;
    }
  `,
})
export class Dashboard {
  private readonly companies = inject(CompaniesApi);
  private readonly customers = inject(CustomersApi);
  private readonly deals = inject(DealsApi);
  private readonly activities = inject(ActivitiesApi);
  private readonly tasks = inject(TasksApi);
  private readonly dialog = inject(MatDialog);
  private readonly router = inject(Router);
  private readonly store = inject(AuthStore);

  readonly firstName = this.store.user()?.firstName ?? '';
  readonly canWriteCompanies = this.store.can(Permissions.companiesWrite);
  readonly canWriteCustomers = this.store.can(Permissions.customersWrite);
  private readonly canReadCompanies = this.store.can(Permissions.companiesRead);
  private readonly canReadCustomers = this.store.can(Permissions.customersRead);
  private readonly canReadDeals = this.store.can(Permissions.dealsRead);
  private readonly canReadActivities = this.store.can(Permissions.activitiesRead);
  private readonly canReadTasks = this.store.can(Permissions.tasksRead);

  readonly icons = ACTIVITY_ICONS;

  readonly overview = toSignal(this.load());

  newCompany(): void {
    CompanyFormDialog.open(this.dialog)
      .pipe(filter(Boolean))
      .subscribe((company) => void this.router.navigate(['/companies', company.id]));
  }

  newCustomer(): void {
    CustomerFormDialog.open(this.dialog)
      .pipe(filter(Boolean))
      .subscribe((customer) => void this.router.navigate(['/customers', customer.id]));
  }

  private load(): Observable<Overview> {
    // Totals come from the paged endpoints' totalCount with a one-row page.
    const count = (request: Observable<{ totalCount: number }>) => request.pipe(map((p) => p.totalCount), catchError(() => of(0)));
    const latest = <T>(request: Observable<{ items: T[] }>) => request.pipe(map((p) => p.items), catchError(() => of([] as T[])));
    const none = of(null);

    return forkJoin({
      companies: this.canReadCompanies ? count(this.companies.list({ pageSize: 1 })) : none,
      activeCompanies: this.canReadCompanies ? count(this.companies.list({ pageSize: 1, isActive: true })) : none,
      customers: this.canReadCustomers ? count(this.customers.list({ pageSize: 1 })) : none,
      activeCustomers: this.canReadCustomers ? count(this.customers.list({ pageSize: 1, isActive: true })) : none,
      latestCompanies: this.canReadCompanies ? latest(this.companies.list({ pageSize: 5, sortBy: 'createdAt', sortDirection: 'desc' })) : none,
      latestCustomers: this.canReadCustomers ? latest(this.customers.list({ pageSize: 5, sortBy: 'createdAt', sortDirection: 'desc' })) : none,
      pipeline: this.canReadDeals ? this.deals.summary().pipe(catchError(() => none)) : none,
      latestActivities: this.canReadActivities ? latest(this.activities.list({ pageSize: 5 })) : none,
      myTasks: this.canReadTasks ? this.tasks.mySummary().pipe(catchError(() => none)) : none,
    }).pipe(
      map((r) => ({
        kpis: [
          { label: 'dashboard.companies', icon: 'apartment', value: r.companies, link: '/companies' },
          { label: 'dashboard.activeCompanies', icon: 'domain_verification', value: r.activeCompanies, link: '/companies' },
          { label: 'dashboard.customers', icon: 'groups', value: r.customers, link: '/customers' },
          { label: 'dashboard.activeCustomers', icon: 'how_to_reg', value: r.activeCustomers, link: '/customers' },
        ].filter((k): k is Kpi => k.value !== null),
        latestCompanies: r.latestCompanies,
        latestCustomers: r.latestCustomers,
        pipeline: r.pipeline,
        latestActivities: r.latestActivities,
        myTasks: r.myTasks,
      })),
    );
  }
}
