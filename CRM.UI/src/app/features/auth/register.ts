import { Component, inject, signal } from '@angular/core';
import { AbstractControl, FormBuilder, ReactiveFormsModule, ValidationErrors, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { MessageModule } from 'primeng/message';
import { PasswordModule } from 'primeng/password';

import { AuthService } from '../../core/auth/auth.service';
import { NotifyService } from '../../core/ui/notify.service';
import { FieldError } from '../../shared/field-error';

export const MIN_PASSWORD_LENGTH = 8;

/** Group validator: `confirmPassword` must equal `password` (error is put on the confirm field). */
export function passwordsMatch(group: AbstractControl): ValidationErrors | null {
  const confirm = group.get('confirmPassword');
  if (!confirm) return null;

  const mismatch = confirm.value && group.get('password')?.value !== confirm.value;
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
  imports: [ReactiveFormsModule, RouterLink, TranslocoPipe, ButtonModule, InputTextModule, PasswordModule, MessageModule, FieldError],
  template: `
    <h2>{{ 'auth.registerTitle' | transloco }}</h2>
    <p class="muted subtitle">{{ 'auth.registerSubtitle' | transloco }}</p>

    @if (error()) {
      <p-message severity="error" styleClass="mb">{{ error() }}</p-message>
    }

    <form [formGroup]="form" (ngSubmit)="submit()" class="form-grid two-columns">
      <div class="field">
        <label for="firstName">{{ 'auth.firstName' | transloco }}</label>
        <input pInputText id="firstName" formControlName="firstName" autocomplete="given-name" />
        <app-field-error [control]="form.controls.firstName" />
      </div>
      <div class="field">
        <label for="lastName">{{ 'auth.lastName' | transloco }}</label>
        <input pInputText id="lastName" formControlName="lastName" autocomplete="family-name" />
        <app-field-error [control]="form.controls.lastName" />
      </div>
      <div class="field span-2">
        <label for="email">{{ 'auth.email' | transloco }}</label>
        <input pInputText id="email" type="email" formControlName="email" autocomplete="email" />
        <app-field-error [control]="form.controls.email" />
      </div>
      <div class="field span-2">
        <label for="password">{{ 'auth.password' | transloco }}</label>
        <p-password inputId="password" formControlName="password" [toggleMask]="true" autocomplete="new-password" />
        <app-field-error [control]="form.controls.password" />
      </div>
      <div class="field span-2">
        <label for="confirmPassword">{{ 'auth.confirmPassword' | transloco }}</label>
        <p-password inputId="confirmPassword" formControlName="confirmPassword" [feedback]="false" [toggleMask]="true" autocomplete="new-password" />
        <app-field-error [control]="form.controls.confirmPassword" />
      </div>

      <div class="span-2">
        <p-button type="submit" [label]="'auth.register' | transloco" [loading]="loading()" styleClass="w-full" />
      </div>
    </form>

    <p class="footer muted">
      {{ 'auth.haveAccount' | transloco }} <a routerLink="/login">{{ 'auth.signInLink' | transloco }}</a>
    </p>
  `,
  styles: `
    .subtitle {
      margin: 0.25rem 0 1.5rem;
    }
    .footer {
      margin: 1.5rem 0 0;
      text-align: center;
    }
    .span-2 {
      grid-column: 1 / -1;
    }
    :host ::ng-deep .w-full,
    :host ::ng-deep .w-full .p-button {
      width: 100%;
    }
    :host ::ng-deep .mb {
      margin-bottom: 1rem;
    }
  `,
})
export class Register {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly notify = inject(NotifyService);

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
