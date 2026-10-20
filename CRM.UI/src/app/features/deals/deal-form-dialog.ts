import { Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialog, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { TranslocoPipe } from '@jsverse/transloco';
import { Observable, of, startWith, switchMap } from 'rxjs';

import { CURRENCIES, Currency, CustomerDto, DealDto, StageDto } from '../../core/api/models';
import { NotifyService } from '../../core/ui/notify.service';
import { FieldError } from '../../shared/field-error';
import { CompanySelect } from '../companies/company-select';
import { CustomersApi } from '../customers/customers.api';
import { DealsApi, StagesApi } from './deals.api';

export interface DealFormData {
  /** Edit this deal; otherwise a new one is created. */
  deal?: DealDto;
  /** New deal for this company (company page). */
  company?: { id: number; name: string } | null;
  /** New deal in this stage (board column). */
  stageId?: number;
}

/** Create or edit a deal; the dialog closes with the saved deal. Stage and status change elsewhere. */
@Component({
  selector: 'app-deal-form-dialog',
  imports: [ReactiveFormsModule, TranslocoPipe, MatDialogModule, MatButtonModule, MatFormFieldModule, MatInputModule, MatSelectModule, FieldError, CompanySelect],
  template: `
    <h2 mat-dialog-title>{{ (deal ? 'deals.edit' : 'deals.new') | transloco }}</h2>
    <mat-dialog-content>
      <form [formGroup]="form" (ngSubmit)="save()" class="form-grid two-columns" id="deal-form">
        <mat-form-field class="span-2">
          <mat-label>{{ 'deals.dealTitle' | transloco }}</mat-label>
          <input matInput id="title" formControlName="title" cdkFocusInitial />
          <mat-error><app-field-error [control]="form.controls.title" /></mat-error>
        </mat-form-field>

        <app-company-select
          class="span-2"
          inputId="companyId"
          formControlName="companyId"
          [label]="'deals.company' | transloco"
          [initialName]="initialCompanyName"
        />

        <mat-form-field class="span-2">
          <mat-label>{{ 'deals.contact' | transloco }}</mat-label>
          <mat-select id="customerId" formControlName="customerId">
            <mat-option [value]="null">{{ 'common.none' | transloco }}</mat-option>
            @for (customer of contacts(); track customer.id) {
              <mat-option [value]="customer.id">{{ customer.firstName }} {{ customer.lastName }}</mat-option>
            }
          </mat-select>
        </mat-form-field>

        <mat-form-field>
          <mat-label>{{ 'deals.amount' | transloco }}</mat-label>
          <input matInput id="amount" type="number" min="0" step="0.01" formControlName="amount" />
          <mat-error><app-field-error [control]="form.controls.amount" /></mat-error>
        </mat-form-field>

        <mat-form-field>
          <mat-label>{{ 'deals.currency' | transloco }}</mat-label>
          <mat-select id="currency" formControlName="currency">
            @for (currency of currencies; track currency) {
              <mat-option [value]="currency">{{ currency }}</mat-option>
            }
          </mat-select>
        </mat-form-field>

        @if (!deal) {
          <mat-form-field>
            <mat-label>{{ 'deals.stage' | transloco }}</mat-label>
            <mat-select id="stageId" formControlName="stageId">
              @for (stage of stages(); track stage.id) {
                <mat-option [value]="stage.id">{{ stage.name }}</mat-option>
              }
            </mat-select>
          </mat-form-field>
        }

        <mat-form-field [class.span-2]="!!deal">
          <mat-label>{{ 'deals.expectedClose' | transloco }}</mat-label>
          <input matInput id="expectedCloseDate" type="date" formControlName="expectedCloseDate" />
        </mat-form-field>
      </form>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button matButton mat-dialog-close type="button">{{ 'common.cancel' | transloco }}</button>
      <button matButton="filled" type="submit" form="deal-form" [disabled]="saving()">{{ 'common.save' | transloco }}</button>
    </mat-dialog-actions>
  `,
})
export class DealFormDialog {
  private readonly api = inject(DealsApi);
  private readonly customers = inject(CustomersApi);
  private readonly notify = inject(NotifyService);
  private readonly dialogRef = inject(MatDialogRef<DealFormDialog, DealDto>);
  private readonly data = inject<DealFormData>(MAT_DIALOG_DATA);

  readonly deal = this.data.deal ?? null;
  readonly initialCompanyName = this.deal?.companyName ?? this.data.company?.name ?? '';
  readonly currencies = CURRENCIES;

  readonly saving = signal(false);
  readonly stages = signal<StageDto[]>([]);
  readonly contacts = signal<CustomerDto[]>([]);

  readonly form = inject(FormBuilder).group({
    title: [this.deal?.title ?? '', [Validators.required, Validators.maxLength(200)]],
    companyId: [this.deal?.companyId ?? this.data.company?.id ?? null, Validators.required],
    customerId: [this.deal?.customerId ?? null],
    amount: [this.deal?.amount ?? null, [Validators.required, Validators.min(0)]],
    currency: [(this.deal?.currency ?? 'TRY') as Currency, Validators.required],
    stageId: [this.data.stageId ?? null],
    expectedCloseDate: [this.deal?.expectedCloseDate ?? ''],
  });

  constructor() {
    if (!this.deal) {
      inject(StagesApi).list().subscribe((stages) => {
        this.stages.set(stages);
        if (this.form.controls.stageId.value === null && stages.length) this.form.controls.stageId.setValue(stages[0].id);
      });
    }

    // Contacts are the chosen company's customers; changing the company clears the contact.
    const companyId = this.form.controls.companyId;
    companyId.valueChanges
      .pipe(
        startWith(companyId.value),
        switchMap((id) => (id ? this.customers.list({ companyId: id, pageSize: 100, sortBy: 'firstName' }) : of({ items: [] as CustomerDto[] }))),
        takeUntilDestroyed(inject(DestroyRef)),
      )
      .subscribe((page) => {
        this.contacts.set(page.items);
        const contact = this.form.controls.customerId;
        if (contact.value !== null && !page.items.some((c) => c.id === contact.value)) contact.setValue(null);
      });
  }

  static open(dialog: MatDialog, data: DealFormData = {}): Observable<DealDto | undefined> {
    return dialog
      .open<DealFormDialog, DealFormData, DealDto>(DealFormDialog, { data, width: 'min(36rem, 95vw)' })
      .afterClosed();
  }

  save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const v = this.form.getRawValue();
    const input = {
      title: v.title!,
      companyId: v.companyId!,
      customerId: v.customerId ?? null,
      amount: Number(v.amount),
      currency: v.currency!,
      expectedCloseDate: v.expectedCloseDate || null,
    };

    const request = this.deal ? this.api.update(this.deal.id, input) : this.api.create({ ...input, stageId: v.stageId });

    this.saving.set(true);
    request.subscribe({
      next: (saved) => {
        this.notify.success(this.deal ? 'common.saved' : 'common.created');
        this.dialogRef.close(saved);
      },
      error: (error) => {
        this.saving.set(false);
        if (!this.notify.applyToForm(this.form, error)) this.notify.error(error);
      },
    });
  }
}
