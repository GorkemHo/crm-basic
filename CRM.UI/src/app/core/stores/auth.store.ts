import { Injectable, computed, signal } from '@angular/core';

const ACCESS_TOKEN_KEY = 'accessToken';
const REFRESH_TOKEN_KEY = 'refreshToken';

// Claim names issued by the API (CRM.Core.Security.CrmClaimTypes).
const CLAIMS = {
  userId: 'sub',
  email: 'email',
  name: 'name',
  givenName: 'given_name',
  familyName: 'family_name',
  role: 'role',
} as const;

// Treat the token as expired slightly early so requests don't race the server clock.
const EXPIRY_SKEW_MS = 30_000;

type JwtPayload = Record<string, unknown> & { exp?: number };

export interface TokenUser {
  id: number | null;
  name: string;
  firstName: string;
  lastName: string;
  email: string;
}

function readStorage(key: string): string | null {
  try {
    return localStorage.getItem(key);
  } catch {
    return null;
  }
}

function decodeJwt(token: string | null): JwtPayload | null {
  if (!token) return null;

  try {
    const payload = token.split('.')[1];
    const base64 = payload.replace(/-/g, '+').replace(/_/g, '/');
    const json = decodeURIComponent(
      atob(base64)
        .split('')
        .map((c) => '%' + c.charCodeAt(0).toString(16).padStart(2, '0'))
        .join(''),
    );
    return JSON.parse(json) as JwtPayload;
  } catch {
    return null;
  }
}

function claimString(payload: JwtPayload, key: string): string {
  const value = payload[key];
  return typeof value === 'string' ? value : '';
}

@Injectable({
  providedIn: 'root',
})
export class AuthStore {
  readonly accessToken = signal<string | null>(readStorage(ACCESS_TOKEN_KEY));

  readonly refreshToken = signal<string | null>(readStorage(REFRESH_TOKEN_KEY));

  private readonly claims = computed(() => decodeJwt(this.accessToken()));

  /** Roles come from the access token, so they survive page reloads. */
  readonly roles = computed<string[]>(() => {
    // A single role is serialized as a string, several as an array.
    const value = this.claims()?.[CLAIMS.role];
    if (Array.isArray(value)) return value.map(String);
    return typeof value === 'string' ? [value] : [];
  });

  readonly user = computed<TokenUser | null>(() => {
    const claims = this.claims();
    if (!claims) return null;

    const id = Number(claims[CLAIMS.userId]);

    return {
      id: Number.isFinite(id) ? id : null,
      name: claimString(claims, CLAIMS.name),
      firstName: claimString(claims, CLAIMS.givenName),
      lastName: claimString(claims, CLAIMS.familyName),
      email: claimString(claims, CLAIMS.email),
    };
  });

  readonly accessTokenExpiresAt = computed<number | null>(() => {
    const exp = this.claims()?.exp;
    return typeof exp === 'number' ? exp * 1000 : null;
  });

  /** A session exists while we hold a token that is valid or can be refreshed. */
  readonly isAuthenticated = computed(() => !!this.accessToken() || !!this.refreshToken());

  hasRole(...roles: string[]): boolean {
    const userRoles = this.roles();
    return roles.some((role) => userRoles.includes(role));
  }

  isAccessTokenExpired(): boolean {
    const expiresAt = this.accessTokenExpiresAt();
    return !this.accessToken() || expiresAt === null || Date.now() >= expiresAt - EXPIRY_SKEW_MS;
  }

  setTokens(accessToken: string, refreshToken: string): void {
    try {
      localStorage.setItem(ACCESS_TOKEN_KEY, accessToken);
      localStorage.setItem(REFRESH_TOKEN_KEY, refreshToken);
    } catch {
      // Storage unavailable (private mode etc.): keep the session in memory only.
    }

    this.accessToken.set(accessToken);
    this.refreshToken.set(refreshToken);
  }

  clear(): void {
    try {
      localStorage.removeItem(ACCESS_TOKEN_KEY);
      localStorage.removeItem(REFRESH_TOKEN_KEY);
    } catch {
      // ignore
    }

    this.accessToken.set(null);
    this.refreshToken.set(null);
  }
}
