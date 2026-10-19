import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Observable, finalize, firstValueFrom, map, shareReplay, tap, throwError } from 'rxjs';
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

  // Shared so that concurrent 401s trigger a single refresh request.
  private refreshInFlight$: Observable<string> | null = null;

  async login(request: LoginRequest) {
    try {
      const response = await firstValueFrom(
        this.http.post<LoginResponse>(
          `${environment.apiUrl}/auth/login`,
          request,
        ),
      );

      this.applyLoginResponse(response);

      return {
        isOk: true,
        data: response,
      };
    } catch (error) {
      const httpError = error as HttpErrorResponse;

      return {
        isOk: false,
        message: httpError.error?.Message ?? httpError.error?.message ?? 'Giriş sırasında bir hata oluştu.',
      };
    }
  }

  /**
   * Exchanges the stored refresh token for a new token pair and emits the new access token.
   */
  refreshAccessToken(): Observable<string> {
    const refreshToken = this.authStore.refreshToken();

    if (!refreshToken) {
      return throwError(() => new Error('No refresh token'));
    }

    if (!this.refreshInFlight$) {
      this.refreshInFlight$ = this.http
        .post<LoginResponse>(`${environment.apiUrl}/auth/refresh`, { refreshToken })
        .pipe(
          tap((response) => this.applyLoginResponse(response)),
          map((response) => response.accessToken),
          finalize(() => (this.refreshInFlight$ = null)),
          shareReplay(1),
        );
    }

    return this.refreshInFlight$;
  }

  logOut(): void {
    if (this.loggingOut) return;

    this.loggingOut = true;

    const refreshToken = this.authStore.refreshToken();

    if (refreshToken) {
      // Best effort: revoke the refresh token server-side; local logout proceeds regardless.
      this.http
        .post(`${environment.apiUrl}/auth/logout`, { refreshToken })
        .subscribe({ error: () => {} });
    }

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
        message: httpError.error?.Message ?? httpError.error?.message ?? 'Kayıt sırasında bir hata oluştu.',
      };
    }
  }

  private applyLoginResponse(response: LoginResponse): void {
    this.authStore.setTokens(response.accessToken, response.refreshToken);
    this.authStore.setRoles(response.roles);
    this._user = response.user;
  }
}
