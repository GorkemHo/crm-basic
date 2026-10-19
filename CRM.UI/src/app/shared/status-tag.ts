import { Component, input } from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';

@Component({
  selector: 'app-status-tag',
  imports: [TranslocoPipe],
  template: `
    <span class="tag" [class.active]="active()">{{ (active() ? 'common.active' : 'common.inactive') | transloco }}</span>
  `,
  styles: `
    .tag {
      display: inline-block;
      padding: 0.15rem 0.6rem;
      border-radius: 1rem;
      font: var(--mat-sys-label-medium);
      background: var(--mat-sys-surface-container-highest);
      color: var(--mat-sys-on-surface-variant);
    }
    .tag.active {
      background: var(--app-success-container);
      color: var(--app-success);
    }
  `,
})
export class StatusTag {
  readonly active = input.required<boolean>();
}
