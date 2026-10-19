import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialog, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { TranslocoPipe } from '@jsverse/transloco';
import { Observable } from 'rxjs';

import { CompanyDto } from '../../core/api/models';
import { NotifyService } from '../../core/ui/notify.service';
import { FieldError } from '../../shared/field-error';
import { CompaniesApi } from './companies.api';

/** Create (no company) or edit a company; the dialog closes with the saved company. */
@Component({
  selector: 'app-company-form-dialog',
  imports: [ReactiveFormsModule, TranslocoPipe, MatDialogModule, MatButtonModule, MatFormFieldModule, MatInputModule, MatSlideToggleModule, FieldError],
  template: `
    <h2 mat-dialog-title>{{ (company ? 'companies.edit' : 'companies.new') | transloco }}</h2>
    <mat-dialog-content>
      <form [formGroup]="form" (ngSubmit)="save()" class="form-grid" id="company-form">
        <mat-form-field>
          <mat-label>{{ 'companies.name' | transloco }}</mat-label>
          <input matInput id="companyName" formControlName="companyName" cdkFocusInitial />
          <mat-error><app-field-error [control]="form.controls.companyName" /></mat-error>
        </mat-form-field>
        <mat-form-field>
          <mat-label>{{ 'companies.taxNumber' | transloco }}</mat-label>
          <input matInput id="taxNumber" formControlName="taxNumber" maxlength="11" />
          <mat-error><app-field-error [control]="form.controls.taxNumber" /></mat-error>
        </mat-form-field>
        @if (company) {
          <mat-slide-toggle formControlName="isActive">{{ 'common.active' | transloco }}</mat-slide-toggle>
        }
      </form>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button matButton mat-dialog-close type="button">{{ 'common.cancel' | transloco }}</button>
      <button matButton="filled" type="submit" form="company-form" [disabled]="saving()">{{ 'common.save' | transloco }}</button>
    </mat-dialog-actions>
  `,
})
export class CompanyFormDialog {
  private readonly api = inject(CompaniesApi);
  private readonly notify = inject(NotifyService);
  private readonly dialogRef = inject(MatDialogRef<CompanyFormDialog, CompanyDto>);
  readonly company = inject<CompanyDto | null>(MAT_DIALOG_DATA);

  readonly saving = signal(false);

  readonly form = inject(FormBuilder).nonNullable.group({
    companyName: [this.company?.companyName ?? '', [Validators.required, Validators.maxLength(100)]],
    taxNumber: [this.company?.taxNumber ?? '', Validators.maxLength(11)],
    isActive: [this.company?.isActive ?? true],
  });

  /** Opens the dialog; emits the saved company (nothing when cancelled). */
  static open(dialog: MatDialog, company: CompanyDto | null = null): Observable<CompanyDto | undefined> {
    return dialog
      .open<CompanyFormDialog, CompanyDto | null, CompanyDto>(CompanyFormDialog, { data: company, width: 'min(32rem, 95vw)' })
      .afterClosed();
  }

  save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const { companyName, taxNumber, isActive } = this.form.getRawValue();

    const request: Observable<CompanyDto> = this.company
      ? this.api.update(this.company.id, { companyName, taxNumber, isActive })
      : this.api.create({ companyName, taxNumber: taxNumber || null });

    this.saving.set(true);
    request.subscribe({
      next: (saved) => {
        this.notify.success(this.company ? 'common.saved' : 'common.created');
        this.dialogRef.close(saved);
      },
      error: (error) => {
        this.saving.set(false);
        if (!this.notify.applyToForm(this.form, error)) this.notify.error(error);
      },
    });
  }
}
