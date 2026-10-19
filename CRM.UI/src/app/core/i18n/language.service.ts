import { DOCUMENT } from '@angular/common';
import { Injectable, inject, signal } from '@angular/core';
import { TranslocoService } from '@jsverse/transloco';
import { PrimeNG } from 'primeng/config';

export const LANGUAGES = ['tr', 'en'] as const;
export type Language = (typeof LANGUAGES)[number];
export const DEFAULT_LANGUAGE: Language = 'tr';

const STORAGE_KEY = 'crm.language';

/** PrimeNG's built-in component texts (paginator, confirm buttons, filters...). */
const PRIMENG_TEXTS: Record<Language, Record<string, unknown>> = {
  tr: {
    accept: 'Evet',
    reject: 'Hayır',
    cancel: 'Vazgeç',
    clear: 'Temizle',
    apply: 'Uygula',
    emptyMessage: 'Kayıt bulunamadı',
    emptyFilterMessage: 'Sonuç bulunamadı',
    searchMessage: '{0} sonuç bulundu',
    selectionMessage: '{0} öğe seçildi',
    dayNames: ['Pazar', 'Pazartesi', 'Salı', 'Çarşamba', 'Perşembe', 'Cuma', 'Cumartesi'],
    dayNamesShort: ['Paz', 'Pzt', 'Sal', 'Çar', 'Per', 'Cum', 'Cmt'],
    dayNamesMin: ['Pz', 'Pt', 'Sa', 'Ça', 'Pe', 'Cu', 'Ct'],
    monthNames: ['Ocak', 'Şubat', 'Mart', 'Nisan', 'Mayıs', 'Haziran', 'Temmuz', 'Ağustos', 'Eylül', 'Ekim', 'Kasım', 'Aralık'],
    monthNamesShort: ['Oca', 'Şub', 'Mar', 'Nis', 'May', 'Haz', 'Tem', 'Ağu', 'Eyl', 'Eki', 'Kas', 'Ara'],
    today: 'Bugün',
    weekHeader: 'Hf',
    firstDayOfWeek: 1,
    passwordPrompt: 'Bir şifre girin',
    weak: 'Zayıf',
    medium: 'Orta',
    strong: 'Güçlü',
  },
  en: {
    accept: 'Yes',
    reject: 'No',
    cancel: 'Cancel',
    clear: 'Clear',
    apply: 'Apply',
    emptyMessage: 'No records found',
    emptyFilterMessage: 'No results found',
    firstDayOfWeek: 1,
  },
};

const isLanguage = (value: string | null | undefined): value is Language =>
  !!value && (LANGUAGES as readonly string[]).includes(value);

/**
 * The active UI language: Transloco texts, PrimeNG texts, `<html lang>`, and the `Accept-Language`
 * header the API uses for validation messages. The signed-in user's preference wins; otherwise the
 * last choice on this device, otherwise Turkish.
 */
@Injectable({ providedIn: 'root' })
export class LanguageService {
  private readonly transloco = inject(TranslocoService);
  private readonly primeng = inject(PrimeNG);
  private readonly document = inject(DOCUMENT);

  readonly current = signal<Language>(DEFAULT_LANGUAGE);

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

    this.current.set(lang);
    this.transloco.setActiveLang(lang);
    this.primeng.setTranslation(PRIMENG_TEXTS[lang]);
    this.document.documentElement.lang = lang;

    try {
      localStorage.setItem(STORAGE_KEY, lang);
    } catch {
      // ignore
    }
  }
}
