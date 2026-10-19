import { Routes } from '@angular/router';

export const USER_ROUTES: Routes = [
  { path: '', loadComponent: () => import('./user-list').then((m) => m.UserList) },
  { path: ':id', loadComponent: () => import('./user-detail').then((m) => m.UserDetail) },
];
