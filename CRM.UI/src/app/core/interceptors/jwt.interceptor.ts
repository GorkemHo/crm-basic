import { HttpErrorResponse, HttpInterceptorFn, HttpRequest } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, of, switchMap, throwError } from 'rxjs';

import { AuthStore } from '../stores/auth.store';
import { AuthService } from '../services/auth.service';

// Endpoints that must never carry a bearer token or trigger a refresh.
const ANONYMOUS_AUTH_PATHS = ['/auth/login', '/auth/register', '/auth/refresh', '/auth/logout'];

const withToken = (req: HttpRequest<unknown>, token: string | null) =>
  token ? req.clone({ setHeaders: { Authorization: `Bearer ${token}` } }) : req;

export const jwtInterceptor: HttpInterceptorFn = (req, next) => {
  const authStore = inject(AuthStore);
  const authService = inject(AuthService);

  if (ANONYMOUS_AUTH_PATHS.some((path) => req.url.includes(path))) {
    return next(req);
  }

  const failRefresh = (error: unknown) => {
    authService.logOut();
    return throwError(() => error);
  };

  // Refresh up front when the access token is already expired, instead of waiting for a 401.
  const token$ =
    authStore.isAccessTokenExpired() && authStore.refreshToken()
      ? authService.refreshAccessToken().pipe(catchError(failRefresh))
      : of(authStore.accessToken());

  return token$.pipe(
    switchMap((token) => next(withToken(req, token))),
    catchError((error: unknown) => {
      if (!(error instanceof HttpErrorResponse) || error.status !== 401) {
        return throwError(() => error);
      }

      if (!authStore.refreshToken()) {
        return failRefresh(error);
      }

      // Token rejected despite looking valid (revoked, key rotated...): refresh once and retry.
      return authService.refreshAccessToken().pipe(
        switchMap((newToken) => next(withToken(req, newToken))),
        catchError(failRefresh),
      );
    }),
  );
};
