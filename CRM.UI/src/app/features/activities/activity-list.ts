import { Component, signal } from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { TranslocoPipe } from '@jsverse/transloco';
import { debounceTime } from 'rxjs';

import { ACTIVITY_TYPES } from '../../core/api/models';
import { PageHeader } from '../../shared/page-header';
import { CompanySelect } from '../companies/company-select';
import { ActivityTimeline } from './activity-timeline';

/** All activities, newest first, filterable by type, company and text. */
@Component({
  selector: 'app-activity-list',
  imports: [FormsModule, TranslocoPipe, MatFormFieldModule, MatInputModule, MatSelectModule, PageHeader, CompanySelect, ActivityTimeline],
  template: `
    <app-page-header [title]="'activities.title' | transloco" [subtitle]="'activities.subtitle' | transloco" />

    <div class="card">
      <div class="table-toolbar">
        <mat-form-field class="grow">
          <mat-label>{{ 'common.search' | transloco }}</mat-label>
          <span matPrefix class="material-symbols-outlined prefix">search</span>
          <input matInput type="search" [placeholder]="'activities.searchPlaceholder' | transloco" [ngModel]="search()" (ngModelChange)="search.set($event)" />
        </mat-form-field>
        <app-company-select
          class="company-filter"
          [label]="'deals.company' | transloco"
          [clearable]="true"
          [ngModel]="companyId()"
          (ngModelChange)="companyId.set($event)"
        />
        <mat-form-field>
          <mat-label>{{ 'activities.type' | transloco }}</mat-label>
          <mat-select id="typeFilter" [ngModel]="type()" (ngModelChange)="type.set($event)">
            <mat-option [value]="null">{{ 'common.all' | transloco }}</mat-option>
            @for (t of types; track t) {
              <mat-option [value]="t">{{ 'activities.types.' + t | transloco }}</mat-option>
            }
          </mat-select>
        </mat-form-field>
      </div>

      <app-activity-timeline [companyId]="companyId()" [type]="type()" [search]="debouncedSearch()" />
    </div>
  `,
  styles: `
    .company-filter {
      width: 16rem;
    }
    .prefix {
      margin: 0 0.25rem 0 0.75rem;
      color: var(--mat-sys-on-surface-variant);
    }
  `,
})
export class ActivityList {
  readonly types = ACTIVITY_TYPES;
  readonly search = signal('');
  readonly companyId = signal<number | null>(null);
  readonly type = signal<string | null>(null);
  readonly debouncedSearch = toSignal(toObservable(this.search).pipe(debounceTime(300)), { initialValue: '' });
}
