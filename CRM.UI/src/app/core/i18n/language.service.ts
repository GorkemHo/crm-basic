import { DOCUMENT } from '@angular/common';
import { Injectable, inject, signal } from '@angular/core';
import { TranslocoService } from '@jsverse/transloco';

export const LANGUAGES = ['tr', 'en'] as const;
export type Language = (typeof LANGUAGES)[number];
export const DEFAULT_LANGUAGE: Language = 'tr';

const STORAGE_KEY = 'crm.language';

const isLanguage = (value: string | null | undefined): value is Language =>
  !!value && (LANGUAGES as readonly string[]).includes(value);

/**
 * The active UI language: Transloco texts, `<html lang>`, date formats and the `Accept-Language`
 * header the API uses for validation messages. The signed-in user's preference wins; otherwise the
 * last choice on this device, otherwise Turkish.
 */
@Injectable({ providedIn: 'root' })
export class LanguageService {
  private readonly transloco = inject(TranslocoService);
  private readonly document = inject(DOCUMENT);

  /** Language whose texts are loaded and shown. */
  readonly current = signal<Language>(DEFAULT_LANGUAGE);

  /** Last requested language (may still be loading). */
  requested: Language = DEFAULT_LANGUAGE;

  /** Called at startup with the language from the access token (if signed in). */
  init(userLanguage?: string | null): void {
    let stored: string | null = null;
    try {
      stored = localStorage.getItem(STORAGE_KEY);
    } catch {
      // ignore
    }

    this.use(isLanguage(userLanguage) ? userLanguage : isLanguage(stored) ? stored : DEFAULT_LANGUAGE);
  }

  use(language: string): void {
    const lang = isLanguage(language) ? language : DEFAULT_LANGUAGE;
    this.requested = lang;

    try {
      localStorage.setItem(STORAGE_KEY, lang);
    } catch {
      // ignore
    }

    // Switch only once the texts are loaded, so code reading `current()` never sees missing keys.
    this.transloco.load(lang).subscribe(() => {
      this.transloco.setActiveLang(lang);
      this.document.documentElement.lang = lang;
      this.current.set(lang);
    });
  }
}
