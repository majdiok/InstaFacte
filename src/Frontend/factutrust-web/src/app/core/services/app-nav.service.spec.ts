import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { AuthService, User } from '@core/services/auth.service';
import { FirmContextService } from '@core/services/firm-context.service';
import { FirmAssignmentService } from '@core/services/firm-assignment.service';
import { AccountingFeatureFlagsService } from '@features/accounting/shared/accounting-feature-flags.service';
import { StudioNavService } from '@features/studio/studio-nav.service';
import { AppModule } from '@core/models/app-module';
import { SECONDARY_NAV_SECTION_ORDER } from '@core/config/secondary-nav.config';
import { AppNavService } from './app-nav.service';

const ALL_MODULES = Object.values(AppModule).filter((v): v is number => typeof v === 'number');

const companyUser: User = {
  id: 'u-company',
  email: 'company@test.c',
  firstName: 'A',
  lastName: 'B',
  fullName: 'A B',
  role: 'Administrator',
  roleDisplay: 'Administrateur',
  tenantId: '00000000-0000-0000-0000-000000000001',
  companyName: 'Ste Test',
  tenantKind: 'Company',
  twoFactorEnabled: false,
  enabledModuleIds: ALL_MODULES,
  effectivePermissions: [
    'quotes:read',
    'delivery_notes:read',
    'invoices:read',
    'suppliers:read',
    'purchase_orders:read',
    'supplier_invoices:read',
    'clients:read',
    'products:read',
    'stock:read',
    'stock_transfers:read',
    'inventory:read',
    'payments:read',
    'reports:view',
    'withholding_tax:read',
    'withholding_tax:export',
    'payroll:read',
    'payroll:declare',
    'payroll:settings',
    'accounting:read',
    'ai:chat'
  ]
};

const firmUser: User = {
  id: 'u1',
  email: 'firm@test.c',
  firstName: 'O',
  lastName: 'G',
  fullName: 'O G',
  role: 'FirmManager',
  roleDisplay: 'Responsable cabinet',
  tenantId: '00000000-0000-0000-0000-000000000002',
  companyName: 'Cabinet Test',
  tenantKind: 'AccountingFirm',
  twoFactorEnabled: false,
  enabledModuleIds: ALL_MODULES,
  effectivePermissions: ['firm:manage', 'accounting:read']
};

const delegatedUser: User = {
  ...firmUser,
  accessMode: 'delegated',
  contextTenantId: '00000000-0000-0000-0000-000000000003',
  contextCompanyName: 'Ste Bouzgarou',
  effectivePermissions: [
    'accounting:read',
    'invoices:read',
    'quotes:read',
    'delivery_notes:read',
    'suppliers:read',
    'purchase_orders:read',
    'supplier_invoices:read',
    'payments:read',
    'reports:view',
    'withholding_tax:read',
    'withholding_tax:export',
    'payroll:read'
  ]
};

function setUser(auth: AuthService, u: User | null): void {
  (auth as unknown as { userSignal: { set: (x: User | null) => void } }).userSignal.set(u);
}

describe('AppNavService — secondary nav parity', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting(),
        {
          provide: FirmAssignmentService,
          useValue: {
            getActiveClients: () => of({ success: true, data: [] }),
            getIncomingInvitations: () => of({ success: true, data: [] })
          }
        },
        {
          provide: AccountingFeatureFlagsService,
          useValue: { flags: () => ({ fixedAssetsEnabled: true }) }
        },
        {
          provide: StudioNavService,
          useValue: { items: () => [] }
        }
      ]
    });
  });

  it('exposes secondary sections in the requested order for a company user', () => {
    const auth = TestBed.inject(AuthService);
    setUser(auth, companyUser);
    TestBed.inject(FirmContextService).syncFromUser();

    const nav = TestBed.inject(AppNavService);
    const labels = nav.secondaryNavSections().map(s => s.label);

    expect(labels).toEqual([...SECONDARY_NAV_SECTION_ORDER]);
    expect(nav.hasSecondaryNav()).toBeTrue();
  });

  it('keeps secondary routes as a subset of sidebar routes (parity)', () => {
    const auth = TestBed.inject(AuthService);
    setUser(auth, companyUser);
    TestBed.inject(FirmContextService).syncFromUser();

    const nav = TestBed.inject(AppNavService);
    const sidebarRoutes = new Set(
      nav
        .navItems()
        .flatMap(item => [
          ...(item.route ? [item.route] : []),
          ...(item.children?.map(c => c.route).filter((r): r is string => !!r) ?? [])
        ])
    );
    const secondaryRoutes = nav.secondaryNavSections().flatMap(item => [
      ...(item.route ? [item.route] : []),
      ...(item.children?.map(c => c.route).filter((r): r is string => !!r) ?? [])
    ]);

    for (const route of secondaryRoutes) {
      expect(sidebarRoutes.has(route)).withContext(route).toBeTrue();
    }
  });

  it('hides secondary nav in firm native mode', () => {
    const auth = TestBed.inject(AuthService);
    setUser(auth, firmUser);
    TestBed.inject(FirmContextService).syncFromUser();

    const nav = TestBed.inject(AppNavService);
    expect(nav.secondaryNavSections()).toEqual([]);
    expect(nav.hasSecondaryNav()).toBeFalse();
  });

  it('respects delegated allowlists for Ventes/Achats in secondary nav', () => {
    const auth = TestBed.inject(AuthService);
    setUser(auth, delegatedUser);
    TestBed.inject(FirmContextService).syncFromUser();

    const nav = TestBed.inject(AppNavService);
    const ventes = nav.secondaryNavSections().find(s => s.label === 'Ventes');
    const achats = nav.secondaryNavSections().find(s => s.label === 'Achats');

    expect(ventes?.children?.map(c => c.route)).toEqual([
      '/invoices',
      '/invoices/unpaid',
      '/reports/sales'
    ]);
    expect(achats?.children?.map(c => c.route)).toEqual([
      '/supplier-invoices',
      '/supplier-invoices/unpaid',
      '/reports/purchases'
    ]);
  });

  it('omits empty sections after permission filtering', () => {
    const auth = TestBed.inject(AuthService);
    setUser(auth, {
      ...companyUser,
      effectivePermissions: ['invoices:read', 'quotes:read', 'delivery_notes:read']
    });
    TestBed.inject(FirmContextService).syncFromUser();

    const nav = TestBed.inject(AppNavService);
    const labels = nav.secondaryNavSections().map(s => s.label);

    expect(labels).toContain('Ventes');
    expect(labels).not.toContain('Stock');
    expect(labels).not.toContain('RH & Paie');
  });
});
