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
import { UserService } from '../../core/services/user.service';

@Component({
  templateUrl: 'users.component.html',
  styleUrls: ['users.component.scss'],
  standalone: true,
  imports: [DxDataGridModule],
})
export class UsersComponent {
  dataSource: any;
  users: any[] = [];

  private userService = inject(UserService);

  constructor() {
    this.userService.getAll().subscribe({
      next: (response) => {
        this.users = response;
      },
      error: (err) => {
        console.error('Users loading error', err);
      },
    });

    this.dataSource = {
      store: new CustomStore({
        key: 'id',

        load: async () => {
          try {
            const response = await lastValueFrom(this.userService.getAll());

            return {
              data: response,
            };
          } catch (error) {
            throw new Error('Users Loading Error');
          }
        },

        insert: async (values) => {
          return await lastValueFrom(this.userService.create(values));
        },

        update: async (key, values) => {
          return await lastValueFrom(this.userService.patch(key, values));
        },

        remove: async (key) => {
          return await lastValueFrom(this.userService.delete(key));
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
