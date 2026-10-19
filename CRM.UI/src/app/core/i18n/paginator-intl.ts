import { Injectable, effect, inject } from '@angular/core';
import { MatPaginatorIntl } from '@angular/material/paginator';
import { TranslocoService } from '@jsverse/transloco';

import { LanguageService } from './language.service';

/** Paginator labels in the active UI language. */
@Injectable()
export class TranslatedPaginatorIntl extends MatPaginatorIntl {
  private readonly transloco = inject(TranslocoService);

  constructor() {
    super();

    const language = inject(LanguageService);
    effect(() => {
      language.current();
      const t = (key: string) => this.transloco.translate(`paginator.${key}`);

      this.itemsPerPageLabel = t('itemsPerPage');
      this.nextPageLabel = t('next');
      this.previousPageLabel = t('previous');
      this.firstPageLabel = t('first');
      this.lastPageLabel = t('last');
      this.changes.next();
    });
  }

  override getRangeLabel = (page: number, pageSize: number, length: number): string => {
    if (length === 0) {
      return this.transloco.translate('paginator.empty');
    }

    const start = page * pageSize + 1;
    const end = Math.min((page + 1) * pageSize, length);
    return this.transloco.translate('paginator.range', { start, end, length });
  };
}
