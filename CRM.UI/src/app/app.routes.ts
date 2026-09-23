import { Routes } from '@angular/router';
import { LoginFormComponent, ResetPasswordFormComponent, CreateAccountFormComponent, ChangePasswordFormComponent } from './shared/components';
import { AuthGuardService } from './core/guards/auth.guard';
import { HomeComponent } from './pages/home/home.component';
import { ProfileComponent } from './pages/profile/profile.component';
import { TasksComponent } from './pages/tasks/tasks.component';
import { CompaniesComponent } from './pages/companies/companies.component';
import { CustomersComponent } from './pages/customers/customers.component';
import { UsersComponent } from './pages/users/users.component';

export const routes: Routes = [
  {
  path: '',
  redirectTo: 'login-form',
  pathMatch: 'full'
  },
  // {
  //   path: '**',
  //   redirectTo: 'login-form'
  // },
  {
    path: 'tasks',
    component: TasksComponent,
    canActivate: [ AuthGuardService ]
  },
  {
    path: 'companies',
    component: CompaniesComponent,
    canActivate: [AuthGuardService]
  },
  {
    path: 'customers',
    component: CustomersComponent,
    canActivate: [AuthGuardService]
  },
   {
    path: 'users',
    component: UsersComponent,
    canActivate: [AuthGuardService]
  },
  {
    path: 'profile',
    component: ProfileComponent,
    canActivate: [ AuthGuardService ]
  },
  {
    path: 'home',
    component: HomeComponent,
    canActivate: [ AuthGuardService ]
  },
  {
    path: 'login-form',
    component: LoginFormComponent,
    // canActivate: [ AuthGuardService ]
  },
  {
    path: 'create-account',
    component: CreateAccountFormComponent,
    //canActivate: [ AuthGuardService ]
  }
  // {
  //   path: 'reset-password',
  //   component: ResetPasswordFormComponent,
  //   canActivate: [ AuthGuardService ]
  // },
  // {
  //   path: 'change-password/:recoveryCode',
  //   component: ChangePasswordFormComponent,
  //   canActivate: [ AuthGuardService ]
  // }
];
