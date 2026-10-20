import { Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialog, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { TranslocoPipe } from '@jsverse/transloco';
import { Observable, forkJoin, of, startWith, switchMap } from 'rxjs';

import { AssigneeDto, CustomerDto, DealDto, TASK_PRIORITIES, TaskDto, TaskPriority } from '../../core/api/models';
import { AuthStore } from '../../core/auth/auth.store';
import { Permissions } from '../../core/auth/permissions';
import { NotifyService } from '../../core/ui/notify.service';
import { FieldError } from '../../shared/field-error';
import { CompanySelect } from '../companies/company-select';
import { CustomersApi } from '../customers/customers.api';
import { DealsApi } from '../deals/deals.api';
import { TasksApi } from './tasks.api';

/** Where a new task is created from; these links are pre-filled. */
export interface TaskContext {
  company?: { id: number; name: string } | null;
  customerId?: number | null;
  dealId?: number | null;
}

export interface TaskFormData {
  task?: TaskDto;
  context?: TaskContext;
}

/** Create or edit a task; the dialog closes with the saved task. */
@Component({
  selector: 'app-task-form-dialog',
  imports: [ReactiveFormsModule, TranslocoPipe, MatDialogModule, MatButtonModule, MatFormFieldModule, MatInputModule, MatSelectModule, FieldError, CompanySelect],
  template: `
    <h2 mat-dialog-title>{{ (task ? 'tasks.edit' : 'tasks.new') | transloco }}</h2>
    <mat-dialog-content>
      <form [formGroup]="form" (ngSubmit)="save()" class="form-grid two-columns" id="task-form">
        <mat-form-field class="span-2">
          <mat-label>{{ 'tasks.taskTitle' | transloco }}</mat-label>
          <input matInput id="taskTitle" formControlName="title" cdkFocusInitial />
          <mat-error><app-field-error [control]="form.controls.title" /></mat-error>
        </mat-form-field>

        <mat-form-field>
          <mat-label>{{ 'tasks.assignee' | transloco }}</mat-label>
          <mat-select id="assigneeId" formControlName="assigneeId">
            @for (user of assignees(); track user.id) {
              <mat-option [value]="user.id">{{ user.fullName }}{{ user.id === myId ? ' (' + ('users.you' | transloco) + ')' : '' }}</mat-option>
            }
          </mat-select>
        </mat-form-field>

        <mat-form-field>
          <mat-label>{{ 'tasks.dueDate' | transloco }}</mat-label>
          <input matInput id="dueDate" type="date" formControlName="dueDate" />
        </mat-form-field>

        <mat-form-field class="span-2">
          <mat-label>{{ 'tasks.priority' | transloco }}</mat-label>
          <mat-select id="priority" formControlName="priority">
            @for (p of priorities; track p) {
              <mat-option [value]="p">{{ 'tasks.priorities.' + p | transloco }}</mat-option>
            }
          </mat-select>
        </mat-form-field>

        <app-company-select
          class="span-2"
          inputId="taskCompany"
          formControlName="companyId"
          [clearable]="true"
          [label]="'tasks.relatedCompany' | transloco"
          [initialName]="initialCompanyName"
        />

        <mat-form-field>
          <mat-label>{{ 'deals.contact' | transloco }}</mat-label>
          <mat-select id="taskCustomer" formControlName="customerId">
            <mat-option [value]="null">{{ 'common.none' | transloco }}</mat-option>
            @for (customer of contacts(); track customer.id) {
              <mat-option [value]="customer.id">{{ customer.firstName }} {{ customer.lastName }}</mat-option>
            }
          </mat-select>
        </mat-form-field>

        <mat-form-field>
          <mat-label>{{ 'activities.deal' | transloco }}</mat-label>
          <mat-select id="taskDeal" formControlName="dealId">
            <mat-option [value]="null">{{ 'common.none' | transloco }}</mat-option>
            @for (deal of deals(); track deal.id) {
              <mat-option [value]="deal.id">{{ deal.title }}</mat-option>
            }
          </mat-select>
        </mat-form-field>

        <mat-form-field class="span-2">
          <mat-label>{{ 'tasks.description' | transloco }}</mat-label>
          <textarea matInput id="taskDescription" rows="3" maxlength="2000" formControlName="description"></textarea>
        </mat-form-field>
      </form>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button matButton mat-dialog-close type="button">{{ 'common.cancel' | transloco }}</button>
      <button matButton="filled" type="submit" form="task-form" [disabled]="saving()">{{ 'common.save' | transloco }}</button>
    </mat-dialog-actions>
  `,
})
export class TaskFormDialog {
  private readonly api = inject(TasksApi);
  private readonly customers = inject(CustomersApi);
  private readonly dealsApi = inject(DealsApi);
  private readonly notify = inject(NotifyService);
  private readonly dialogRef = inject(MatDialogRef<TaskFormDialog, TaskDto>);
  private readonly data = inject<TaskFormData>(MAT_DIALOG_DATA);
  private readonly store = inject(AuthStore);
  private readonly canReadDeals = this.store.can(Permissions.dealsRead);

  readonly task = this.data.task ?? null;
  private readonly context = this.data.context ?? {};
  readonly initialCompanyName = this.task?.companyName ?? this.context.company?.name ?? '';
  readonly myId = this.store.user()?.id;

  readonly priorities = TASK_PRIORITIES;
  readonly saving = signal(false);
  readonly assignees = signal<AssigneeDto[]>([]);
  readonly contacts = signal<CustomerDto[]>([]);
  readonly deals = signal<DealDto[]>([]);

  readonly form = inject(FormBuilder).group({
    title: [this.task?.title ?? '', [Validators.required, Validators.maxLength(200)]],
    description: [this.task?.description ?? ''],
    dueDate: [this.task?.dueDate ?? ''],
    priority: [(this.task?.priority ?? 'normal') as TaskPriority, Validators.required],
    assigneeId: [this.task?.assigneeId ?? this.myId ?? null, Validators.required],
    companyId: [this.task?.companyId ?? this.context.company?.id ?? null],
    customerId: [this.task?.customerId ?? this.context.customerId ?? null],
    dealId: [this.task?.dealId ?? this.context.dealId ?? null],
  });

  constructor() {
    this.api.assignees().subscribe((users) => {
      // Keep an assignee who was deactivated since, so editing doesn't change it silently.
      const current = this.task;
      const missing = current && !users.some((u) => u.id === current.assigneeId);
      this.assignees.set(missing ? [...users, { id: current.assigneeId, fullName: current.assigneeName ?? `#${current.assigneeId}` }] : users);
    });

    // Contacts and deals are the chosen company's; changing the company clears links that no longer fit.
    const companyId = this.form.controls.companyId;
    companyId.valueChanges
      .pipe(
        startWith(companyId.value),
        switchMap((id) =>
          id
            ? forkJoin({
                contacts: this.customers.list({ companyId: id, pageSize: 100, sortBy: 'firstName' }),
                deals: this.canReadDeals ? this.dealsApi.list({ companyId: id, pageSize: 100 }) : of({ items: [] as DealDto[] }),
              })
            : of({ contacts: { items: [] as CustomerDto[] }, deals: { items: [] as DealDto[] } }),
        ),
        takeUntilDestroyed(inject(DestroyRef)),
      )
      .subscribe(({ contacts, deals }) => {
        this.contacts.set(contacts.items);
        this.deals.set(deals.items);
        const { customerId, dealId } = this.form.controls;
        if (customerId.value !== null && !contacts.items.some((c) => c.id === customerId.value)) customerId.setValue(null);
        if (dealId.value !== null && !deals.items.some((d) => d.id === dealId.value)) dealId.setValue(null);
      });
  }

  static open(dialog: MatDialog, data: TaskFormData = {}): Observable<TaskDto | undefined> {
    return dialog.open<TaskFormDialog, TaskFormData, TaskDto>(TaskFormDialog, { data, width: 'min(38rem, 95vw)' }).afterClosed();
  }

  save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const v = this.form.getRawValue();
    const input = {
      title: v.title!,
      description: v.description || null,
      dueDate: v.dueDate || null,
      priority: v.priority!,
      assigneeId: v.assigneeId ?? null,
      companyId: v.companyId ?? null,
      customerId: v.companyId ? (v.customerId ?? null) : null,
      dealId: v.companyId ? (v.dealId ?? null) : null,
    };

    const request = this.task ? this.api.update(this.task.id, input) : this.api.create(input);

    this.saving.set(true);
    request.subscribe({
      next: (saved) => {
        this.notify.success(this.task ? 'common.saved' : 'common.created');
        this.dialogRef.close(saved);
      },
      error: (error) => {
        this.saving.set(false);
        if (!this.notify.applyToForm(this.form, error)) this.notify.error(error);
      },
    });
  }
}
