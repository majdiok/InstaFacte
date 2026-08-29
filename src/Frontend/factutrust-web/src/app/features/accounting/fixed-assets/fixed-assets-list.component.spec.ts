import { TestBed } from '@angular/core/testing';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient, withInterceptorsFromDi } from '@angular/common/http';
import { RouterTestingModule } from '@angular/router/testing';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { environment } from '@environments/environment';
import { FixedAssetsListComponent } from './fixed-assets-list.component';

describe('FixedAssetsListComponent', () => {
  const base = `${environment.apiUrl}/accounting/fixed-assets`;

  const row = {
    id: 'asset-1',
    inventoryNumber: 'IMMO-2026-0001',
    label: 'Camion',
    status: 'InService',
    depreciationRateCategoryLabel: 'Transport',
    depreciationMethod: 'Linear',
    totalCapitalizedCost: 50000,
    accumulatedDepreciation: 5000,
    netBookValue: 45000,
    depreciationRatePercent: 20
  };

  function setup() {
    TestBed.configureTestingModule({
      imports: [FixedAssetsListComponent, RouterTestingModule, NoopAnimationsModule],
      providers: [provideHttpClient(withInterceptorsFromDi()), provideHttpClientTesting()]
    });

    const fixture = TestBed.createComponent(FixedAssetsListComponent);
    const httpMock = TestBed.inject(HttpTestingController);
    fixture.detectChanges();

    httpMock.expectOne(`${base}/rate-categories`).flush({ success: true, data: [] });

    return { fixture, httpMock };
  }

  // T12 (bug C5) — le libellé de la ligne de totaux doit refléter la pagination réelle
  // (« Totaux page X / Y ») et non un « Totaux (page) » trompeur laissant croire à un total global.
  it('labels the footer totals row with the actual current page and page count', () => {
    const { fixture, httpMock } = setup();

    const listReq = httpMock.expectOne(r => r.url === `${base}`);
    listReq.flush({ success: true, data: { items: [row, { ...row, id: 'asset-2' }], totalCount: 60 } });
    fixture.detectChanges();

    expect(fixture.componentInstance.page()).toBe(1);
    expect(fixture.componentInstance.totalPages()).toBe(3);

    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('Totaux page 1 / 3');
    expect(text).not.toContain('Totaux (page)');
    httpMock.verify();
  });

  it('updates the footer totals label after navigating to the next page', () => {
    const { fixture, httpMock } = setup();

    httpMock.expectOne(r => r.url === `${base}`).flush({ success: true, data: { items: [row], totalCount: 60 } });
    fixture.detectChanges();

    fixture.componentInstance.nextPage();
    httpMock.expectOne(r => r.url === `${base}` && r.params.get('page') === '2').flush({
      success: true,
      data: { items: [row], totalCount: 60 }
    });
    fixture.detectChanges();

    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('Totaux page 2 / 3');
    httpMock.verify();
  });
});
