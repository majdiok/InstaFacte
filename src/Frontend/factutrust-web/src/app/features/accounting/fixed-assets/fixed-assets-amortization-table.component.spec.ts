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
    httpMock.verify();
  });

  it('reloads report when grouping mode changes', () => {
    const fixture = TestBed.createComponent(FixedAssetsAmortizationTableComponent);
    const httpMock = TestBed.inject(HttpTestingController);
    fixture.detectChanges();

    httpMock.expectOne(`${base}/rate-categories`).flush({ success: true, data: [] });
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
});
