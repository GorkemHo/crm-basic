import { DOCUMENT } from '@angular/common';
import { Injectable, effect, inject, signal } from '@angular/core';

const STORAGE_KEY = 'crm.theme';

/** Light/dark mode; PrimeNG switches via the `app-dark` class (see app.config.ts). */
@Injectable({ providedIn: 'root' })
export class ThemeService {
  private readonly document = inject(DOCUMENT);

  readonly dark = signal<boolean>(this.initial());

  constructor() {
    effect(() => {
      const dark = this.dark();
      this.document.documentElement.classList.toggle('app-dark', dark);
      try {
        localStorage.setItem(STORAGE_KEY, dark ? 'dark' : 'light');
      } catch {
        // ignore
      }
    });
  }

  toggle(): void {
    this.dark.update((dark) => !dark);
  }

  private initial(): boolean {
    try {
      const stored = localStorage.getItem(STORAGE_KEY);
      if (stored) return stored === 'dark';
    } catch {
      // ignore
    }
    return this.document.defaultView?.matchMedia?.('(prefers-color-scheme: dark)').matches ?? false;
  }
}
