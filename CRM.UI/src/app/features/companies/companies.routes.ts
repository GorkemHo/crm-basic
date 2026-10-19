import { Routes } from '@angular/router';

export const COMPANY_ROUTES: Routes = [
  { path: '', loadComponent: () => import('./company-list').then((m) => m.CompanyList) },
  { path: ':id', loadComponent: () => import('./company-detail').then((m) => m.CompanyDetail) },
];
