import { Component, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TranslocoPipe } from '@jsverse/transloco';
import { SelectModule } from 'primeng/select';
import { switchMap } from 'rxjs';

import { AuthService } from '../core/auth/auth.service';
import { AuthStore } from '../core/auth/auth.store';
import { LANGUAGES, LanguageService } from '../core/i18n/language.service';
import { NotifyService } from '../core/ui/notify.service';

/** tr/en switch. When signed in the choice is saved as the user's preference (and lands in the next token). */
@Component({
  selector: 'app-language-switcher',
  imports: [SelectModule, FormsModule, TranslocoPipe],
  template: `
    <p-select
      [options]="languages"
      [ngModel]="language.current()"
      (ngModelChange)="change($event)"
      [ariaLabel]="'nav.language' | transloco"
      size="small"
    >
      <ng-template #selectedItem let-lang>
        <span class="lang"><i class="pi pi-globe"></i> {{ lang.toUpperCase() }}</span>
      </ng-template>
      <ng-template #item let-lang>{{ 'languages.' + lang | transloco }}</ng-template>
    </p-select>
  `,
  styles: `
    .lang {
      display: inline-flex;
      gap: 0.4rem;
      align-items: center;
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
