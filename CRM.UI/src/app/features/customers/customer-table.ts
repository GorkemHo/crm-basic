import { Component, computed, inject, input, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatPaginatorModule } from '@angular/material/paginator';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { MatSortModule } from '@angular/material/sort';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';
import { Router, RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { filter } from 'rxjs';

import { CustomerDto } from '../../core/api/models';
import { AuthStore } from '../../core/auth/auth.store';
import { Permissions } from '../../core/auth/permissions';
import { AppDatePipe } from '../../core/i18n/date.pipe';
import { ConfirmService } from '../../core/ui/confirm.service';
import { NotifyService } from '../../core/ui/notify.service';
import { PagedTable } from '../../shared/paged-table';
import { StatusTag } from '../../shared/status-tag';
import { activeFilterOptions } from '../../shared/translated';
import { CompanySelect } from '../companies/company-select';
import { CustomerFormDialog } from './customer-form-dialog';
import { CustomersApi } from './customers.api';

/**
 * Searchable, paged customer table with create/edit/delete.
 * With `company` set it shows only that company's customers (company page tab).
 */
@Component({
  selector: 'app-customer-table',
  imports: [
    FormsModule, RouterLink, TranslocoPipe, MatTableModule, MatSortModule, MatPaginatorModule, MatProgressBarModule,
    MatButtonModule, MatFormFieldModule, MatInputModule, MatSelectModule, MatTooltipModule, StatusTag, AppDatePipe, CompanySelect,
  ],
  template: `
    <div class="table-toolbar">
      <mat-form-field class="grow">
        <mat-label>{{ 'common.search' | transloco }}</mat-label>
        <span matPrefix class="material-symbols-outlined prefix">search</span>
        <input matInput type="search" [placeholder]="'customers.searchPlaceholder' | transloco" [ngModel]="search()" (ngModelChange)="search.set($event)" />
      </mat-form-field>
      @if (!company()) {
        <app-company-select
          class="company-filter"
          [label]="'customers.company' | transloco"
          [clearable]="true"
          [ngModel]="companyFilter()"
          (ngModelChange)="companyFilter.set($event)"
        />
      }
      <mat-form-field>
        <mat-label>{{ 'common.status' | transloco }}</mat-label>
        <mat-select [ngModel]="status()" (ngModelChange)="status.set($event)">
          @for (option of statusOptions(); track option.value) {
            <mat-option [value]="option.value">{{ option.label }}</mat-option>
          }
        </mat-select>
      </mat-form-field>
      @if (canWrite) {
        <button matButton="filled" class="new-button" (click)="create()">
          <span class="material-symbols-outlined">add</span> {{ (company() ? 'companies.addCustomer' : 'customers.new') | transloco }}
        </button>
      }
    </div>

    <div class="table-wrapper">
      @if (table.loading()) {
        <mat-progress-bar mode="indeterminate" />
      }
      <table
        mat-table
        [dataSource]="table.items()"
        matSort
        [matSortActive]="table.sort().active"
        [matSortDirection]="table.sort().direction"
        (matSortChange)="table.onSort($event)"
      >
        <ng-container matColumnDef="name">
          <th mat-header-cell *matHeaderCellDef mat-sort-header="firstName">{{ 'customers.fullName' | transloco }}</th>
          <td mat-cell *matCellDef="let c"><a [routerLink]="['/customers', c.id]" (click)="$event.stopPropagation()">{{ c.firstName }} {{ c.lastName }}</a></td>
        </ng-container>
        <ng-container matColumnDef="email">
          <th mat-header-cell *matHeaderCellDef mat-sort-header>{{ 'customers.email' | transloco }}</th>
          <td mat-cell *matCellDef="let c">{{ c.email }}</td>
        </ng-container>
        <ng-container matColumnDef="phoneNumber">
          <th mat-header-cell *matHeaderCellDef>{{ 'customers.phone' | transloco }}</th>
          <td mat-cell *matCellDef="let c">{{ c.phoneNumber || ('common.none' | transloco) }}</td>
        </ng-container>
        <ng-container matColumnDef="company">
          <th mat-header-cell *matHeaderCellDef>{{ 'customers.company' | transloco }}</th>
          <td mat-cell *matCellDef="let c"><a [routerLink]="['/companies', c.companyId]" (click)="$event.stopPropagation()">{{ c.companyName }}</a></td>
        </ng-container>
        <ng-container matColumnDef="isActive">
          <th mat-header-cell *matHeaderCellDef mat-sort-header>{{ 'common.status' | transloco }}</th>
          <td mat-cell *matCellDef="let c"><app-status-tag [active]="c.isActive" /></td>
        </ng-container>
        <ng-container matColumnDef="createdAt">
          <th mat-header-cell *matHeaderCellDef mat-sort-header>{{ 'common.createdAt' | transloco }}</th>
          <td mat-cell *matCellDef="let c">{{ c.createdAt | appDate }}</td>
        </ng-container>
        <ng-container matColumnDef="actions">
          <th mat-header-cell *matHeaderCellDef></th>
          <td mat-cell *matCellDef="let c">
            <div class="row-actions" (click)="$event.stopPropagation()">
              @if (canWrite) {
                <button matIconButton [matTooltip]="'common.edit' | transloco" (click)="edit(c)" [attr.aria-label]="'common.edit' | transloco">
                  <span class="material-symbols-outlined">edit</span>
                </button>
              }
              @if (canDelete) {
                <button matIconButton [matTooltip]="'common.delete' | transloco" (click)="remove(c)" [attr.aria-label]="'common.delete' | transloco">
                  <span class="material-symbols-outlined">delete</span>
                </button>
              }
            </div>
          </td>
        </ng-container>

        <tr mat-header-row *matHeaderRowDef="columns()"></tr>
        <tr mat-row *matRowDef="let c; columns: columns()" class="clickable" (click)="router.navigate(['/customers', c.id])"></tr>
        <tr class="mat-row" *matNoDataRow>
          <td class="empty muted" [attr.colspan]="columns().length">{{ 'customers.empty' | transloco }}</td>
        </tr>
      </table>
      <mat-paginator
        [length]="table.total()"
        [pageIndex]="table.pageIndex()"
        [pageSize]="table.pageSize()"
        [pageSizeOptions]="table.pageSizeOptions"
        (page)="table.onPage($event)"
      />
    </div>
  `,
  styles: `
    .company-filter {
      width: 16rem;
    }
    .prefix {
      margin: 0 0.25rem 0 0.75rem;
      color: var(--mat-sys-on-surface-variant);
    }
    .new-button .material-symbols-outlined {
      margin-right: 0.25rem;
    }
  `,
})
export class CustomerTable {
  private readonly api = inject(CustomersApi);
  private readonly dialog = inject(MatDialog);
  private readonly confirm = inject(ConfirmService);
  private readonly notify = inject(NotifyService);
  readonly router = inject(Router);

  private readonly store = inject(AuthStore);
  readonly canWrite = this.store.can(Permissions.customersWrite);
  readonly canDelete = this.store.can(Permissions.customersDelete);

  /** Restricts the table to one company. */
  readonly company = input<{ id: number; name: string } | null>(null);

  readonly columns = computed(() =>
    ['name', 'email', 'phoneNumber', 'company', 'isActive', 'createdAt', 'actions'].filter((c) => c !== 'company' || !this.company()),
  );

  readonly search = signal('');
  readonly status = signal<boolean | null>(null);
  readonly companyFilter = signal<number | null>(null);
  readonly statusOptions = activeFilterOptions();

  readonly table = new PagedTable<CustomerDto>({
    load: (query) => this.api.list(query),
    filters: () => ({ isActive: this.status(), companyId: this.company()?.id ?? this.companyFilter() }),
    search: this.search,
    sort: { active: 'firstName', direction: 'asc' },
  });

  create(): void {
    CustomerFormDialog.open(this.dialog, { company: this.company() })
      .pipe(filter(Boolean))
      .subscribe(() => this.table.reload());
  }

  edit(customer: CustomerDto): void {
    CustomerFormDialog.open(this.dialog, { customer })
      .pipe(filter(Boolean))
      .subscribe(() => this.table.reload());
  }

  remove(customer: CustomerDto): void {
    this.confirm.delete(`${customer.firstName} ${customer.lastName}`, () =>
      this.api.delete(customer.id).subscribe({
        next: () => {
          this.notify.success('common.deleted');
          this.table.reload();
        },
        error: (error) => this.notify.error(error),
      }),
    );
  }
}
