import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialog, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { TranslocoPipe } from '@jsverse/transloco';
import { Observable } from 'rxjs';

import { CustomerDto } from '../../core/api/models';
import { NotifyService } from '../../core/ui/notify.service';
import { FieldError } from '../../shared/field-error';
import { CompanySelect } from '../companies/company-select';
import { CustomersApi } from './customers.api';

export interface CustomerDialogData {
  /** Edit this customer; omit to create one. */
  customer?: CustomerDto | null;
  /** Preselect and lock the company (used on a company's page). */
  company?: { id: number; name: string } | null;
}

/** Create or edit a customer; the dialog closes with the saved customer. */
@Component({
  selector: 'app-customer-form-dialog',
  imports: [
    ReactiveFormsModule, TranslocoPipe, MatDialogModule, MatButtonModule, MatFormFieldModule, MatInputModule,
    MatSlideToggleModule, FieldError, CompanySelect,
  ],
  template: `
    <h2 mat-dialog-title>{{ (customer ? 'customers.edit' : 'customers.new') | transloco }}</h2>
    <mat-dialog-content>
      <form [formGroup]="form" (ngSubmit)="save()" class="form-grid two-columns" id="customer-form">
        <mat-form-field>
          <mat-label>{{ 'customers.firstName' | transloco }}</mat-label>
          <input matInput id="firstName" formControlName="firstName" cdkFocusInitial />
          <mat-error><app-field-error [control]="form.controls.firstName" /></mat-error>
        </mat-form-field>
        <mat-form-field>
          <mat-label>{{ 'customers.lastName' | transloco }}</mat-label>
          <input matInput id="lastName" formControlName="lastName" />
          <mat-error><app-field-error [control]="form.controls.lastName" /></mat-error>
        </mat-form-field>
        <mat-form-field>
          <mat-label>{{ 'customers.email' | transloco }}</mat-label>
          <input matInput id="email" type="email" formControlName="email" />
          <mat-error><app-field-error [control]="form.controls.email" /></mat-error>
        </mat-form-field>
        <mat-form-field>
          <mat-label>{{ 'customers.phone' | transloco }}</mat-label>
          <input matInput id="phoneNumber" type="tel" formControlName="phoneNumber" maxlength="20" />
          <mat-error><app-field-error [control]="form.controls.phoneNumber" /></mat-error>
        </mat-form-field>
        <app-company-select
          class="span-2"
          inputId="companyId"
          formControlName="companyId"
          [label]="'customers.company' | transloco"
          [initialName]="customer?.companyName ?? data.company?.name"
        />
        @if (customer) {
          <mat-slide-toggle class="span-2" formControlName="isActive">{{ 'common.active' | transloco }}</mat-slide-toggle>
        }
      </form>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button matButton mat-dialog-close type="button">{{ 'common.cancel' | transloco }}</button>
      <button matButton="filled" type="submit" form="customer-form" [disabled]="saving()">{{ 'common.save' | transloco }}</button>
    </mat-dialog-actions>
  `,
})
export class CustomerFormDialog {
  private readonly api = inject(CustomersApi);
  private readonly notify = inject(NotifyService);
  private readonly dialogRef = inject(MatDialogRef<CustomerFormDialog, CustomerDto>);
  readonly data = inject<CustomerDialogData>(MAT_DIALOG_DATA);
  readonly customer = this.data.customer ?? null;

  readonly saving = signal(false);

  readonly form = inject(FormBuilder).group({
    firstName: [this.customer?.firstName ?? '', [Validators.required, Validators.maxLength(100)]],
    lastName: [this.customer?.lastName ?? '', [Validators.required, Validators.maxLength(100)]],
    email: [this.customer?.email ?? '', [Validators.required, Validators.email, Validators.maxLength(150)]],
    phoneNumber: [this.customer?.phoneNumber ?? '', Validators.maxLength(20)],
    companyId: [
      { value: this.customer?.companyId ?? this.data.company?.id ?? null, disabled: !this.customer && !!this.data.company },
      Validators.required,
    ],
    isActive: [this.customer?.isActive ?? true],
  });

  /** Opens the dialog; emits the saved customer (nothing when cancelled). */
  static open(dialog: MatDialog, data: CustomerDialogData = {}): Observable<CustomerDto | undefined> {
    return dialog
      .open<CustomerFormDialog, CustomerDialogData, CustomerDto>(CustomerFormDialog, { data, width: 'min(40rem, 95vw)' })
      .afterClosed();
  }

  save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const value = this.form.getRawValue();
    const input = {
      firstName: value.firstName!,
      lastName: value.lastName!,
      email: value.email!,
      phoneNumber: value.phoneNumber ?? '',
      companyId: value.companyId!,
    };

    const request: Observable<CustomerDto> = this.customer
      ? this.api.update(this.customer.id, { ...input, isActive: value.isActive! })
      : this.api.create({ ...input, phoneNumber: input.phoneNumber || null });

    this.saving.set(true);
    request.subscribe({
      next: (saved) => {
        this.notify.success(this.customer ? 'common.saved' : 'common.created');
        this.dialogRef.close(saved);
      },
      error: (error) => {
        this.saving.set(false);
        if (!this.notify.applyToForm(this.form, error)) this.notify.error(error);
      },
    });
  }
}
