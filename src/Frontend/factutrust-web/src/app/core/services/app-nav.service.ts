import { Injectable, computed, inject, signal } from '@angular/core';
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
import {
  buildAccountingModuleNavItems,
  buildFirmDelegatedAccountingModuleNavItems
} from '@core/config/accounting-modules.config';
import {
  applyCompanyAccountingSidebar,
  COMPANY_ACCOUNTING_FIXED_ASSETS_ROUTE
} from '@core/config/company-accounting-nav.config';
import { FirmContextService } from '@core/services/firm-context.service';
import { FirmAssignmentService, FirmClientDossier } from '@core/services/firm-assignment.service';
import { FirmBadgeService } from '@core/services/firm-badge.service';
import { FirmFeatureFlagsService } from '@core/services/firm-feature-flags.service';
import { AccountingFeatureFlagsService } from '@features/accounting/shared/accounting-feature-flags.service';
import { AppModule } from '@core/models/app-module';
import { PERMISSIONS } from '@core/config/permission-keys';
import { StudioNavService } from '@features/studio/studio-nav.service';
import {
  ACCOUNTING_FIRM_SECONDARY_EXCLUDED_LABELS,
  SECONDARY_NAV_SECTION_ORDER
} from '@core/config/secondary-nav.config';

/**
 * Source unique du graphe de navigation filtré (sidebar + 2e barre).
 * Toute évolution des règles de visibilité doit passer par ce service.
 */
@Injectable({ providedIn: 'root' })
export class AppNavService {
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

  /** Arbre complet visible (identique à l’ancien `SidebarComponent.navItems`). */
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
    const studioParent = this.buildStudioNavSection();
    const studioFiltered = filterNavItems(this.auth, [studioParent]);
    return [...items, ...studioFiltered];
  });

  /**
   * Sections de la 2e barre desktop.
   * - Entreprise : ordre `SECONDARY_NAV_SECTION_ORDER` dérivé de `navItems`.
   * - Cabinet délégué : modules comptables (`ACCOUNTING_MODULES`) uniquement ;
   *   Ventes/Achats/Trésorerie/RH&Paie restent dans le sidebar via `navItems`.
   * - Cabinet natif : vide (pas de sections métier company).
   */
  readonly secondaryNavSections = computed((): NavItem[] => {
    if (this.auth.isAccountingFirm() && this.auth.isDelegatedMode()) {
      const moduleSections = filterNavItems(this.auth, buildAccountingModuleNavItems());
      return moduleSections
        .filter(item => !ACCOUNTING_FIRM_SECONDARY_EXCLUDED_LABELS.has(item.label))
        .filter(item => !!item.route || !!item.children?.length);
    }

    const byLabel = new Map(this.navItems().map(item => [item.label, item]));
    const sections: NavItem[] = [];
    for (const label of SECONDARY_NAV_SECTION_ORDER) {
      const item = byLabel.get(label);
      if (!item) {
        continue;
      }
      if (item.children?.length || item.route) {
        sections.push(item);
      }
    }
    return sections;
  });

  readonly hasSecondaryNav = computed(() => this.secondaryNavSections().length > 0);

  ensureFirmNavDataLoaded(): void {
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
}
