import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { of } from 'rxjs';
import { PayrollProrataPreviewPanelComponent } from './payroll-prorata-preview-panel.component';
import { PayrollService } from '@core/services/payroll.service';
import { ToastService } from '@core/services/toast.service';

describe('PayrollProrataPreviewPanelComponent', () => {
  let fixture: ComponentFixture<PayrollProrataPreviewPanelComponent>;
  let payroll: jasmine.SpyObj<PayrollService>;

  const preview = {
    year: 2026,
    month: 8,
    isEnabled: true,
    employeeCount: 1,
    totalDeduction: 250,
    lines: [{
      employeeId: 'e1',
      employeeName: 'Test User',
      employeeNumber: 'EMP001',
      workedDays: 20,
      nonWorkedDays: 6,
      deductionAmount: 250,
      reason: 'Sortie',
      warnings: []
    }]
  };

  beforeEach(() => {
    payroll = jasmine.createSpyObj('PayrollService', ['getProrataPreview']);
    payroll.getProrataPreview.and.returnValue(of({ success: true, data: preview }));

    TestBed.configureTestingModule({
      imports: [PayrollProrataPreviewPanelComponent],
      providers: [
        provideNoopAnimations(),
        { provide: PayrollService, useValue: payroll },
        { provide: ToastService, useValue: jasmine.createSpyObj('ToastService', ['add']) }
      ]
    });

    fixture = TestBed.createComponent(PayrollProrataPreviewPanelComponent);
    fixture.componentInstance.runId = 'run-1';
    fixture.detectChanges();
  });

  it('loads preview on button click', () => {
    fixture.componentInstance.load();
    fixture.detectChanges();
    expect(payroll.getProrataPreview).toHaveBeenCalledWith('run-1');
    expect(fixture.componentInstance.preview()?.employeeCount).toBe(1);
  });
});
