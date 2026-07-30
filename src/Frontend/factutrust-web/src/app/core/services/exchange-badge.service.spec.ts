import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { ExchangeBadgeService } from './exchange-badge.service';
import { AuthService } from './auth.service';
import { ExchangeService } from './exchange.service';

describe('ExchangeBadgeService', () => {
  let service: ExchangeBadgeService;
  let auth: jasmine.SpyObj<AuthService>;
  let exchange: jasmine.SpyObj<ExchangeService>;

  beforeEach(() => {
    auth = jasmine.createSpyObj('AuthService', ['isAuthenticated', 'isDelegatedMode', 'user']);
    exchange = jasmine.createSpyObj('ExchangeService', ['getUnreadSummary']);
    auth.isAuthenticated.and.returnValue(true);
    auth.isDelegatedMode.and.returnValue(false);
    auth.user.and.returnValue({
      id: '1',
      email: 'a@b.c',
      firstName: 'A',
      lastName: 'B',
      fullName: 'A B',
      role: 'FirmManager',
      roleDisplay: 'Manager',
      tenantId: 't',
      companyName: 'Cabinet',
      twoFactorEnabled: false
    } as ReturnType<AuthService['user']>);
    exchange.getUnreadSummary.and.returnValue(
      of({
        success: true,
        data: { totalUnreadMessages: 3, openRequests: 1, threads: [] },
        message: null,
        errors: []
      })
    );

    TestBed.configureTestingModule({
      providers: [
        ExchangeBadgeService,
        { provide: AuthService, useValue: auth },
        { provide: ExchangeService, useValue: exchange }
      ]
    });
    service = TestBed.inject(ExchangeBadgeService);
  });

  it('refreshes unread count for firm manager', () => {
    service.refresh();
    expect(service.unreadCount()).toBe(3);
    expect(service.openRequests()).toBe(1);
  });

  it('clears badge when delegated', () => {
    auth.isDelegatedMode.and.returnValue(true);
    service.unreadCount.set(9);
    service.refresh();
    expect(service.unreadCount()).toBe(0);
  });

  it('ignores API errors silently', () => {
    exchange.getUnreadSummary.and.returnValue(throwError(() => new Error('x')));
    service.unreadCount.set(2);
    service.refresh();
    expect(service.unreadCount()).toBe(2);
  });
});
