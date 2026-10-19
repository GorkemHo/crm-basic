import { Component, inject } from '@angular/core';
import { DxDataGridModule } from 'devextreme-angular/ui/data-grid';
import { CustomStore } from 'devextreme-angular/common/data';
import { ExportingEvent } from 'devextreme/ui/data_grid';
import { CompanyService } from '../../core/services/company.service';
import { Company } from '../../core/models/company/company.model';
import { request } from '../../core/utils/http-error';
import { exportGridToExcel } from '../../core/utils/grid-export';

@Component({
  templateUrl: 'companies.component.html',
  styleUrls: ['companies.component.scss'],
  standalone: true,
  imports: [DxDataGridModule],
})
export class CompaniesComponent {
  private companyService = inject(CompanyService);

  readonly dataSource = {
    store: new CustomStore<Company, number>({
      key: 'id',
      load: () => request(this.companyService.getAll()),
      insert: (values) => request(this.companyService.create(values)),
      update: (key, values) => request(this.companyService.patch(key, values)),
      remove: (key) => request(this.companyService.delete(key)),
    }),
  };

  exportGrid(event: ExportingEvent) {
    exportGridToExcel(event, 'Companies');
  }
}
