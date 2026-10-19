import { Routes } from '@angular/router';

export const CUSTOMER_ROUTES: Routes = [
  { path: '', loadComponent: () => import('./customer-list').then((m) => m.CustomerList) },
  { path: ':id', loadComponent: () => import('./customer-detail').then((m) => m.CustomerDetail) },
];
