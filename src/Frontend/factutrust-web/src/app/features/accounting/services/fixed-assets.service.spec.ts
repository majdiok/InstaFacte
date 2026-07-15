import { TestBed } from '@angular/core/testing';
import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
import {
  DepreciationMethod,
  FixedAssetStatus,
  FixedAssetsService
} from './fixed-assets.service';
import { environment } from '@environments/environment';

describe('FixedAssetsService', () => {
  let service: FixedAssetsService;
  let httpMock: HttpTestingController;
  const base = `${environment.apiUrl}/accounting/fixed-assets`;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [HttpClientTestingModule]
    });
    service = TestBed.inject(FixedAssetsService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('should list fixed assets and map PascalCase enums from API', () => {
    service.list({ page: 1, pageSize: 25, search: 'machine' }).subscribe(res => {
      expect(res.success).toBe(true);
      expect(res.data?.items.length).toBe(1);
      expect(res.data?.items[0].status).toBe(FixedAssetStatus.Draft);
    });

    const req = httpMock.expectOne(r => r.url === base);
    expect(req.request.method).toBe('GET');
    expect(req.request.params.get('search')).toBe('machine');
    req.flush({
      success: true,
      data: {
        items: [{ id: 'a1', inventoryNumber: 'IMMO-2026-0001', label: 'Machine', status: 'Draft' }],
        totalCount: 1,
        page: 1,
        pageSize: 25
      }
    });
  });

  it('should export schedule excel as blob', () => {
    const blob = new Blob(['xlsx'], { type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet' });
    service.exportScheduleExcel('asset-id').subscribe(res => {
      expect(res).toBeInstanceOf(Blob);
    });

    const req = httpMock.expectOne(`${base}/asset-id/schedule/export.xlsx`);
    expect(req.request.method).toBe('GET');
    expect(req.request.responseType).toBe('blob');
    req.flush(blob);
  });

  it('should export depreciation report excel', () => {
    const blob = new Blob(['xlsx'], { type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet' });
    service.exportDepreciationReportExcel(2026).subscribe(res => {
      expect(res).toBeInstanceOf(Blob);
    });

    const req = httpMock.expectOne(r => r.url === `${base}/depreciation-report/export.xlsx`);
    expect(req.request.method).toBe('GET');
    expect(req.request.params.get('fiscalYear')).toBe('2026');
    req.flush(blob);
  });

  it('should update a draft fixed asset via PUT with string enum body', () => {
    service
      .update('asset-id', {
        label: 'Machine modifiée',
        acquisitionCost: 12000,
        capitalizedFees: 0,
        residualValue: 0,
        acquisitionDate: '2026-02-01',
        depreciationMethod: DepreciationMethod.Accelerated,
        accelerationCoefficient: 2
      })
      .subscribe(res => {
        expect(res.success).toBe(true);
      });

    const req = httpMock.expectOne(`${base}/asset-id`);
    expect(req.request.method).toBe('PUT');
    expect(req.request.body.label).toBe('Machine modifiée');
    expect(req.request.body.depreciationMethod).toBe('Accelerated');
    req.flush({ success: true, data: 'asset-id' });
  });

  it('should preview the depreciation schedule and map method from API', () => {
    service.previewSchedule('asset-id', '2026-10-01').subscribe(res => {
      expect(res.success).toBe(true);
      expect(res.data?.lines.length).toBe(1);
      expect(res.data?.depreciationMethod).toBe(DepreciationMethod.Linear);
    });

    const req = httpMock.expectOne(`${base}/asset-id/schedule/preview`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body.inServiceDate).toBe('2026-10-01');
    req.flush({
      success: true,
      data: {
        fixedAssetId: 'asset-id',
        inventoryNumber: 'IMMO-2026-0001',
        label: 'Machine',
        acquisitionDate: '2026-01-10',
        inServiceDate: '2026-10-01',
        totalCapitalizedCost: 4000,
        depreciationRatePercent: 20,
        usefulLifeYears: 5,
        depreciableBase: 4000,
        depreciationMethod: 'Linear',
        accelerationCoefficient: 1,
        lines: [
          {
            id: 'l1',
            fiscalYear: 2026,
            periodMonth: null,
            openingNbv: 4000,
            normalAnnualAmount: 800,
            priorAccumulatedDepreciation: 0,
            depreciationAmount: 200,
            accumulatedDepreciation: 200,
            closingNbv: 3800,
            isPosted: false
          }
        ]
      }
    });
  });
});
