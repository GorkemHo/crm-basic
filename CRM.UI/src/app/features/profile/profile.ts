import { Component, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormBuilder, FormGroupDirective, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatChipsModule } from '@angular/material/chips';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { TranslocoPipe } from '@jsverse/transloco';
import { switchMap } from 'rxjs';

import { AuthService } from '../../core/auth/auth.service';
import { AuthStore } from '../../core/auth/auth.store';
import { AppDatePipe } from '../../core/i18n/date.pipe';
import { LANGUAGES, LanguageService } from '../../core/i18n/language.service';
import { NotifyService } from '../../core/ui/notify.service';
import { ThemeService } from '../../core/ui/theme.service';
import { FieldError } from '../../shared/field-error';
import { PageHeader } from '../../shared/page-header';
import { MIN_PASSWORD_LENGTH, passwordsMatch } from '../auth/register';

@Component({
  selector: 'app-profile',
  imports: [
    ReactiveFormsModule, TranslocoPipe, MatButtonModule, MatButtonToggleModule, MatChipsModule, MatFormFieldModule, MatInputModule,
    MatProgressBarModule, PageHeader, FieldError, AppDatePipe,
  ],
  template: `
    <app-page-header [title]="'profile.title' | transloco" />

    @if (me(); as user) {
      <div class="grid">
        <section class="card account">
          <div class="identity">
            <span class="avatar">{{ user.firstName[0] }}{{ user.lastName[0] }}</span>
            <div>
              <h2>{{ user.firstName }} {{ user.lastName }}</h2>
              <span class="muted">{{ user.email }}</span>
            </div>
          </div>
          <dl class="details">
            <div><dt>{{ 'common.createdAt' | transloco }}</dt><dd>{{ user.createdAt | appDate }}</dd></div>
          </dl>
          <h3>{{ 'profile.roles' | transloco }}</h3>
          <mat-chip-set>
            @for (role of user.roles; track role) {
              <mat-chip>{{ role }}</mat-chip>
            }
          </mat-chip-set>
          <h3>{{ 'profile.permissions' | transloco }}</h3>
          <div class="permissions">
            @for (permission of user.permissions; track permission) {
              <code>{{ permission }}</code>
            }
          </div>
        </section>

        <div class="column">
          <section class="card">
            <h2>{{ 'profile.preferences' | transloco }}</h2>
            <div class="preference">
              <span>{{ 'profile.language' | transloco }}</span>
              <mat-button-toggle-group [value]="language.current()" (change)="changeLanguage($event.value)" [disabled]="savingLanguage()">
                @for (lang of languages; track lang) {
                  <mat-button-toggle [value]="lang">{{ 'languages.' + lang | transloco }}</mat-button-toggle>
                }
              </mat-button-toggle-group>
            </div>
            <div class="preference">
              <span>{{ 'profile.theme' | transloco }}</span>
              <mat-button-toggle-group [value]="theme.dark() ? 'dark' : 'light'" (change)="theme.dark.set($event.value === 'dark')">
                <mat-button-toggle value="light"><span class="material-symbols-outlined">light_mode</span> {{ 'profile.light' | transloco }}</mat-button-toggle>
                <mat-button-toggle value="dark"><span class="material-symbols-outlined">dark_mode</span> {{ 'profile.dark' | transloco }}</mat-button-toggle>
              </mat-button-toggle-group>
            </div>
          </section>

          <section class="card">
            <h2>{{ 'profile.changePassword' | transloco }}</h2>
            <form [formGroup]="passwordForm" (ngSubmit)="changePassword(formDirective)" #formDirective="ngForm" class="form-grid">
              <mat-form-field>
                <mat-label>{{ 'profile.currentPassword' | transloco }}</mat-label>
                <input matInput id="currentPassword" type="password" formControlName="currentPassword" autocomplete="current-password" />
                <mat-error><app-field-error [control]="passwordForm.controls.currentPassword" /></mat-error>
              </mat-form-field>
              <mat-form-field>
                <mat-label>{{ 'profile.newPassword' | transloco }}</mat-label>
                <input matInput id="newPassword" type="password" formControlName="password" autocomplete="new-password" />
                <mat-hint>{{ 'common.minLength' | transloco: { n: minLength } }}</mat-hint>
                <mat-error><app-field-error [control]="passwordForm.controls.password" /></mat-error>
              </mat-form-field>
              <mat-form-field>
                <mat-label>{{ 'auth.confirmPassword' | transloco }}</mat-label>
                <input matInput id="confirmPassword" type="password" formControlName="confirmPassword" autocomplete="new-password" />
                <mat-error><app-field-error [control]="passwordForm.controls.confirmPassword" /></mat-error>
              </mat-form-field>
              <div>
                <button matButton="filled" type="submit" [disabled]="savingPassword()">{{ 'profile.changePassword' | transloco }}</button>
              </div>
            </form>
          </section>
        </div>
      </div>
    } @else {
      <mat-progress-bar mode="indeterminate" />
    }
  `,
  styles: `
    .grid {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(22rem, 1fr));
      gap: var(--app-gap);
      align-items: start;
    }
    .column {
      display: flex;
      flex-direction: column;
      gap: var(--app-gap);
    }
    .identity {
      display: flex;
      align-items: center;
      gap: 1rem;
      margin-bottom: 1.5rem;
    }
    .avatar {
      width: 4rem;
      height: 4rem;
      border-radius: 50%;
      display: inline-flex;
      align-items: center;
      justify-content: center;
      background: var(--mat-sys-primary);
      color: var(--mat-sys-on-primary);
      font: var(--mat-sys-title-large);
    }
    h2 {
      font: var(--mat-sys-title-large);
      margin-bottom: 0.25rem;
    }
    section > h2 {
      margin-bottom: 1rem;
    }
    h3 {
      font: var(--mat-sys-title-small);
      margin: 1.5rem 0 0.5rem;
    }
    .permissions {
      display: flex;
      flex-wrap: wrap;
      gap: 0.4rem;
    }
    .permissions code {
      padding: 0.15rem 0.5rem;
      border-radius: 0.5rem;
      background: var(--mat-sys-surface-container-high);
      font-size: 0.8rem;
    }
    .preference {
      display: flex;
      flex-wrap: wrap;
      justify-content: space-between;
      align-items: center;
      gap: 0.75rem;
      padding: 0.5rem 0;
    }
    mat-button-toggle .material-symbols-outlined {
      font-size: 1.1rem;
      margin-right: 0.25rem;
    }
  `,
})
export class Profile {
  private readonly auth = inject(AuthService);
  private readonly store = inject(AuthStore);
  private readonly notify = inject(NotifyService);
  readonly language = inject(LanguageService);
  readonly theme = inject(ThemeService);

