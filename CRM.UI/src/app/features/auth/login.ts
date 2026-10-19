import { Component, inject, input, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { Router, RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';

import { AuthService } from '../../core/auth/auth.service';
import { LanguageService } from '../../core/i18n/language.service';
import { NotifyService } from '../../core/ui/notify.service';
import { FieldError } from '../../shared/field-error';

@Component({
  selector: 'app-login',
  imports: [ReactiveFormsModule, RouterLink, TranslocoPipe, MatFormFieldModule, MatInputModule, MatButtonModule, MatProgressSpinnerModule, FieldError],
  template: `
    <h2>{{ 'auth.loginTitle' | transloco }}</h2>
    <p class="muted subtitle">{{ 'auth.loginSubtitle' | transloco }}</p>

    @if (error()) {
      <div class="alert" role="alert">{{ error() }}</div>
    }

    <form [formGroup]="form" (ngSubmit)="submit()" class="form-grid">
      <mat-form-field>
        <mat-label>{{ 'auth.email' | transloco }}</mat-label>
        <input matInput id="email" type="email" formControlName="email" autocomplete="username" />
        <mat-error><app-field-error [control]="form.controls.email" /></mat-error>
      </mat-form-field>

      <mat-form-field>
        <mat-label>{{ 'auth.password' | transloco }}</mat-label>
        <input matInput id="password" [type]="showPassword() ? 'text' : 'password'" formControlName="password" autocomplete="current-password" />
        <button matIconButton matSuffix type="button" (click)="showPassword.set(!showPassword())" [attr.aria-label]="'common.showPassword' | transloco">
          <span class="material-symbols-outlined">{{ showPassword() ? 'visibility_off' : 'visibility' }}</span>
        </button>
        <mat-error><app-field-error [control]="form.controls.password" /></mat-error>
      </mat-form-field>

      <button matButton="filled" type="submit" class="submit" [disabled]="loading()">
        @if (loading()) {
          <mat-spinner diameter="20" />
        } @else {
          {{ 'auth.signIn' | transloco }}
        }
      </button>
    </form>

    <p class="footer muted">
      {{ 'auth.noAccount' | transloco }} <a routerLink="/register">{{ 'auth.register' | transloco }}</a>
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
export class Login {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly notify = inject(NotifyService);
  private readonly language = inject(LanguageService);

  /** Where to go after signing in (set by the auth guard). */
  readonly returnUrl = input<string>();

  readonly loading = signal(false);
  readonly error = signal<string | null>(null);
  readonly showPassword = signal(false);

  readonly form = inject(FormBuilder).nonNullable.group({
    email: ['', [Validators.required, Validators.email]],
    password: ['', Validators.required],
  });

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.loading.set(true);
    this.error.set(null);

    const { email, password } = this.form.getRawValue();

    this.auth.login(email, password).subscribe({
      next: (user) => {
        this.language.use(user.preferredLanguage);
        void this.router.navigateByUrl(this.returnUrl() || '/');
      },
      error: (error) => {
        this.loading.set(false);
        this.error.set(this.notify.describe(error));
      },
    });
  }
}
