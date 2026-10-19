import { Signal, computed, inject } from '@angular/core';
import { TranslocoService } from '@jsverse/transloco';

import { LanguageService } from '../core/i18n/language.service';

/**
 * A computed value that is re-evaluated when the UI language changes, for translated data that
 * PrimeNG components take as inputs (option lists, menu items). Call in an injection context.
 */
export function translated<T>(build: (t: (key: string, params?: Record<string, unknown>) => string) => T): Signal<T> {
  const transloco = inject(TranslocoService);
  const language = inject(LanguageService);

  return computed(() => {
    language.current();
    return build((key, params) => transloco.translate(key, params));
  });
}

/** All / Active / Inactive options for an `isActive` filter. */
export function activeFilterOptions() {
  return translated((t) => [
    { label: t('common.all'), value: null },
    { label: t('common.active'), value: true },
    { label: t('common.inactive'), value: false },
  ]);
}
