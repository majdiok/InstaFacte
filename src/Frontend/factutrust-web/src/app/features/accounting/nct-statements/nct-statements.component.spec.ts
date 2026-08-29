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
      isBalanced: true,
      difference: 0,
      warnings: []
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

  // T21 : restitution de l'écart bilan (badge avec montant au lieu du badge binaire).
  it('affiche le badge « Non équilibré (écart : X TND) » pour un bilan déséquilibré', () => {
    accountingMock.getNctStatements.and.returnValue(of({
      success: true,
      data: makeStatements({
        balanceSheet: {
          assets: [], equityAndLiabilities: [], totalAssets: 100, totalEquityAndLiabilities: 350,
          previousTotalAssets: 0, previousTotalEquityAndLiabilities: 0,
          isBalanced: false, difference: 250, warnings: []
        }
      })
    }));
    fixture.componentInstance.load();
    fixture.detectChanges();
    const badge: HTMLElement | null = fixture.nativeElement.querySelector('.nct-badge');
    expect(badge?.textContent).toContain('Non équilibré (écart');
    expect(badge?.textContent).toContain('250.000');
    expect(badge?.textContent).toContain('TND');
  });

  // T21 : bannière des avertissements qualité (rubriques négatives, à-nouveaux absents/manuels).
  it('rend une bannière listant les avertissements qualité du bilan', () => {
    accountingMock.getNctStatements.and.returnValue(of({
      success: true,
      data: makeStatements({
        balanceSheet: {
          assets: [], equityAndLiabilities: [], totalAssets: 0, totalEquityAndLiabilities: 0,
          previousTotalAssets: 0, previousTotalEquityAndLiabilities: 0,
          isBalanced: false, difference: 250,
          warnings: [
            'Rubrique « Immobilisations corporelles » négative.',
            'À-nouveaux manuels détectés (journal JAN).'
          ]
        }
      })
    }));
    fixture.componentInstance.load();
    fixture.detectChanges();
    const banner: HTMLElement | null = fixture.nativeElement.querySelector('.nct-alert-banner');
    expect(banner).toBeTruthy();
    expect(banner?.textContent).toContain('Rubrique « Immobilisations corporelles » négative.');
    expect(banner?.textContent).toContain('À-nouveaux manuels détectés');
  });

  it("n'affiche pas de bannière d'avertissements quand il n'y en a aucun", () => {
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('.nct-alert-banner')).toBeNull();
  });

  // T21 : badge flux affichant le montant CFECART quand le flux n'est pas rapproché.
  it('affiche le badge de réconciliation des flux avec le montant CFECART quand non rapproché', () => {
    accountingMock.getNctStatements.and.returnValue(of({
      success: true,
      data: makeStatements({
        cashFlow: { ...makeStatements().cashFlow, isReconciled: false }
      })
    }));
    fixture.componentInstance.load();
    fixture.componentInstance.tab.set('flux');
    fixture.detectChanges();
    const badges: HTMLElement[] = Array.from(fixture.nativeElement.querySelectorAll('.nct-badge'));
    const fluxBadge = badges.find(b => (b.textContent ?? '').includes('Flux'));
    expect(fluxBadge?.textContent).toContain('Flux non rapproché (écart');
    expect(fluxBadge?.textContent).toContain('5.000');
    expect(fluxBadge?.textContent).toContain('TND');
  });

  it('affiche le badge flux « rapproché » quand la trésorerie est réconciliée', () => {
    fixture.componentInstance.tab.set('flux');
    fixture.detectChanges();
    const badges: HTMLElement[] = Array.from(fixture.nativeElement.querySelectorAll('.nct-badge'));
    const fluxBadge = badges.find(b => (b.textContent ?? '').includes('Flux'));
    expect(fluxBadge?.textContent).toContain('Flux de trésorerie rapproché');
  });
});
