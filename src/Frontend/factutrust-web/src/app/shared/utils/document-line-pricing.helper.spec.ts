import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { DocumentLinePricingService } from './document-line-pricing.helper';
import { PricingService } from '@core/services/pricing.service';

describe('DocumentLinePricingService', () => {
  let service: DocumentLinePricingService;
  let resolveSpy: jasmine.Spy;

  beforeEach(() => {
    resolveSpy = jasmine.createSpy('resolve').and.returnValue(
      of({
        success: true,
        data: {
          unitPriceHT: 1900,
          currency: 'TND',
          source: 'ClientPrice',
          isNegotiated: true
        }
      })
    );

    TestBed.configureTestingModule({
      providers: [
        DocumentLinePricingService,
        {
          provide: PricingService,
          useValue: { resolve: resolveSpy }
        }
      ]
    });

    service = TestBed.inject(DocumentLinePricingService);
  });

  it('returns resolved price when not overridden', (done) => {
    service
      .resolveLinePrice({
        productId: 'prod-1',
        clientId: 'client-1',
        quantity: 1,
        documentDate: new Date(2026, 7, 1),
        priceOverridden: false
      })
      .subscribe(result => {
        expect(result?.unitPriceHT).toBe(1900);
        expect(result?.source).toBe('ClientPrice');
        expect(resolveSpy).toHaveBeenCalledWith('prod-1', 'client-1', 1, '2026-08-01');
        done();
      });
  });

  it('returns null without calling API when price is overridden', (done) => {
    service
      .resolveLinePrice({
        productId: 'prod-1',
        clientId: 'client-1',
        quantity: 1,
        priceOverridden: true
      })
      .subscribe(result => {
        expect(result).toBeNull();
        expect(resolveSpy).not.toHaveBeenCalled();
        done();
      });
  });

  it('ignores response when isOverrideCheck returns true', (done) => {
    service
      .resolveLinePrice({
        productId: 'prod-1',
        clientId: 'client-1',
        quantity: 1,
        priceOverridden: false,
        isOverrideCheck: () => true
      })
      .subscribe(result => {
        expect(result).toBeNull();
        done();
      });
  });

  it('formatDocumentDate uses local calendar', () => {
    const d = new Date(2026, 5, 18);
    expect(service.formatDocumentDate(d)).toBe('2026-06-18');
  });
});
