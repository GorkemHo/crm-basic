import { Component } from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';

import { PageHeader } from '../../shared/page-header';
import { CustomerTable } from './customer-table';

@Component({
  selector: 'app-customer-list',
  imports: [TranslocoPipe, PageHeader, CustomerTable],
  template: `
    <app-page-header [title]="'customers.title' | transloco" [subtitle]="'customers.subtitle' | transloco" />
    <div class="card">
      <app-customer-table />
    </div>
  `,
})
export class CustomerList {}
