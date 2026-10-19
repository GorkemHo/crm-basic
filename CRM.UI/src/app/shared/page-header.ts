import { Component, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ButtonModule } from 'primeng/button';

/** Page title with optional subtitle, back link and right-aligned actions (projected content). */
@Component({
  selector: 'app-page-header',
  imports: [RouterLink, ButtonModule],
  template: `
    <header class="page-header">
      <div class="title">
        @if (backLink(); as link) {
          <p-button icon="pi pi-arrow-left" [text]="true" [rounded]="true" severity="secondary" [routerLink]="link" />
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
      font-size: 1.5rem;
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
