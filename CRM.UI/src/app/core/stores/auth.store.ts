import { Injectable, computed, signal } from '@angular/core';

const ACCESS_TOKEN_KEY = 'accessToken';
const REFRESH_TOKEN_KEY = 'refreshToken';

// The API issues .NET ClaimTypes URIs; short JWT names are accepted too.
const ROLE_CLAIMS = ['role', 'http://schemas.microsoft.com/ws/2008/06/identity/claims/role'];
const NAME_CLAIMS = ['unique_name', 'name', 'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name'];
const EMAIL_CLAIMS = ['email', 'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress'];
const ID_CLAIMS = ['sub', 'nameid', 'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier'];

// Treat the token as expired slightly early so requests don't race the server clock.
const EXPIRY_SKEW_MS = 30_000;

type JwtPayload = Record<string, unknown> & { exp?: number };

export interface TokenUser {
  id: number | null;
  name: string;
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

function firstClaim(payload: JwtPayload | null, keys: string[]): unknown {
  if (!payload) return undefined;
  const key = keys.find((k) => payload[k] !== undefined);
  return key ? payload[key] : undefined;
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
    const value = firstClaim(this.claims(), ROLE_CLAIMS);
    if (Array.isArray(value)) return value.map(String);
    return typeof value === 'string' ? [value] : [];
  });

  readonly user = computed<TokenUser | null>(() => {
    const claims = this.claims();
    if (!claims) return null;

    const id = Number(firstClaim(claims, ID_CLAIMS));

    return {
      id: Number.isFinite(id) ? id : null,
      name: String(firstClaim(claims, NAME_CLAIMS) ?? ''),
      email: String(firstClaim(claims, EMAIL_CLAIMS) ?? ''),
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
