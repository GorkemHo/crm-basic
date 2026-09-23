import { CommonModule } from '@angular/common';
import { Component, inject } from '@angular/core';
import { Router, RouterModule } from '@angular/router';
import { DxFormModule } from 'devextreme-angular/ui/form';
import { DxButtonModule } from 'devextreme-angular/ui/button';
import { DxLoadIndicatorModule } from 'devextreme-angular/ui/load-indicator';
import notify from 'devextreme/ui/notify';
import { AuthService } from '../../../core/services/auth.service';
import { ChangeDetectorRef } from '@angular/core';

@Component({
  selector: 'app-login-form',
  templateUrl: './login-form.component.html',
  styleUrls: ['./login-form.component.scss'],
  standalone: true,
  imports: [
    CommonModule,
    RouterModule,
    DxFormModule,
    DxButtonModule,
    DxLoadIndicatorModule,
  ],
})
export class LoginFormComponent {
  loading = false;
  formData: any = {};

  constructor(
    private authService: AuthService,
    private router: Router,
  ) {}

  private cdr = inject(ChangeDetectorRef);

  async onSubmit(e: Event) {
    e.preventDefault();

    const { email, password } = this.formData;

    this.loading = true;

    try {
      const result = await this.authService.login({ email, password });

      if (result.isOk) {
        this.router.navigate(['/home']);
        notify('Successfully signed in', 'success', 2000);
        return;
      }

      notify(result.message, 'error', 2000);
    } catch (error) {
      notify('Beklenmeyen bir hata oluştu.', 'error', 2000);
    } finally {
      this.loading = false;
      this.cdr.detectChanges();
    }
  }

  onCreateAccountClick = () => {
    this.router.navigate(['/create-account']);
  };
}
