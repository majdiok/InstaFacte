import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { signal } from '@angular/core';
import { AuthService } from '@core/services/auth.service';
import { FirmAssignmentService } from '@core/services/firm-assignment.service';
import { FirmFiscalScheduleService } from '@core/services/firm-fiscal-schedule.service';
import { ToastService } from '@core/services/toast.service';
import { FiscalScheduleService } from '../services/fiscal-schedule.service';
import { FiscalScheduleComponent } from './fiscal-schedule.component';

describe('FiscalScheduleComponent', () => {
  let fixture: ComponentFixture<FiscalScheduleComponent>;
  let component: FiscalScheduleComponent;

  const scheduleSpy = jasmine.createSpyObj<FiscalScheduleService>('FiscalScheduleService', [
    'getSchedule',
    'getAssignableUsers',
    'create',
    'ensureFiscalYear'
  ]);
  const firmScheduleSpy = jasmine.createSpyObj<FirmFiscalScheduleService>('FirmFiscalScheduleService', ['getSchedule']);
  const firmAssignmentSpy = jasmine.createSpyObj<FirmAssignmentService>('FirmAssignmentService', ['getActiveClients']);
  const authSpy = jasmine.createSpyObj<AuthService>('AuthService', ['hasPermission']);
  Object.defineProperty(authSpy, 'user', { value: signal({ companyName: 'Test Co' }) });
  const toastSpy = jasmine.createSpyObj<ToastService>('ToastService', ['add']);

  beforeEach(async () => {
    scheduleSpy.getSchedule.and.returnValue(of({
      success: true,
      data: {
        summary: {
          upcomingWithin7DaysCount: 0,
          upcomingWithin7DaysAmount: 0,
          upcomingAfter7DaysCount: 0,
          upcomingAfter7DaysAmount: 0,
          overdueCount: 0,
          overdueAmount: 0,
          depositedThisMonthCount: 0,
          depositedThisMonthAmount: 0,
          totalCount: 0,
          totalAmount: 0
        },
        items: [],
        page: 1,
        pageSize: 25,
        totalCount: 0
      },
      message: null,
      errors: []
    }));
    firmAssignmentSpy.getActiveClients.and.returnValue(of({ success: true, data: [], message: null, errors: [] }));
    scheduleSpy.getAssignableUsers.and.returnValue(of({ success: true, data: [], message: null, errors: [] }));
    authSpy.hasPermission.and.returnValue(true);

    await TestBed.configureTestingModule({
      imports: [FiscalScheduleComponent],
      providers: [
        provideRouter([]),
        { provide: FiscalScheduleService, useValue: scheduleSpy },
        { provide: FirmFiscalScheduleService, useValue: firmScheduleSpy },
        { provide: FirmAssignmentService, useValue: firmAssignmentSpy },
        { provide: AuthService, useValue: authSpy },
        { provide: ToastService, useValue: toastSpy }
      ]
    }).compileComponents();
    fixture = TestBed.createComponent(FiscalScheduleComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('opens create dialog with visible fiscal modal content', () => {
    component.openCreate();
    fixture.detectChanges();

    const backdrop = fixture.nativeElement.querySelector('.fiscal-modal-backdrop') as HTMLElement;
    const modal = fixture.nativeElement.querySelector('.fiscal-modal') as HTMLElement;

    expect(backdrop).toBeTruthy();
    expect(modal).toBeTruthy();
    expect(getComputedStyle(modal).display).not.toBe('none');
  });

  it('closes create dialog and removes backdrop', () => {
    component.openCreate();
    fixture.detectChanges();
    component.closeForm();
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('.fiscal-modal-backdrop')).toBeNull();
  });

  it('does not open create dialog without accounting:create permission', () => {
    authSpy.hasPermission.and.returnValue(false);
    component.openCreate();
    fixture.detectChanges();

    expect(component.formOpen).toBeFalse();
    expect(fixture.nativeElement.querySelector('.fiscal-modal')).toBeNull();
  });

  it('populates responsible users from assignable-users on init', () => {
    scheduleSpy.getAssignableUsers.and.returnValue(of({
      success: true,
      data: [{ id: 'u1', displayName: 'Sonia Ben Ali' }],
      message: null,
      errors: []
    }));

    const fx = TestBed.createComponent(FiscalScheduleComponent);
    fx.detectChanges();

    expect(fx.componentInstance.users.length).toBe(1);
    expect(fx.componentInstance.users[0].displayName).toBe('Sonia Ben Ali');
  });

  it('keeps the screen usable when assignable-users fails (no users, no error)', () => {
    scheduleSpy.getAssignableUsers.and.returnValue(throwError(() => new Error('403')));

    const fx = TestBed.createComponent(FiscalScheduleComponent);
    fx.detectChanges();

    expect(fx.componentInstance.users).toEqual([]);
    // Le chargement de la liste continue malgré l'échec du lookup responsables.
    expect(scheduleSpy.getSchedule).toHaveBeenCalled();
  });
});
