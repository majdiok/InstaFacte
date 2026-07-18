import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { AuthService, User } from '../services/auth.service';
import { buildFlatNavSearchEntries, getVisibleNavSearchEntries } from './app-navigation.registry';

const companyUser: User = {
  id: 'u1',
  email: 'company@test.c',
  firstName: 'A',
  lastName: 'B',
  fullName: 'A B',
  role: 'Administrator',
  roleDisplay: 'Administrateur',
  tenantId: '00000000-0000-0000-0000-000000000001',
  companyName: 'Société Test',
  tenantKind: 'Company',
  twoFactorEnabled: false,
  // Requis depuis le fail-closed : hasModule refuse quand le champ est absent.
  enabledModuleIds: [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12],
  effectivePermissions: ['accounting:read', 'invoices:read']
};

const delegatedFirmUser: User = {
  id: 'u2',
  email: 'firm@test.c',
  firstName: 'O',
  lastName: 'G',
  fullName: 'O G',
  role: 'FirmManager',
  roleDisplay: 'Responsable cabinet',
  tenantId: '00000000-0000-0000-0000-000000000002',
  companyName: 'Cabinet Test',
  tenantKind: 'AccountingFirm',
  accessMode: 'delegated',
  contextTenantId: '00000000-0000-0000-0000-000000000003',
  contextCompanyName: 'Ste Bouzgarou',
  twoFactorEnabled: false,
  enabledModuleIds: [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12],
  effectivePermissions: ['accounting:read', 'treasury:read', 'invoices:read']
};

function setUser(auth: AuthService, u: User | null): void {
  (auth as unknown as { userSignal: { set: (x: User | null) => void } }).userSignal.set(u);
}

describe('app-navigation.registry', () => {
  describe('buildFlatNavSearchEntries', () => {
    it('does not duplicate routes', () => {
      const entries = buildFlatNavSearchEntries();
      const routes = entries.map(e => e.route);
      expect(routes.length).toBe(new Set(routes).size);
    });

    it('includes invoices list with keywords', () => {
      const invoices = buildFlatNavSearchEntries().find(e => e.route === '/invoices');
      expect(invoices).toBeDefined();
      expect(invoices!.label.toLowerCase()).toContain('facture');
      expect(invoices!.keywords?.some(k => k.toLowerCase().includes('facture'))).toBe(true);
    });

    it('includes quick-access create routes', () => {
      const routes = buildFlatNavSearchEntries().map(e => e.route);
      expect(routes).toContain('/invoices/new');
    });

    it('assigns stable ids', () => {
      const entries = buildFlatNavSearchEntries();
      expect(entries.every(e => e.id.length > 0)).toBe(true);
      expect(entries.map(e => e.id).length).toBe(new Set(entries.map(e => e.id)).size);
    });

    it('does not include documentation routes', () => {
      const routes = buildFlatNavSearchEntries().map(e => e.route);
      expect(routes.some(route => route.startsWith('/documentation'))).toBe(false);
    });
  });

  describe('getVisibleNavSearchEntries — company accounting restriction', () => {
    beforeEach(() => {
      TestBed.configureTestingModule({
        providers: [provideHttpClient(), provideHttpClientTesting()]
      });
    });

    it('excludes operational accounting routes for company users', () => {
      const auth = TestBed.inject(AuthService);
      setUser(auth, companyUser);

      const labels = getVisibleNavSearchEntries(auth).map(e => e.label);
      expect(labels).not.toContain('Saisie manuelle');
      expect(labels).not.toContain('Plan comptable');
    });

    it('excludes documentation routes for company users', () => {
      const auth = TestBed.inject(AuthService);
      setUser(auth, companyUser);

      const routes = getVisibleNavSearchEntries(auth).map(e => e.route);
      expect(routes.some(route => route.startsWith('/documentation'))).toBe(false);
    });

    it('includes allowed accounting state routes for company users', () => {
      const auth = TestBed.inject(AuthService);
      setUser(auth, companyUser);

      const routes = getVisibleNavSearchEntries(auth).map(e => e.route);
      expect(routes).toContain('/accounting/journal');
      expect(routes).toContain('/accounting/vat-declaration');
    });

    it('includes accounting assistant route when company user has ai:chat', () => {
      const auth = TestBed.inject(AuthService);
      setUser(auth, { ...companyUser, effectivePermissions: ['accounting:read', 'ai:chat'] });

      const routes = getVisibleNavSearchEntries(auth).map(e => e.route);
      expect(routes).toContain('/ai-assistant/comptabilite');
    });

    it('excludes accounting assistant route when company user lacks ai:chat', () => {
      const auth = TestBed.inject(AuthService);
      setUser(auth, companyUser);

      const routes = getVisibleNavSearchEntries(auth).map(e => e.route);
      expect(routes).not.toContain('/ai-assistant/comptabilite');
    });
  });

  describe('getVisibleNavSearchEntries — firm delegated accounting restriction', () => {
    beforeEach(() => {
      TestBed.configureTestingModule({
        providers: [provideHttpClient(), provideHttpClientTesting()]
      });
    });

    it('includes declaration search entries for delegated firm users', () => {
      const auth = TestBed.inject(AuthService);
      setUser(auth, delegatedFirmUser);

      const entries = getVisibleNavSearchEntries(auth);
      const routes = entries.map(e => e.route);
      const labels = entries.map(e => e.label);

      expect(routes).toContain('/accounting/vat-declaration');
      expect(labels).toContain('Declarations · TVA');
    });

    it('keeps vat-declaration visible for company users', () => {
      const auth = TestBed.inject(AuthService);
      setUser(auth, companyUser);

      const routes = getVisibleNavSearchEntries(auth).map(e => e.route);
      expect(routes).toContain('/accounting/vat-declaration');
    });
  });
});
