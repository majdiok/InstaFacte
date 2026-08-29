import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { of, Subject } from 'rxjs';
import { AccountingService, NctFinancialStatementsDto } from '../services/accounting.service';
import { NctStatementsComponent } from './nct-statements.component';

function makeStatements(overrides: Partial<NctFinancialStatementsDto> = {}): NctFinancialStatementsDto {
  return {
    fiscalYear: 2025,
    balanceSheet: {
      assets: [],
      equityAndLiabilities: [],
      totalAssets: 0,
      totalEquityAndLiabilities: 0,
      previousTotalAssets: 0,
      previousTotalEquityAndLiabilities: 0,
      isBalanced: true
    },
    incomeStatement: { lines: [], operatingResult: 0, financialResult: 0, resultBeforeTax: 0, netResult: 0, previousNetResult: 0 },
    cashFlow: {
      lines: [
        { code: 'CFOPE', label: 'Flux d\u2019exploitation', amount: 100, previousAmount: 90, isSubtotal: true, level: 0 },
        { code: 'CFECART', label: 'Écart de rapprochement', amount: 5, previousAmount: 0, isSubtotal: false, level: 1 }
      ],
      operatingCashFlow: 100,
      investingCashFlow: 0,
      financingCashFlow: 0,
      netChange: 100,
      openingCash: 0,
      closingCash: 100,
      isReconciled: true
    },
    equityChanges: { lines: [], openingEquity: 0, netResult: 0, closingEquity: 0 },
    notes: [],
    nctStatementsEnabled: true,
    ...overrides
  };
}

describe('NctStatementsComponent', () => {
  let fixture: ComponentFixture<NctStatementsComponent>;
  let accountingMock: { getNctStatements: jasmine.Spy };

  beforeEach(async () => {
    accountingMock = {
      getNctStatements: jasmine.createSpy('getNctStatements').and.returnValue(of({ success: true, data: makeStatements() }))
    };

    await TestBed.configureTestingModule({
      imports: [NctStatementsComponent],
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: AccountingService, useValue: accountingMock }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(NctStatementsComponent);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
  });

  // T19 : bornes d'exercice.
  it('désactive le bouton Charger pour une année hors bornes (2150)', () => {
    fixture.componentInstance.onYearInputChange(2150);
    fixture.detectChanges();
    expect(fixture.componentInstance.isYearValid()).toBeFalse();
    const button: HTMLButtonElement | null = fixture.nativeElement.querySelector('[aria-label="Charger la liasse NCT"]');
    expect(button?.disabled).toBeTrue();
    expect((fixture.nativeElement.textContent as string)).toContain('Exercice invalide');
  });

  // T20 : course sur Charger.
  it('deux appels rapprochés à load() ne rendent que la dernière réponse (switchMap)', () => {
    const first$ = new Subject<{ success: boolean; data: NctFinancialStatementsDto }>();
    const second$ = new Subject<{ success: boolean; data: NctFinancialStatementsDto }>();
    accountingMock.getNctStatements.and.returnValues(first$, second$);

    fixture.componentInstance.fiscalYear = 2024;
    fixture.componentInstance.load();
    fixture.componentInstance.fiscalYear = 2023;
    fixture.componentInstance.load();

    first$.next({ success: true, data: makeStatements({ fiscalYear: 2024 }) });
    second$.next({ success: true, data: makeStatements({ fiscalYear: 2023 }) });

    expect(fixture.componentInstance.data()?.fiscalYear).toBe(2023);
  });

  // BUG #004 : mise en évidence de la ligne de réconciliation des flux.
  it('marque la ligne CFECART avec la classe .nct-row--reconciliation dans l’onglet Flux', () => {
    fixture.componentInstance.tab.set('flux');
    fixture.detectChanges();
    const rows: HTMLTableRowElement[] = Array.from(fixture.nativeElement.querySelectorAll('tr'));
    const reconciliationRow = rows.find(r => (r.textContent ?? '').includes('Écart de rapprochement'));
    expect(reconciliationRow).toBeTruthy();
    expect(reconciliationRow?.classList.contains('nct-row--reconciliation')).toBeTrue();
  });
});
