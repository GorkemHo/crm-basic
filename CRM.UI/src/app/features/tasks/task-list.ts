import { Component, computed, effect, inject, input, signal, untracked } from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatMenuModule } from '@angular/material/menu';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { MatTooltipModule } from '@angular/material/tooltip';
import { RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { Subscription, debounceTime, filter } from 'rxjs';

import { TaskDto } from '../../core/api/models';
import { PageQuery } from '../../core/api/paging';
import { AuthStore } from '../../core/auth/auth.store';
import { Permissions } from '../../core/auth/permissions';
import { AppDatePipe } from '../../core/i18n/date.pipe';
import { ConfirmService } from '../../core/ui/confirm.service';
import { NotifyService } from '../../core/ui/notify.service';
import { dateOnly } from '../deals/deal-status';
import { TaskContext, TaskFormDialog } from './task-form-dialog';
import { TasksApi, localToday } from './tasks.api';

/**
 * Tasks in work-list order with a checkbox to complete them, edit / delete for the assignee, the creator
 * or a manager, and "new task". On a record page pass `companyId` / `customerId` / `dealId` plus `context`
 * (pre-filled links) and `hide` (that record's link is left out). `scope` shows the Mine / All switch.
 */
@Component({
  selector: 'app-task-list',
  imports: [
    FormsModule, RouterLink, TranslocoPipe, MatButtonModule, MatButtonToggleModule, MatCheckboxModule, MatFormFieldModule,
    MatInputModule, MatMenuModule, MatProgressBarModule, MatSelectModule, MatTooltipModule, AppDatePipe,
  ],
  template: `
    @if (toolbar()) {
      <div class="table-toolbar">
        @if (scope()) {
          <mat-button-toggle-group class="scope" [ngModel]="mine()" (ngModelChange)="mine.set($event)" [hideSingleSelectionIndicator]="true">
            <mat-button-toggle [value]="true">{{ 'tasks.mine' | transloco }}</mat-button-toggle>
            <mat-button-toggle [value]="false">{{ 'tasks.everyone' | transloco }}</mat-button-toggle>
          </mat-button-toggle-group>
        }
        <mat-form-field class="grow">
          <mat-label>{{ 'common.search' | transloco }}</mat-label>
          <input matInput type="search" [placeholder]="'tasks.searchPlaceholder' | transloco" [ngModel]="search()" (ngModelChange)="search.set($event)" />
        </mat-form-field>
        <mat-form-field>
          <mat-label>{{ 'common.status' | transloco }}</mat-label>
          <mat-select id="taskStatus" [ngModel]="status()" (ngModelChange)="status.set($event)">
            <mat-option [value]="'open'">{{ 'tasks.open' | transloco }}</mat-option>
            <mat-option [value]="'done'">{{ 'tasks.done' | transloco }}</mat-option>
            <mat-option [value]="null">{{ 'common.all' | transloco }}</mat-option>
          </mat-select>
        </mat-form-field>
        @if (canWrite) {
          <button matButton="filled" class="new-task" (click)="create()">
            <span class="material-symbols-outlined">add_task</span> {{ 'tasks.new' | transloco }}
          </button>
        }
      </div>
    }

    @if (loading() && items().length === 0) {
      <mat-progress-bar mode="indeterminate" />
    } @else if (items().length === 0) {
      <p class="muted empty">{{ 'tasks.empty' | transloco }}</p>
    } @else {
      <ul class="tasks">
        @for (t of items(); track t.id) {
          <li class="task" [class.done]="t.isDone" [attr.data-id]="t.id">
            <mat-checkbox
              class="check"
              [checked]="t.isDone"
              [disabled]="!canChange(t) || busy() === t.id"
              (change)="toggle(t)"
              [attr.aria-label]="(t.isDone ? 'tasks.reopen' : 'tasks.complete') | transloco"
            />
            <div class="body">
              <div class="title-row">
                <span class="title">{{ t.title }}</span>
                @if (t.priority !== 'normal') {
                  <span class="priority" [class]="t.priority">{{ 'tasks.priorities.' + t.priority | transloco }}</span>
                }
              </div>
              <div class="meta muted">
                @if (t.dueDate) {
                  <span class="due" [class.overdue]="due(t) === 'overdue'" [class.today]="due(t) === 'today'">
                    <span class="material-symbols-outlined">event</span>
                    {{ due(t) === 'today' ? ('tasks.today' | transloco) : (toDate(t.dueDate) | appDate) }}
                  </span>
                }
                <span class="assignee"><span class="material-symbols-outlined">person</span>{{ t.assigneeName }}</span>
                @if (t.companyId && hide() !== 'company') {
                  <a [routerLink]="['/companies', t.companyId]">{{ t.companyName }}</a>
                }
                @if (t.customerId && hide() !== 'customer') {
                  <a [routerLink]="['/customers', t.customerId]">{{ t.customerName }}</a>
                }
                @if (t.dealId && hide() !== 'deal') {
                  <a [routerLink]="['/deals', t.dealId]">{{ t.dealTitle }}</a>
                }
              </div>
              @if (t.description && toolbar()) {
                <p class="description">{{ t.description }}</p>
              }
            </div>
            @if (canChange(t)) {
              <button matIconButton class="task-menu" [matMenuTriggerFor]="menu" [attr.aria-label]="'common.actions' | transloco">
                <span class="material-symbols-outlined">more_vert</span>
              </button>
              <mat-menu #menu="matMenu" xPosition="before">
                <button mat-menu-item (click)="edit(t)">
                  <span class="material-symbols-outlined">edit</span><span>{{ 'common.edit' | transloco }}</span>
                </button>
                <button mat-menu-item (click)="remove(t)">
                  <span class="material-symbols-outlined">delete</span><span>{{ 'common.delete' | transloco }}</span>
                </button>
              </mat-menu>
            }
          </li>
        }
      </ul>

      @if (items().length < total() && toolbar()) {
        <button matButton (click)="loadMore()" [disabled]="loading()">{{ 'activities.loadMore' | transloco }}</button>
      }
    }
  `,
  styles: `
    .scope {
      margin-right: 0.5rem;
    }
    .new-task .material-symbols-outlined {
      margin-right: 0.25rem;
    }
    .empty {
      padding: 1rem 0;
    }
    .tasks {
      list-style: none;
      margin: 0;
      padding: 0;
    }
    .task {
      display: flex;
      align-items: flex-start;
      gap: 0.5rem;
      padding: 0.5rem 0;
      border-bottom: 1px solid var(--mat-sys-outline-variant);
    }
    .task:last-child {
      border-bottom: none;
    }
    .body {
      flex: 1;
      min-width: 0;
      padding-top: 0.6rem;
    }
    .title-row {
      display: flex;
      align-items: center;
      gap: 0.5rem;
      flex-wrap: wrap;
    }
    .title {
      font: var(--mat-sys-body-large);
    }
    .done .title {
      text-decoration: line-through;
      color: var(--mat-sys-on-surface-variant);
    }
    .priority {
      font: var(--mat-sys-label-small);
      padding: 0.05rem 0.5rem;
      border-radius: 1rem;
      background: var(--mat-sys-surface-container-highest);
    }
    .priority.high {
      background: var(--mat-sys-error-container);
      color: var(--mat-sys-on-error-container);
    }
    .meta {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: 0.15rem 1rem;
      font: var(--mat-sys-body-small);
      margin-top: 0.25rem;
    }
    .meta .material-symbols-outlined {
      font-size: 1rem;
      vertical-align: -0.2rem;
      margin-right: 0.15rem;
    }
    .due.overdue {
      color: var(--mat-sys-error);
      font-weight: 600;
    }
    .due.today {
      color: var(--mat-sys-primary);
      font-weight: 600;
    }
    .description {
      margin: 0.35rem 0 0;
      white-space: pre-line;
    }
  `,
})
export class TaskList {
  private readonly api = inject(TasksApi);
  private readonly dialog = inject(MatDialog);
  private readonly confirm = inject(ConfirmService);
  private readonly notify = inject(NotifyService);

  private readonly store = inject(AuthStore);
  readonly canWrite = this.store.can(Permissions.tasksWrite);
  private readonly canManage = this.store.can(Permissions.tasksManage);

  readonly companyId = input<number | null>(null);
  readonly customerId = input<number | null>(null);
  readonly dealId = input<number | null>(null);
  readonly context = input<TaskContext>({});
  readonly hide = input<'company' | 'customer' | 'deal' | null>(null);
  /** Show the Mine / Everyone switch (tasks page). */
  readonly scope = input(false);
  /** Start with only the caller's tasks. */
  readonly onlyMine = input(false);
  /** Search, filters and "new task"; off for the compact dashboard list. */
  readonly toolbar = input(true);
  readonly pageSize = input(25);

  readonly search = signal('');
  readonly status = signal<string | null>('open');
  readonly mine = signal(false);
  private readonly debouncedSearch = toSignal(toObservable(this.search).pipe(debounceTime(300)), { initialValue: '' });

  readonly items = signal<TaskDto[]>([]);
  readonly total = signal(0);
  readonly loading = signal(true);
  readonly busy = signal<number | null>(null);
  private page = 1;
  private request?: Subscription;
  private readonly today = localToday();

  readonly toDate = dateOnly;

  private readonly query = computed<PageQuery>(() => ({
    companyId: this.companyId(),
    customerId: this.customerId(),
    dealId: this.dealId(),
    mine: this.mine() || undefined,
    status: this.status(),
    search: this.debouncedSearch().trim() || undefined,
  }));

  constructor() {
    effect(() => {
      const mine = this.onlyMine();
      untracked(() => this.mine.set(mine));
    });

    // Reload from the first page whenever a filter changes.
    effect(() => {
      this.query();
      untracked(() => this.reload());
    });
  }

  reload(): void {
    this.page = 1;
    this.fetch(false);
  }

  loadMore(): void {
    this.page++;
    this.fetch(true);
  }

  due(task: TaskDto): 'overdue' | 'today' | 'later' {
    if (!task.dueDate || task.isDone) return 'later';
    return task.dueDate < this.today ? 'overdue' : task.dueDate === this.today ? 'today' : 'later';
  }

  canChange(task: TaskDto): boolean {
    const me = this.store.user()?.id;
    return this.canWrite && (this.canManage || task.assigneeId === me || task.createdBy === me);
  }

  toggle(task: TaskDto): void {
    this.busy.set(task.id);
    (task.isDone ? this.api.reopen(task.id) : this.api.complete(task.id)).subscribe({
      next: () => {
        this.busy.set(null);
        this.notify.success(task.isDone ? 'tasks.reopened' : 'tasks.completed');
        this.reload();
      },
      error: (error) => {
        this.busy.set(null);
        this.notify.error(error);
        this.reload();
      },
    });
  }

  create(): void {
    TaskFormDialog.open(this.dialog, { context: this.context() })
      .pipe(filter(Boolean))
      .subscribe(() => this.reload());
  }

  edit(task: TaskDto): void {
    TaskFormDialog.open(this.dialog, { task })
      .pipe(filter(Boolean))
      .subscribe(() => this.reload());
  }

  remove(task: TaskDto): void {
    this.confirm.delete(task.title, () =>
      this.api.delete(task.id).subscribe({
        next: () => {
          this.notify.success('common.deleted');
          this.reload();
        },
        error: (error) => this.notify.error(error),
      }),
    );
  }

  private fetch(append: boolean): void {
    this.request?.unsubscribe();
    this.loading.set(true);

    this.request = this.api.list({ ...this.query(), pageNumber: this.page, pageSize: this.pageSize() }).subscribe({
      next: (page) => {
        this.items.update((items) => (append ? [...items, ...page.items] : page.items));
        this.total.set(page.totalCount);
        this.loading.set(false);
      },
      error: (error) => {
        this.loading.set(false);
        this.notify.error(error);
      },
    });
  }
}
