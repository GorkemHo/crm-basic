import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { catchError, map, of } from 'rxjs';

import { AuthStore } from '../stores/auth.store';
import { AuthService } from '../services/auth.service';

export const AuthGuardService: CanActivateFn = (route) => {
  const authStore = inject(AuthStore);
  const authService = inject(AuthService);
  const router = inject(Router);

  const loginUrl = router.parseUrl('/login-form');

  if (!authStore.isAuthenticated()) {
    return loginUrl;
  }

  const requiredRoles = route.data?.['roles'] as string[] | undefined;

  const checkRoles = () =>
    !requiredRoles?.length || authStore.hasRole(...requiredRoles) ? true : router.parseUrl('/home');

  if (!authStore.isAccessTokenExpired()) {
    return checkRoles();
  }

  // Access token expired: try to refresh before letting the user in.
  return authService.refreshAccessToken().pipe(
    map(() => checkRoles()),
    catchError(() => {
      authService.logOut();
      return of(loginUrl);
    }),
  );
};
