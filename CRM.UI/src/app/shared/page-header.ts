import { Component, input } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { RouterLink } from '@angular/router';

/** Page title with optional subtitle, back link and right-aligned actions (projected content). */
@Component({
  selector: 'app-page-header',
  imports: [RouterLink, MatButtonModule],
  template: `
    <header class="page-header">
      <div class="title">
        @if (backLink(); as link) {
          <a matIconButton [routerLink]="link" aria-label="back"><span class="material-symbols-outlined">arrow_back</span></a>
        }
        <div>
          <h1>{{ title() }}</h1>
          @if (subtitle()) {
            <p class="muted">{{ subtitle() }}</p>
          }
        </div>
      </div>
      <div class="actions"><ng-content /></div>
    </header>
  `,
  styles: `
    .page-header {
      display: flex;
      flex-wrap: wrap;
      gap: 1rem;
      align-items: center;
      justify-content: space-between;
      margin-bottom: var(--app-gap);
    }
    .title {
      display: flex;
      align-items: center;
      gap: 0.5rem;
    }
    h1 {
      font: var(--mat-sys-headline-small);
    }
    p {
      margin: 0.25rem 0 0;
    }
    .actions {
      display: flex;
      gap: 0.5rem;
      flex-wrap: wrap;
    }
  `,
})
export class PageHeader {
  readonly title = input.required<string>();
  readonly subtitle = input<string>();
  readonly backLink = input<string | unknown[]>();
}
