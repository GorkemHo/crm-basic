import { Injectable, computed, signal } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class AuthStore {
  readonly accessToken = signal<string | null>(
    localStorage.getItem('accessToken'),
  );

  readonly refreshToken = signal<string | null>(
    localStorage.getItem('refreshToken'),
  );

  readonly isAuthenticated = computed(() => !!this.accessToken());

  readonly roles = signal<string[]>([]);

  setRoles(roles: string[]): void {
    this.roles.set(roles);
  }

  setTokens(accessToken: string, refreshToken: string): void {
    localStorage.setItem('accessToken', accessToken);

    localStorage.setItem('refreshToken', refreshToken);

    this.accessToken.set(accessToken);
    this.refreshToken.set(refreshToken);
  }

  clear(): void {
    localStorage.removeItem('accessToken');
    localStorage.removeItem('refreshToken');

    this.accessToken.set(null);
    this.refreshToken.set(null);
    this.roles.set([]);
  }
}
