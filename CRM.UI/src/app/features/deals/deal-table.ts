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

import { CURRENCIES, DealDto } from '../../core/api/models';
import { AuthStore } from '../../core/auth/auth.store';
import { Permissions } from '../../core/auth/permissions';
import { AppDatePipe } from '../../core/i18n/date.pipe';
import { AppMoneyPipe } from '../../core/i18n/money.pipe';
import { ConfirmService } from '../../core/ui/confirm.service';
import { NotifyService } from '../../core/ui/notify.service';
import { PagedTable } from '../../shared/paged-table';
import { translated } from '../../shared/translated';
import { DealFormDialog } from './deal-form-dialog';
import { DealStatusTag, dateOnly, isOverdue } from './deal-status';
import { DealsApi } from './deals.api';

/**
 * Searchable, paged deal table with create/edit/delete.
 * With `company` set it shows only that company's deals (company page tab).
 */
@Component({
  selector: 'app-deal-table',
  imports: [
    FormsModule, RouterLink, TranslocoPipe, MatTableModule, MatSortModule, MatPaginatorModule, MatProgressBarModule,
    MatButtonModule, MatFormFieldModule, MatInputModule, MatSelectModule, MatTooltipModule, AppDatePipe, AppMoneyPipe, DealStatusTag,
  ],
  template: `
    <div class="table-toolbar">
      <mat-form-field class="grow">
        <mat-label>{{ 'common.search' | transloco }}</mat-label>
        <span matPrefix class="material-symbols-outlined prefix">search</span>
        <input matInput type="search" [placeholder]="'deals.searchPlaceholder' | transloco" [ngModel]="search()" (ngModelChange)="search.set($event)" />
      </mat-form-field>
      <mat-form-field>
        <mat-label>{{ 'common.status' | transloco }}</mat-label>
        <mat-select id="statusFilter" [ngModel]="status()" (ngModelChange)="status.set($event)">
          @for (option of statusOptions(); track option.value) {
            <mat-option [value]="option.value">{{ option.label }}</mat-option>
          }
        </mat-select>
      </mat-form-field>
      <mat-form-field>
        <mat-label>{{ 'deals.currency' | transloco }}</mat-label>
        <mat-select [ngModel]="currency()" (ngModelChange)="currency.set($event)">
          <mat-option [value]="null">{{ 'common.all' | transloco }}</mat-option>
          @for (c of currencies; track c) {
            <mat-option [value]="c">{{ c }}</mat-option>
          }
        </mat-select>
      </mat-form-field>
      @if (canWrite) {
        <button matButton="filled" class="new-button" (click)="create()">
          <span class="material-symbols-outlined">add</span> {{ 'deals.new' | transloco }}
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
        <ng-container matColumnDef="title">
          <th mat-header-cell *matHeaderCellDef mat-sort-header>{{ 'deals.dealTitle' | transloco }}</th>
          <td mat-cell *matCellDef="let d"><a [routerLink]="['/deals', d.id]" (click)="$event.stopPropagation()">{{ d.title }}</a></td>
        </ng-container>
        <ng-container matColumnDef="company">
          <th mat-header-cell *matHeaderCellDef>{{ 'deals.company' | transloco }}</th>
          <td mat-cell *matCellDef="let d"><a [routerLink]="['/companies', d.companyId]" (click)="$event.stopPropagation()">{{ d.companyName }}</a></td>
        </ng-container>
        <ng-container matColumnDef="amount">
          <th mat-header-cell *matHeaderCellDef mat-sort-header class="numeric">{{ 'deals.amount' | transloco }}</th>
          <td mat-cell *matCellDef="let d" class="numeric">{{ d.amount | appMoney: d.currency }}</td>
        </ng-container>
        <ng-container matColumnDef="stage">
          <th mat-header-cell *matHeaderCellDef>{{ 'deals.stage' | transloco }}</th>
          <td mat-cell *matCellDef="let d">{{ d.stageName }}</td>
        </ng-container>
        <ng-container matColumnDef="status">
          <th mat-header-cell *matHeaderCellDef mat-sort-header>{{ 'common.status' | transloco }}</th>
          <td mat-cell *matCellDef="let d"><app-deal-status [status]="d.status" /></td>
        </ng-container>
        <ng-container matColumnDef="expectedCloseDate">
          <th mat-header-cell *matHeaderCellDef mat-sort-header>{{ 'deals.expectedClose' | transloco }}</th>
          <td mat-cell *matCellDef="let d" [class.overdue]="overdue(d)">{{ toDate(d.expectedCloseDate) | appDate }}</td>
        </ng-container>
        <ng-container matColumnDef="actions">
          <th mat-header-cell *matHeaderCellDef></th>
          <td mat-cell *matCellDef="let d">
            <div class="row-actions" (click)="$event.stopPropagation()">
              @if (canWrite) {
                <button matIconButton [matTooltip]="'common.edit' | transloco" (click)="edit(d)" [attr.aria-label]="'common.edit' | transloco">
                  <span class="material-symbols-outlined">edit</span>
                </button>
              }
              @if (canDelete) {
                <button matIconButton [matTooltip]="'common.delete' | transloco" (click)="remove(d)" [attr.aria-label]="'common.delete' | transloco">
                  <span class="material-symbols-outlined">delete</span>
                </button>
              }
            </div>
          </td>
        </ng-container>

        <tr mat-header-row *matHeaderRowDef="columns()"></tr>
        <tr mat-row *matRowDef="let d; columns: columns()" class="clickable" (click)="router.navigate(['/deals', d.id])"></tr>
        <tr class="mat-row" *matNoDataRow>
          <td class="empty muted" [attr.colspan]="columns().length">{{ 'deals.empty' | transloco }}</td>
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
    .prefix {
      margin: 0 0.25rem 0 0.75rem;
      color: var(--mat-sys-on-surface-variant);
    }
    .new-button .material-symbols-outlined {
      margin-right: 0.25rem;
    }
    .numeric {
      text-align: right;
      white-space: nowrap;
    }
    .overdue {
      color: var(--mat-sys-error);
    }
  `,
})
export class DealTable {
  private readonly api = inject(DealsApi);
  private readonly dialog = inject(MatDialog);
  private readonly confirm = inject(ConfirmService);
  private readonly notify = inject(NotifyService);
  readonly router = inject(Router);

