import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { Router, provideRouter, UrlTree } from '@angular/router';
import { AuthService, User } from '@core/services/auth.service';
import { FirmFeatureFlagsService } from '@core/services/firm-feature-flags.service';
import { firmRevisionFeatureGuard } from './firm-revision.guard';

describe('firmRevisionFeatureGuard', () => {
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
    effectivePermissions: ['firm:manage', 'firm:revision:view', 'firm:revision:manage']
  };

  /** Collaborateur cabinet : consultation seule, mais accès à l'écran. */
  const accountant: User = {
    ...manager,
    id: 'a1',
    role: 'FirmAccountant',
    roleDisplay: 'Comptable cabinet',
    effectivePermissions: ['accounting:read', 'firm:revision:view']
  };

  /** Connecté avant l'activation du module : le JWT ne porte pas encore la permission. */
  const staleToken: User = {
    ...manager,
    id: 's1',
    effectivePermissions: ['firm:manage']
  };

  function setUser(auth: AuthService, u: User | null): void {
    (auth as unknown as { userSignal: { set: (x: User | null) => void } }).userSignal.set(u);
  }

  function run(): boolean | UrlTree {
    return TestBed.runInInjectionContext(() =>
      firmRevisionFeatureGuard({} as never, { url: '/firm/revision' } as never)
    ) as boolean | UrlTree;
  }

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()]
    });
    // Le service lit localStorage, partagé entre specs : sans reset le résultat dépend de
    // l'ordre d'exécution.
    TestBed.inject(FirmFeatureFlagsService).reset();
  });

  afterEach(() => {
    // `setFlag` persiste dans localStorage, partagé par toute la suite Karma : sans ce nettoyage
    // un drapeau éteint ici fuirait vers les specs de navigation.
    TestBed.inject(FirmFeatureFlagsService).reset();
  });

  it('laisse passer le responsable de cabinet', () => {
    setUser(TestBed.inject(AuthService), manager);

    expect(run()).toBe(true);
  });

  it('laisse passer le collaborateur cabinet (consultation)', () => {
    setUser(TestBed.inject(AuthService), accountant);

    expect(run()).toBe(true);
  });

  it('redirige vers le tableau de bord quand la permission manque', () => {
    setUser(TestBed.inject(AuthService), staleToken);
    const router = TestBed.inject(Router);

    const result = run();

    expect(result).toBeInstanceOf(UrlTree);
    expect(router.serializeUrl(result as UrlTree)).toBe('/firm/dashboard');
  });

  it('redirige quand le drapeau est éteint, permission présente', () => {
    setUser(TestBed.inject(AuthService), manager);
    TestBed.inject(FirmFeatureFlagsService).setFlag('firmRevision', false);
    const router = TestBed.inject(Router);

    const result = run();

    expect(result).toBeInstanceOf(UrlTree);
    expect(router.serializeUrl(result as UrlTree)).toBe('/firm/dashboard');
  });
});
