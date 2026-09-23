import { Component, NgModule, OnInit } from '@angular/core';
import { count, forkJoin } from 'rxjs';

import { CompanyService } from '../../core/services/company.service';
import { CustomerService } from '../../core/services/customer.service';
import { UserService } from '../../core/services/user.service';

import { ChangeDetectorRef } from '@angular/core';
import { DxPieChartModule } from 'devextreme-angular';

import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';

@Component({
  templateUrl: 'home.component.html',
  styleUrls: ['./home.component.scss'],
  standalone: true,
  imports: [DxPieChartModule,TranslocoPipe],
})
export class HomeComponent implements OnInit {
  companyCount = 0;
  customerCount = 0;
  userCount = 0;

  loading = true;

  chartData = [
    {
      count: 'Users',
      area: 0,
    },
    {
      count: 'Companies',
      area: 0,
    },
    {
      count: 'Customers',
      area: 0,
    },
  ];

  constructor(
    private companyService: CompanyService,
    private customerService: CustomerService,
    private userService: UserService,
    private cdr: ChangeDetectorRef,
    private translocoService: TranslocoService,
  ) {}

  ngOnInit(): void {
    forkJoin({
      companies: this.companyService.getAll(),
      customers: this.customerService.getAll(),
      users: this.userService.getAll(),
    }).subscribe({
      next: (response) => {
        this.companyCount = response.companies.length;
        this.customerCount = response.customers.length;
        this.userCount = response.users.length;

        this.chartData = [
          {
            count: 'Users',
            area: this.userCount,
          },
          {
            count: 'Companies',
            area: this.companyCount,
          },
          {
            count: 'Customers',
            area: this.customerCount,
          },
        ];

        this.loading = false;
        this.cdr.detectChanges();
      },

      error: (err) => {
        console.error(err);
        this.loading = false;
      },
    });
  }

   pointClickHandler(e:any) {
    this.toggleVisibility(e.target);
  }

  legendClickHandler(e:any) {
    const arg = e.target;
    const item = e.component.getAllSeries()[0].getPointsByArg(arg)[0];

    this.toggleVisibility(item);
  }

   toggleVisibility(item:any) {
    if (item.isVisible()) {
      item.hide();
    } else {
      item.show();
    }
  }
}
