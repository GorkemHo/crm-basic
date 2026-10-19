import { Component, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { RouterOutlet } from '@angular/router';

import { ThemeService } from '../core/ui/theme.service';
import { LanguageSwitcher } from './language-switcher';

/** Centered card for sign-in / sign-up. */
@Component({
  selector: 'app-auth-layout',
  imports: [RouterOutlet, LanguageSwitcher, MatButtonModule],
  template: `
    <div class="auth-page">
      <div class="corner">
        <app-language-switcher />
        <button matIconButton (click)="theme.toggle()" aria-label="theme">
          <span class="material-symbols-outlined">{{ theme.dark() ? 'light_mode' : 'dark_mode' }}</span>
        </button>
      </div>
      <div class="auth-card">
        <div class="brand"><span class="material-symbols-outlined">hub</span> CRM</div>
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
        radial-gradient(circle at 15% 20%, color-mix(in srgb, var(--mat-sys-primary) 16%, transparent), transparent 45%),
        radial-gradient(circle at 85% 80%, color-mix(in srgb, var(--mat-sys-tertiary) 14%, transparent), transparent 45%),
        var(--mat-sys-surface-container-low);
    }
    .corner {
      position: fixed;
      top: 1rem;
      right: 1rem;
      display: flex;
      gap: 0.25rem;
      align-items: center;
    }
    .auth-card {
      width: min(100%, 27rem);
      background: var(--mat-sys-surface);
      border: 1px solid var(--mat-sys-outline-variant);
      border-radius: 1.5rem;
      padding: 2rem;
      box-shadow: var(--mat-sys-level2);
    }
    .brand {
      display: flex;
      align-items: center;
      gap: 0.5rem;
      font: var(--mat-sys-title-large);
      font-weight: 700;
      color: var(--mat-sys-primary);
      margin-bottom: 1.5rem;
    }
    .brand .material-symbols-outlined {
      font-size: 1.75rem;
    }
  `,
})
export class AuthLayout {
  readonly theme = inject(ThemeService);
}
