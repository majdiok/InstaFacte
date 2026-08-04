import { TestBed } from '@angular/core/testing';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ActivatedRoute, convertToParamMap } from '@angular/router';
import { RouterTestingModule } from '@angular/router/testing';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { environment } from '@environments/environment';
import { FixedAssetDetailComponent } from './fixed-asset-detail.component';
import { DepreciationMethod, FixedAssetStatus } from '../services/fixed-assets.service';
import { provideHttpClient, withInterceptorsFromDi } from '@angular/common/http';

describe('FixedAssetDetailComponent', () => {
  const base = `${environment.apiUrl}/accounting/fixed-assets`;

  const draftAsset = {
    id: 'asset-1',
    inventoryNumber: 'IMMO-2026-0001',
    label: 'Camion',
    status: 'Draft',
    assetAccountNumber: '218',
    depreciationAccountNumber: '2818',
    expenseAccountNumber: '6818',
    acquisitionCost: 50000,
    capitalizedFees: 0,
    residualValue: 0,
    totalCapitalizedCost: 50000,
    vatAmount: 0,
    acquisitionDate: '2026-01-10',
    depreciationRateCategoryId: 'cat-1',
    depreciationRateCategoryLabel: 'Transport',
    depreciationRatePercent: 20,
    usefulLifeYears: 5,
    depreciationMethod: 'Linear',
    accelerationCoefficient: 1,
    accumulatedDepreciation: 0,
    netBookValue: 50000,
    supplierId: 'sup-1'
  };

  function setup(routeSnapshot: Partial<{ data: Record<string, unknown>; id: string | null }>) {
    TestBed.configureTestingModule({
      imports: [FixedAssetDetailComponent, RouterTestingModule, NoopAnimationsModule],
      providers: [
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: {
              data: routeSnapshot.data ?? {},
              paramMap: convertToParamMap(routeSnapshot.id ? { id: routeSnapshot.id } : {}),
              queryParamMap: convertToParamMap({})
            }
          }
        },
        provideHttpClient(withInterceptorsFromDi()),
        provideHttpClientTesting()
      ]
    });

    const fixture = TestBed.createComponent(FixedAssetDetailComponent);
    const httpMock = TestBed.inject(HttpTestingController);
    fixture.detectChanges();

    httpMock.expectOne(`${base}/rate-categories`).flush({ success: true, data: [] });

    const isNew =
      routeSnapshot.data?.['mode'] === 'new' || routeSnapshot.id === 'new' || !routeSnapshot.id;
    if (isNew) {
      httpMock.expectOne(r => r.url.includes('/suppliers')).flush({
        success: true,
        data: { items: [{ id: 'sup-1', name: 'Fournisseur A' }], page: 1, pageSize: 200, totalCount: 1 }
      });
    }

    return { fixture, httpMock };
  }

  function flushDraftAsset(httpMock: HttpTestingController, overrides: Record<string, unknown> = {}): void {
    httpMock.expectOne(`${base}/asset-1`).flush({
      success: true,
      data: { ...draftAsset, ...overrides }
    });
    httpMock.expectOne(r => r.url.includes('/suppliers')).flush({
      success: true,
      data: {
        items: [{ id: 'sup-1', name: 'Fournisseur A' }],
        page: 1,
        pageSize: 200,
        totalCount: 1
      }
    });
    httpMock.expectOne(`${base}/asset-1/schedule`).flush({ success: true, data: null });
  }

  it('should enter creation mode on the static /new route (route data)', () => {
    const { fixture, httpMock } = setup({ data: { mode: 'new' } });

    expect(fixture.componentInstance.isNew()).toBeTrue();
    expect(fixture.nativeElement.textContent).toContain('Enregistrer');
    httpMock.verify();
  });

  it('should load the asset when an id param is present and map API enums', () => {
    const { fixture, httpMock } = setup({ id: 'asset-1' });

    expect(fixture.componentInstance.isNew()).toBeFalse();
    flushDraftAsset(httpMock, {
      label: 'Machine',
      acquisitionCost: 10000,
      totalCapitalizedCost: 10000,
      depreciationRateCategoryLabel: 'Autres',
      depreciationRatePercent: 15,
      usefulLifeYears: 6.67,
      netBookValue: 10000
    });

    const component = fixture.componentInstance;
    expect(component.asset()?.label).toBe('Machine');
    expect(component.asset()?.status).toBe(FixedAssetStatus.Draft);
    expect(component.form.label).toBe('Machine');
    expect(component.form.depreciationMethod).toBe(DepreciationMethod.Linear);
    expect(component.form.supplierId).toBe('sup-1');
    httpMock.verify();
  });

  it('should show put-in-service section for draft assets', () => {
    const { fixture, httpMock } = setup({ id: 'asset-1' });

    flushDraftAsset(httpMock);
    fixture.detectChanges();

    expect(fixture.componentInstance.isDraftAsset()).toBeTrue();
    expect(fixture.nativeElement.textContent).toContain('Mise en service');
    expect(fixture.nativeElement.textContent).toContain('Mettre en service et comptabiliser');
    expect(fixture.nativeElement.textContent).toContain('Brouillon');
    httpMock.verify();
  });

  it('should not preload suppliers for in-service assets', () => {
    const { fixture, httpMock } = setup({ id: 'asset-1' });

    httpMock.expectOne(`${base}/asset-1`).flush({
      success: true,
      data: { ...draftAsset, status: 'InService', supplierId: 'sup-1' }
    });
    httpMock.expectOne(`${base}/asset-1/schedule`).flush({ success: true, data: null });
    fixture.detectChanges();

    expect(fixture.componentInstance.form.supplierId).toBe('sup-1');
    expect(fixture.componentInstance.suppliers().length).toBe(0);
    httpMock.verify();
  });

  it('should reload the asset after put-in-service concurrency error', () => {
    const { fixture, httpMock } = setup({ id: 'asset-1' });

    flushDraftAsset(httpMock);
    fixture.detectChanges();

    fixture.componentInstance.putInService();

    const putReq = httpMock.expectOne(`${base}/asset-1/put-in-service`);
    putReq.flush(
      { code: 'CONCURRENCY_CONFLICT', message: 'Conflit' },
      { status: 409, statusText: 'Conflict' }
    );

    expect(fixture.componentInstance.error()).toContain('modifiées entre-temps');

    flushDraftAsset(httpMock);
    httpMock.verify();
  });

  it('should suggest integral amortization for low-value assets', () => {
    const { fixture, httpMock } = setup({ data: { mode: 'new' } });
    const component = fixture.componentInstance;

    component.form.acquisitionCost = 150;
    component.form.capitalizedFees = 0;
    component.form.residualValue = 0;
    fixture.detectChanges();

    expect(component.suggestIntegral()).toBeTrue();
    httpMock.verify();
  });
});
