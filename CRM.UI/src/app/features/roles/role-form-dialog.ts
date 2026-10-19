import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialog, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { TranslocoPipe } from '@jsverse/transloco';
import { Observable } from 'rxjs';

import { RoleDto } from '../../core/api/models';
import { NotifyService } from '../../core/ui/notify.service';
import { FieldError } from '../../shared/field-error';
import { RolesApi } from './roles.api';

/** Create a role, or rename / (de)activate a custom one. Closes with the saved role. */
@Component({
  selector: 'app-role-form-dialog',
  imports: [ReactiveFormsModule, TranslocoPipe, MatDialogModule, MatButtonModule, MatFormFieldModule, MatInputModule, MatSlideToggleModule, FieldError],
  template: `
    <h2 mat-dialog-title>{{ (role ? 'roles.rename' : 'roles.new') | transloco }}</h2>
    <mat-dialog-content>
      <form [formGroup]="form" (ngSubmit)="save()" class="form-grid" id="role-form">
        <mat-form-field>
          <mat-label>{{ 'roles.name' | transloco }}</mat-label>
          <input matInput id="roleName" formControlName="name" maxlength="50" cdkFocusInitial />
          <mat-error><app-field-error [control]="form.controls.name" /></mat-error>
        </mat-form-field>
        @if (role) {
          <mat-slide-toggle formControlName="isActive">{{ 'common.active' | transloco }}</mat-slide-toggle>
        }
      </form>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button matButton mat-dialog-close type="button">{{ 'common.cancel' | transloco }}</button>
      <button matButton="filled" type="submit" form="role-form" [disabled]="saving()">{{ 'common.save' | transloco }}</button>
    </mat-dialog-actions>
  `,
})
export class RoleFormDialog {
  private readonly api = inject(RolesApi);
  private readonly notify = inject(NotifyService);
  private readonly dialogRef = inject(MatDialogRef<RoleFormDialog, RoleDto>);
  readonly role = inject<RoleDto | null>(MAT_DIALOG_DATA);

  readonly saving = signal(false);

  readonly form = inject(FormBuilder).nonNullable.group({
    name: [this.role?.name ?? '', [Validators.required, Validators.maxLength(50)]],
    isActive: [this.role?.isActive ?? true],
  });

  static open(dialog: MatDialog, role: RoleDto | null = null): Observable<RoleDto | undefined> {
    return dialog.open<RoleFormDialog, RoleDto | null, RoleDto>(RoleFormDialog, { data: role, width: 'min(28rem, 95vw)' }).afterClosed();
  }

  save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const { name, isActive } = this.form.getRawValue();
    const request = this.role ? this.api.update(this.role.id, { name, isActive }) : this.api.create(name);

    this.saving.set(true);
    request.subscribe({
      next: (saved) => {
        this.notify.success(this.role ? 'common.saved' : 'common.created');
        this.dialogRef.close(saved);
      },
      error: (error) => {
        this.saving.set(false);
        if (!this.notify.applyToForm(this.form, error)) this.notify.error(error);
      },
    });
  }
}
