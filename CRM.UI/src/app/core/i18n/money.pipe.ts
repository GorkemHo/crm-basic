import { Pipe, PipeTransform, inject } from '@angular/core';

import { LanguageService } from './language.service';

/** Formats an amount in its currency for the active UI language: `{{ deal.amount | appMoney: deal.currency }}`. */
@Pipe({ name: 'appMoney', pure: false })
export class AppMoneyPipe implements PipeTransform {
  private readonly language = inject(LanguageService);

  transform(value: number | null | undefined, currency: string, compact = false): string {
    if (value === null || value === undefined) return '';
    return new Intl.NumberFormat(this.language.current(), {
      style: 'currency',
      currency,
      notation: compact ? 'compact' : 'standard',
      maximumFractionDigits: compact ? 1 : 2,
    }).format(value);
  }
}
