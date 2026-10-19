import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, finalize, firstValueFrom, map, shareReplay, tap, throwError } from 'rxjs';
import { Router } from '@angular/router';
import { environment } from '../../../environments/environment';

import { LoginRequest } from '../models/auth/login-request.model';
import { LoginResponse } from '../models/auth/login-response.model';
import { AuthStore } from '../stores/auth.store';
import { RegisterUser } from '../models/user/register-user.model';
import { UserDto } from '../models/user/user.model';
import { getErrorMessage } from '../utils/http-error';

export interface AuthResult<T = unknown> {
  isOk: boolean;
  data?: T;
  message?: string;
}

@Injectable({
  providedIn: 'root',
})
export class AuthService {
  private http = inject(HttpClient);
  private router = inject(Router);
  private authStore = inject(AuthStore);

  private readonly apiUrl = `${environment.apiUrl}/auth`;

  private loggingOut = false;

  // Shared so that concurrent requests trigger a single refresh call.
  private refreshInFlight$: Observable<string> | null = null;

  async login(request: LoginRequest): Promise<AuthResult<LoginResponse>> {
    try {
      const response = await firstValueFrom(
        this.http.post<LoginResponse>(`${this.apiUrl}/login`, request),
      );

      this.authStore.setTokens(response.accessToken, response.refreshToken);

      return { isOk: true, data: response };
    } catch (error) {
      return { isOk: false, message: getErrorMessage(error, 'Giriş sırasında bir hata oluştu.') };
    }
  }

  /** Exchanges the stored refresh token for a new token pair and emits the new access token. */
  refreshAccessToken(): Observable<string> {
    const refreshToken = this.authStore.refreshToken();

    if (!refreshToken) {
      return throwError(() => new Error('No refresh token'));
    }

    if (!this.refreshInFlight$) {
      this.refreshInFlight$ = this.http
        .post<LoginResponse>(`${this.apiUrl}/refresh`, { refreshToken })
        .pipe(
          tap((response) => this.authStore.setTokens(response.accessToken, response.refreshToken)),
          map((response) => response.accessToken),
          finalize(() => (this.refreshInFlight$ = null)),
          shareReplay(1),
        );
    }

    return this.refreshInFlight$;
  }

  getCurrentUser(): Observable<UserDto> {
    return this.http.get<UserDto>(`${this.apiUrl}/me`);
  }

  logOut(): void {
    if (this.loggingOut) return;

    this.loggingOut = true;

    const refreshToken = this.authStore.refreshToken();

    if (refreshToken) {
      // Best effort: revoke the refresh token server-side; local logout proceeds regardless.
      this.http.post(`${this.apiUrl}/logout`, { refreshToken }).subscribe({ error: () => {} });
    }

    this.authStore.clear();
    this.router.navigate(['/login-form']).finally(() => {
      this.loggingOut = false;
    });
  }

  async createAccount(request: RegisterUser): Promise<AuthResult> {
    try {
      const response = await firstValueFrom(
        this.http.post<{ message: string }>(`${this.apiUrl}/register`, request),
      );

      return { isOk: true, message: response.message };
    } catch (error) {
      return { isOk: false, message: getErrorMessage(error, 'Kayıt sırasında bir hata oluştu.') };
    }
  }
}
