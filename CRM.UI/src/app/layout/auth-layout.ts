import { Component, inject } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { ButtonModule } from 'primeng/button';

import { ThemeService } from '../core/ui/theme.service';
import { LanguageSwitcher } from './language-switcher';

/** Centered card for sign-in / sign-up. */
@Component({
  selector: 'app-auth-layout',
  imports: [RouterOutlet, LanguageSwitcher, ButtonModule],
  template: `
    <div class="auth-page">
      <div class="corner">
        <app-language-switcher />
        <p-button
          [icon]="theme.dark() ? 'pi pi-sun' : 'pi pi-moon'"
          [text]="true"
          [rounded]="true"
          severity="secondary"
          (onClick)="theme.toggle()"
        />
      </div>
      <div class="auth-card">
        <div class="brand"><i class="pi pi-briefcase"></i> CRM</div>
        <router-outlet />
      </div>
    </div>
  `,
  styles: `
    .auth-page {
      min-height: 100vh;
      display: grid;
      place-items: center;
      padding: 1.5rem;
      background:
        radial-gradient(circle at 15% 20%, color-mix(in srgb, var(--p-primary-color) 18%, transparent), transparent 45%),
        radial-gradient(circle at 85% 80%, color-mix(in srgb, var(--p-primary-color) 12%, transparent), transparent 45%);
    }
    .corner {
      position: fixed;
      top: 1rem;
      right: 1rem;
      display: flex;
      gap: 0.5rem;
      align-items: center;
    }
    .auth-card {
      width: min(100%, 26rem);
      background: var(--p-content-background);
      border: 1px solid var(--p-content-border-color);
      border-radius: 1rem;
      padding: 2rem;
      box-shadow: 0 10px 40px rgb(0 0 0 / 0.08);
    }
    .brand {
      display: flex;
      align-items: center;
      gap: 0.5rem;
      font-size: 1.25rem;
      font-weight: 700;
      color: var(--p-primary-color);
      margin-bottom: 1.5rem;
    }
  `,
})
export class AuthLayout {
  readonly theme = inject(ThemeService);
}
