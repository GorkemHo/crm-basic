import { Pipe, PipeTransform, inject } from '@angular/core';

import { LanguageService } from './language.service';

const UNITS: [Intl.RelativeTimeFormatUnit, number][] = [
  ['year', 365 * 24 * 3600],
  ['month', 30 * 24 * 3600],
  ['week', 7 * 24 * 3600],
  ['day', 24 * 3600],
  ['hour', 3600],
  ['minute', 60],
];

/** "5 minutes ago" / "5 dakika önce" in the active UI language: `{{ value | appTimeAgo }}`. */
@Pipe({ name: 'appTimeAgo', pure: false })
export class AppTimeAgoPipe implements PipeTransform {
  private readonly language = inject(LanguageService);

  transform(value: string | Date | null | undefined): string {
    if (!value) return '';

    const format = new Intl.RelativeTimeFormat(this.language.current(), { numeric: 'auto' });
    const seconds = Math.round((new Date(value).getTime() - Date.now()) / 1000);

    for (const [unit, size] of UNITS) {
      if (Math.abs(seconds) >= size) return format.format(Math.round(seconds / size), unit);
    }
    return format.format(0, 'minute');
  }
}
