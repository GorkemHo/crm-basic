import { Component, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatChipsModule } from '@angular/material/chips';
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
import { catchError, filter, of } from 'rxjs';

import { UserDto } from '../../core/api/models';
import { AuthStore } from '../../core/auth/auth.store';
import { Permissions } from '../../core/auth/permissions';
import { AppDatePipe } from '../../core/i18n/date.pipe';
import { ConfirmService } from '../../core/ui/confirm.service';
import { NotifyService } from '../../core/ui/notify.service';
import { PageHeader } from '../../shared/page-header';
import { PagedTable } from '../../shared/paged-table';
import { StatusTag } from '../../shared/status-tag';
import { activeFilterOptions } from '../../shared/translated';
import { RolesApi } from '../roles/roles.api';
import { UserFormDialog } from './user-form-dialog';
import { UsersApi } from './users.api';

@Component({
  selector: 'app-user-list',
  imports: [
    FormsModule, RouterLink, TranslocoPipe, MatTableModule, MatSortModule, MatPaginatorModule, MatProgressBarModule, MatChipsModule,
    MatButtonModule, MatFormFieldModule, MatInputModule, MatSelectModule, MatTooltipModule, PageHeader, StatusTag, AppDatePipe,
  ],
  template: `
    <app-page-header [title]="'users.title' | transloco" [subtitle]="'users.subtitle' | transloco">
      @if (canManage) {
        <button matButton="filled" class="new-button" (click)="create()">
          <span class="material-symbols-outlined">person_add</span> {{ 'users.new' | transloco }}
        </button>
      }
    </app-page-header>

    <div class="card">
      <div class="table-toolbar">
        <mat-form-field class="grow">
          <mat-label>{{ 'common.search' | transloco }}</mat-label>
          <span matPrefix class="material-symbols-outlined prefix">search</span>
          <input matInput type="search" [placeholder]="'users.searchPlaceholder' | transloco" [ngModel]="search()" (ngModelChange)="search.set($event)" />
        </mat-form-field>
        @if (roles().length) {
          <mat-form-field>
            <mat-label>{{ 'users.role' | transloco }}</mat-label>
            <mat-select [ngModel]="roleId()" (ngModelChange)="roleId.set($event)">
              <mat-option [value]="null">{{ 'common.all' | transloco }}</mat-option>
              @for (role of roles(); track role.id) {
                <mat-option [value]="role.id">{{ role.name }}</mat-option>
              }
            </mat-select>
          </mat-form-field>
        }
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
          <ng-container matColumnDef="name">
            <th mat-header-cell *matHeaderCellDef mat-sort-header="firstName">{{ 'users.name' | transloco }}</th>
            <td mat-cell *matCellDef="let u">
              <a [routerLink]="['/users', u.id]" (click)="$event.stopPropagation()">{{ u.firstName }} {{ u.lastName }}</a>
              @if (u.id === currentUserId) {
                <span class="you">{{ 'users.you' | transloco }}</span>
              }
            </td>
          </ng-container>
          <ng-container matColumnDef="email">
            <th mat-header-cell *matHeaderCellDef mat-sort-header>{{ 'users.email' | transloco }}</th>
            <td mat-cell *matCellDef="let u">{{ u.email }}</td>
          </ng-container>
          <ng-container matColumnDef="roles">
            <th mat-header-cell *matHeaderCellDef>{{ 'users.roles' | transloco }}</th>
            <td mat-cell *matCellDef="let u">
              <mat-chip-set>
                @for (role of u.roles; track role) {
                  <mat-chip>{{ role }}</mat-chip>
                }
              </mat-chip-set>
            </td>
          </ng-container>
          <ng-container matColumnDef="language">
            <th mat-header-cell *matHeaderCellDef>{{ 'users.language' | transloco }}</th>
            <td mat-cell *matCellDef="let u">{{ u.preferredLanguage.toUpperCase() }}</td>
          </ng-container>
          <ng-container matColumnDef="isActive">
            <th mat-header-cell *matHeaderCellDef mat-sort-header>{{ 'common.status' | transloco }}</th>
            <td mat-cell *matCellDef="let u"><app-status-tag [active]="u.isActive" /></td>
          </ng-container>
          <ng-container matColumnDef="createdAt">
            <th mat-header-cell *matHeaderCellDef mat-sort-header>{{ 'common.createdAt' | transloco }}</th>
            <td mat-cell *matCellDef="let u">{{ u.createdAt | appDate }}</td>
          </ng-container>
          <ng-container matColumnDef="actions">
            <th mat-header-cell *matHeaderCellDef></th>
            <td mat-cell *matCellDef="let u">
              @if (canManage) {
                <div class="row-actions" (click)="$event.stopPropagation()">
                  <button matIconButton [matTooltip]="'common.edit' | transloco" (click)="edit(u)" [attr.aria-label]="'common.edit' | transloco">
                    <span class="material-symbols-outlined">edit</span>
                  </button>
                  @if (u.id !== currentUserId) {
                    <button matIconButton [matTooltip]="'common.delete' | transloco" (click)="remove(u)" [attr.aria-label]="'common.delete' | transloco">
                      <span class="material-symbols-outlined">delete</span>
                    </button>
                  }
                </div>
              }
            </td>
          </ng-container>

          <tr mat-header-row *matHeaderRowDef="columns"></tr>
          <tr mat-row *matRowDef="let u; columns: columns" class="clickable" (click)="router.navigate(['/users', u.id])"></tr>
          <tr class="mat-row" *matNoDataRow>
            <td class="empty muted" [attr.colspan]="columns.length">{{ 'users.empty' | transloco }}</td>
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
    .you {
      margin-left: 0.5rem;
      padding: 0.1rem 0.5rem;
      border-radius: 1rem;
      font: var(--mat-sys-label-small);
      background: var(--mat-sys-tertiary-container);
      color: var(--mat-sys-on-tertiary-container);
    }
  `,
})
export class UserList {
  private readonly api = inject(UsersApi);
  private readonly dialog = inject(MatDialog);
  private readonly confirm = inject(ConfirmService);
  private readonly notify = inject(NotifyService);
  readonly router = inject(Router);

  private readonly store = inject(AuthStore);
  readonly canManage = this.store.can(Permissions.usersManage);
  readonly currentUserId = this.store.user()?.id;

  readonly columns = ['name', 'email', 'roles', 'language', 'isActive', 'createdAt', 'actions'];

  readonly search = signal('');
  readonly status = signal<boolean | null>(null);
  readonly roleId = signal<number | null>(null);
  readonly statusOptions = activeFilterOptions();

  /** Role filter options; hidden when the caller may not read roles. */
  readonly roles = toSignal(inject(RolesApi).list().pipe(catchError(() => of([]))), { initialValue: [] });

  readonly table = new PagedTable<UserDto>({
    load: (query) => this.api.list(query),
    filters: () => ({ isActive: this.status(), roleId: this.roleId() }),
    search: this.search,
  });

  create(): void {
    UserFormDialog.open(this.dialog)
      .pipe(filter(Boolean))
      .subscribe((user) => void this.router.navigate(['/users', user.id]));
  }

  edit(user: UserDto): void {
    UserFormDialog.open(this.dialog, user)
      .pipe(filter(Boolean))
      .subscribe(() => this.table.reload());
  }

  remove(user: UserDto): void {
    this.confirm.delete(`${user.firstName} ${user.lastName}`, () =>
      this.api.delete(user.id).subscribe({
        next: () => {
          this.notify.success('common.deleted');
          this.table.reload();
        },
        error: (error) => this.notify.error(error),
      }),
    );
  }
}