  private readonly store = inject(AuthStore);
  readonly canWrite = this.store.can(Permissions.dealsWrite);
  readonly canDelete = this.store.can(Permissions.dealsDelete);

  /** Restricts the table to one company. */
  readonly company = input<{ id: number; name: string } | null>(null);

  readonly columns = computed(() =>
    ['title', 'company', 'amount', 'stage', 'status', 'expectedCloseDate', 'actions'].filter((c) => c !== 'company' || !this.company()),
  );

  readonly currencies = CURRENCIES;
  readonly search = signal('');
  readonly status = signal<string | null>(null);
  readonly currency = signal<string | null>(null);
  readonly statusOptions = translated((t) => [
    { label: t('common.all'), value: null },
    { label: t('deals.status.open'), value: 'open' },
    { label: t('deals.status.won'), value: 'won' },
    { label: t('deals.status.lost'), value: 'lost' },
  ]);

  readonly table = new PagedTable<DealDto>({
    load: (query) => this.api.list(query),
    filters: () => ({ status: this.status(), currency: this.currency(), companyId: this.company()?.id }),
    search: this.search,
  });

  readonly toDate = dateOnly;
  overdue(deal: DealDto): boolean {
    return isOverdue(deal.expectedCloseDate, deal.status);
  }

  create(): void {
    DealFormDialog.open(this.dialog, { company: this.company() })
      .pipe(filter(Boolean))
      .subscribe(() => this.table.reload());
  }

  edit(deal: DealDto): void {
    DealFormDialog.open(this.dialog, { deal })
      .pipe(filter(Boolean))
      .subscribe(() => this.table.reload());
  }

  remove(deal: DealDto): void {
    this.confirm.delete(deal.title, () =>
      this.api.delete(deal.id).subscribe({
        next: () => {
          this.notify.success('common.deleted');
          this.table.reload();
        },
        error: (error) => this.notify.error(error),
      }),
    );
  }
}
