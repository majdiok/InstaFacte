import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { ProductClientPricesComponent } from './product-client-prices.component';
import { AuthService } from '@core/services/auth.service';

describe('ProductClientPricesComponent', () => {
  let fixture: ComponentFixture<ProductClientPricesComponent>;
  let component: ProductClientPricesComponent;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ProductClientPricesComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        {
          provide: AuthService,
          useValue: {
            hasPermission: () => true
          }
        }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(ProductClientPricesComponent);
    component = fixture.componentInstance;
    fixture.componentRef.setInput('productId', 'prod-1');
    fixture.detectChanges();
  });

  describe('dateRangeInvalid', () => {
    it('returns false when dates are empty', () => {
      component.formValidFrom = null;
      component.formValidUntil = null;
      expect(component.dateRangeInvalid()).toBeFalse();
    });

    it('returns false when only one date is set', () => {
      component.formValidFrom = '2026-01-01';
      component.formValidUntil = null;
      expect(component.dateRangeInvalid()).toBeFalse();
    });

    it('returns true when end date precedes start date', () => {
      component.formValidFrom = '2026-06-01';
      component.formValidUntil = '2026-01-01';
      expect(component.dateRangeInvalid()).toBeTrue();
    });

    it('returns false when end date equals start date', () => {
      component.formValidFrom = '2026-06-01';
      component.formValidUntil = '2026-06-01';
      expect(component.dateRangeInvalid()).toBeFalse();
    });
  });

  describe('canSave', () => {
    it('requires client, price and valid date range', () => {
      component.selectedClient = { id: 'client-1', label: 'Client A' };
      component.formPrice = 100;
      component.formValidFrom = '2026-06-01';
      component.formValidUntil = '2026-01-01';
      expect(component.canSave()).toBeFalse();

      component.formValidUntil = '2026-12-01';
      expect(component.canSave()).toBeTrue();
    });
  });

  describe('formPriceDelta', () => {
    it('computes delta label and class against catalog price', () => {
      fixture.componentRef.setInput('catalogUnitPriceHT', 100);
      component.formPrice = 110;
      expect(component.formPriceDeltaLabel()).toBe('+10.0 %');
      expect(component.formPriceDeltaClass()).toBe('ft-delta--up');

      component.formPrice = 90;
      expect(component.formPriceDeltaLabel()).toBe('-10.0 %');
      expect(component.formPriceDeltaClass()).toBe('ft-delta--down');
    });
  });
});
