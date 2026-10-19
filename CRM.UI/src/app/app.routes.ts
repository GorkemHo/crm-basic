import { Routes } from '@angular/router';
import { LoginFormComponent, CreateAccountFormComponent } from './shared/components';
import { AuthGuardService } from './core/guards/auth.guard';
import { HomeComponent } from './pages/home/home.component';
import { ProfileComponent } from './pages/profile/profile.component';
import { TasksComponent } from './pages/tasks/tasks.component';
import { CompaniesComponent } from './pages/companies/companies.component';
import { CustomersComponent } from './pages/customers/customers.component';
import { UsersComponent } from './pages/users/users.component';

// `data.roles` must match the API's [Authorize(Roles = ...)] on the endpoints the page uses.
export const routes: Routes = [
  {
    path: '',
    redirectTo: 'home',
    pathMatch: 'full',
  },
  {
    path: 'tasks',
    component: TasksComponent,
    canActivate: [AuthGuardService],
  },
  {
    path: 'companies',
    component: CompaniesComponent,
    canActivate: [AuthGuardService],
    data: { roles: ['Admin', 'User'] },
  },
  {
    path: 'customers',
    component: CustomersComponent,
    canActivate: [AuthGuardService],
    data: { roles: ['Admin', 'User'] },
  },
  {
    path: 'users',
    component: UsersComponent,
    canActivate: [AuthGuardService],
    data: { roles: ['Admin'] },
  },
  {
    path: 'profile',
    component: ProfileComponent,
    canActivate: [AuthGuardService],
  },
  {
    path: 'home',
    component: HomeComponent,
    canActivate: [AuthGuardService],
  },
  {
    path: 'login-form',
    component: LoginFormComponent,
  },
  {
    path: 'create-account',
    component: CreateAccountFormComponent,
  },
  {
    path: '**',
    redirectTo: 'home',
  },
];