  readonly languages = [...LANGUAGES];
  readonly minLength = MIN_PASSWORD_LENGTH;

  /** Fresh from the API (permissions reflect role changes made since sign-in). */
  readonly me = toSignal(this.auth.me());

  readonly savingLanguage = signal(false);
  readonly savingPassword = signal(false);

  readonly passwordForm = inject(FormBuilder).nonNullable.group(
    {
      currentPassword: ['', Validators.required],
      password: ['', [Validators.required, Validators.minLength(MIN_PASSWORD_LENGTH)]],
      confirmPassword: ['', Validators.required],
    },
    { validators: passwordsMatch },
  );

  private readonly email = computed(() => this.store.user()?.email ?? '');

  changeLanguage(lang: string): void {
    this.language.use(lang);
    this.savingLanguage.set(true);

    this.auth
      .updatePreferences(lang)
      // A fresh token carries the new `locale` claim.
      .pipe(switchMap(() => this.auth.refresh()))
      .subscribe({
        next: () => {
          this.savingLanguage.set(false);
          this.notify.success('profile.languageSaved');
        },
        error: (error) => {
          this.savingLanguage.set(false);
          this.notify.error(error);
        },
      });
  }

  changePassword(formDirective: FormGroupDirective): void {
    if (this.passwordForm.invalid) {
      this.passwordForm.markAllAsTouched();
      return;
    }

    const { currentPassword, password } = this.passwordForm.getRawValue();
    this.savingPassword.set(true);

    this.auth
      .changePassword(currentPassword, password)
      // The API signs out every session, this one included: sign in again with the new password.
      .pipe(switchMap(() => this.auth.login(this.email(), password)))
      .subscribe({
        next: () => {
          this.savingPassword.set(false);
          formDirective.resetForm();
          this.notify.success('profile.passwordChanged');
        },
        error: (error) => {
          this.savingPassword.set(false);
          if (!this.notify.applyToForm(this.passwordForm, error)) this.notify.error(error);
        },
      });
  }
}
