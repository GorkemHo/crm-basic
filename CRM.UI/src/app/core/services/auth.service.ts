import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { firstValueFrom, tap } from 'rxjs';
import { Router } from '@angular/router';
import { environment } from '../../../environments/environment';

import { LoginRequest } from '../models/auth/login-request.model';
import { LoginResponse } from '../models/auth/login-response.model';
import { AuthStore } from '../stores/auth.store';
import { RegisterUser } from '../models/user/register-user.model';
import { UserDto } from '../models/user/user.model';

@Injectable({
  providedIn: 'root',
})
export class AuthService {
  constructor(private router: Router) {}

  private http = inject(HttpClient);
  private authStore = inject(AuthStore);
  public _user: UserDto | null = null;

  private loggingOut = false;

  async login(request: LoginRequest) {
    try {
      const response = await firstValueFrom(
        this.http.post<LoginResponse>(
          `${environment.apiUrl}/auth/login`,
          request,
        ),
      );

      this.authStore.setTokens(response.accessToken, response.refreshToken);
      this.authStore.setRoles(response.roles)
      this._user = response.user;

      return {
        isOk: true,
        data: response,
      };
    } catch (error) {
      const httpError = error as HttpErrorResponse;

      return {
        isOk: false,
        message: httpError.error?.message ?? 'Giriş sırasında bir hata oluştu.',
      };
    }
  }

  logOut(): void {
    if (this.loggingOut) return;

    this.loggingOut = true;

    this.authStore.clear();
    this._user = null;
    this.router.navigate(['/login-form']).finally(() => {
      this.loggingOut = false;
    });
  }

  async createAccount(request: RegisterUser) {
    try {
      const response = await firstValueFrom(
        this.http.post<{ message: string }>(
          `${environment.apiUrl}/auth/register`,
          request,
        ),
      );

      return {
        isOk: true,
        message: response.message,
      };
    } catch (error) {
      const httpError = error as HttpErrorResponse;

      return {
        isOk: false,
        message: httpError.error?.message ?? 'Kayıt sırasında bir hata oluştu.',
      };
    }
  }
}
