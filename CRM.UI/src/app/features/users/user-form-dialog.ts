import { Component, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialog, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { TranslocoPipe } from '@jsverse/transloco';
import { Observable, catchError, of } from 'rxjs';

import { UserDto } from '../../core/api/models';
import { AuthStore } from '../../core/auth/auth.store';
import { DEFAULT_LANGUAGE, LANGUAGES } from '../../core/i18n/language.service';
import { NotifyService } from '../../core/ui/notify.service';
import { FieldError } from '../../shared/field-error';
import { MIN_PASSWORD_LENGTH } from '../auth/register';
import { RolesApi } from '../roles/roles.api';
import { UsersApi } from './users.api';

/**
 * Create a user (with password and roles) or edit one (name, email, language, active).
 * Roles of existing users are edited on the user page. Closes with the saved user.
 */
@Component({
  selector: 'app-user-form-dialog',
  imports: [
    ReactiveFormsModule, TranslocoPipe, MatDialogModule, MatButtonModule, MatFormFieldModule, MatInputModule,
    MatSelectModule, MatSlideToggleModule, FieldError,
  ],
  template: `
    <h2 mat-dialog-title>{{ (user ? 'users.edit' : 'users.new') | transloco }}</h2>
    <mat-dialog-content>
      <form [formGroup]="form" (ngSubmit)="save()" class="form-grid two-columns" id="user-form">
        <mat-form-field>
          <mat-label>{{ 'users.firstName' | transloco }}</mat-label>
          <input matInput id="firstName" formControlName="firstName" cdkFocusInitial />
          <mat-error><app-field-error [control]="form.controls.firstName" /></mat-error>
        </mat-form-field>
        <mat-form-field>
          <mat-label>{{ 'users.lastName' | transloco }}</mat-label>
          <input matInput id="lastName" formControlName="lastName" />
          <mat-error><app-field-error [control]="form.controls.lastName" /></mat-error>
        </mat-form-field>
        <mat-form-field class="span-2">
          <mat-label>{{ 'users.email' | transloco }}</mat-label>
          <input matInput id="email" type="email" formControlName="email" />
          <mat-error><app-field-error [control]="form.controls.email" /></mat-error>
        </mat-form-field>
        @if (!user) {
          <mat-form-field class="span-2">
            <mat-label>{{ 'users.password' | transloco }}</mat-label>
            <input matInput id="password" type="password" formControlName="password" autocomplete="new-password" />
            <mat-hint>{{ 'common.minLength' | transloco: { n: minLength } }}</mat-hint>
            <mat-error><app-field-error [control]="form.controls.password" /></mat-error>
          </mat-form-field>
          <mat-form-field>
            <mat-label>{{ 'users.roles' | transloco }}</mat-label>
            <mat-select id="roleIds" formControlName="roleIds" multiple>
              @for (role of roles(); track role.id) {
                <mat-option [value]="role.id">{{ role.name }}</mat-option>
              }
            </mat-select>
          </mat-form-field>
        }
        <mat-form-field>
          <mat-label>{{ 'users.language' | transloco }}</mat-label>
          <mat-select formControlName="preferredLanguage">
            @for (lang of languages; track lang) {
              <mat-option [value]="lang">{{ 'languages.' + lang | transloco }}</mat-option>
            }
          </mat-select>
        </mat-form-field>
        @if (user && !isSelf) {
          <mat-slide-toggle class="span-2" formControlName="isActive">{{ 'common.active' | transloco }}</mat-slide-toggle>
        }
      </form>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button matButton mat-dialog-close type="button">{{ 'common.cancel' | transloco }}</button>
      <button matButton="filled" type="submit" form="user-form" [disabled]="saving()">{{ 'common.save' | transloco }}</button>
    </mat-dialog-actions>
  `,
})
export class UserFormDialog {
  private readonly api = inject(UsersApi);
  private readonly notify = inject(NotifyService);
  private readonly dialogRef = inject(MatDialogRef<UserFormDialog, UserDto>);
  readonly user = inject<UserDto | null>(MAT_DIALOG_DATA);
  readonly isSelf = this.user?.id === inject(AuthStore).user()?.id;

  readonly languages = [...LANGUAGES];
  readonly minLength = MIN_PASSWORD_LENGTH;
  readonly saving = signal(false);

  /** Only needed when creating; empty if the caller may not read roles. */
  readonly roles = toSignal(this.user ? of([]) : inject(RolesApi).list().pipe(catchError(() => of([]))), { initialValue: [] });

  readonly form = inject(FormBuilder).nonNullable.group({
    firstName: [this.user?.firstName ?? '', [Validators.required, Validators.maxLength(100)]],
    lastName: [this.user?.lastName ?? '', [Validators.required, Validators.maxLength(100)]],
    email: [this.user?.email ?? '', [Validators.required, Validators.email, Validators.maxLength(150)]],
    password: ['', this.user ? [] : [Validators.required, Validators.minLength(MIN_PASSWORD_LENGTH)]],
    roleIds: [[] as number[]],
    preferredLanguage: [this.user?.preferredLanguage ?? DEFAULT_LANGUAGE],
    isActive: [this.user?.isActive ?? true],
  });

  static open(dialog: MatDialog, user: UserDto | null = null): Observable<UserDto | undefined> {
    return dialog.open<UserFormDialog, UserDto | null, UserDto>(UserFormDialog, { data: user, width: 'min(40rem, 95vw)' }).afterClosed();
  }

  save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const { firstName, lastName, email, password, roleIds, preferredLanguage, isActive } = this.form.getRawValue();

    const request: Observable<UserDto> = this.user
      ? this.api.update(this.user.id, { firstName, lastName, email, preferredLanguage, ...(this.isSelf ? {} : { isActive }) })
      : this.api.create({ firstName, lastName, email, password, preferredLanguage, roleIds });

    this.saving.set(true);
    request.subscribe({
      next: (saved) => {
        this.notify.success(this.user ? 'common.saved' : 'common.created');
        this.dialogRef.close(saved);
      },
      error: (error) => {
        this.saving.set(false);
        if (!this.notify.applyToForm(this.form, error)) this.notify.error(error);
      },
    });
  }
}
