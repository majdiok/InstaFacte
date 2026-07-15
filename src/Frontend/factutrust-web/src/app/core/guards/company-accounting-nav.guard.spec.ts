import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { Router, provideRouter } from '@angular/router';
import { AuthService, User } from '@core/services/auth.service';
import { companyAccountingNavGuard } from './company-accounting-nav.guard';

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
  twoFactorEnabled: false
};

const delegatedUser: User = {
  ...firmUser,
  accessMode: 'delegated',
  contextTenantId: '00000000-0000-0000-0000-000000000003',
  contextCompanyName: 'Ste Bouzgarou'
};

const companyUser: User = {
  ...firmUser,
  role: 'Administrator',
  roleDisplay: 'Administrateur',
  tenantKind: 'Company',
  companyName: 'Société Test'
};

function setUser(auth: AuthService, u: User | null): void {
  (auth as unknown as { userSignal: { set: (x: User | null) => void } }).userSignal.set(u);
}

describe('companyAccountingNavGuard', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()]
    });
  });

  it('blocks company user on /accounting/manual-entry', async () => {
    const auth = TestBed.inject(AuthService);
    setUser(auth, companyUser);
    const result = await TestBed.runInInjectionContext(() =>
      companyAccountingNavGuard({} as never, { url: '/accounting/manual-entry' } as never)
    );
    expect(result).not.toBe(true);
    const router = TestBed.inject(Router);
    expect(router.serializeUrl(result as never)).toContain('/access-denied');
  });

  it('allows company user on /accounting/balance', async () => {
    const auth = TestBed.inject(AuthService);
    setUser(auth, companyUser);
    const result = await TestBed.runInInjectionContext(() =>
      companyAccountingNavGuard({} as never, { url: '/accounting/balance' } as never)
    );
    expect(result).toBe(true);
  });

  it('allows delegated firm user on /accounting/manual-entry', async () => {
    const auth = TestBed.inject(AuthService);
    setUser(auth, delegatedUser);
    const result = await TestBed.runInInjectionContext(() =>
      companyAccountingNavGuard({} as never, { url: '/accounting/manual-entry' } as never)
    );
    expect(result).toBe(true);
  });

  it('allows native firm user on /firm/dashboard', async () => {
    const auth = TestBed.inject(AuthService);
    setUser(auth, firmUser);
    const result = await TestBed.runInInjectionContext(() =>
      companyAccountingNavGuard({} as never, { url: '/firm/dashboard' } as never)
    );
    expect(result).toBe(true);
  });

  it('allows company user on /ai-assistant/comptabilite', async () => {
    const auth = TestBed.inject(AuthService);
    setUser(auth, companyUser);
    const result = await TestBed.runInInjectionContext(() =>
      companyAccountingNavGuard({} as never, { url: '/ai-assistant/comptabilite' } as never)
    );
    expect(result).toBe(true);
  });

  it('allows company user on /ai-assistant/ventes (not guarded by company accounting nav)', async () => {
    const auth = TestBed.inject(AuthService);
    setUser(auth, companyUser);
    const result = await TestBed.runInInjectionContext(() =>
      companyAccountingNavGuard({} as never, { url: '/ai-assistant/ventes' } as never)
    );
    expect(result).toBe(true);
  });
});
