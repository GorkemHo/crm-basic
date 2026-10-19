import { Component, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { ButtonModule } from 'primeng/button';

/** 404 and 403 pages; `kind` comes from the route data. */
@Component({
  selector: 'app-error-page',
  imports: [RouterLink, TranslocoPipe, ButtonModule],
  template: `
    <div class="error-page">
      <div class="code">{{ kind() === 'forbidden' ? 403 : 404 }}</div>
      <h1>{{ 'pages.' + kind() + 'Title' | transloco }}</h1>
      <p class="muted">{{ 'pages.' + kind() + 'Text' | transloco }}</p>
      <p-button [label]="'pages.home' | transloco" icon="pi pi-home" routerLink="/" />
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
      font-size: 4rem;
      font-weight: 800;
      color: var(--p-primary-color);
      line-height: 1;
    }
  `,
})
export class ErrorPage {
  readonly kind = input<'notFound' | 'forbidden'>('notFound');
}
