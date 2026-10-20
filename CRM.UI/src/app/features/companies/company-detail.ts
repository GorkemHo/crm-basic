import { Component, OnInit, computed, inject, input, numberAttribute, signal, viewChild } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatTabsModule } from '@angular/material/tabs';
import { Router } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { filter } from 'rxjs';

import { CompanyDto } from '../../core/api/models';
import { AuthStore } from '../../core/auth/auth.store';
import { Permissions } from '../../core/auth/permissions';
import { AppDatePipe } from '../../core/i18n/date.pipe';
import { ConfirmService } from '../../core/ui/confirm.service';
import { NotifyService } from '../../core/ui/notify.service';
import { HistoryTimeline } from '../../shared/history-timeline';
import { PageHeader } from '../../shared/page-header';
import { StatusTag } from '../../shared/status-tag';
import { CustomerTable } from '../customers/customer-table';
import { DealTable } from '../deals/deal-table';
import { ActivityTimeline } from '../activities/activity-timeline';
import { TaskList } from '../tasks/task-list';
import { AttachmentsPanel } from '../attachments/attachments-panel';
import { CompaniesApi } from './companies.api';
import { CompanyFormDialog } from './company-form-dialog';

@Component({
  selector: 'app-company-detail',
  imports: [TranslocoPipe, MatButtonModule, MatTabsModule, MatProgressBarModule, PageHeader, StatusTag, AppDatePipe, HistoryTimeline, CustomerTable, DealTable, ActivityTimeline, TaskList, AttachmentsPanel],
  template: `
    @if (company(); as c) {
      <app-page-header [title]="c.companyName" [subtitle]="c.taxNumber ?? ''" backLink="/companies">
        @if (canWrite) {
          <button matButton="outlined" (click)="toggleActive(c)" [disabled]="toggling()">
            <span class="material-symbols-outlined">{{ c.isActive ? 'pause_circle' : 'play_circle' }}</span>
            {{ (c.isActive ? 'companies.deactivate' : 'companies.activate') | transloco }}
          </button>
          <button matButton="tonal" (click)="edit(c)"><span class="material-symbols-outlined">edit</span> {{ 'common.edit' | transloco }}</button>
        }
        @if (canDelete) {
          <button matButton="outlined" class="danger" (click)="remove(c)"><span class="material-symbols-outlined">delete</span> {{ 'common.delete' | transloco }}</button>
        }
      </app-page-header>

      <div class="card tabs-card">
        <mat-tab-group animationDuration="0ms">
          <mat-tab [label]="'companies.overview' | transloco">
            <dl class="details tab-body">
              <div><dt>{{ 'companies.name' | transloco }}</dt><dd>{{ c.companyName }}</dd></div>
              <div><dt>{{ 'companies.taxNumber' | transloco }}</dt><dd>{{ c.taxNumber || ('common.none' | transloco) }}</dd></div>
              <div><dt>{{ 'companies.customerCount' | transloco }}</dt><dd>{{ c.customerCount }}</dd></div>
              <div><dt>{{ 'common.status' | transloco }}</dt><dd><app-status-tag [active]="c.isActive" /></dd></div>
              <div><dt>{{ 'common.createdAt' | transloco }}</dt><dd>{{ c.createdAt | appDate: 'datetime' }}</dd></div>
              <div><dt>{{ 'common.updatedAt' | transloco }}</dt><dd>{{ c.updatedAt | appDate: 'datetime' }}</dd></div>
            </dl>
          </mat-tab>
          @if (canReadActivities) {
            <mat-tab [label]="'activities.title' | transloco">
              <ng-template matTabContent>
                <div class="tab-body">
                  <app-activity-timeline [companyId]="c.id" [context]="{ company: companyRef() }" hide="company" />
                </div>
              </ng-template>
            </mat-tab>
          }
          @if (canReadCustomers) {
            <mat-tab [label]="('companies.customersTab' | transloco) + ' (' + c.customerCount + ')'">
              <ng-template matTabContent>
                <div class="tab-body"><app-customer-table [company]="companyRef()" /></div>
              </ng-template>
            </mat-tab>
          }
          @if (canReadDeals) {
            <mat-tab [label]="'companies.dealsTab' | transloco">
              <ng-template matTabContent>
                <div class="tab-body"><app-deal-table [company]="companyRef()" /></div>
              </ng-template>
            </mat-tab>
          }
          @if (canReadTasks) {
            <mat-tab [label]="'tasks.title' | transloco">
              <ng-template matTabContent>
                <div class="tab-body">
                  <app-task-list [companyId]="c.id" [context]="{ company: companyRef() }" hide="company" />
                </div>
              </ng-template>
            </mat-tab>
          }
          @if (canReadFiles) {
            <mat-tab [label]="'attachments.title' | transloco">
              <ng-template matTabContent>
                <div class="tab-body"><app-attachments-panel recordType="company" [recordId]="c.id" /></div>
              </ng-template>
            </mat-tab>
          }
          <mat-tab [label]="'companies.history' | transloco">
            <ng-template matTabContent>
              <div class="tab-body"><app-history-timeline #history [url]="api.historyUrl(c.id)" /></div>
            </ng-template>
          </mat-tab>
        </mat-tab-group>
      </div>
    } @else {
      <mat-progress-bar mode="indeterminate" />
    }
  `,
  styles: `
    .tabs-card {
      padding: 0;
      overflow: hidden;
    }
    .tab-body {
      padding: var(--app-gap);
    }
    button .material-symbols-outlined {
      margin-right: 0.25rem;
    }
    .danger {
      --mat-button-outlined-label-text-color: var(--mat-sys-error);
    }
  `,
})
export class CompanyDetail implements OnInit {
  readonly api = inject(CompaniesApi);
  private readonly router = inject(Router);
  private readonly dialog = inject(MatDialog);
  private readonly notify = inject(NotifyService);
  private readonly confirm = inject(ConfirmService);

