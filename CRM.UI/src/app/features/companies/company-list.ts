import { Component, inject, signal } from '@angular/core';
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

import { CompanyDto } from '../../core/api/models';
import { AuthStore } from '../../core/auth/auth.store';
import { Permissions } from '../../core/auth/permissions';
import { AppDatePipe } from '../../core/i18n/date.pipe';
import { ConfirmService } from '../../core/ui/confirm.service';
import { NotifyService } from '../../core/ui/notify.service';
import { PageHeader } from '../../shared/page-header';
import { PagedTable } from '../../shared/paged-table';
import { StatusTag } from '../../shared/status-tag';
import { activeFilterOptions } from '../../shared/translated';
import { CompaniesApi } from './companies.api';
import { CompanyFormDialog } from './company-form-dialog';

@Component({
  selector: 'app-company-list',
  imports: [
    FormsModule, RouterLink, TranslocoPipe, MatTableModule, MatSortModule, MatPaginatorModule, MatProgressBarModule,
    MatButtonModule, MatFormFieldModule, MatInputModule, MatSelectModule, MatTooltipModule, PageHeader, StatusTag, AppDatePipe,
  ],
  template: `
    <app-page-header [title]="'companies.title' | transloco" [subtitle]="'companies.subtitle' | transloco">
      @if (canWrite) {
        <button matButton="filled" class="new-button" (click)="create()">
          <span class="material-symbols-outlined">add</span> {{ 'companies.new' | transloco }}
        </button>
      }
    </app-page-header>

    <div class="card">
      <div class="table-toolbar">
        <mat-form-field class="grow">
          <mat-label>{{ 'common.search' | transloco }}</mat-label>
          <span matPrefix class="material-symbols-outlined prefix">search</span>
          <input matInput type="search" [placeholder]="'companies.searchPlaceholder' | transloco" [ngModel]="search()" (ngModelChange)="search.set($event)" />
        </mat-form-field>
        <mat-form-field>
          <mat-label>{{ 'common.status' | transloco }}</mat-label>
          <mat-select [ngModel]="status()" (ngModelChange)="status.set($event)">
            @for (option of statusOptions(); track option.value) {
              <mat-option [value]="option.value">{{ option.label }}</mat-option>
            }
          </mat-select>
        </mat-form-field>
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
          <ng-container matColumnDef="companyName">
            <th mat-header-cell *matHeaderCellDef mat-sort-header>{{ 'companies.name' | transloco }}</th>
            <td mat-cell *matCellDef="let c"><a [routerLink]="['/companies', c.id]" (click)="$event.stopPropagation()">{{ c.companyName }}</a></td>
          </ng-container>
          <ng-container matColumnDef="taxNumber">
            <th mat-header-cell *matHeaderCellDef mat-sort-header>{{ 'companies.taxNumber' | transloco }}</th>
            <td mat-cell *matCellDef="let c">{{ c.taxNumber || ('common.none' | transloco) }}</td>
          </ng-container>
          <ng-container matColumnDef="customerCount">
            <th mat-header-cell *matHeaderCellDef>{{ 'companies.customerCount' | transloco }}</th>
            <td mat-cell *matCellDef="let c">{{ c.customerCount }}</td>
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

          <tr mat-header-row *matHeaderRowDef="columns"></tr>
          <tr mat-row *matRowDef="let c; columns: columns" class="clickable" (click)="router.navigate(['/companies', c.id])"></tr>
          <tr class="mat-row" *matNoDataRow>
            <td class="empty muted" [attr.colspan]="columns.length">{{ 'companies.empty' | transloco }}</td>
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
    </div>
  `,
  styles: `
    .prefix {
      margin: 0 0.25rem 0 0.75rem;
      color: var(--mat-sys-on-surface-variant);
    }
    .new-button .material-symbols-outlined {
      margin-right: 0.25rem;
    }
  `,
})
export class CompanyList {
  private readonly api = inject(CompaniesApi);
  private readonly dialog = inject(MatDialog);
  private readonly confirm = inject(ConfirmService);
  private readonly notify = inject(NotifyService);
  readonly router = inject(Router);

  private readonly store = inject(AuthStore);
  readonly canWrite = this.store.can(Permissions.companiesWrite);
  readonly canDelete = this.store.can(Permissions.companiesDelete);

  readonly columns = ['companyName', 'taxNumber', 'customerCount', 'isActive', 'createdAt', 'actions'];

  readonly search = signal('');
  readonly status = signal<boolean | null>(null);
  readonly statusOptions = activeFilterOptions();

  readonly table = new PagedTable<CompanyDto>({
    load: (query) => this.api.list(query),
    filters: () => ({ isActive: this.status() }),
    search: this.search,
    sort: { active: 'companyName', direction: 'asc' },
  });

  create(): void {
    CompanyFormDialog.open(this.dialog)
      .pipe(filter(Boolean))
      .subscribe((company) => void this.router.navigate(['/companies', company.id]));
  }

  edit(company: CompanyDto): void {
    CompanyFormDialog.open(this.dialog, company)
      .pipe(filter(Boolean))
      .subscribe(() => this.table.reload());
  }

  remove(company: CompanyDto): void {
    this.confirm.delete(company.companyName, () =>
      this.api.delete(company.id).subscribe({
        next: () => {
          this.notify.success('common.deleted');
          this.table.reload();
        },
        error: (error) => this.notify.error(error),
      }),
    );
  }
}
