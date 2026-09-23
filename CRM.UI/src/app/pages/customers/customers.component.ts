import { Component, inject } from '@angular/core';
import { DxDataGridModule } from 'devextreme-angular/ui/data-grid';
import { CustomStore } from 'devextreme-angular/common/data';
import { environment } from '../../../environments/environment';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom, lastValueFrom } from 'rxjs';
import { ExportingEvent } from 'devextreme/ui/data_grid';
import { Workbook } from 'exceljs';
import saveAs from 'file-saver';
import { exportDataGrid } from 'devextreme/excel_exporter';
import { CustomerService } from '../../core/services/customer.service';
import { CompanyService } from '../../core/services/company.service';

@Component({
  templateUrl: 'customers.component.html',
  styleUrls: ['customers.component.scss'],
  standalone: true,
  imports: [DxDataGridModule],
})
export class CustomersComponent {
  dataSource: any;
  companies: any[] = [];

  private customerService = inject(CustomerService);
  private companyService = inject(CompanyService);

  constructor() {
    this.companyService.getAll().subscribe({
      next: (response) => {
        this.companies = response;
      },
      error: (err) => {
        console.error('Company loading error', err);
      },
    });

    this.dataSource = {
      store: new CustomStore({
        key: 'id',

        load: async () => {
          try {
            const response = await lastValueFrom(this.customerService.getAll());

            return {
              data: response,
            };
          } catch (error) {
            throw new Error('Customer Loading Error');
          }
        },

        insert: async (values) => {
          return await lastValueFrom(this.customerService.create(values));
        },

        update: async (key, values) => {
          return await lastValueFrom(this.customerService.patch(key, values));
        },

        remove: async (key) => {
          return await lastValueFrom(this.customerService.delete(key));
        },
      }),
    };
  }

  exportGrid($event: ExportingEvent) {
    const workbook = new Workbook();
    const worksheet = workbook.addWorksheet('Main Sheet');

    exportDataGrid({
      component: $event.component,
      worksheet: worksheet,
      autoFilterEnabled: true,
      keepColumnWidths: true,
    }).then(() => {
      workbook.xlsx.writeBuffer().then((buffer) => {
        saveAs(
          new Blob([buffer], { type: 'application/octet-stream' }),
          'DataGrid_Export_test.xlsx',
        );
      });
    });
    $event.cancel = true;
  }
}
