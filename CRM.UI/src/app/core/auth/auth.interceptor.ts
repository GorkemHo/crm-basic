import { HttpErrorResponse, HttpInterceptorFn, HttpRequest } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, of, switchMap, throwError } from 'rxjs';

import { LanguageService } from '../i18n/language.service';
import { AuthService } from './auth.service';
import { AuthStore } from './auth.store';

// Endpoints that must not carry a bearer token or trigger a refresh.
const ANONYMOUS = ['/api/auth/login', '/api/auth/register', '/api/auth/refresh', '/api/auth/logout'];

/**
 * Adds `Accept-Language` (API validation messages follow it) and the bearer token.
 * An expired access token is refreshed before sending; a 401 triggers one refresh and a retry;
 * if refreshing fails the user is signed out.
 */
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const store = inject(AuthStore);
  const auth = inject(AuthService);
  const language = inject(LanguageService);

  const localized = req.clone({ setHeaders: { 'Accept-Language': language.current() } });

  if (!req.url.startsWith('/api/') || ANONYMOUS.some((path) => req.url.startsWith(path))) {
    return next(localized);
  }

  const withToken = (request: HttpRequest<unknown>, token: string | null) =>
    token ? request.clone({ setHeaders: { Authorization: `Bearer ${token}` } }) : request;

  const signOut = (error: unknown) => {
    auth.logout();
    return throwError(() => error);
  };

  const token$ =
    store.isAccessTokenExpired() && store.refreshToken()
      ? auth.refresh().pipe(catchError(signOut))
      : of(store.accessToken());

  return token$.pipe(
    switchMap((token) => next(withToken(localized, token))),
    catchError((error: unknown) => {
      if (!(error instanceof HttpErrorResponse) || error.status !== 401) {
        return throwError(() => error);
      }

      if (!store.refreshToken()) {
        return signOut(error);
      }

      return auth.refresh().pipe(
        switchMap((token) => next(withToken(localized, token))),
        catchError(signOut),
      );
    }),
  );
};
