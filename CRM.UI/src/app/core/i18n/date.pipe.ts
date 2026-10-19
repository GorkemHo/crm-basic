import { Pipe, PipeTransform, inject } from '@angular/core';

import { LanguageService } from './language.service';

const FORMATS = {
  date: { dateStyle: 'medium' },
  datetime: { dateStyle: 'medium', timeStyle: 'short' },
} satisfies Record<string, Intl.DateTimeFormatOptions>;

/** Formats an ISO date in the active UI language: `{{ value | appDate }}` or `{{ value | appDate: 'datetime' }}`. */
@Pipe({ name: 'appDate', pure: false })
export class AppDatePipe implements PipeTransform {
  private readonly language = inject(LanguageService);

  transform(value: string | Date | null | undefined, style: keyof typeof FORMATS = 'date'): string {
    if (!value) return '';
    return new Intl.DateTimeFormat(this.language.current(), FORMATS[style]).format(new Date(value));
  }
}
