import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient, withInterceptorsFromDi } from '@angular/common/http';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { environment } from '@environments/environment';
import { JournalSummaryComponent } from './journal-summary.component';
import { JournalSummaryDto } from '../services/accounting.service';

describe('JournalSummaryComponent', () => {
  let fixture: ComponentFixture<JournalSummaryComponent>;
  let component: JournalSummaryComponent;
  let httpMock: HttpTestingController;
  const base = `${environment.apiUrl}/accounting`;

  /** Centralisateur : JV en janvier et mars, JA en janvier — sur un exercice de 3 mois. */
  const monthlySummary: JournalSummaryDto = {
    grouping: 0,
    from: '2026-01-01',
    to: '2026-03-31',
    periods: [
      { year: 2026, month: 1, label: '01/2026' },
      { year: 2026, month: 2, label: '02/2026' },
      { year: 2026, month: 3, label: '03/2026' }
    ],
    cells: [
      { journalCode: 'JV', journalLabel: 'Ventes', year: 2026, month: 1, debit: 1000, credit: 1000 },
      { journalCode: 'JV', journalLabel: 'Ventes', year: 2026, month: 3, debit: 300, credit: 300 },
      { journalCode: 'JA', journalLabel: 'Achats', year: 2026, month: 1, debit: 500, credit: 500 }
    ],
    journalTotals: [
      { journalCode: 'JV', journalLabel: 'Ventes', debit: 1300, credit: 1300, entryCount: 2 },
      { journalCode: 'JA', journalLabel: 'Achats', debit: 500, credit: 500, entryCount: 1 }
    ],
    totalDebit: 1800,
    totalCredit: 1800,
    isBalanced: true
  };

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [JournalSummaryComponent, NoopAnimationsModule],
      providers: [provideHttpClient(withInterceptorsFromDi()), provideHttpClientTesting()]
    }).compileComponents();

    fixture = TestBed.createComponent(JournalSummaryComponent);
    component = fixture.componentInstance;
    httpMock = TestBed.inject(HttpTestingController);
    fixture.detectChanges();

    httpMock.expectOne(r => r.url === `${base}/journals`).flush({ success: true, data: [] });
  });

  afterEach(() => httpMock.verify());

  function flushSummary(summary: JournalSummaryDto, expectedGrouping: string): void {
    const req = httpMock.expectOne(r => r.url === `${base}/journal-summary`);
    expect(req.request.params.get('grouping')).toBe(expectedGrouping);
    req.flush({ success: true, data: summary });
    fixture.detectChanges();
  }

  it('charge le centralisateur au démarrage', () => {
    flushSummary(monthlySummary, '0');
    expect(component.summary()?.totalDebit).toBe(1800);
  });

  it('pivote les cellules en lignes journal × mois, trous inclus', () => {
    flushSummary(monthlySummary, '0');

    const rows = component.centralizerRows();
    expect(rows.length).toBe(2);

    const jv = rows.find(r => r.journalCode === 'JV')!;
    expect(jv.cells.length).toBe(3);
    expect(jv.cells[0].debit).toBe(1000);
    expect(jv.cells[1].debit).toBe(0);      // février sans mouvement
    expect(jv.cells[2].debit).toBe(300);
    expect(jv.totalDebit).toBe(1300);
  });

  it('articule les totaux de colonnes avec le total général', () => {
    flushSummary(monthlySummary, '0');

    const columnSum = component.periodTotals().reduce((s, c) => s + c.debit, 0);
    expect(columnSum).toBe(component.summary()!.totalDebit);
  });

  it('recharge avec le regroupement du nouvel onglet', () => {
    flushSummary(monthlySummary, '0');

    component.activeTabIndex = 1;
    component.onTabChange();
    flushSummary({ ...monthlySummary, grouping: 1, cells: [], periods: [] }, '1');

    expect(component.centralizerRows()).toEqual([]);   // le pivot ne vaut que pour le mensuel
  });

  it('signale une erreur réseau sans casser l’écran', () => {
    httpMock
      .expectOne(r => r.url === `${base}/journal-summary`)
      .error(new ProgressEvent('network'));
    fixture.detectChanges();

    expect(component.error()).toBe('Erreur réseau');
    expect(component.loading()).toBeFalse();
  });
});
