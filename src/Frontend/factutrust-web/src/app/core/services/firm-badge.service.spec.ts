import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { of, throwError } from 'rxjs';
import { AuthService, User } from './auth.service';
import { FirmAssignmentService } from './firm-assignment.service';
import { FirmBadgeService } from './firm-badge.service';

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
  enabledModuleIds: [0, 1, 2, 3],
  effectivePermissions: ['firm:manage']
};

const delegatedUser: User = {
  ...firmUser,
  accessMode: 'delegated',
  contextTenantId: '00000000-0000-0000-0000-000000000003',
  contextCompanyName: 'Ste Cliente'
};

function setUser(auth: AuthService, u: User | null): void {
  (auth as unknown as { userSignal: { set: (x: User | null) => void } }).userSignal.set(u);
}

describe('FirmBadgeService', () => {
  let getIncoming: jasmine.Spy;

  beforeEach(() => {
    getIncoming = jasmine
      .createSpy('getIncomingInvitations')
      .and.returnValue(of({ success: true, data: [{ id: 'a' }, { id: 'b' }] }));

    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        {
          provide: FirmAssignmentService,
          useValue: { getIncomingInvitations: getIncoming }
        }
      ]
    });
  });

  it('refresh compte les invitations en attente pour un cabinet natif', () => {
    const auth = TestBed.inject(AuthService);
    setUser(auth, firmUser);

    const service = TestBed.inject(FirmBadgeService);
    service.refresh();

    expect(getIncoming).toHaveBeenCalled();
    expect(service.pendingInvitationsCount()).toBe(2);
  });

  it('refresh remet le compteur à zéro hors contexte cabinet natif (délégué)', () => {
    const auth = TestBed.inject(AuthService);
    setUser(auth, delegatedUser);

    const service = TestBed.inject(FirmBadgeService);
    service.pendingInvitationsCount.set(3);
    service.refresh();

    expect(getIncoming).not.toHaveBeenCalled();
    expect(service.pendingInvitationsCount()).toBe(0);
  });

  it('invalidate déclenche un refresh (badge mis à jour après accepter/refuser)', () => {
    const auth = TestBed.inject(AuthService);
    setUser(auth, firmUser);

    const service = TestBed.inject(FirmBadgeService);
    service.invalidate();

    expect(service.pendingInvitationsCount()).toBe(2);
  });

  it('erreur réseau silencieuse : la valeur courante est conservée', () => {
    getIncoming.and.returnValue(throwError(() => new Error('réseau')));
    const auth = TestBed.inject(AuthService);
    setUser(auth, firmUser);

    const service = TestBed.inject(FirmBadgeService);
    service.pendingInvitationsCount.set(1);
    service.refresh();

    expect(service.pendingInvitationsCount()).toBe(1);
  });
});
