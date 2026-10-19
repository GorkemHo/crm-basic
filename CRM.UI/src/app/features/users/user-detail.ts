import { Component, OnInit, computed, inject, input, numberAttribute, signal, viewChild } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatDialog } from '@angular/material/dialog';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatTabsModule } from '@angular/material/tabs';
import { Router } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { catchError, filter, of } from 'rxjs';

import { UserDto } from '../../core/api/models';
import { AuthStore } from '../../core/auth/auth.store';
import { Permissions } from '../../core/auth/permissions';
import { AppDatePipe } from '../../core/i18n/date.pipe';
import { ConfirmService } from '../../core/ui/confirm.service';
import { NotifyService } from '../../core/ui/notify.service';
import { HistoryTimeline } from '../../shared/history-timeline';
import { PageHeader } from '../../shared/page-header';
import { StatusTag } from '../../shared/status-tag';
import { RolesApi } from '../roles/roles.api';
import { UserFormDialog } from './user-form-dialog';
import { UsersApi } from './users.api';

@Component({
  selector: 'app-user-detail',
  imports: [
    FormsModule, TranslocoPipe, MatButtonModule, MatCheckboxModule, MatTabsModule, MatProgressBarModule,
    PageHeader, StatusTag, AppDatePipe, HistoryTimeline,
  ],
  template: `
    @if (user(); as u) {
      <app-page-header [title]="u.firstName + ' ' + u.lastName" [subtitle]="u.email" backLink="/users">
        @if (canManage) {
          <button matButton="tonal" (click)="edit(u)"><span class="material-symbols-outlined">edit</span> {{ 'common.edit' | transloco }}</button>
          @if (!isSelf()) {
            <button matButton="outlined" class="danger" (click)="remove(u)"><span class="material-symbols-outlined">delete</span> {{ 'common.delete' | transloco }}</button>
          }
        }
      </app-page-header>

      <div class="card tabs-card">
        <mat-tab-group animationDuration="0ms">
          <mat-tab [label]="'companies.overview' | transloco">
            <div class="tab-body">
              <dl class="details">
                <div><dt>{{ 'users.name' | transloco }}</dt><dd>{{ u.firstName }} {{ u.lastName }}</dd></div>
                <div><dt>{{ 'users.email' | transloco }}</dt><dd>{{ u.email }}</dd></div>
                <div><dt>{{ 'users.language' | transloco }}</dt><dd>{{ 'languages.' + u.preferredLanguage | transloco }}</dd></div>
                <div><dt>{{ 'common.status' | transloco }}</dt><dd><app-status-tag [active]="u.isActive" /></dd></div>
                <div><dt>{{ 'common.createdAt' | transloco }}</dt><dd>{{ u.createdAt | appDate: 'datetime' }}</dd></div>
                <div><dt>{{ 'common.updatedAt' | transloco }}</dt><dd>{{ u.updatedAt | appDate: 'datetime' }}</dd></div>
              </dl>

              <h3>{{ 'users.roles' | transloco }}</h3>
              @if (canManage && roles().length) {
                <div class="roles">
                  @for (role of roles(); track role.id) {
                    <mat-checkbox [checked]="selectedRoles().has(role.name)" (change)="toggleRole(role.name, $event.checked)">
                      {{ role.name }}
                    </mat-checkbox>
                  }
                </div>
                <button matButton="filled" (click)="saveRoles(u)" [disabled]="!rolesChanged() || savingRoles()">
                  {{ 'common.save' | transloco }}
                </button>
              } @else {
                <p>{{ u.roles.join(', ') || ('common.none' | transloco) }}</p>
              }
            </div>
          </mat-tab>
          <mat-tab [label]="'companies.history' | transloco">
            <ng-template matTabContent>
              <div class="tab-body"><app-history-timeline #history [url]="api.historyUrl(u.id)" /></div>
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
    h3 {
      font: var(--mat-sys-title-medium);
      margin: 2rem 0 0.75rem;
    }
    .roles {
      display: flex;
      flex-wrap: wrap;
      gap: 0.5rem 1.5rem;
      margin-bottom: 1rem;
    }
    button .material-symbols-outlined {
      margin-right: 0.25rem;
    }
    .danger {
      --mat-button-outlined-label-text-color: var(--mat-sys-error);
    }
  `,
})
export class UserDetail implements OnInit {
  readonly api = inject(UsersApi);
  private readonly router = inject(Router);
  private readonly dialog = inject(MatDialog);
  private readonly notify = inject(NotifyService);
  private readonly confirm = inject(ConfirmService);

  private readonly store = inject(AuthStore);
  readonly canManage = this.store.can(Permissions.usersManage);

  /** Route parameter. */
  readonly id = input.required({ transform: numberAttribute });

  readonly user = signal<UserDto | null>(null);
  readonly isSelf = computed(() => this.user()?.id === this.store.user()?.id);

  readonly roles = toSignal(inject(RolesApi).list().pipe(catchError(() => of([]))), { initialValue: [] });
  readonly selectedRoles = signal(new Set<string>());
  readonly rolesChanged = computed(() => {
    const current = new Set(this.user()?.roles ?? []);
    const selected = this.selectedRoles();
    return current.size !== selected.size || [...selected].some((r) => !current.has(r));
  });
  readonly savingRoles = signal(false);

  private readonly history = viewChild<HistoryTimeline>('history');

  ngOnInit(): void {
    this.api.get(this.id()).subscribe({
      next: (user) => this.setUser(user),
      error: (error) => {
        this.notify.error(error);
        void this.router.navigate(['/users']);
      },
    });
  }

  toggleRole(name: string, checked: boolean): void {
    this.selectedRoles.update((roles) => {
      const next = new Set(roles);
      if (checked) next.add(name);
      else next.delete(name);
      return next;
    });
  }

  saveRoles(user: UserDto): void {
    const roleIds = this.roles().filter((r) => this.selectedRoles().has(r.name)).map((r) => r.id);

    this.savingRoles.set(true);
    this.api.setRoles(user.id, roleIds).subscribe({
      next: (updated) => {
        this.savingRoles.set(false);
        this.notify.success('users.rolesSaved');
        this.setUser(updated);
      },
      error: (error) => {
        this.savingRoles.set(false);
        this.notify.error(error);
      },
    });
  }

  edit(user: UserDto): void {
    UserFormDialog.open(this.dialog, user)
      .pipe(filter(Boolean))
      .subscribe((saved) => this.setUser(saved));
  }

  remove(user: UserDto): void {
    this.confirm.delete(`${user.firstName} ${user.lastName}`, () =>
      this.api.delete(user.id).subscribe({
        next: () => {
          this.notify.success('common.deleted');
          void this.router.navigate(['/users']);
        },
        error: (error) => this.notify.error(error),
      }),
    );
  }

  private setUser(user: UserDto): void {
    this.user.set(user);
    this.selectedRoles.set(new Set(user.roles));
    this.history()?.reload();
  }
}
