import { Component, OnInit, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';
import { Router, RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { filter } from 'rxjs';

import { RoleDto } from '../../core/api/models';
import { AuthStore } from '../../core/auth/auth.store';
import { Permissions } from '../../core/auth/permissions';
import { AppDatePipe } from '../../core/i18n/date.pipe';
import { ConfirmService } from '../../core/ui/confirm.service';
import { NotifyService } from '../../core/ui/notify.service';
import { PageHeader } from '../../shared/page-header';
import { StatusTag } from '../../shared/status-tag';
import { RoleFormDialog } from './role-form-dialog';
import { RolesApi } from './roles.api';

@Component({
  selector: 'app-role-list',
  imports: [RouterLink, TranslocoPipe, MatTableModule, MatProgressBarModule, MatButtonModule, MatTooltipModule, PageHeader, StatusTag, AppDatePipe],
  template: `
    <app-page-header [title]="'roles.title' | transloco" [subtitle]="'roles.subtitle' | transloco">
      @if (canManage) {
        <button matButton="filled" class="new-button" (click)="create()">
          <span class="material-symbols-outlined">add</span> {{ 'roles.new' | transloco }}
        </button>
      }
    </app-page-header>

    <div class="card">
      <div class="table-wrapper">
        @if (loading()) {
          <mat-progress-bar mode="indeterminate" />
        }
        <table mat-table [dataSource]="roles()">
          <ng-container matColumnDef="name">
            <th mat-header-cell *matHeaderCellDef>{{ 'roles.name' | transloco }}</th>
            <td mat-cell *matCellDef="let r">
              <a [routerLink]="['/roles', r.id]" (click)="$event.stopPropagation()">{{ r.name }}</a>
              @if (r.isSystem) {
                <span class="system">{{ 'roles.system' | transloco }}</span>
              }
            </td>
          </ng-container>
          <ng-container matColumnDef="users">
            <th mat-header-cell *matHeaderCellDef>{{ 'roles.users' | transloco }}</th>
            <td mat-cell *matCellDef="let r">{{ r.userCount }}</td>
          </ng-container>
          <ng-container matColumnDef="permissions">
            <th mat-header-cell *matHeaderCellDef>{{ 'roles.permissions' | transloco }}</th>
            <td mat-cell *matCellDef="let r">{{ r.permissions.length }}</td>
          </ng-container>
          <ng-container matColumnDef="isActive">
            <th mat-header-cell *matHeaderCellDef>{{ 'common.status' | transloco }}</th>
            <td mat-cell *matCellDef="let r"><app-status-tag [active]="r.isActive" /></td>
          </ng-container>
          <ng-container matColumnDef="createdAt">
            <th mat-header-cell *matHeaderCellDef>{{ 'common.createdAt' | transloco }}</th>
            <td mat-cell *matCellDef="let r">{{ r.createdAt | appDate }}</td>
          </ng-container>
          <ng-container matColumnDef="actions">
            <th mat-header-cell *matHeaderCellDef></th>
            <td mat-cell *matCellDef="let r">
              @if (canManage && !r.isSystem) {
                <div class="row-actions" (click)="$event.stopPropagation()">
                  <button matIconButton [matTooltip]="'common.edit' | transloco" (click)="edit(r)" [attr.aria-label]="'common.edit' | transloco">
                    <span class="material-symbols-outlined">edit</span>
                  </button>
                  <button matIconButton [matTooltip]="'common.delete' | transloco" (click)="remove(r)" [attr.aria-label]="'common.delete' | transloco">
                    <span class="material-symbols-outlined">delete</span>
                  </button>
                </div>
              }
            </td>
          </ng-container>

          <tr mat-header-row *matHeaderRowDef="columns"></tr>
          <tr mat-row *matRowDef="let r; columns: columns" class="clickable" (click)="router.navigate(['/roles', r.id])"></tr>
        </table>
      </div>
    </div>
  `,
  styles: `
    .new-button .material-symbols-outlined {
      margin-right: 0.25rem;
    }
    .system {
      margin-left: 0.5rem;
      padding: 0.1rem 0.5rem;
      border-radius: 1rem;
      font: var(--mat-sys-label-small);
      background: var(--mat-sys-surface-container-highest);
      color: var(--mat-sys-on-surface-variant);
    }
  `,
})
export class RoleList implements OnInit {
  private readonly api = inject(RolesApi);
  private readonly dialog = inject(MatDialog);
  private readonly confirm = inject(ConfirmService);
  private readonly notify = inject(NotifyService);
  readonly router = inject(Router);

  readonly canManage = inject(AuthStore).can(Permissions.rolesManage);
  readonly columns = ['name', 'users', 'permissions', 'isActive', 'createdAt', 'actions'];

  readonly roles = signal<RoleDto[]>([]);
  readonly loading = signal(true);

  ngOnInit(): void {
    this.load();
  }

  create(): void {
    RoleFormDialog.open(this.dialog)
      .pipe(filter(Boolean))
      .subscribe((role) => void this.router.navigate(['/roles', role.id]));
  }

  edit(role: RoleDto): void {
    RoleFormDialog.open(this.dialog, role)
      .pipe(filter(Boolean))
      .subscribe(() => this.load());
  }

  remove(role: RoleDto): void {
    this.confirm.delete(role.name, () =>
      this.api.delete(role.id).subscribe({
        next: () => {
          this.notify.success('common.deleted');
          this.load();
        },
        error: (error) => this.notify.error(error),
      }),
    );
  }

  private load(): void {
    this.loading.set(true);
    this.api.list().subscribe({
      next: (roles) => {
        this.roles.set(roles);
        this.loading.set(false);
      },
      error: (error) => {
        this.loading.set(false);
        this.notify.error(error);
      },
    });
  }
}
