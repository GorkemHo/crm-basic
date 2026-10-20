import { Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MAT_DIALOG_DATA, MatDialog, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { TranslocoPipe } from '@jsverse/transloco';
import { Observable, forkJoin, of, startWith, switchMap } from 'rxjs';

import { ACTIVITY_TYPES, ActivityDto, ActivityType, CustomerDto, DealDto } from '../../core/api/models';
import { AuthStore } from '../../core/auth/auth.store';
import { Permissions } from '../../core/auth/permissions';
import { NotifyService } from '../../core/ui/notify.service';
import { FieldError } from '../../shared/field-error';
import { CompanySelect } from '../companies/company-select';
import { CustomersApi } from '../customers/customers.api';
import { DealsApi } from '../deals/deals.api';
import { ACTIVITY_ICONS, ActivitiesApi } from './activities.api';

/** Where a new activity is logged from; these links are pre-filled. */
export interface ActivityContext {
  company?: { id: number; name: string } | null;
  customerId?: number | null;
  dealId?: number | null;
}

export interface ActivityFormData {
  activity?: ActivityDto;
  context?: ActivityContext;
}

/** `yyyy-MM-ddTHH:mm` in local time, for `<input type="datetime-local">`. */
function toLocalInput(date: Date): string {
  const pad = (n: number) => String(n).padStart(2, '0');
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}T${pad(date.getHours())}:${pad(date.getMinutes())}`;
}

/** Log or edit an activity; the dialog closes with the saved activity. */
@Component({
  selector: 'app-activity-form-dialog',
  imports: [
    ReactiveFormsModule, TranslocoPipe, MatDialogModule, MatButtonModule, MatButtonToggleModule,
    MatFormFieldModule, MatInputModule, MatSelectModule, FieldError, CompanySelect,
  ],
  template: `
    <h2 mat-dialog-title>{{ (activity ? 'activities.edit' : 'activities.new') | transloco }}</h2>
    <mat-dialog-content>
      <form [formGroup]="form" (ngSubmit)="save()" class="form-grid two-columns" id="activity-form">
        <mat-button-toggle-group class="span-2 types" formControlName="type" [hideSingleSelectionIndicator]="true">
          @for (type of types; track type) {
            <mat-button-toggle [value]="type" [attr.data-type]="type">
              <span class="material-symbols-outlined">{{ icons[type] }}</span> {{ 'activities.types.' + type | transloco }}
            </mat-button-toggle>
          }
        </mat-button-toggle-group>

        <mat-form-field class="span-2">
          <mat-label>{{ 'activities.subject' | transloco }}</mat-label>
          <input matInput id="subject" formControlName="subject" cdkFocusInitial />
          <mat-error><app-field-error [control]="form.controls.subject" /></mat-error>
        </mat-form-field>

        <mat-form-field [class.span-2]="!hasDuration()">
          <mat-label>{{ 'activities.when' | transloco }}</mat-label>
          <input matInput id="occurredAt" type="datetime-local" formControlName="occurredAt" />
          <mat-error><app-field-error [control]="form.controls.occurredAt" /></mat-error>
        </mat-form-field>

        @if (hasDuration()) {
          <mat-form-field>
            <mat-label>{{ 'activities.duration' | transloco }}</mat-label>
            <input matInput id="durationMinutes" type="number" min="1" max="1440" formControlName="durationMinutes" />
            <span matTextSuffix>{{ 'activities.minutes' | transloco }}</span>
          </mat-form-field>
        }

        <app-company-select
          class="span-2"
          inputId="activityCompany"
          formControlName="companyId"
          [label]="'deals.company' | transloco"
          [initialName]="initialCompanyName"
        />

        <mat-form-field>
          <mat-label>{{ 'deals.contact' | transloco }}</mat-label>
          <mat-select id="activityCustomer" formControlName="customerId">
            <mat-option [value]="null">{{ 'common.none' | transloco }}</mat-option>
            @for (customer of contacts(); track customer.id) {
              <mat-option [value]="customer.id">{{ customer.firstName }} {{ customer.lastName }}</mat-option>
            }
          </mat-select>
        </mat-form-field>

        <mat-form-field>
          <mat-label>{{ 'activities.deal' | transloco }}</mat-label>
          <mat-select id="activityDeal" formControlName="dealId">
            <mat-option [value]="null">{{ 'common.none' | transloco }}</mat-option>
            @for (deal of deals(); track deal.id) {
              <mat-option [value]="deal.id">{{ deal.title }}</mat-option>
            }
          </mat-select>
        </mat-form-field>

        <mat-form-field class="span-2">
          <mat-label>{{ 'activities.notes' | transloco }}</mat-label>
          <textarea matInput id="notes" rows="4" maxlength="4000" formControlName="notes"></textarea>
        </mat-form-field>
      </form>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button matButton mat-dialog-close type="button">{{ 'common.cancel' | transloco }}</button>
      <button matButton="filled" type="submit" form="activity-form" [disabled]="saving()">{{ 'common.save' | transloco }}</button>
    </mat-dialog-actions>
  `,
  styles: `
    .types {
      margin-bottom: 1rem;
      flex-wrap: wrap;
    }
    .types .material-symbols-outlined {
      font-size: 1.1rem;
      vertical-align: middle;
      margin-right: 0.15rem;
    }
  `,
})
export class ActivityFormDialog {
  private readonly api = inject(ActivitiesApi);
  private readonly customers = inject(CustomersApi);
  private readonly dealsApi = inject(DealsApi);
  private readonly notify = inject(NotifyService);
  private readonly dialogRef = inject(MatDialogRef<ActivityFormDialog, ActivityDto>);
  private readonly data = inject<ActivityFormData>(MAT_DIALOG_DATA);
  private readonly canReadDeals = inject(AuthStore).can(Permissions.dealsRead);

  readonly activity = this.data.activity ?? null;
  private readonly context = this.data.context ?? {};
  readonly initialCompanyName = this.activity?.companyName ?? this.context.company?.name ?? '';

  readonly types = ACTIVITY_TYPES;
  readonly icons = ACTIVITY_ICONS;
  readonly saving = signal(false);
  readonly contacts = signal<CustomerDto[]>([]);
  readonly deals = signal<DealDto[]>([]);

  readonly form = inject(FormBuilder).group({
    type: [(this.activity?.type ?? 'note') as ActivityType, Validators.required],
    subject: [this.activity?.subject ?? '', [Validators.required, Validators.maxLength(200)]],
    occurredAt: [toLocalInput(this.activity ? new Date(this.activity.occurredAt) : new Date()), Validators.required],
    durationMinutes: [this.activity?.durationMinutes ?? null, [Validators.min(1), Validators.max(1440)]],
    companyId: [this.activity?.companyId ?? this.context.company?.id ?? null, Validators.required],
    customerId: [this.activity?.customerId ?? this.context.customerId ?? null],
    dealId: [this.activity?.dealId ?? this.context.dealId ?? null],
    notes: [this.activity?.notes ?? ''],
  });

  private readonly type = toSignal(this.form.controls.type.valueChanges, { initialValue: this.form.controls.type.value });
  readonly hasDuration = computed(() => this.type() === 'call' || this.type() === 'meeting');

  constructor() {
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

  static open(dialog: MatDialog, data: ActivityFormData = {}): Observable<ActivityDto | undefined> {
    return dialog
      .open<ActivityFormDialog, ActivityFormData, ActivityDto>(ActivityFormDialog, { data, width: 'min(40rem, 95vw)' })
      .afterClosed();
  }

  save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const v = this.form.getRawValue();
    const input = {
      type: v.type!,
      subject: v.subject!,
      notes: v.notes || null,
      occurredAt: new Date(v.occurredAt!).toISOString(),
      durationMinutes: this.hasDuration() && v.durationMinutes ? Number(v.durationMinutes) : null,
      companyId: v.companyId!,
      customerId: v.customerId ?? null,
      dealId: v.dealId ?? null,
    };

    const request = this.activity ? this.api.update(this.activity.id, input) : this.api.create(input);

    this.saving.set(true);
    request.subscribe({
      next: (saved) => {
        this.notify.success(this.activity ? 'common.saved' : 'activities.logged');
        this.dialogRef.close(saved);
      },
      error: (error) => {
        this.saving.set(false);
        if (!this.notify.applyToForm(this.form, error)) this.notify.error(error);
      },
    });
  }
}
