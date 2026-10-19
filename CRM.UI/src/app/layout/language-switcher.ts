import { Component, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatMenuModule } from '@angular/material/menu';
import { TranslocoPipe } from '@jsverse/transloco';
import { switchMap } from 'rxjs';

import { AuthService } from '../core/auth/auth.service';
import { AuthStore } from '../core/auth/auth.store';
import { LANGUAGES, LanguageService } from '../core/i18n/language.service';
import { NotifyService } from '../core/ui/notify.service';

/** tr/en switch. When signed in the choice is saved as the user's preference (and lands in the next token). */
@Component({
  selector: 'app-language-switcher',
  imports: [MatButtonModule, MatMenuModule, TranslocoPipe],
  template: `
    <button matButton [matMenuTriggerFor]="menu" [attr.aria-label]="'nav.language' | transloco" class="trigger">
      <span class="material-symbols-outlined">language</span>
      {{ language.current().toUpperCase() }}
    </button>
    <mat-menu #menu="matMenu">
      @for (lang of languages; track lang) {
        <button mat-menu-item (click)="change(lang)" [class.selected]="lang === language.current()">
          {{ 'languages.' + lang | transloco }}
        </button>
      }
    </mat-menu>
  `,
  styles: `
    .trigger .material-symbols-outlined {
      margin-right: 0.25rem;
    }
    .selected {
      font-weight: 600;
    }
  `,
})
export class LanguageSwitcher {
  readonly language = inject(LanguageService);
  private readonly store = inject(AuthStore);
  private readonly auth = inject(AuthService);
  private readonly notify = inject(NotifyService);

  readonly languages = [...LANGUAGES];

  change(lang: string): void {
    if (lang === this.language.current()) return;

    this.language.use(lang);

    if (this.store.isAuthenticated()) {
      this.auth
        .updatePreferences(lang)
        // A fresh token carries the new `locale` claim, so the choice survives a reload.
        .pipe(switchMap(() => this.auth.refresh()))
        .subscribe({ error: (error) => this.notify.error(error) });
    }
  }
}
