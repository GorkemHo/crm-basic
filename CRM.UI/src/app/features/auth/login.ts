import { Component, inject, input, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { MessageModule } from 'primeng/message';
import { PasswordModule } from 'primeng/password';

import { AuthService } from '../../core/auth/auth.service';
import { LanguageService } from '../../core/i18n/language.service';
import { NotifyService } from '../../core/ui/notify.service';
import { FieldError } from '../../shared/field-error';

@Component({
  selector: 'app-login',
  imports: [ReactiveFormsModule, RouterLink, TranslocoPipe, ButtonModule, InputTextModule, PasswordModule, MessageModule, FieldError],
  template: `
    <h2>{{ 'auth.loginTitle' | transloco }}</h2>
    <p class="muted subtitle">{{ 'auth.loginSubtitle' | transloco }}</p>

    @if (error()) {
      <p-message severity="error" styleClass="mb">{{ error() }}</p-message>
    }

    <form [formGroup]="form" (ngSubmit)="submit()" class="form-grid">
      <div class="field">
        <label for="email">{{ 'auth.email' | transloco }}</label>
        <input pInputText id="email" type="email" formControlName="email" autocomplete="username" autofocus />
        <app-field-error [control]="form.controls.email" />
      </div>

      <div class="field">
        <label for="password">{{ 'auth.password' | transloco }}</label>
        <p-password inputId="password" formControlName="password" [feedback]="false" [toggleMask]="true" autocomplete="current-password" />
        <app-field-error [control]="form.controls.password" />
      </div>

      <p-button type="submit" [label]="'auth.signIn' | transloco" [loading]="loading()" styleClass="w-full" />
    </form>

    <p class="footer muted">
      {{ 'auth.noAccount' | transloco }} <a routerLink="/register">{{ 'auth.register' | transloco }}</a>
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
    :host ::ng-deep .w-full,
    :host ::ng-deep .w-full .p-button {
      width: 100%;
    }
    :host ::ng-deep .mb {
      margin-bottom: 1rem;
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
