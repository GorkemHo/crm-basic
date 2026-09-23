import { Injectable } from '@angular/core';
import { AuthService } from '../../core/services/auth.service';

@Injectable()
export class AppInfoService {
  userName: string = '';

  constructor(authService: AuthService) {
    this.userName =
      `${authService._user?.firstName ?? ''} ${authService._user?.lastName ?? ''}`.trim();
  }

  public get title() {
    const title = 'CRM - ISNET' + this.userName;
    return title;
  }

  public get currentYear() {
    return new Date().getFullYear();
  }
}
