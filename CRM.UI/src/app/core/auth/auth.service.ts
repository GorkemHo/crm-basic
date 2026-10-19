import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Router } from '@angular/router';
import { Observable, finalize, map, shareReplay, tap, throwError } from 'rxjs';

import { AuthResponse, CurrentUserDto, UserDto } from '../api/models';
import { AuthStore } from './auth.store';

const API = '/api/auth';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);
  private readonly store = inject(AuthStore);

  // Shared so that concurrent requests trigger a single refresh call.
  private refreshInFlight$: Observable<string> | null = null;

  login(email: string, password: string): Observable<CurrentUserDto> {
    return this.http.post<AuthResponse>(`${API}/login`, { email, password }).pipe(
      tap((response) => this.store.setTokens(response.accessToken, response.refreshToken)),
      map((response) => response.user),
    );
  }

  register(request: { firstName: string; lastName: string; email: string; password: string }): Observable<UserDto> {
    return this.http.post<UserDto>(`${API}/register`, request);
  }

  /** Exchanges the refresh token for a new pair; emits the new access token. */
  refresh(): Observable<string> {
    const refreshToken = this.store.refreshToken();
    if (!refreshToken) {
      return throwError(() => new Error('No refresh token'));
    }

    this.refreshInFlight$ ??= this.http.post<AuthResponse>(`${API}/refresh`, { refreshToken }).pipe(
      tap((response) => this.store.setTokens(response.accessToken, response.refreshToken)),
      map((response) => response.accessToken),
      finalize(() => (this.refreshInFlight$ = null)),
      shareReplay(1),
    );

    return this.refreshInFlight$;
  }

  me(): Observable<CurrentUserDto> {
    return this.http.get<CurrentUserDto>(`${API}/me`);
  }

  updatePreferences(preferredLanguage: string): Observable<UserDto> {
    return this.http.put<UserDto>(`${API}/me/preferences`, { preferredLanguage });
  }

  changePassword(currentPassword: string, newPassword: string): Observable<void> {
    return this.http.post<void>(`${API}/me/change-password`, { currentPassword, newPassword });
  }

  logout(): void {
    const refreshToken = this.store.refreshToken();
    if (refreshToken) {
      // Best effort: revoke server-side; local sign-out happens regardless.
      this.http.post(`${API}/logout`, { refreshToken }).subscribe({ error: () => undefined });
    }

    this.store.clear();
    void this.router.navigate(['/login']);
  }
}
