import { TestBed } from '@angular/core/testing';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { ActivatedRoute, convertToParamMap } from '@angular/router';
import { environment } from '@environments/environment';
import { FixedAssetsAmortizationTableComponent } from './fixed-assets-amortization-table.component';
import { AuthService } from '@core/services/auth.service';
import { provideHttpClient, withInterceptorsFromDi } from '@angular/common/http';

describe('FixedAssetsAmortizationTableComponent', () => {
  const base = `${environment.apiUrl}/accounting/fixed-assets`;

  const mockReport = {
    header: {
      companyName: 'Ma Société',
      fiscalYear: 2026,
      periodStart: '2026-01-01',
      periodEnd: '2026-12-31',
      generatedAtUtc: '2026-07-09T12:00:00Z'
    },
    groupingMode: 'AssetAccount',
    groups: [
      {
        groupCode: '228',
        groupLabel: '228 Matériel',
        rows: [
          {
            assetId: 'a1',
            assetAccountNumber: '228',
            inventoryNumber: 'IMMO-2026-0001',
            label: 'Camion',
            acquisitionDate: '2026-01-10',
            originValue: 50000,
            usefulLifeYears: 5,
            depreciationMethod: 'Linear',
            priorAccumulatedDepreciation: 1000,
            dotationCalculeeExercice: 5000,
            dotationComptabiliseeExercice: 5000,
            endOfYearAccumulatedDepreciation: 6000,
            endOfYearNetBookValue: 44000,
            postingStatus: 'FullyPosted'
          }
        ],
        subtotal: {
          originValue: 50000,
          priorAccumulatedDepreciation: 1000,
          dotationCalculeeExercice: 5000,
          dotationComptabiliseeExercice: 5000,
          endOfYearAccumulatedDepreciation: 6000,
          endOfYearNetBookValue: 44000
        }
      }
    ],
    grandTotal: {
      originValue: 50000,
      priorAccumulatedDepreciation: 1000,
      dotationCalculeeExercice: 5000,
      dotationComptabiliseeExercice: 5000,
      endOfYearAccumulatedDepreciation: 6000,
      endOfYearNetBookValue: 44000
    },
    summaryByNature: [
      {
        natureLabel: 'Transport',
        originValue: 50000,
        priorAccumulatedDepreciation: 1000,
        dotationCalculeeExercice: 5000,
        endOfYearNetBookValue: 44000
      }
    ],
    infoBox: {
      currencyCode: 'TND',
      generatedAtUtc: '2026-07-09T12:00:00Z',
      productName: 'InstaFact Comptabilité'
    }
  };

  beforeEach(() => {
    TestBed.configureTestingModule({
    imports: [FixedAssetsAmortizationTableComponent, NoopAnimationsModule],
    providers: [
        {
            provide: AuthService,
            useValue: { user: () => ({ companyName: 'Ma Société' }) }
        },
        {
            provide: ActivatedRoute,
            useValue: { snapshot: { queryParamMap: convertToParamMap({}) } }
        },
        provideHttpClient(withInterceptorsFromDi()),
        provideHttpClientTesting()
    ]
});
  });

  it('loads Sage report zones and renders grouped table', () => {
    const fixture = TestBed.createComponent(FixedAssetsAmortizationTableComponent);
    const httpMock = TestBed.inject(HttpTestingController);
    fixture.detectChanges();

    httpMock.expectOne(`${base}/rate-categories`).flush({ success: true, data: [] });
    httpMock.expectOne(`${base}/settings`).flush({
      success: true,
      data: { fiscalYearStartMonth: 1, fiscalYearLabelFormat: 'N/N+1', fiscalYearLabelSample: '2026' }
    });
    const req = httpMock.expectOne(r => r.url === `${base}/amortization-report`);
    expect(req.request.params.get('groupingMode')).toBe('AssetAccount');
    req.flush({ success: true, data: mockReport });
    fixture.detectChanges();

    const text = fixture.nativeElement.textContent;
    expect(text).toContain('TABLEAU DES AMORTISSEMENTS');
    expect(text).toContain('RÉCAPITULATIF PAR NATURE');
    expect(text).toContain('INFORMATIONS');
    expect(text).toContain('TOTAL GÉNÉRAL');
    expect(text).toContain('Dotation calculée');
    expect(text).toContain('Totalement comptabilisée');
    // C4 (captures 2408/2409): groupLabel already contains groupCode ('228 Matériel') — must not
    // be duplicated into "228 228 Matériel".
    expect(text).not.toContain('228 228');
    expect(text).toContain('228 Matériel');
    httpMock.verify();
  });

  // T12 (bug C4) — l'en-tête de groupe ne doit jamais dupliquer le code lorsqu'il est déjà inclus
  // dans le libellé (captures 2408/2409 : « 224 224 Véhicules… »).
  describe('groupHeaderLabel (T12 / C4)', () => {
    function component() {
      const fixture = TestBed.createComponent(FixedAssetsAmortizationTableComponent);
      const httpMock = TestBed.inject(HttpTestingController);
      fixture.detectChanges();
      httpMock.expectOne(`${base}/rate-categories`).flush({ success: true, data: [] });
      httpMock.expectOne(`${base}/settings`).flush({
        success: true,
        data: { fiscalYearStartMonth: 1, fiscalYearLabelFormat: 'N/N+1', fiscalYearLabelSample: '2026' }
      });
      httpMock.expectOne(r => r.url === `${base}/amortization-report`).flush({ success: true, data: mockReport });
      fixture.detectChanges();
      httpMock.verify();
      return fixture.componentInstance;
    }

    it('does not duplicate the code when the label already includes it', () => {
      const instance = component();
      expect(instance.groupHeaderLabel({ groupCode: '224', groupLabel: '224 Véhicules', rows: [], subtotal: mockReport.groups[0].subtotal })).toBe(
        '224 Véhicules'
      );
    });

    it('prepends the code when the label does not include it', () => {
      const instance = component();
      expect(instance.groupHeaderLabel({ groupCode: '228', groupLabel: 'Matériel', rows: [], subtotal: mockReport.groups[0].subtotal })).toBe(
        '228 Matériel'
      );
    });

    it('falls back to the label alone when there is no code', () => {
      const instance = component();
      expect(instance.groupHeaderLabel({ groupCode: '', groupLabel: 'Non catégorisé', rows: [], subtotal: mockReport.groups[0].subtotal })).toBe(
        'Non catégorisé'
      );
    });
  });

  it('reloads report when grouping mode changes', () => {
    const fixture = TestBed.createComponent(FixedAssetsAmortizationTableComponent);
    const httpMock = TestBed.inject(HttpTestingController);
    fixture.detectChanges();

    httpMock.expectOne(`${base}/rate-categories`).flush({ success: true, data: [] });
    httpMock.expectOne(`${base}/settings`).flush({
      success: true,
      data: { fiscalYearStartMonth: 1, fiscalYearLabelFormat: 'N/N+1', fiscalYearLabelSample: '2026' }
    });
    httpMock.expectOne(r => r.url === `${base}/amortization-report`).flush({ success: true, data: mockReport });

    fixture.componentInstance.groupingMode = 'FiscalCategory';
    fixture.componentInstance.reload();
    const second = httpMock.expectOne(
      r => r.url === `${base}/amortization-report` && r.params.get('groupingMode') === 'FiscalCategory'
    );
    expect(second.request.params.get('groupingMode')).toBe('FiscalCategory');
    second.flush({ success: true, data: { ...mockReport, groupingMode: 'FiscalCategory' } });
    fixture.detectChanges();
    expect(fixture.componentInstance.groupingMode).toBe('FiscalCategory');
    httpMock.verify();
  });

  // P4 (plan « Exercices décalés ») — le tableau Sage affiche l'exercice sous forme « N/N+1 »
  // quand le paramétrage est décalé ; sinon « N » (calcul côté client depuis fiscalYearStartMonth).
  describe('fiscal-year label (P4)', () => {
    it('displays the N/N+1 exercise label when the dossier is offset', () => {
      const fixture = TestBed.createComponent(FixedAssetsAmortizationTableComponent);
      const httpMock = TestBed.inject(HttpTestingController);
      fixture.detectChanges();

      httpMock.expectOne(`${base}/rate-categories`).flush({ success: true, data: [] });
      httpMock.expectOne(`${base}/settings`).flush({
        success: true,
        data: { fiscalYearStartMonth: 7, fiscalYearLabelFormat: 'N/N+1', fiscalYearLabelSample: '2026/2027' }
      });
      httpMock.expectOne(r => r.url === `${base}/amortization-report`).flush({ success: true, data: mockReport });
      fixture.detectChanges();

      // fiscalYearFilter defaults to the civil current year (2026); for a july-offset dossier the
      // label is « 2026/2027 ».
      expect(fixture.componentInstance.fiscalYearDisplay(2026)).toBe('2026/2027');
      expect(fixture.nativeElement.textContent).toContain('Exercice affiché : 2026/2027');
      // The Sage header meta labels the exercise too.
      expect(fixture.nativeElement.textContent).toContain('Exercice : 2026/2027');
      httpMock.verify();
    });

    it('displays the bare year for a civil exercise', () => {
      const fixture = TestBed.createComponent(FixedAssetsAmortizationTableComponent);
      const httpMock = TestBed.inject(HttpTestingController);
      fixture.detectChanges();

      httpMock.expectOne(`${base}/rate-categories`).flush({ success: true, data: [] });
      httpMock.expectOne(`${base}/settings`).flush({
        success: true,
        data: { fiscalYearStartMonth: 1, fiscalYearLabelFormat: 'N/N+1', fiscalYearLabelSample: '2026' }
      });
      httpMock.expectOne(r => r.url === `${base}/amortization-report`).flush({ success: true, data: mockReport });
      fixture.detectChanges();

      expect(fixture.componentInstance.fiscalYearDisplay(2026)).toBe('2026');
      expect(fixture.nativeElement.textContent).toContain('Exercice affiché : 2026');
      httpMock.verify();
    });
  });
});
