import {
  Component,
  Output,
  Input,
  EventEmitter,
  ViewChild,
  ElementRef,
  AfterViewInit,
  OnDestroy,
  inject,
} from '@angular/core';
import {
  DxTreeViewModule,
  DxTreeViewComponent,
  DxTreeViewTypes,
} from 'devextreme-angular/ui/tree-view';
import { navigation } from '../../../app-navigation';

import * as events from 'devextreme-angular/common/core/events';
import { AuthStore } from '../../../core/stores/auth.store';

@Component({
  selector: 'app-side-navigation-menu',
  templateUrl: './side-navigation-menu.component.html',
  styleUrls: ['./side-navigation-menu.component.scss'],
  standalone: true,
  imports: [DxTreeViewModule],
})
export class SideNavigationMenuComponent implements AfterViewInit, OnDestroy {
  @ViewChild(DxTreeViewComponent, { static: true })
  menu!: DxTreeViewComponent;

  @Output()
  selectedItemChanged = new EventEmitter<DxTreeViewTypes.ItemClickEvent>();

  @Output()
  openMenu = new EventEmitter<any>();

  private _selectedItem!: String;
  private authStore = inject(AuthStore);

  @Input()
  set selectedItem(value: String) {
    this._selectedItem = value;
    if (!this.menu.instance) {
      return;
    }

    this.menu.instance.selectItem(value);
  }

  private _items: Record<string, unknown>[] = [];
  private _itemsRolesKey: string | null = null;

  // Rebuilt whenever the user's roles change (login as another user, token refresh),
  // but kept stable otherwise so the tree view doesn't re-render on every check.
  get items() {
    const userRoles = this.authStore.roles();
    const rolesKey = userRoles.join('|');

    if (this._itemsRolesKey !== rolesKey) {
      this._itemsRolesKey = rolesKey;

      this._items = navigation
        // Items without roles are visible to everyone; otherwise one matching role is enough.
        .filter((item) => !item.roles?.length || item.roles.some((role) => userRoles.includes(role)))
        .map((item) => ({
          ...item,
          path: item.path && !item.path.startsWith('/') ? `/${item.path}` : item.path,
          expanded: !this._compactMode,
        }));
    }

    return this._items;
  }

  private _compactMode = false;
  @Input()
  get compactMode() {
    return this._compactMode;
  }
  set compactMode(val) {
    this._compactMode = val;

    if (!this.menu.instance) {
      return;
    }

    if (val) {
      this.menu.instance.collapseAll();
    } else {
      this.menu.instance.expandItem(this._selectedItem);
    }
  }

  constructor(private elementRef: ElementRef) {}

  onItemClick(event: DxTreeViewTypes.ItemClickEvent) {
    this.selectedItemChanged.emit(event);
  }

  ngAfterViewInit() {
    events.on(this.elementRef.nativeElement, 'dxclick', (e: Event) => {
      this.openMenu.next(e);
    });
  }

  ngOnDestroy() {
    events.off(this.elementRef.nativeElement, 'dxclick');
  }

  
}
