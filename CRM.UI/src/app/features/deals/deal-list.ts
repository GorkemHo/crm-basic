import { Component } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';

import { PageHeader } from '../../shared/page-header';
import { DealTable } from './deal-table';

@Component({
  selector: 'app-deal-list',
  imports: [TranslocoPipe, RouterLink, MatButtonModule, PageHeader, DealTable],
  template: `
    <app-page-header [title]="'deals.title' | transloco" [subtitle]="'deals.subtitle' | transloco">
      <a matButton="outlined" routerLink="/pipeline"><span class="material-symbols-outlined">view_kanban</span> {{ 'pipeline.boardView' | transloco }}</a>
    </app-page-header>
    <div class="card"><app-deal-table /></div>
  `,
  styles: `
    a .material-symbols-outlined {
      margin-right: 0.25rem;
    }
  `,
})
export class DealList {}
