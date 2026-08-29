import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter, Router } from '@angular/router';
import { AuthService, User } from './auth.service';
import { WarehouseContextService } from './warehouse-context.service';
import { readWarehouseIdForUser, writeWarehouseIdForUser } from './warehouse-storage';

const firmUser: User = {
  id: 'u-firm',
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
  effectivePermissions: ['firm:manage']
};

const companyUserNoStock: User = {
  id: 'u-salesrep',
  email: 'salesrep@test.c',
  firstName: 'S',
  lastName: 'R',
  fullName: 'S R',
  role: 'SalesRep',
  roleDisplay: 'Commercial',
  tenantId: '00000000-0000-0000-0000-000000000004',
  companyName: 'Ste Test',
  tenantKind: 'Company',
  twoFactorEnabled: false,
  effectivePermissions: ['invoices:read', 'clients:read']
};

const companyUserWithStock: User = {
  ...companyUserNoStock,
  id: 'u-purchaser',
  effectivePermissions: ['invoices:read', 'clients:read', 'stock:read']
};

function setUser(auth: AuthService, u: User | null): void {
  (auth as unknown as { userSignal: { set: (x: User | null) => void } }).userSignal.set(u);
}

describe('WarehouseContextService — accounting firm', () => {
  let httpMock: HttpTestingController;

  beforeEach(() => {
    localStorage.clear();
    sessionStorage.clear();
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])]
    });
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('does not call getWarehouses when user is accounting firm with stored warehouse id', () => {
    const auth = TestBed.inject(AuthService);
    setUser(auth, firmUser);
    writeWarehouseIdForUser(firmUser.id, firmUser.tenantId, 'wh-1');

    TestBed.inject(WarehouseContextService);

    httpMock.expectNone(r => r.url.includes('/warehouses'));
    expect(TestBed.inject(WarehouseContextService).selectedWarehouseId()).toBeNull();
  });
});

// §7.3.5 — les trois chemins d'appel de WarehouseContextService sans stock:read.
describe('WarehouseContextService — user without stock:read', () => {
  let httpMock: HttpTestingController;

  beforeEach(() => {
    localStorage.clear();
    sessionStorage.clear();
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])]
    });
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('constructor pipeline never calls getWarehouses and purges the persisted stale selection', () => {
    const auth = TestBed.inject(AuthService);
    setUser(auth, companyUserNoStock);
    writeWarehouseIdForUser(companyUserNoStock.id, companyUserNoStock.tenantId, 'wh-stale');
    expect(readWarehouseIdForUser(companyUserNoStock.id, companyUserNoStock.tenantId)).toBe('wh-stale');

    const service = TestBed.inject(WarehouseContextService);
    // Le pipeline du constructeur (toObservable → switchMap) s'exécute via un effect planifié en
    // microtâche : on le force à s'exécuter avant les assertions.
    TestBed.flushEffects();

    httpMock.expectNone(r => r.url.includes('/warehouses'));
    expect(readWarehouseIdForUser(companyUserNoStock.id, companyUserNoStock.tenantId)).toBeNull();
    expect(service.resolvedWarehouse()).toBeNull();
  });

  it('navigateAfterSuccessfulAuth navigates directly without calling getWarehouses', () => {
    const auth = TestBed.inject(AuthService);
    setUser(auth, companyUserNoStock);
    writeWarehouseIdForUser(companyUserNoStock.id, companyUserNoStock.tenantId, 'wh-stale');

    const service = TestBed.inject(WarehouseContextService);
    const router = TestBed.inject(Router);
    const navigateSpy = spyOn(router, 'navigateByUrl');

    service.navigateAfterSuccessfulAuth('/dashboard');

    httpMock.expectNone(r => r.url.includes('/warehouses'));
    expect(navigateSpy).toHaveBeenCalledWith('/dashboard');
    expect(readWarehouseIdForUser(companyUserNoStock.id, companyUserNoStock.tenantId)).toBeNull();
  });

  it('resolveForAuthenticatedRoute resolves true without calling getWarehouses', done => {
    const auth = TestBed.inject(AuthService);
    setUser(auth, companyUserNoStock);
    writeWarehouseIdForUser(companyUserNoStock.id, companyUserNoStock.tenantId, 'wh-stale');

    const service = TestBed.inject(WarehouseContextService);

    service.resolveForAuthenticatedRoute('/dashboard').subscribe(result => {
      expect(result).toBe(true);
      httpMock.expectNone(r => r.url.includes('/warehouses'));
      expect(readWarehouseIdForUser(companyUserNoStock.id, companyUserNoStock.tenantId)).toBeNull();
      done();
    });
  });
});

// Non-régression : avec stock:read, les trois chemins appellent toujours getWarehouses.
describe('WarehouseContextService — user with stock:read (non-régression)', () => {
  let httpMock: HttpTestingController;

  beforeEach(() => {
    localStorage.clear();
    sessionStorage.clear();
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])]
    });
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('constructor pipeline calls getWarehouses when a warehouse id is persisted', () => {
    const auth = TestBed.inject(AuthService);
    setUser(auth, companyUserWithStock);
    writeWarehouseIdForUser(companyUserWithStock.id, companyUserWithStock.tenantId, 'wh-1');

    const service = TestBed.inject(WarehouseContextService);
    // Le pipeline du constructeur s'exécute via un effect planifié en microtâche.
    TestBed.flushEffects();

    const req = httpMock.expectOne(r => r.url.includes('/warehouses'));
    req.flush({ success: true, data: [{ id: 'wh-1', name: 'Entrepôt 1' }], message: null, errors: [] });
    expect(service.resolvedWarehouse()?.id).toBe('wh-1');
  });

  it('navigateAfterSuccessfulAuth calls getWarehouses', () => {
    const auth = TestBed.inject(AuthService);
    setUser(auth, companyUserWithStock);

    const service = TestBed.inject(WarehouseContextService);
    const router = TestBed.inject(Router);
    const navigateSpy = spyOn(router, 'navigateByUrl');

    service.navigateAfterSuccessfulAuth('/dashboard');

    const req = httpMock.expectOne(r => r.url.includes('/warehouses'));
    req.flush({ success: true, data: [], message: null, errors: [] });
    expect(navigateSpy).toHaveBeenCalledWith('/dashboard');
  });

  it('resolveForAuthenticatedRoute calls getWarehouses', done => {
    const auth = TestBed.inject(AuthService);
    setUser(auth, companyUserWithStock);

    const service = TestBed.inject(WarehouseContextService);

    service.resolveForAuthenticatedRoute('/dashboard').subscribe(result => {
      expect(result).toBe(true);
      done();
    });

    const req = httpMock.expectOne(r => r.url.includes('/warehouses'));
    req.flush({ success: true, data: [], message: null, errors: [] });
  });
});
