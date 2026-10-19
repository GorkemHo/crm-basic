import { Component, inject, signal } from '@angular/core';
import { AbstractControl, FormBuilder, ReactiveFormsModule, ValidationErrors, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { Router, RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';

import { AuthService } from '../../core/auth/auth.service';
import { NotifyService } from '../../core/ui/notify.service';
import { FieldError } from '../../shared/field-error';

export const MIN_PASSWORD_LENGTH = 8;

/** Group validator: `confirmPassword` must equal `password` (the error is put on the confirm field). */
export function passwordsMatch(group: AbstractControl): ValidationErrors | null {
  const confirm = group.get('confirmPassword');
  if (!confirm) return null;

  const mismatch = !!confirm.value && group.get('password')?.value !== confirm.value;
  if (mismatch) {
    confirm.setErrors({ ...confirm.errors, mismatch: true });
  } else if (confirm.hasError('mismatch')) {
    const { mismatch: _, ...rest } = confirm.errors ?? {};
    confirm.setErrors(Object.keys(rest).length ? rest : null);
  }

  return null;
}

@Component({
  selector: 'app-register',
  imports: [ReactiveFormsModule, RouterLink, TranslocoPipe, MatFormFieldModule, MatInputModule, MatButtonModule, MatProgressSpinnerModule, FieldError],
  template: `
    <h2>{{ 'auth.registerTitle' | transloco }}</h2>
    <p class="muted subtitle">{{ 'auth.registerSubtitle' | transloco }}</p>

    @if (error()) {
      <div class="alert" role="alert">{{ error() }}</div>
    }

    <form [formGroup]="form" (ngSubmit)="submit()" class="form-grid two-columns">
      <mat-form-field>
        <mat-label>{{ 'auth.firstName' | transloco }}</mat-label>
        <input matInput id="firstName" formControlName="firstName" autocomplete="given-name" />
        <mat-error><app-field-error [control]="form.controls.firstName" /></mat-error>
      </mat-form-field>
      <mat-form-field>
        <mat-label>{{ 'auth.lastName' | transloco }}</mat-label>
        <input matInput id="lastName" formControlName="lastName" autocomplete="family-name" />
        <mat-error><app-field-error [control]="form.controls.lastName" /></mat-error>
      </mat-form-field>
      <mat-form-field class="span-2">
        <mat-label>{{ 'auth.email' | transloco }}</mat-label>
        <input matInput id="email" type="email" formControlName="email" autocomplete="email" />
        <mat-error><app-field-error [control]="form.controls.email" /></mat-error>
      </mat-form-field>
      <mat-form-field class="span-2">
        <mat-label>{{ 'auth.password' | transloco }}</mat-label>
        <input matInput id="password" type="password" formControlName="password" autocomplete="new-password" />
        <mat-hint>{{ 'common.minLength' | transloco: { n: minLength } }}</mat-hint>
        <mat-error><app-field-error [control]="form.controls.password" /></mat-error>
      </mat-form-field>
      <mat-form-field class="span-2">
        <mat-label>{{ 'auth.confirmPassword' | transloco }}</mat-label>
        <input matInput id="confirmPassword" type="password" formControlName="confirmPassword" autocomplete="new-password" />
        <mat-error><app-field-error [control]="form.controls.confirmPassword" /></mat-error>
      </mat-form-field>

      <button matButton="filled" type="submit" class="submit span-2" [disabled]="loading()">
        @if (loading()) {
          <mat-spinner diameter="20" />
        } @else {
          {{ 'auth.register' | transloco }}
        }
      </button>
    </form>

    <p class="footer muted">
      {{ 'auth.haveAccount' | transloco }} <a routerLink="/login">{{ 'auth.signInLink' | transloco }}</a>
    </p>
  `,
  styles: `
    h2 {
      font: var(--mat-sys-headline-small);
    }
    .subtitle {
      margin: 0.25rem 0 1.5rem;
    }
    .submit {
      width: 100%;
      height: 3rem;
      margin-top: 0.5rem;
    }
    .footer {
      margin: 1.5rem 0 0;
      text-align: center;
    }
    .alert {
      padding: 0.75rem 1rem;
      margin-bottom: 1rem;
      border-radius: 0.75rem;
      background: var(--mat-sys-error-container);
      color: var(--mat-sys-on-error-container);
    }
  `,
})
export class Register {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly notify = inject(NotifyService);

  readonly minLength = MIN_PASSWORD_LENGTH;
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);

  readonly form = inject(FormBuilder).nonNullable.group(
    {
      firstName: ['', [Validators.required, Validators.maxLength(100)]],
      lastName: ['', [Validators.required, Validators.maxLength(100)]],
      email: ['', [Validators.required, Validators.email, Validators.maxLength(150)]],
      password: ['', [Validators.required, Validators.minLength(MIN_PASSWORD_LENGTH)]],
      confirmPassword: ['', Validators.required],
    },
    { validators: passwordsMatch },
  );

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.loading.set(true);
    this.error.set(null);

    const { confirmPassword: _, ...request } = this.form.getRawValue();

    this.auth.register(request).subscribe({
      next: () => {
        this.notify.success('auth.registered');
        void this.router.navigate(['/login']);
      },
      error: (error) => {
        this.loading.set(false);
        if (!this.notify.applyToForm(this.form, error)) {
          this.error.set(this.notify.describe(error));
        }
      },
    });
  }
}
