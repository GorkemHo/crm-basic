import { BreakpointObserver } from '@angular/cdk/layout';
import { Component, computed, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatDividerModule } from '@angular/material/divider';
import { MatMenuModule } from '@angular/material/menu';
import { MatSidenavModule } from '@angular/material/sidenav';
import { MatTooltipModule } from '@angular/material/tooltip';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { map } from 'rxjs';

import { AuthService } from '../core/auth/auth.service';
import { AuthStore } from '../core/auth/auth.store';
import { Permissions } from '../core/auth/permissions';
import { ThemeService } from '../core/ui/theme.service';
import { NotificationBell } from '../features/notifications/notification-bell';
import { LanguageSwitcher } from './language-switcher';

interface NavItem {
  label: string;
  /** Material Symbols name. */
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
  {
    items: [
      { label: 'nav.dashboard', icon: 'space_dashboard', route: '/', exact: true },
      { label: 'nav.tasks', icon: 'task_alt', route: '/tasks', permission: Permissions.tasksRead },
    ],
  },
  {
    label: 'nav.crm',
    items: [
      { label: 'nav.companies', icon: 'apartment', route: '/companies', permission: Permissions.companiesRead },
      { label: 'nav.customers', icon: 'groups', route: '/customers', permission: Permissions.customersRead },
      { label: 'nav.activities', icon: 'event_note', route: '/activities', permission: Permissions.activitiesRead },
    ],
  },
  {
    label: 'nav.sales',
    items: [
      { label: 'nav.pipeline', icon: 'view_kanban', route: '/pipeline', permission: Permissions.dealsRead },
      { label: 'nav.deals', icon: 'handshake', route: '/deals', permission: Permissions.dealsRead },
    ],
  },
  {
    label: 'nav.administration',
    items: [
      { label: 'nav.users', icon: 'manage_accounts', route: '/users', permission: Permissions.usersRead },
      { label: 'nav.roles', icon: 'admin_panel_settings', route: '/roles', permission: Permissions.rolesRead },
    ],
  },
];

@Component({
  selector: 'app-shell',
  imports: [
    RouterOutlet, RouterLink, RouterLinkActive, TranslocoPipe,
    MatSidenavModule, MatButtonModule, MatMenuModule, MatDividerModule, MatTooltipModule, LanguageSwitcher, NotificationBell,
  ],
  templateUrl: './shell.html',
  styleUrl: './shell.scss',
})
export class Shell {
  private readonly auth = inject(AuthService);
  readonly store = inject(AuthStore);
  readonly theme = inject(ThemeService);

  /** Below 960px the sidebar overlays the content and is opened from the menu button. */
  readonly isMobile = toSignal(
    inject(BreakpointObserver).observe('(max-width: 959.98px)').pipe(map((state) => state.matches)),
    { initialValue: false },
  );

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

  logout(): void {
    this.auth.logout();
  }
}
