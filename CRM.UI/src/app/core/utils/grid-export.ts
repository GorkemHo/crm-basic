import { ExportingEvent } from 'devextreme/ui/data_grid';
import { exportDataGrid } from 'devextreme/excel_exporter';
import { Workbook } from 'exceljs';
import saveAs from 'file-saver';

/** Exports a DevExtreme data grid to an .xlsx file named `<baseName>_<yyyy-mm-dd>.xlsx`. */
export function exportGridToExcel(event: ExportingEvent, baseName: string): void {
  const workbook = new Workbook();
  const worksheet = workbook.addWorksheet(baseName);

  event.cancel = true;

  exportDataGrid({
    component: event.component,
    worksheet,
    autoFilterEnabled: true,
    keepColumnWidths: true,
  })
    .then(() => workbook.xlsx.writeBuffer())
    .then((buffer) => {
      const date = new Date().toISOString().slice(0, 10);
      saveAs(new Blob([buffer], { type: 'application/octet-stream' }), `${baseName}_${date}.xlsx`);
    });
}
