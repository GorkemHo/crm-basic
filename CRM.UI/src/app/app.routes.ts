import { Routes } from '@angular/router';

import { authGuard, guestGuard, permissionGuard } from './core/auth/guards';
import { Permissions } from './core/auth/permissions';
import { AuthLayout } from './layout/auth-layout';
import { Shell } from './layout/shell';

/** Feature routes inside the app shell. `data.permission` blocks pages the user may not open. */
const FEATURE_ROUTES: Routes = [
  {
    path: 'companies',
    data: { permission: Permissions.companiesRead },
    loadChildren: () => import('./features/companies/companies.routes').then((m) => m.COMPANY_ROUTES),
  },
  {
    path: 'customers',
    data: { permission: Permissions.customersRead },
    loadChildren: () => import('./features/customers/customers.routes').then((m) => m.CUSTOMER_ROUTES),
  },
];

export const routes: Routes = [
  // Each auth page has its own path, so an empty URL never matches the auth layout.
  {
    path: 'login',
    component: AuthLayout,
    canActivate: [guestGuard],
    children: [{ path: '', loadComponent: () => import('./features/auth/login').then((m) => m.Login) }],
  },
  {
    path: 'register',
    component: AuthLayout,
    canActivate: [guestGuard],
    children: [{ path: '', loadComponent: () => import('./features/auth/register').then((m) => m.Register) }],
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
