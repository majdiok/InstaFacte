import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { Router, provideRouter, UrlTree } from '@angular/router';
import { AuthService, User } from '@core/services/auth.service';
import { firmManagerGuard } from './firm-manager.guard';

describe('firmManagerGuard', () => {
  const manager: User = {
    id: 'm1',
    email: 'manager@test.c',
    firstName: 'Marie',
    lastName: 'Manager',
    fullName: 'Marie Manager',
    role: 'FirmManager',
    roleDisplay: 'Responsable cabinet',
    tenantId: '00000000-0000-0000-0000-000000000002',
    companyName: 'Cabinet Test',
    tenantKind: 'AccountingFirm',
    twoFactorEnabled: false,
    enabledModuleIds: [],
    effectivePermissions: ['firm:manage']
  };

  const accountant: User = {
    ...manager,
    id: 'a1',
    role: 'FirmAccountant',
    roleDisplay: 'Comptable cabinet',
    effectivePermissions: ['accounting:read']
  };

  function setUser(auth: AuthService, u: User | null): void {
    (auth as unknown as { userSignal: { set: (x: User | null) => void } }).userSignal.set(u);
  }

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()]
    });
  });

  it('allows FirmManager', async () => {
    const auth = TestBed.inject(AuthService);
    setUser(auth, manager);
    const result = await TestBed.runInInjectionContext(() =>
      firmManagerGuard({} as never, { url: '/firm/collaborateurs' } as never)
    );
    expect(result).toBe(true);
  });

  it('redirects FirmAccountant to access-denied', async () => {
    const auth = TestBed.inject(AuthService);
    setUser(auth, accountant);
    const router = TestBed.inject(Router);
    const result = await TestBed.runInInjectionContext(() =>
      firmManagerGuard({} as never, { url: '/firm/billing/invoices?tab=open' } as never)
    );
    expect(result).toBeInstanceOf(UrlTree);
    expect(router.serializeUrl(result as UrlTree)).toBe(
      '/access-denied?returnUrl=%2Ffirm%2Fbilling%2Finvoices'
    );
  });
});
