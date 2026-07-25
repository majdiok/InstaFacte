import {
  Component,
  inject,
  computed,
  effect,
  signal,
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
  ALL_NAV_ITEMS,
  filterNavItems,
  NavItem,
  NavSubItem
} from '@core/config/app-navigation.registry';
import {
  DELEGATED_SECTION_LABELS,
  FIRM_NATIVE_NAV,
  filterFirmGovernanceNav,
  filterDelegatedFirmSectionChildren
} from '@core/config/firm-navigation.registry';
import { buildFirmDelegatedAccountingModuleNavItems } from '@core/config/accounting-modules.config';
import {
  applyCompanyAccountingSidebar,
  COMPANY_ACCOUNTING_FIXED_ASSETS_ROUTE
} from '@core/config/company-accounting-nav.config';
import { FirmContextService } from '@core/services/firm-context.service';
import { FirmAssignmentService, FirmClientDossier } from '@core/services/firm-assignment.service';
import { FirmBadgeService } from '@core/services/firm-badge.service';
import { FirmFeatureFlagsService } from '@core/services/firm-feature-flags.service';
import { AccountingFeatureFlagsService } from '@features/accounting/shared/accounting-feature-flags.service';
import { BRAND } from '@core/constants/brand';
import { AppModule } from '@core/models/app-module';
import { PERMISSIONS } from '@core/config/permission-keys';
import { StudioNavService } from '@features/studio/studio-nav.service';

/** Path only: no query, no hash; trailing slash removed except root. */
function stripPathForMatch(url: string): string {
  let path = url.split('?')[0].split('#')[0];
  if (path.length > 1 && path.endsWith('/')) {
    path = path.slice(0, -1);
  }
  return path;
}

/** Avoids `/invoice` matching `/invoices` (segment boundary). */
function pathMatchesRoute(path: string, route: string | undefined): boolean {
  if (!route) {
    return false;
  }
  if (path === route) {
    return true;
  }
  return path.startsWith(route + '/');
}

