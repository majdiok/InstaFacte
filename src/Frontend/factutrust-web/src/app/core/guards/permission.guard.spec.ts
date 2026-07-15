import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { Router, provideRouter } from '@angular/router';
import { AuthService, User } from '@core/services/auth.service';
import { permissionGuard } from './permission.guard';
import { AppModule } from '@core/models/app-module';
import { PERMISSIONS } from '@core/config/permission-keys';

describe('permissionGuard', () => {
  const user: User = {
    id: 'u1',
    email: 'a@b.c',
    firstName: 'A',
    lastName: 'B',
    fullName: 'A B',
    role: 'Accountant',
    roleDisplay: 'Compta',
    tenantId: '00000000-0000-0000-0000-000000000001',
    companyName: 'Co',
    twoFactorEnabled: false,
    effectivePermissions: [PERMISSIONS.products.create],
    enabledModuleIds: [AppModule.Products]
  };

  function setUser(auth: AuthService, u: User | null): void {
    (auth as unknown as { userSignal: { set: (x: User | null) => void } }).userSignal.set(u);
  }

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()]
    });
  });

  it('allows when permissions satisfied', async () => {
    const auth = TestBed.inject(AuthService);
    setUser(auth, user);
    const result = await TestBed.runInInjectionContext(() =>
      permissionGuard(
        { data: { permissions: [PERMISSIONS.products.create] } } as never,
        { url: '/products/new' } as never
      )
    );
    expect(result).toBe(true);
  });

  it('redirects when permission missing', async () => {
    const auth = TestBed.inject(AuthService);
    setUser(auth, { ...user, effectivePermissions: ['products:read'] });
    const router = TestBed.inject(Router);
    spyOn(router, 'navigate');
    const result = await TestBed.runInInjectionContext(() =>
      permissionGuard(
        { data: { permissions: [PERMISSIONS.products.create] } } as never,
        { url: '/products/new' } as never
      )
    );
    expect(result).toBe(false);
    expect(router.navigate).toHaveBeenCalledWith(['/access-denied'], {
      queryParams: { returnUrl: '/products/new' }
    });
  });

  it('checks modules', async () => {
    const auth = TestBed.inject(AuthService);
    setUser(auth, { ...user, enabledModuleIds: [AppModule.Clients] });
    const router = TestBed.inject(Router);
    spyOn(router, 'navigate');
    const result = await TestBed.runInInjectionContext(() =>
      permissionGuard(
        { data: { modules: [AppModule.Products], permissions: [PERMISSIONS.products.create] } } as never,
        { url: '/x' } as never
      )
    );
    expect(result).toBe(false);
  });
});
