import { ComponentFixture, TestBed } from '@angular/core/testing';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { FirmCollaboratorPayrollOnboardingComponent } from './firm-collaborator-payroll-onboarding.component';

describe('FirmCollaboratorPayrollOnboardingComponent', () => {
  let fixture: ComponentFixture<FirmCollaboratorPayrollOnboardingComponent>;
  let component: FirmCollaboratorPayrollOnboardingComponent;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [FirmCollaboratorPayrollOnboardingComponent, NoopAnimationsModule]
    }).compileComponents();

    fixture = TestBed.createComponent(FirmCollaboratorPayrollOnboardingComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('buildPayload returns null when matricule is empty', () => {
    component.form.patchValue({ employeeNumber: '' });
    expect(component.buildPayload()).toBeNull();
  });

  it('buildPayload returns null for CDD without end date', () => {
    component.form.patchValue({
      employeeNumber: 'CAB-1',
      contractType: 'Cdd',
      contractEndDate: null,
      baseSalary: 1500
    });
    expect(component.buildPayload()).toBeNull();
  });

  it('buildPayload returns payroll contract for valid CDI', () => {
    const start = new Date(2025, 0, 15);
    component.form.patchValue({
      employeeNumber: 'CAB-42',
      hireDate: start,
      contractType: 'Cdi',
      contractStartDate: start,
      baseSalary: 1800,
      workAccidentRate: 0.4
    });

    const payload = component.buildPayload();
    expect(payload).not.toBeNull();
    expect(payload!.employeeNumber).toBe('CAB-42');
    expect(payload!.hireDate).toBe('2025-01-15');
    expect(payload!.contract.baseSalary).toBe(1800);
    expect(payload!.contract.type).toBe('Cdi');
  });
});
