import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { ExchangeShellComponent } from './exchange-shell.component';
import { AuthService } from '@core/services/auth.service';
import { ExchangeService } from '@core/services/exchange.service';
import { ExchangeBadgeService } from '@core/services/exchange-badge.service';
import { FirmAssignmentService } from '@core/services/firm-assignment.service';
import { signal } from '@angular/core';

describe('ExchangeShellComponent', () => {
  let fixture: ComponentFixture<ExchangeShellComponent>;

  beforeEach(async () => {
    const auth = {
      isAccountingFirm: () => false,
      user: () => ({
        id: 'u1',
        role: 'Administrator',
        fullName: 'Admin'
      }),
      isAdmin: () => true
    };
    const exchange = jasmine.createSpyObj('ExchangeService', [
      'listThreads',
      'ensureThread',
      'getMessages',
      'listRequests',
      'listTasks',
      'listDocuments',
      'getHistory',
      'getThread',
      'sendMessage'
    ]);
    exchange.listThreads.and.returnValue(of({ success: true, data: [], message: null, errors: [] }));
    const assignments = jasmine.createSpyObj('FirmAssignmentService', ['getCompanyCurrent', 'getActiveClients']);
    assignments.getCompanyCurrent.and.returnValue(
      of({ success: true, data: null, message: null, errors: [] })
    );
    const badge = {
      invalidate: jasmine.createSpy('invalidate'),
      unreadCount: signal(0),
      openRequests: signal(0)
    };

    await TestBed.configureTestingModule({
      imports: [ExchangeShellComponent],
      providers: [
        provideRouter([]),
        { provide: AuthService, useValue: auth },
        { provide: ExchangeService, useValue: exchange },
        { provide: FirmAssignmentService, useValue: assignments },
        { provide: ExchangeBadgeService, useValue: badge }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(ExchangeShellComponent);
    fixture.detectChanges();
  });

  it('shows empty hint when company has no active firm link', () => {
    const el: HTMLElement = fixture.nativeElement;
    expect(el.textContent).toContain('Cabinet comptable');
  });
});