  private readonly store = inject(AuthStore);
  readonly canWrite = this.store.can(Permissions.companiesWrite);
  readonly canDelete = this.store.can(Permissions.companiesDelete);
  readonly canReadCustomers = this.store.can(Permissions.customersRead);
  readonly canReadDeals = this.store.can(Permissions.dealsRead);
  readonly canReadActivities = this.store.can(Permissions.activitiesRead);
  readonly canReadTasks = this.store.can(Permissions.tasksRead);
  readonly canReadFiles = this.store.can(Permissions.attachmentsRead);

  /** Route parameter. */
  readonly id = input.required({ transform: numberAttribute });

  readonly company = signal<CompanyDto | null>(null);
  readonly companyRef = computed(() => {
    const c = this.company();
    return c ? { id: c.id, name: c.companyName } : null;
  });

  readonly toggling = signal(false);
  private readonly history = viewChild<HistoryTimeline>('history');

  ngOnInit(): void {
    this.api.get(this.id()).subscribe({
      next: (company) => this.company.set(company),
      error: (error) => {
        this.notify.error(error);
        void this.router.navigate(['/companies']);
      },
    });
  }

  edit(company: CompanyDto): void {
    CompanyFormDialog.open(this.dialog, company)
      .pipe(filter(Boolean))
      .subscribe((saved) => this.onSaved(saved));
  }

  toggleActive(company: CompanyDto): void {
    this.toggling.set(true);
    this.api.update(company.id, { isActive: !company.isActive }).subscribe({
      next: (updated) => {
        this.toggling.set(false);
        this.notify.success('common.saved');
        this.onSaved(updated);
      },
      error: (error) => {
        this.toggling.set(false);
        this.notify.error(error);
      },
    });
  }

  remove(company: CompanyDto): void {
    this.confirm.delete(company.companyName, () =>
      this.api.delete(company.id).subscribe({
        next: () => {
          this.notify.success('common.deleted');
          void this.router.navigate(['/companies']);
        },
        error: (error) => this.notify.error(error),
      }),
    );
  }

  private onSaved(company: CompanyDto): void {
    this.company.set(company);
    this.history()?.reload();
  }
}
