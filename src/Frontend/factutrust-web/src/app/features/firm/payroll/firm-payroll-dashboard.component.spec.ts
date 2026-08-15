import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { signal } from '@angular/core';
import { of, throwError } from 'rxjs';
import { FirmPayrollDashboardComponent } from './firm-payroll-dashboard.component';
import { PayrollService, PayrollDashboard } from '@core/services/payroll.service';
import { AuthService } from '@core/services/auth.service';
import { ToastService } from '@core/services/toast.service';

describe('FirmPayrollDashboardComponent', () => {
  let fixture: ComponentFixture<FirmPayrollDashboardComponent>;
  let getDashboard: jasmine.Spy;
  let toastAdd: jasmine.Spy;

  const fullDashboard: PayrollDashboard = {
    year: 2026,
    month: 6,
    periodLabel: 'Juin 2026',
    hasRun: true,
    runId: 'run-1',
    runStatus: 'Calculated',
    runStatusDisplay: 'Calculé',
    gross: { amount: 128540, previousAmount: 118000, changePercent: 8.9 },
    net: { amount: 96825.25, previousAmount: 90000, changePercent: 7.6 },
    employerCharges: { amount: 31714.75, previousAmount: 29000, changePercent: 9.4 },
    employeeCount: 18,
    previousEmployeeCount: 17,
    earningsBreakdown: [
      { label: 'Salaires de base', amount: 96000 },
      { label: 'Primes & indemnités', amount: 9200 },
      { label: 'Heures supplémentaires', amount: 3340 },
      { label: 'Avantages en nature', amount: 20000 }
    ],
    deductionBreakdown: [
      { label: 'CNSS salariale', amount: 6400.25 },
      { label: 'Retenue à la source (IRPP)', amount: 8900 }
    ],
    employerChargeBreakdown: [
      { label: 'CNSS patronale', amount: 16253.5 },
      { label: 'TFP', amount: 3250.5 }
    ],
    monthlySeries: Array.from({ length: 12 }, (_, i) => ({
      month: i + 1, gross: 100000, net: 80000, employerCharges: 20000,
      status: 'Validated', statusDisplay: 'Validé'
    })),
    payslips: [{
      id: 'p1', employeeId: 'e1', employeeName: 'Amine Trabelsi', employeeNumber: 'EMP-001',
      grossSalary: 4200, cnssEmployee: 385, irpp: 700, css: 20, irppSmigExemption: 0,
      netSalary: 3630, paidAmount: 0, remainingToPay: 3630,
      paymentStatus: 'Unpaid', paymentStatusDisplay: 'Non payé'
    } as never],
    recentRuns: [{
      id: 'run-1', year: 2026, month: 6, label: 'Juin 2026',
      status: 'Calculated', statusDisplay: 'Calculé',
      totalGross: 128540, totalNet: 96825.25
    }],
    upcomingDeadlines: [
      { label: 'Paiement CNSS', dueDate: '2026-07-15', daysRemaining: 12, isOverdue: false, estimatedAmount: 0 }
    ]
  };

  async function setup(data: PayrollDashboard | null, isManager = true, fail = false) {
    getDashboard = jasmine.createSpy('getDashboard').and.returnValue(
      fail ? throwError(() => ({ error: { message: 'boom' } })) : of({ success: true, data })
    );
    toastAdd = jasmine.createSpy('add');

    TestBed.resetTestingModule();
    await TestBed.configureTestingModule({
      imports: [FirmPayrollDashboardComponent],
      providers: [
        provideRouter([]),
        provideNoopAnimations(),
        { provide: PayrollService, useValue: { getDashboard } },
        { provide: AuthService, useValue: { isFirmManager: signal(isManager) } },
        { provide: ToastService, useValue: { add: toastAdd } }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(FirmPayrollDashboardComponent);
    fixture.detectChanges();
  }

  it('charge le mois courant au démarrage', async () => {
    await setup(fullDashboard);
    const now = new Date();
    expect(getDashboard).toHaveBeenCalledWith(now.getFullYear(), now.getMonth() + 1);
    expect(fixture.componentInstance.data()?.periodLabel).toBe('Juin 2026');
  });

  it('affiche les indicateurs et la ventilation du mois', async () => {
    await setup(fullDashboard);
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';

    expect(text).toContain('Total brut');
    expect(text).toContain('Charges patronales');
    expect(text).toContain('Salaires de base');
    expect(text).toContain('Amine Trabelsi');
    expect(text).toContain('Paiement CNSS');
  });

  it('affiche les accès rapides entre les KPI et les graphiques', async () => {
    await setup(fullDashboard);
    const el = fixture.nativeElement as HTMLElement;
    const kpiRow = el.querySelector('.kpi-row');
    const hubTitle = el.querySelector('#quick-access-title');
    const chartsGrid = el.querySelector('.charts-grid');

    expect(kpiRow && hubTitle && chartsGrid).toBeTruthy();
    expect(kpiRow!.compareDocumentPosition(hubTitle!) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
    expect(hubTitle!.compareDocumentPosition(chartsGrid!) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
  });

  it('totalise les retenues affichées', async () => {
    await setup(fullDashboard);
    expect(fixture.componentInstance.totalDeductions()).toBeCloseTo(15300.25, 2);
  });

  it('reste utilisable sur un mois sans cycle', async () => {
    await setup({
      ...fullDashboard,
      hasRun: false,
      runId: undefined,
      gross: { amount: 0, previousAmount: null, changePercent: null },
      net: { amount: 0, previousAmount: null, changePercent: null },
      employerCharges: { amount: 0, previousAmount: null, changePercent: null },
      employeeCount: 0,
      earningsBreakdown: [],
      deductionBreakdown: [],
      employerChargeBreakdown: [],
      payslips: [],
      upcomingDeadlines: []
    });

    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('Aucun cycle de paie');
    // L'écran ne doit pas se vider : les accès rapides restent offerts.
    expect(text).toContain('Accès rapides');
    expect(toastAdd).not.toHaveBeenCalled();
  });

  it('n’affiche aucune tendance quand le mois précédent est absent', async () => {
    await setup({
      ...fullDashboard,
      gross: { amount: 1000, previousAmount: null, changePercent: null }
    });

    // Le badge de tendance n'apparaît que sur une variation non nulle.
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).not.toContain('+8.9%');
  });

  it('masque les accès réservés au responsable pour le comptable', async () => {
    await setup(fullDashboard, /* isManager */ false);
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';

    expect(text).toContain('Salariés');
    expect(text).not.toContain('Déclarations sociales');
    expect(text).not.toContain('Paramètres paie');
  });

  it('signale une ventilation approchée sans masquer les montants', async () => {
    await setup({ ...fullDashboard, breakdownWarning: 'La ventilation du brut est approchée.' });
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';

    expect(text).toContain('approchée');
    expect(text).toContain('Salaires de base');
  });

  it('remonte une erreur de chargement sans casser l’écran', async () => {
    await setup(null, true, /* fail */ true);

    expect(toastAdd).toHaveBeenCalled();
    expect(fixture.componentInstance.loading()).toBeFalse();
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Accès rapides');
  });

  it('qualifie l’urgence de la prochaine échéance', async () => {
    await setup(fullDashboard);
    const c = fixture.componentInstance;

    expect(c.describeDeadline({ label: 'x', dueDate: '2026-07-15', daysRemaining: 12, isOverdue: false, estimatedAmount: 0 }))
      .toBe('Dans 12 j');
    expect(c.describeDeadline({ label: 'x', dueDate: '2026-06-01', daysRemaining: -3, isOverdue: true, estimatedAmount: 0 }))
      .toBe('En retard de 3 j');
    expect(c.nextDeadlineVariant()).toBe('primary');
  });
});
