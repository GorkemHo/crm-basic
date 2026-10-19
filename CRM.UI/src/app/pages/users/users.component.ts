import { Component, inject } from '@angular/core';
import { DxDataGridModule } from 'devextreme-angular/ui/data-grid';
import { CustomStore } from 'devextreme-angular/common/data';
import { ExportingEvent } from 'devextreme/ui/data_grid';
import { UserService } from '../../core/services/user.service';
import { UserDto } from '../../core/models/user/user.model';
import { request } from '../../core/utils/http-error';
import { exportGridToExcel } from '../../core/utils/grid-export';

@Component({
  templateUrl: 'users.component.html',
  styleUrls: ['users.component.scss'],
  standalone: true,
  imports: [DxDataGridModule],
})
export class UsersComponent {
  private userService = inject(UserService);

  // The password can only be set when creating a user, never edited from the grid.
  isNewRow = false;

  readonly dataSource = {
    store: new CustomStore<UserDto & { password?: string }, number>({
      key: 'id',
      load: () => request(this.userService.getAll()),
      insert: (values) =>
        request(
          this.userService.create({
            firstName: values.firstName,
            lastName: values.lastName,
            email: values.email,
            password: values.password ?? '',
          }),
        ),
      update: (key, values) => request(this.userService.patch(key, values)),
      remove: (key) => request(this.userService.delete(key)),
    }),
  };

  onInitNewRow() {
    this.isNewRow = true;
  }

  onEditingStart() {
    this.isNewRow = false;
  }

  exportGrid(event: ExportingEvent) {
    exportGridToExcel(event, 'Users');
  }
}
