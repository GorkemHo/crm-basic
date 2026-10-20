import { Component, OnInit, inject, input, numberAttribute, signal, viewChild } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatTabsModule } from '@angular/material/tabs';
import { Router, RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { filter } from 'rxjs';

import { CustomerDto } from '../../core/api/models';
import { AuthStore } from '../../core/auth/auth.store';
import { Permissions } from '../../core/auth/permissions';
import { AppDatePipe } from '../../core/i18n/date.pipe';
import { ConfirmService } from '../../core/ui/confirm.service';
import { NotifyService } from '../../core/ui/notify.service';
import { HistoryTimeline } from '../../shared/history-timeline';
import { PageHeader } from '../../shared/page-header';
import { StatusTag } from '../../shared/status-tag';
import { CustomerFormDialog } from './customer-form-dialog';
import { ActivityTimeline } from '../activities/activity-timeline';
import { TaskList } from '../tasks/task-list';
import { AttachmentsPanel } from '../attachments/attachments-panel';
import { CustomersApi } from './customers.api';

@Component({
  selector: 'app-customer-detail',
  imports: [RouterLink, TranslocoPipe, MatButtonModule, MatTabsModule, MatProgressBarModule, PageHeader, StatusTag, AppDatePipe, HistoryTimeline, ActivityTimeline, TaskList, AttachmentsPanel],
  template: `
    @if (customer(); as c) {
      <app-page-header [title]="c.firstName + ' ' + c.lastName" [subtitle]="c.email" backLink="/customers">
        @if (canWrite) {
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
              <div><dt>{{ 'customers.fullName' | transloco }}</dt><dd>{{ c.firstName }} {{ c.lastName }}</dd></div>
              <div><dt>{{ 'customers.email' | transloco }}</dt><dd><a [href]="'mailto:' + c.email">{{ c.email }}</a></dd></div>
              <div><dt>{{ 'customers.phone' | transloco }}</dt><dd>{{ c.phoneNumber || ('common.none' | transloco) }}</dd></div>
              <div><dt>{{ 'customers.company' | transloco }}</dt><dd><a [routerLink]="['/companies', c.companyId]">{{ c.companyName }}</a></dd></div>
              <div><dt>{{ 'common.status' | transloco }}</dt><dd><app-status-tag [active]="c.isActive" /></dd></div>
              <div><dt>{{ 'common.createdAt' | transloco }}</dt><dd>{{ c.createdAt | appDate: 'datetime' }}</dd></div>
              <div><dt>{{ 'common.updatedAt' | transloco }}</dt><dd>{{ c.updatedAt | appDate: 'datetime' }}</dd></div>
            </dl>
          </mat-tab>
          @if (canReadActivities) {
            <mat-tab [label]="'activities.title' | transloco">
              <ng-template matTabContent>
                <div class="tab-body">
                  <app-activity-timeline
                    [customerId]="c.id"
                    [context]="{ company: { id: c.companyId, name: c.companyName }, customerId: c.id }"
                    hide="customer"
                  />
                </div>
              </ng-template>
            </mat-tab>
          }
          @if (canReadTasks) {
            <mat-tab [label]="'tasks.title' | transloco">
              <ng-template matTabContent>
                <div class="tab-body">
                  <app-task-list [customerId]="c.id" [context]="{ company: { id: c.companyId, name: c.companyName }, customerId: c.id }" hide="customer" />
                </div>
              </ng-template>
            </mat-tab>
          }
          @if (canReadFiles) {
            <mat-tab [label]="'attachments.title' | transloco">
              <ng-template matTabContent>
                <div class="tab-body"><app-attachments-panel recordType="customer" [recordId]="c.id" /></div>
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
export class CustomerDetail implements OnInit {
  readonly api = inject(CustomersApi);
  private readonly router = inject(Router);
  private readonly dialog = inject(MatDialog);
  private readonly notify = inject(NotifyService);
  private readonly confirm = inject(ConfirmService);

  private readonly store = inject(AuthStore);
  readonly canWrite = this.store.can(Permissions.customersWrite);
  readonly canDelete = this.store.can(Permissions.customersDelete);
  readonly canReadActivities = this.store.can(Permissions.activitiesRead);
  readonly canReadTasks = this.store.can(Permissions.tasksRead);
  readonly canReadFiles = this.store.can(Permissions.attachmentsRead);

  /** Route parameter. */
  readonly id = input.required({ transform: numberAttribute });

  readonly customer = signal<CustomerDto | null>(null);
  private readonly history = viewChild<HistoryTimeline>('history');

  ngOnInit(): void {
    this.api.get(this.id()).subscribe({
      next: (customer) => this.customer.set(customer),
      error: (error) => {
        this.notify.error(error);
        void this.router.navigate(['/customers']);
      },
    });
  }

  edit(customer: CustomerDto): void {
    CustomerFormDialog.open(this.dialog, { customer })
      .pipe(filter(Boolean))
      .subscribe((saved) => {
        this.customer.set(saved);
        this.history()?.reload();
      });
  }

  remove(customer: CustomerDto): void {
    this.confirm.delete(`${customer.firstName} ${customer.lastName}`, () =>
      this.api.delete(customer.id).subscribe({
        next: () => {
          this.notify.success('common.deleted');
          void this.router.navigate(['/customers']);
        },
        error: (error) => this.notify.error(error),
      }),
    );
  }
}
