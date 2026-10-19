import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { catchError, map, of } from 'rxjs';

import { AuthService } from './auth.service';
import { AuthStore } from './auth.store';

/** Signed-in users only; refreshes an expired access token first. */
export const authGuard: CanActivateFn = (_route, state) => {
  const store = inject(AuthStore);
  const auth = inject(AuthService);
  const router = inject(Router);

  const toLogin = router.createUrlTree(['/login'], { queryParams: { returnUrl: state.url } });

  if (!store.isAuthenticated()) {
    return toLogin;
  }

  if (!store.isAccessTokenExpired()) {
    return true;
  }

  return auth.refresh().pipe(
    map(() => true),
    catchError(() => {
      store.clear();
      return of(toLogin);
    }),
  );
};

/** Login/register pages: signed-in users go to the dashboard. */
export const guestGuard: CanActivateFn = () => {
  const store = inject(AuthStore);
  return store.isAuthenticated() ? inject(Router).createUrlTree(['/']) : true;
};

/** Requires `data.permission` on the route. Use after `authGuard`. */
export const permissionGuard: CanActivateFn = (route) => {
  const permission = route.data['permission'] as string | undefined;
  return !permission || inject(AuthStore).can(permission) ? true : inject(Router).createUrlTree(['/forbidden']);
};
