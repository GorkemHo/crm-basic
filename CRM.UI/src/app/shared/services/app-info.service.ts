import { Injectable, inject } from '@angular/core';
import { AuthStore } from '../../core/stores/auth.store';

@Injectable()
export class AppInfoService {
  private authStore = inject(AuthStore);

  public get userName(): string {
    return this.authStore.user()?.name ?? '';
  }

  public get title() {
    return this.userName ? `CRM - ISNET | ${this.userName}` : 'CRM - ISNET';
  }

  public get currentYear() {
    return new Date().getFullYear();
  }
}
