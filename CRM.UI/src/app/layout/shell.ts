import { Component, computed, inject, signal } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { MenuItem } from 'primeng/api';
import { AvatarModule } from 'primeng/avatar';
import { ButtonModule } from 'primeng/button';
import { MenuModule } from 'primeng/menu';
import { TooltipModule } from 'primeng/tooltip';

import { AuthService } from '../core/auth/auth.service';
import { AuthStore } from '../core/auth/auth.store';
import { Permissions } from '../core/auth/permissions';
import { LanguageService } from '../core/i18n/language.service';
import { ThemeService } from '../core/ui/theme.service';
import { LanguageSwitcher } from './language-switcher';

interface NavItem {
  label: string;
  icon: string;
  route: string;
  /** Hidden unless the user has this permission. */
  permission?: string;
  exact?: boolean;
}

interface NavSection {
  label?: string;
  items: NavItem[];
}

/** Add new modules' pages here; items the user can't access are hidden. */
const NAVIGATION: NavSection[] = [
  { items: [{ label: 'nav.dashboard', icon: 'pi pi-home', route: '/', exact: true }] },
  {
    label: 'nav.crm',
    items: [
      { label: 'nav.companies', icon: 'pi pi-building', route: '/companies', permission: Permissions.companiesRead },
      { label: 'nav.customers', icon: 'pi pi-users', route: '/customers', permission: Permissions.customersRead },
    ],
  },
  {
    label: 'nav.administration',
    items: [
      { label: 'nav.users', icon: 'pi pi-user-edit', route: '/users', permission: Permissions.usersRead },
      { label: 'nav.roles', icon: 'pi pi-shield', route: '/roles', permission: Permissions.rolesRead },
    ],
  },
];

@Component({
  selector: 'app-shell',
  imports: [RouterOutlet, RouterLink, RouterLinkActive, TranslocoPipe, ButtonModule, MenuModule, AvatarModule, TooltipModule, LanguageSwitcher],
  templateUrl: './shell.html',
  styleUrl: './shell.scss',
})
export class Shell {
  private readonly auth = inject(AuthService);
  private readonly transloco = inject(TranslocoService);
  private readonly language = inject(LanguageService);
  readonly store = inject(AuthStore);
  readonly theme = inject(ThemeService);

  /** Mobile: sidebar is an overlay toggled by the menu button. */
  readonly sidebarOpen = signal(false);

  readonly sections = computed(() => {
    const permissions = this.store.permissions();
    return NAVIGATION
      .map((section) => ({ ...section, items: section.items.filter((i) => !i.permission || permissions.has(i.permission)) }))
      .filter((section) => section.items.length > 0);
  });

  readonly initials = computed(() => {
    const user = this.store.user();
    return ((user?.firstName[0] ?? '') + (user?.lastName[0] ?? '')).toUpperCase() || '?';
  });

  readonly userMenu = computed<MenuItem[]>(() => {
    this.language.current(); // re-translate on language change
    return [
      { label: this.transloco.translate('nav.profile'), icon: 'pi pi-user', routerLink: '/profile' },
      {
        label: this.transloco.translate(this.theme.dark() ? 'nav.lightMode' : 'nav.darkMode'),
        icon: this.theme.dark() ? 'pi pi-sun' : 'pi pi-moon',
        command: () => this.theme.toggle(),
      },
      { separator: true },
      { label: this.transloco.translate('nav.logout'), icon: 'pi pi-sign-out', command: () => this.auth.logout() },
    ];
  });
}
