import { Component, inject } from '@angular/core';
import { DxDataGridModule } from 'devextreme-angular/ui/data-grid';
import { CustomStore } from 'devextreme-angular/common/data';
import { ExportingEvent } from 'devextreme/ui/data_grid';
import { CustomerService } from '../../core/services/customer.service';
import { CompanyService } from '../../core/services/company.service';
import { Customer } from '../../core/models/customer/customer.model';
import { request } from '../../core/utils/http-error';
import { exportGridToExcel } from '../../core/utils/grid-export';

@Component({
  templateUrl: 'customers.component.html',
  styleUrls: ['customers.component.scss'],
  standalone: true,
  imports: [DxDataGridModule],
})
export class CustomersComponent {
  private customerService = inject(CustomerService);
  private companyService = inject(CompanyService);

  readonly dataSource = {
    store: new CustomStore<Customer, number>({
      key: 'id',
      load: () => request(this.customerService.getAll()),
      insert: (values) => request(this.customerService.create(values)),
      update: (key, values) => request(this.customerService.patch(key, values)),
      remove: (key) => request(this.customerService.delete(key)),
    }),
  };

  // Lookup source for the Company column; loaded lazily by the grid.
  readonly companies = {
    store: new CustomStore({
      key: 'id',
      loadMode: 'raw',
      load: () => request(this.companyService.getAll()),
    }),
  };

  exportGrid(event: ExportingEvent) {
    exportGridToExcel(event, 'Customers');
  }
}
