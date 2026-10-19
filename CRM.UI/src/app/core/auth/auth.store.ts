import { Injectable, computed, signal } from '@angular/core';

const ACCESS_TOKEN_KEY = 'crm.accessToken';
const REFRESH_TOKEN_KEY = 'crm.refreshToken';

// Treat the token as expired slightly early so requests don't race the server clock.
const EXPIRY_SKEW_MS = 30_000;

/** Claims issued by the API (CRM.BuildingBlocks.Security.CrmClaimTypes). */
interface AccessTokenClaims {
  sub?: string;
  email?: string;
  name?: string;
  given_name?: string;
  family_name?: string;
  locale?: string;
  role?: string | string[];
  perm?: string | string[];
  exp?: number;
}

export interface SignedInUser {
  id: number;
  email: string;
  name: string;
  firstName: string;
  lastName: string;
  locale: string;
}

function readStorage(key: string): string | null {
  try {
    return localStorage.getItem(key);
  } catch {
    return null;
  }
}

function writeStorage(key: string, value: string | null): void {
  try {
    if (value === null) localStorage.removeItem(key);
    else localStorage.setItem(key, value);
  } catch {
    // Storage unavailable (private mode etc.): the session lives in memory only.
  }
}

function decode(token: string | null): AccessTokenClaims | null {
  if (!token) return null;

  try {
    const base64 = token.split('.')[1].replace(/-/g, '+').replace(/_/g, '/');
    const bytes = Uint8Array.from(atob(base64), (c) => c.charCodeAt(0));
    return JSON.parse(new TextDecoder().decode(bytes)) as AccessTokenClaims;
  } catch {
    return null;
  }
}

const asArray = (value: string | string[] | undefined): string[] =>
  value === undefined ? [] : Array.isArray(value) ? value : [value];

/**
 * Session state. Everything about the user (roles, permissions, language) is read from the access
 * token, so it survives reloads and always matches what the API will authorize.
 */
@Injectable({ providedIn: 'root' })
export class AuthStore {
  readonly accessToken = signal<string | null>(readStorage(ACCESS_TOKEN_KEY));
  readonly refreshToken = signal<string | null>(readStorage(REFRESH_TOKEN_KEY));

  private readonly claims = computed(() => decode(this.accessToken()));

  readonly user = computed<SignedInUser | null>(() => {
    const c = this.claims();
    if (!c?.sub) return null;

    return {
      id: Number(c.sub),
      email: c.email ?? '',
      name: c.name ?? '',
      firstName: c.given_name ?? '',
      lastName: c.family_name ?? '',
      locale: c.locale ?? '',
    };
  });

  readonly roles = computed(() => asArray(this.claims()?.role));
  readonly permissions = computed(() => new Set(asArray(this.claims()?.perm)));

  /** A session exists while there is a token that is valid or can be refreshed. */
  readonly isAuthenticated = computed(() => !!this.accessToken() || !!this.refreshToken());

  can(permission: string): boolean {
    return this.permissions().has(permission);
  }

  isAccessTokenExpired(): boolean {
    const exp = this.claims()?.exp;
    return !exp || Date.now() >= exp * 1000 - EXPIRY_SKEW_MS;
  }

  setTokens(accessToken: string, refreshToken: string): void {
    writeStorage(ACCESS_TOKEN_KEY, accessToken);
    writeStorage(REFRESH_TOKEN_KEY, refreshToken);
    this.accessToken.set(accessToken);
    this.refreshToken.set(refreshToken);
  }

  clear(): void {
    writeStorage(ACCESS_TOKEN_KEY, null);
    writeStorage(REFRESH_TOKEN_KEY, null);
    this.accessToken.set(null);
    this.refreshToken.set(null);
  }
}
