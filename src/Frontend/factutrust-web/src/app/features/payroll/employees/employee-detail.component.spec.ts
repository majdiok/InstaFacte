import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, provideRouter } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { of } from 'rxjs';
import { EmployeeDetailComponent } from './employee-detail.component';
import { EmployeeService } from '@core/services/employee.service';
import { AuthService } from '@core/services/auth.service';
import { ToastService } from '@core/services/toast.service';
import { ConfirmationService } from '@core/services/confirmation.service';
import { PayrollService } from '@core/services/payroll.service';
import { PERMISSIONS } from '@core/config/permission-keys';

describe('EmployeeDetailComponent', () => {
  const employeeServiceSpy = jasmine.createSpyObj<EmployeeService>('EmployeeService', ['getById', 'listLeaves', 'listAdvances', 'getLeaveBalance']);
  const toastSpy = jasmine.createSpyObj<ToastService>('ToastService', ['add']);
  const confirmSpy = jasmine.createSpyObj<ConfirmationService>('ConfirmationService', ['confirm']);

  const employeeData = {
    id: 'e1',
    employeeNumber: 'EMP-1',
    firstName: 'Ali',
    lastName: 'Ben',
    fullName: 'Ali Ben',
    maritalStatus: 'Single',
    maritalStatusDisplay: 'Célibataire',
    isHeadOfFamily: false,
    dependentChildren: 0,
    studentChildren: 0,
    disabledChildren: 0,
    dependentParents: 0,
    hireDate: '2026-01-01',
    isActive: true,
    contracts: [] as never[]
  };

  function createAuthMock(canManage: boolean) {
    return {
      hasPermission: (p: string) => canManage && p === PERMISSIONS.payroll.manageEmployees,
      isAccountingFirm: () => !canManage,
      isDelegatedMode: () => !canManage
    };
  }

  async function setup(canManage: boolean): Promise<ComponentFixture<EmployeeDetailComponent>> {
    employeeServiceSpy.getById.and.returnValue(of({ success: true, data: employeeData }));
    employeeServiceSpy.listLeaves.and.returnValue(of({ success: true, data: [] }));
    employeeServiceSpy.listAdvances.and.returnValue(of({ success: true, data: [] }));
    employeeServiceSpy.getLeaveBalance.and.returnValue(of({
      success: true,
      data: { employeeId: 'e1', year: 2026, openingBalance: 0, accruedInYear: 0, totalAcquired: 0, consumed: 0, remaining: 0 }
    }));

    await TestBed.configureTestingModule({
      imports: [EmployeeDetailComponent],
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: EmployeeService, useValue: employeeServiceSpy },
        { provide: PayrollService, useValue: {
          getParameters: () => of({ success: true, data: { monthlySmig: 528.32 } })
        } },
        { provide: ToastService, useValue: toastSpy },
        { provide: ConfirmationService, useValue: confirmSpy },
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: { get: () => 'e1' } } } },
        { provide: AuthService, useValue: createAuthMock(canManage) }
      ]
    }).compileComponents();

    const fixture = TestBed.createComponent(EmployeeDetailComponent);
    fixture.detectChanges();
    return fixture;
  }

  it('hides manage actions when read-only', async () => {
    const fixture = await setup(false);
    const el = fixture.nativeElement as HTMLElement;
    expect(el.textContent).not.toContain('Désactiver');
    expect(el.textContent).not.toContain('Ajouter un contrat');
    expect(el.textContent).not.toContain('Ajouter un congé');
    expect(el.textContent).not.toContain('Nouvelle avance');
  });

  it('shows manage actions when user can manage employees', async () => {
    const fixture = await setup(true);
    const el = fixture.nativeElement as HTMLElement;
    expect(el.textContent).toContain('Désactiver');
    expect(el.textContent).toContain('Ajouter un contrat');
    expect(el.textContent).toContain('Ajouter un congé');
    expect(el.textContent).toContain('Nouvelle avance');
  });
});
