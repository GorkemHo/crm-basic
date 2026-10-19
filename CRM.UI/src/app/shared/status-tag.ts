import { Component, input } from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';
import { TagModule } from 'primeng/tag';

@Component({
  selector: 'app-status-tag',
  imports: [TagModule, TranslocoPipe],
  template: `
    <p-tag
      [value]="(active() ? 'common.active' : 'common.inactive') | transloco"
      [severity]="active() ? 'success' : 'secondary'"
      [rounded]="true"
    />
  `,
})
export class StatusTag {
  readonly active = input.required<boolean>();
}
