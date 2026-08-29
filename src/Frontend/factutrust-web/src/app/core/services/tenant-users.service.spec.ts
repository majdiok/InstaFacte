import { TestBed } from '@angular/core/testing';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient, withInterceptorsFromDi } from '@angular/common/http';
import { AppModule } from '@core/models/app-module';
import {
  ApiResponse,
  ModuleCatalogDto,
  TenantUsersService,
  UserRole
} from './tenant-users.service';
import { environment } from '@environments/environment';

describe('TenantUsersService.getModuleCatalog', () => {
  let service: TenantUsersService;
  let httpMock: HttpTestingController;

  /** Catalogue factice tel que sérialisé par l'API (enums en chaînes PascalCase, propriétés camelCase). */
  const catalogBody = (role: UserRole): ApiResponse<ModuleCatalogDto> => ({
    success: true,
    data: {
      role,
      modules: [
        {
          // L'API sérialise l'enum en chaîne PascalCase ("Treasury") — le front attend l'enum numérique.
          module: 'Treasury' as unknown as AppModule,
          displayName: 'Trésorerie (paiements)',
          grantable: true,
          defaultEnabled: true,
          features: [
            {
              key: 'read',
              basePermissions: ['payments:read'],
              allowedPermissions: ['payments:read'],
              defaultSelected: true,
              isExtension: false
            },
            {
              key: 'manage',
              basePermissions: [],
              allowedPermissions: ['payments:create', 'payments:update'],
              defaultSelected: false,
              isExtension: true
            },
            {
              // Feature sans aucune allowedPermission → doit être masquée côté UI.
              key: 'forecast_manage',
              basePermissions: [],
              allowedPermissions: [],
              defaultSelected: false,
              isExtension: false
            }
          ]
        },
        {
          module: 'Administration' as unknown as AppModule,
          displayName: 'Paramètres et utilisateurs',
          grantable: false,
          defaultEnabled: false,
          features: []
        }
      ]
    },
    message: null,
    code: null,
    errors: []
  });

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        TenantUsersService,
        provideHttpClient(withInterceptorsFromDi()),
        provideHttpClientTesting()
      ]
    });
    service = TestBed.inject(TenantUsersService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('normalizes PascalCase enum strings into numeric AppModule values', () => {
    let emitted!: ApiResponse<ModuleCatalogDto>;
    service.getModuleCatalog('SalesRep').subscribe(res => (emitted = res));

    const req = httpMock.expectOne(
      `${environment.apiUrl}/tenant-users/module-catalog?role=SalesRep`
    );
    expect(req.request.method).toBe('GET');
    req.flush(catalogBody('SalesRep'));

    expect(emitted.success).toBeTrue();
    const treasury = emitted.data.modules.find(m => m.module === AppModule.Treasury);
    expect(treasury).withContext('Treasury normalisé en enum numérique').toBeDefined();
    expect(treasury!.displayName).toBe('Trésorerie (paiements)');
    expect(treasury!.grantable).toBeTrue();
  });

  it('memoizes the catalog per role (a second call does not re-hit HTTP)', () => {
    service.getModuleCatalog('SalesRep').subscribe();
    httpMock.expectOne(`${environment.apiUrl}/tenant-users/module-catalog?role=SalesRep`).flush(catalogBody('SalesRep'));

    // Second appel, même rôle : servi depuis le cache, aucune nouvelle requête HTTP.
    let cached!: ApiResponse<ModuleCatalogDto>;
    service.getModuleCatalog('SalesRep').subscribe(res => (cached = res));
    httpMock.expectNone(`${environment.apiUrl}/tenant-users/module-catalog?role=SalesRep`);

    expect(cached.data.modules.find(m => m.module === AppModule.Treasury)).toBeDefined();
  });

  it('does not cache a failed load so a retry re-fetches (fail-closed per attempt)', () => {
    service.getModuleCatalog('Accountant').subscribe({
      next: () => fail('expected error'),
      error: () => {}
    });
    httpMock
      .expectOne(`${environment.apiUrl}/tenant-users/module-catalog?role=Accountant`)
      .flush('boom', { status: 500, statusText: 'Server Error' });

    // Retry : nouvelle requête HTTP émise (le cache ne retient pas l'échec).
    service.getModuleCatalog('Accountant').subscribe();
    httpMock.expectOne(`${environment.apiUrl}/tenant-users/module-catalog?role=Accountant`).flush(catalogBody('Accountant'));
  });
});
