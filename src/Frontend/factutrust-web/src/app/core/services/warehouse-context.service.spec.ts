import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { AuthService, User } from './auth.service';
import { WarehouseContextService } from './warehouse-context.service';
import { writeWarehouseIdForUser } from './warehouse-storage';

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
    (auth as unknown as { userSignal: { set: (x: User | null) => void } }).userSignal.set(firmUser);
    writeWarehouseIdForUser(firmUser.id, firmUser.tenantId, 'wh-1');

    TestBed.inject(WarehouseContextService);

    httpMock.expectNone(r => r.url.includes('/warehouses'));
    expect(TestBed.inject(WarehouseContextService).selectedWarehouseId()).toBeNull();
  });
});
