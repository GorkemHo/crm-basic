import { Component, OnInit, computed, inject, input, numberAttribute, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { Router } from '@angular/router';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { forkJoin } from 'rxjs';

import { PermissionDto, RoleDto } from '../../core/api/models';
import { AuthStore } from '../../core/auth/auth.store';
import { Permissions } from '../../core/auth/permissions';
import { NotifyService } from '../../core/ui/notify.service';
import { PageHeader } from '../../shared/page-header';
import { RolesApi } from './roles.api';

interface PermissionGroup {
  key: string;
  module: string;
  group: string;
  permissions: PermissionDto[];
}

/** Permission matrix of a role, grouped by module and resource. Admin is shown read-only. */
@Component({
  selector: 'app-role-detail',
  imports: [TranslocoPipe, MatButtonModule, MatCheckboxModule, MatProgressBarModule, PageHeader],
  template: `
    @if (role(); as r) {
      <app-page-header [title]="r.name" [subtitle]="'roles.permissions' | transloco" backLink="/roles">
        @if (editable()) {
          <button matButton (click)="reset()" [disabled]="!changed() || saving()">{{ 'common.cancel' | transloco }}</button>
          <button matButton="filled" (click)="save(r)" [disabled]="!changed() || saving()">{{ 'common.save' | transloco }}</button>
        }
      </app-page-header>

      <div class="notice" [class.info]="r.name !== 'Admin'">
        <span class="material-symbols-outlined">info</span>
        {{ (r.name === 'Admin' ? 'roles.adminAllPermissions' : 'roles.permissionsHint') | transloco }}
      </div>

      <div class="groups">
        @for (group of groups(); track group.key) {
          <section class="card group">
            <header>
              <div>
                <span class="module">{{ 'roles.modules.' + group.module | transloco }}</span>
                <h3>{{ groupLabel(group.group) }}</h3>
              </div>
              @if (editable()) {
                <mat-checkbox
                  [checked]="allSelected(group)"
                  [indeterminate]="someSelected(group)"
                  (change)="toggleGroup(group, $event.checked)"
                >{{ 'roles.selectAll' | transloco }}</mat-checkbox>
              }
            </header>
            @for (permission of group.permissions; track permission.name) {
              <mat-checkbox
                class="permission"
                [checked]="selected().has(permission.name)"
                [disabled]="!editable()"
                (change)="toggle(permission.name, $event.checked)"
              >
                <span class="description">{{ permission.description }}</span>
                <code>{{ permission.name }}</code>
              </mat-checkbox>
            }
          </section>
        }
      </div>
    } @else {
      <mat-progress-bar mode="indeterminate" />
    }
  `,
  styles: `
    .notice {
      display: flex;
      align-items: center;
      gap: 0.5rem;
      padding: 0.75rem 1rem;
      margin-bottom: var(--app-gap);
      border-radius: 0.75rem;
      background: var(--mat-sys-tertiary-container);
      color: var(--mat-sys-on-tertiary-container);
    }
    .notice.info {
      background: var(--mat-sys-surface-container-high);
      color: var(--mat-sys-on-surface-variant);
    }
    .groups {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(20rem, 1fr));
      gap: var(--app-gap);
    }
    .group header {
      display: flex;
      justify-content: space-between;
      align-items: flex-start;
      gap: 1rem;
      margin-bottom: 0.5rem;
    }
    .module {
      font: var(--mat-sys-label-small);
      text-transform: uppercase;
      letter-spacing: 0.05em;
      color: var(--mat-sys-on-surface-variant);
    }
    h3 {
      font: var(--mat-sys-title-medium);
    }
    .permission {
      display: block;
    }
    .description {
      display: block;
    }
    code {
      font-size: 0.75rem;
      color: var(--mat-sys-on-surface-variant);
    }
  `,
})
export class RoleDetail implements OnInit {
  private readonly api = inject(RolesApi);
  private readonly router = inject(Router);
  private readonly notify = inject(NotifyService);
  private readonly transloco = inject(TranslocoService);
  private readonly canManage = inject(AuthStore).can(Permissions.rolesManage);

  /** Route parameter. */
  readonly id = input.required({ transform: numberAttribute });

  readonly role = signal<RoleDto | null>(null);
  readonly catalog = signal<PermissionDto[]>([]);
  readonly selected = signal(new Set<string>());
  readonly saving = signal(false);

  readonly editable = computed(() => this.canManage && this.role()?.name !== 'Admin');

  readonly groups = computed<PermissionGroup[]>(() => {
    const groups = new Map<string, PermissionGroup>();
    for (const permission of this.catalog()) {
      const key = `${permission.module}/${permission.group}`;
      if (!groups.has(key)) {
        groups.set(key, { key, module: permission.module, group: permission.group, permissions: [] });
      }
      groups.get(key)!.permissions.push(permission);
    }
    return [...groups.values()];
  });

  readonly changed = computed(() => {
    const original = new Set(this.role()?.permissions ?? []);
    const selected = this.selected();
    return original.size !== selected.size || [...selected].some((p) => !original.has(p));
  });

  ngOnInit(): void {
    forkJoin([this.api.get(this.id()), this.api.permissions()]).subscribe({
      next: ([role, catalog]) => {
        this.catalog.set(catalog);
        this.setRole(role);
      },
      error: (error) => {
        this.notify.error(error);
        void this.router.navigate(['/roles']);
      },
    });
  }

  groupLabel(group: string): string {
    const key = `roles.groups.${group}`;
    const translated = this.transloco.translate(key);
    return translated === key ? group : translated;
  }

  toggle(name: string, checked: boolean): void {
    this.selected.update((current) => {
      const next = new Set(current);
      if (checked) next.add(name);
      else next.delete(name);
      return next;
    });
  }

  toggleGroup(group: PermissionGroup, checked: boolean): void {
    for (const permission of group.permissions) this.toggle(permission.name, checked);
  }

  allSelected(group: PermissionGroup): boolean {
    return group.permissions.every((p) => this.selected().has(p.name));
  }

  someSelected(group: PermissionGroup): boolean {
    const count = group.permissions.filter((p) => this.selected().has(p.name)).length;
    return count > 0 && count < group.permissions.length;
  }

  reset(): void {
    this.selected.set(new Set(this.role()?.permissions ?? []));
  }

  save(role: RoleDto): void {
    this.saving.set(true);
    this.api.setPermissions(role.id, [...this.selected()]).subscribe({
      next: (updated) => {
        this.saving.set(false);
        this.notify.success('roles.permissionsSaved');
        this.setRole(updated);
      },
      error: (error) => {
        this.saving.set(false);
        this.notify.error(error);
      },
    });
  }

  private setRole(role: RoleDto): void {
    this.role.set(role);
    this.selected.set(new Set(role.permissions));
  }
}
