import { Component, input } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';

/** 404 and 403 pages; `kind` comes from the route data. */
@Component({
  selector: 'app-error-page',
  imports: [RouterLink, TranslocoPipe, MatButtonModule],
  template: `
    <div class="error-page">
      <div class="code">{{ kind() === 'forbidden' ? 403 : 404 }}</div>
      <h1>{{ 'pages.' + kind() + 'Title' | transloco }}</h1>
      <p class="muted">{{ 'pages.' + kind() + 'Text' | transloco }}</p>
      <a matButton="filled" routerLink="/">{{ 'pages.home' | transloco }}</a>
    </div>
  `,
  styles: `
    .error-page {
      min-height: 60vh;
      display: flex;
      flex-direction: column;
      align-items: center;
      justify-content: center;
      text-align: center;
      gap: 0.75rem;
    }
    .code {
      font: var(--mat-sys-display-large);
      font-weight: 800;
      color: var(--mat-sys-primary);
    }
    h1 {
      font: var(--mat-sys-headline-small);
    }
  `,
})
export class ErrorPage {
  readonly kind = input<'notFound' | 'forbidden'>('notFound');
}
