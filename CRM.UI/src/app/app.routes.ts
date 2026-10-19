import { Routes } from '@angular/router';

import { authGuard, guestGuard, permissionGuard } from './core/auth/guards';
import { AuthLayout } from './layout/auth-layout';
import { Shell } from './layout/shell';

/** Feature routes inside the app shell. `data.permission` hides pages the user may not open. */
const FEATURE_ROUTES: Routes = [];

export const routes: Routes = [
  {
    path: '',
    component: AuthLayout,
    canActivate: [guestGuard],
    children: [
      { path: 'login', loadComponent: () => import('./features/auth/login').then((m) => m.Login) },
      { path: 'register', loadComponent: () => import('./features/auth/register').then((m) => m.Register) },
    ],
  },
  {
    path: '',
    component: Shell,
    canActivate: [authGuard],
    canActivateChild: [permissionGuard],
    children: [
      ...FEATURE_ROUTES,
      {
        path: 'forbidden',
        data: { kind: 'forbidden' },
        loadComponent: () => import('./features/errors/error-page').then((m) => m.ErrorPage),
      },
      { path: '**', loadComponent: () => import('./features/errors/error-page').then((m) => m.ErrorPage) },
    ],
  },
];
