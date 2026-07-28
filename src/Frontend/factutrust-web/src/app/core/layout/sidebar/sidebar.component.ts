import {
  Component,
  inject,
  effect,
  OnInit,
  input,
  output
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { CommonModule } from '@angular/common';
import { RouterModule, Router, NavigationEnd } from '@angular/router';
import { filter } from 'rxjs';
import { AuthService } from '@core/services/auth.service';
import {
  NavItem,
  NavSubItem
} from '@core/config/app-navigation.registry';
import { FirmContextService } from '@core/services/firm-context.service';
import { BRAND } from '@core/constants/brand';
import { AppNavService } from '@core/services/app-nav.service';
import { isNavChildActive } from '@core/utils/nav-path-match';

@Component({
  selector: 'app-sidebar',
  standalone: true,
  imports: [CommonModule, RouterModule],
  templateUrl: './sidebar.component.html',
  styleUrl: './sidebar.component.scss'
})
export class SidebarComponent implements OnInit {
  readonly collapsed = input(false);
  readonly toggleCollapse = output<void>();
  readonly requestExpand = output<void>();

  readonly brand = BRAND;

  private readonly router = inject(Router);
  private readonly auth = inject(AuthService);
  private readonly firmContext = inject(FirmContextService);
  private readonly appNav = inject(AppNavService);

  readonly dashboardHomeLink = this.appNav.dashboardHomeLink;
  readonly navItems = this.appNav.navItems;

  /** At most one parent section with its submenu open; closed after each navigation. */
  expandedParentLabel: string | null = null;

  constructor() {
    effect(() => {
      if (this.collapsed()) {
        this.expandedParentLabel = null;
      }
    });

    this.router.events
      .pipe(
        filter((e): e is NavigationEnd => e instanceof NavigationEnd),
        takeUntilDestroyed()
      )
      .subscribe(() => this.collapseSubmenuPanel());

    effect(() => {
      const items = this.navItems();
      if (this.expandedParentLabel !== null) {
        const parent = items.find(i => i.label === this.expandedParentLabel);
        if (!parent?.children?.length) {
          this.expandedParentLabel = null;
        }
      }
    });

    effect(() => {
      if (this.auth.isAccountingFirm() && !this.auth.isDelegatedMode()) {
        this.appNav.ensureFirmNavDataLoaded();
      }
    });
  }

  ngOnInit(): void {
    this.appNav.ensureFirmNavDataLoaded();
  }

  private collapseSubmenuPanel(): void {
    this.expandedParentLabel = null;
  }

  isExpanded(item: NavItem): boolean {
    return this.expandedParentLabel === item.label;
  }

  toggleSubmenu(item: NavItem): void {
    if (!item.children?.length) {
      return;
    }
    if (this.collapsed()) {
      this.requestExpand.emit();
      this.expandedParentLabel = item.label;
      return;
    }
    this.expandedParentLabel =
      this.expandedParentLabel === item.label ? null : item.label;
  }

  onSubmenuToggleKeydown(event: KeyboardEvent, item: NavItem): void {
    if (event.key === 'Enter' || event.key === ' ') {
      event.preventDefault();
      this.toggleSubmenu(item);
    }
  }

  /** True if this parent owns the current URL (longest child match within parent). */
  isChildActive(item: NavItem): boolean {
    return isNavChildActive(this.router.url, item);
  }

  /**
   * Strong rail highlight: current URL matches a child of this section (only one such section at a time).
   */
  isRailParentActive(item: NavItem): boolean {
    if (!item.children?.length) {
      return false;
    }
    return this.isChildActive(item);
  }

  /**
   * Submenu is open for this section but the current route is outside it — discrete styling, not `.active`.
   */
  isRailSectionExpandedOnly(item: NavItem): boolean {
    if (!item.children?.length) {
      return false;
    }
    return this.expandedParentLabel === item.label && !this.isChildActive(item);
  }

  async onNavSubItemAction(child: NavSubItem): Promise<void> {
    if (child.action === 'returnToFirm') {
      await this.firmContext.returnToFirmHome();
      return;
    }
    if (child.action === 'changeDossier') {
      await this.firmContext.navigateToClientList();
    }
  }
}