function findLongestMatchingChildInParent(path: string, item: NavItem): NavSubItem | null {
  if (!item.children?.length) {
    return null;
  }
  let best: NavSubItem | null = null;
  let bestLen = -1;
  for (const child of item.children) {
    if (!child.route) {
      continue;
    }
    if (pathMatchesRoute(path, child.route) && child.route.length > bestLen) {
      bestLen = child.route.length;
      best = child;
    }
  }
  return best;
}

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
  private readonly firmAssignments = inject(FirmAssignmentService);
  private readonly firmBadge = inject(FirmBadgeService);
  private readonly firmFeatureFlags = inject(FirmFeatureFlagsService);
  private readonly accountingFlags = inject(AccountingFeatureFlagsService);
  private readonly studioNav = inject(StudioNavService);

  private readonly activeClients = signal<FirmClientDossier[]>([]);

  readonly dashboardHomeLink = computed(() =>
    this.auth.isAccountingFirm() ? '/firm/dashboard' : '/dashboard'
  );

  /** At most one parent section with its submenu open; closed after each navigation. */
  expandedParentLabel: string | null = null;

  readonly navItems = computed(() => {
    this.auth.user();
    this.firmContext.context();
    this.activeClients();
    this.firmBadge.pendingInvitationsCount();

    if (this.auth.isAccountingFirm()) {
      if (this.auth.isDelegatedMode()) {
        return this.buildDelegatedNav();
      }
      return this.buildFirmNativeNav();
    }

    const fixedAssetsEnabled = this.accountingFlags.flags().fixedAssetsEnabled;
    let items = filterNavItems(this.auth, ALL_NAV_ITEMS);
    items = applyCompanyAccountingSidebar(items);
    items = filterNavItems(this.auth, items);
    if (!fixedAssetsEnabled) {
      items = items.map(item =>
        item.children?.length && item.label === 'Comptabilité'
          ? {
              ...item,
              children: item.children.filter(
                c => c.route !== COMPANY_ACCOUNTING_FIXED_ASSETS_ROUTE
              )
            }
          : item
      );
    }
    // Append the dynamic low-code "Studio" section (design entry + one item per custom table).
    // Built fresh and run through the same filterNavItems gating → no change to the static registry.
    const studioParent = this.buildStudioNavSection();
    const studioFiltered = filterNavItems(this.auth, [studioParent]);
    return [...items, ...studioFiltered];
  });

  private buildFirmNativeNav(): NavItem[] {
    const clients = this.activeClients();
    const dossierChildren: NavSubItem[] = clients.slice(0, 8).map(c => ({
      label: c.companyName,
      route: `/firm/open/${c.companyTenantId}`,
      icon: 'fa-solid fa-building'
    }));
    if (clients.length > 0) {
      dossierChildren.push({
        label: 'Voir tous les dossiers',
        route: '/firm/clients',
        icon: 'fa-solid fa-list'
      });
    }

    const pending = this.firmBadge.pendingInvitationsCount();
    const dashboard = FIRM_NATIVE_NAV.find(i => i.route === '/firm/dashboard')!;
    const invitations = FIRM_NATIVE_NAV.find(i => i.route === '/firm/invitations')!;
    const tail = FIRM_NATIVE_NAV.filter(
      i =>
        i.route !== '/firm/dashboard' &&
        i.route !== '/firm/clients' &&
        i.route !== '/firm/invitations'
    );

    return [
      dashboard,
      {
        label: 'Mes dossiers clients',
        icon: 'fa-solid fa-briefcase',
        children: dossierChildren.length
          ? dossierChildren
          : [{
              label: this.auth.isFirmAccountant()
                ? 'Aucun dossier affecté'
                : 'Aucun dossier actif',
              route: '/firm/clients',
              icon: 'fa-solid fa-inbox'
            }]
      },
      {
        ...invitations,
        badge: pending > 0 ? pending : undefined
      },
      ...this.filterFirmManagerOnlyNav(
        filterFirmGovernanceNav(tail, this.firmFeatureFlags.isEnabled('firmGovernance'))
      )
    ];
  }

  /** Masque les entrées réservées FirmManager (ex. affectation des dossiers). */
  private filterFirmManagerOnlyNav(items: NavItem[]): NavItem[] {
    if (this.auth.isFirmManager()) {
      return items;
    }
    return items
      .map(item =>
        item.children?.length
          ? {
              ...item,
              children: item.children.filter(c => c.route !== '/firm/affectation')
            }
          : item
      )
      .filter(item => item.route !== '/firm/affectation')
      .filter(item => !item.children || item.children.length > 0);
  }

  private buildDelegatedNav(): NavItem[] {
    const client = this.firmContext.activeClient();
    let items = filterNavItems(this.auth, ALL_NAV_ITEMS);
    items = items.filter(i => DELEGATED_SECTION_LABELS.has(i.label));
    items = items
      .map(item =>
        item.children?.length && (item.label === 'Ventes' || item.label === 'Achats')
          ? { ...item, children: filterDelegatedFirmSectionChildren(item.label, item.children) }
          : item
      )
      .filter(item => !item.children?.length || item.children.length > 0);

    // Cabinet délégué : hub Comptabilité + menus Budgétaire, Declarations, Immobilisations.
    // Source unique : accounting-modules.config.ts (filtre cabinet délégué).
    const comptaIndex = items.findIndex(i => i.label === 'Comptabilité');
    if (comptaIndex >= 0) {
      const comptaHub: NavItem = {
        label: 'Comptabilité',
        icon: 'fa-solid fa-table-cells-large',
        route: '/accounting/home',
        modules: [AppModule.Accounting],
        permissionsAll: [PERMISSIONS.accounting.read]
      };
      const moduleItems = filterNavItems(this.auth, buildFirmDelegatedAccountingModuleNavItems());
      items = [
        ...items.slice(0, comptaIndex),
        ...filterNavItems(this.auth, [comptaHub]),
        ...moduleItems,
        ...items.slice(comptaIndex + 1)
      ];
    }

    const contextHeader: NavItem = {
      label: client ? `Dossier : ${client.companyName}` : 'Dossier client',
      icon: 'fa-solid fa-folder-open',
      children: [
        {
          label: 'Retour au cabinet',
          icon: 'fa-solid fa-arrow-left',
          action: 'returnToFirm'
        },
        {
          label: 'Changer de dossier',
          icon: 'fa-solid fa-right-left',
          action: 'changeDossier'
        }
      ]
    };

    const fixedAssetsEnabled = this.accountingFlags.flags().fixedAssetsEnabled;
    if (!fixedAssetsEnabled) {
      // startsWith couvre aussi les sous-routes (ex. dotations) du module Immobilisations ;
      // un module vidé de tous ses liens est retiré du rail.
      items = items
        .map(item =>
          item.children?.length
            ? { ...item, children: item.children.filter(c => !c.route?.startsWith('/accounting/fixed-assets')) }
            : item
        )
        .filter(item => !item.children || item.children.length > 0);
    }
    return [contextHeader, ...items];
  }

  private buildStudioNavSection(): NavItem {
    const children: NavSubItem[] = [
      {
        label: 'Concepteur de tables',
        route: '/studio',
        icon: 'fa-solid fa-screwdriver-wrench',
        modules: [AppModule.Studio],
        permissionsAll: [PERMISSIONS.studio.designEntities]
      },
      {
        label: 'Formulaires',
        route: '/studio/forms',
        icon: 'fa-solid fa-table-cells-large',
        modules: [AppModule.Studio],
        permissionsAll: [PERMISSIONS.studio.designForms]
      },
      {
        label: 'Rapports',
        route: '/studio/reports',
        icon: 'fa-solid fa-chart-column',
        modules: [AppModule.Studio],
        permissionsAll: [PERMISSIONS.studio.designReports]
      },
      ...this.studioNav.items().map<NavSubItem>(e => ({
        label: e.label,
        route: e.route.startsWith('/') ? e.route : `/studio/d/${e.key}`,
        icon: e.icon || (e.label.trimStart() !== e.label ? 'fa-solid fa-table' : 'fa-solid fa-folder-tree'),
        modules: [AppModule.Studio],
        permissionsAll: [PERMISSIONS.customData.recordsRead]
      }))
    ];
    return { label: 'Studio', icon: 'fa-solid fa-shapes', children };
  }

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
        this.refreshFirmNavData();
      }
    });
  }

  ngOnInit(): void {
    if (this.auth.isAccountingFirm() && !this.auth.isDelegatedMode()) {
      this.refreshFirmNavData();
    }
  }

  private refreshFirmNavData(): void {
    this.firmAssignments.getActiveClients().subscribe(r => {
      if (r.success) this.activeClients.set(r.data);
    });
    this.firmBadge.refresh();
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
    if (!item.children?.length) {
      return false;
    }
    const path = stripPathForMatch(this.router.url);
    return findLongestMatchingChildInParent(path, item) !== null;
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
