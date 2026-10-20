import { Routes } from '@angular/router';

import { Permissions } from '../../core/auth/permissions';

export const PIPELINE_ROUTES: Routes = [
  { path: '', loadComponent: () => import('./deal-board').then((m) => m.DealBoard) },
  {
    path: 'stages',
    data: { permission: Permissions.stagesManage },
    loadComponent: () => import('./stage-settings').then((m) => m.StageSettings),
  },
];

export const DEAL_ROUTES: Routes = [
  { path: '', loadComponent: () => import('./deal-list').then((m) => m.DealList) },
  { path: ':id', loadComponent: () => import('./deal-detail').then((m) => m.DealDetail) },
];
