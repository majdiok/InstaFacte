import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { AuthService, User } from '@core/services/auth.service';
import { StudioNavService } from './studio-nav.service';
import { StudioService } from './studio.service';
import { of } from 'rxjs';

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
  accessMode: 'native',
  twoFactorEnabled: false,
  effectivePermissions: ['firm:manage', 'custom_records:read']
};

describe('StudioNavService — accounting firm native', () => {
  let studioSpy: jasmine.SpyObj<StudioService>;

  beforeEach(() => {
    studioSpy = jasmine.createSpyObj('StudioService', ['getNav']);
    studioSpy.getNav.and.returnValue(of({ success: true, data: [], message: null, errors: [] }));

    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        { provide: StudioService, useValue: studioSpy }
      ]
    });
  });

  it('does not refresh studio nav for native accounting firm user', () => {
    const auth = TestBed.inject(AuthService);
    (auth as unknown as { userSignal: { set: (x: User | null) => void } }).userSignal.set(firmUser);

    TestBed.inject(StudioNavService);

    expect(studioSpy.getNav).not.toHaveBeenCalled();
    expect(TestBed.inject(StudioNavService).nodes()).toEqual([]);
  });
});
