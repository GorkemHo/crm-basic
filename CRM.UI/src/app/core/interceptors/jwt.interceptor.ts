import { HttpErrorResponse, HttpInterceptorFn, HttpRequest } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, switchMap, throwError } from 'rxjs';

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

  return next(withToken(req, authStore.accessToken())).pipe(
    catchError((error: HttpErrorResponse) => {
      if (error.status !== 401) {
        return throwError(() => error);
      }

      if (!authStore.refreshToken()) {
        authService.logOut();
        return throwError(() => error);
      }

      // Access token rejected: refresh once and retry the original request.
      return authService.refreshAccessToken().pipe(
        switchMap((newToken) => next(withToken(req, newToken))),
        catchError((refreshError) => {
          authService.logOut();
          return throwError(() => refreshError);
        }),
      );
    }),
  );
};
