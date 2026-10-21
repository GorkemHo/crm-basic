import { Routes } from '@angular/router';

import { authGuard, guestGuard, permissionGuard } from './core/auth/guards';
import { Permissions } from './core/auth/permissions';
import { AuthLayout } from './layout/auth-layout';
import { Shell } from './layout/shell';

/** Feature routes inside the app shell. `data.permission` blocks pages the user may not open. */
const FEATURE_ROUTES: Routes = [
  { path: '', pathMatch: 'full', loadComponent: () => import('./features/dashboard/dashboard').then((m) => m.Dashboard) },
  { path: 'notifications', loadComponent: () => import('./features/notifications/notification-page').then((m) => m.NotificationPage) },
  { path: 'profile', loadComponent: () => import('./features/profile/profile').then((m) => m.Profile) },
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
  {
    path: 'reports',
    data: { permission: Permissions.reportsRead },
    loadComponent: () => import('./features/reports/report-page').then((m) => m.ReportPage),
  },
  {
    path: 'tasks',
    data: { permission: Permissions.tasksRead },
    loadComponent: () => import('./features/tasks/task-page').then((m) => m.TaskPage),
  },
  {
    path: 'activities',
    data: { permission: Permissions.activitiesRead },
    loadComponent: () => import('./features/activities/activity-list').then((m) => m.ActivityList),
  },
  {
    path: 'pipeline',
    data: { permission: Permissions.dealsRead },
    loadChildren: () => import('./features/deals/deals.routes').then((m) => m.PIPELINE_ROUTES),
  },
  {
    path: 'deals',
    data: { permission: Permissions.dealsRead },
    loadChildren: () => import('./features/deals/deals.routes').then((m) => m.DEAL_ROUTES),
  },
  {
    path: 'users',
    data: { permission: Permissions.usersRead },
    loadChildren: () => import('./features/users/users.routes').then((m) => m.USER_ROUTES),
  },
  {
    path: 'roles',
    data: { permission: Permissions.rolesRead },
    loadChildren: () => import('./features/roles/roles.routes').then((m) => m.ROLE_ROUTES),
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
      {
        path: '**',
        data: { kind: 'notFound' },
        loadComponent: () => import('./features/errors/error-page').then((m) => m.ErrorPage),
      },
    ],
  },
];
