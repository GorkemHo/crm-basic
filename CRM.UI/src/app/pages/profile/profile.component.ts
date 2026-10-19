import { Component, inject, signal } from '@angular/core';
import { DxFormModule } from 'devextreme-angular/ui/form';
import notify from 'devextreme/ui/notify';
import { AuthService } from '../../core/services/auth.service';
import { AuthStore } from '../../core/stores/auth.store';
import { UserDto } from '../../core/models/user/user.model';
import { getErrorMessage } from '../../core/utils/http-error';

@Component({
  templateUrl: 'profile.component.html',
  styleUrls: ['./profile.component.scss'],
  standalone: true,
  imports: [DxFormModule],
})
export class ProfileComponent {
  private authService = inject(AuthService);
  readonly authStore = inject(AuthStore);

  readonly user = signal<UserDto | null>(null);

  readonly colCountByScreen = {
    xs: 1,
    sm: 2,
    md: 3,
    lg: 4,
  };

  constructor() {
    this.authService.getCurrentUser().subscribe({
      next: (user) => this.user.set(user),
      error: (error) => notify(getErrorMessage(error), 'error', 3000),
    });
  }
}
