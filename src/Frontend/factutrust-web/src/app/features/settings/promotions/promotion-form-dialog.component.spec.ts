import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { of } from 'rxjs';
import {
  PromotionFormDialogComponent,
  PromotionFormValue,
  toCreateRequest,
  toUpdateRequest
} from './promotion-form-dialog.component';
import { ProductCategoryService } from '@core/services/product-category.service';

describe('PromotionFormDialog', () => {
  let fixture: ComponentFixture<PromotionFormDialogComponent>;
  let component: PromotionFormDialogComponent;

  const base: PromotionFormValue = {
    name: ' Promo test ',
    startsOn: '2026-08-01',
    endsOn: '2026-08-31',
    discountType: 'Percentage',
    discountPercent: 12,
    discountAmount: null,
    minQuantity: 2,
    priority: 1,
    isActive: true,
    productId: 'prod-1',
    productCategoryId: null,
    clientId: 'client-1'
  };

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [PromotionFormDialogComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        {
          provide: ProductCategoryService,
          useValue: {
            getCategoryOptionsForDropdown: () => of([])
          }
        }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(PromotionFormDialogComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  function setValidForm(): void {
    component.form = {
      name: 'Promo test',
      startsOn: '2026-08-01',
      endsOn: '2026-08-31',
      discountType: 'Percentage',
      discountPercent: 10,
      discountAmount: null,
      minQuantity: 1,
      priority: 0,
      isActive: true,
      productId: null,
      productCategoryId: null,
      clientId: null
    };
    component.selectedProduct = null;
    component.selectedCategoryId = null;
    component.selectedClient = null;
  }

  describe('mappers', () => {
    it('maps create request with scope', () => {
      const request = toCreateRequest({ ...base, name: ' Promo test ' });
      expect(request.productId).toBe('prod-1');
      expect(request.clientId).toBe('client-1');
      expect(request.discountPercent).toBe(12);
    });

    it('maps update request with scope fields', () => {
      const request = toUpdateRequest(base);
      expect(request.isActive).toBe(true);
      expect(request.productCategoryId).toBeNull();
      expect(request.clientId).toBe('client-1');
    });

    it('clears amount when percentage type is selected', () => {
      const request = toCreateRequest({
        ...base,
        discountType: 'Amount',
        discountPercent: 5,
        discountAmount: 3
      });
      expect(request.discountPercent).toBeNull();
      expect(request.discountAmount).toBe(3);
    });
  });

  describe('dateRangeInvalid', () => {
    it('returns true when end date precedes start date', () => {
      component.form.startsOn = '2026-08-31';
      component.form.endsOn = '2026-08-01';
      expect(component.dateRangeInvalid()).toBeTrue();
    });

    it('returns false when dates are valid', () => {
      component.form.startsOn = '2026-08-01';
      component.form.endsOn = '2026-08-31';
      expect(component.dateRangeInvalid()).toBeFalse();
    });
  });

  describe('canSave', () => {
    it('returns true for a valid percentage promotion', () => {
      setValidForm();
      expect(component.canSave()).toBeTrue();
    });

    it('returns false when name is empty', () => {
      setValidForm();
      component.form.name = '   ';
      expect(component.canSave()).toBeFalse();
    });

    it('returns false when dates are missing', () => {
      setValidForm();
      component.form.startsOn = '';
      expect(component.canSave()).toBeFalse();
    });

    it('returns false when date range is invalid', () => {
      setValidForm();
      component.form.startsOn = '2026-08-31';
      component.form.endsOn = '2026-08-01';
      expect(component.canSave()).toBeFalse();
    });

    it('returns false when product and category are both selected', () => {
      setValidForm();
      component.selectedProduct = { id: 'p1', label: 'Produit A' };
      component.selectedCategoryId = 'cat1';
      expect(component.canSave()).toBeFalse();
    });

    it('returns false when percentage discount is zero', () => {
      setValidForm();
      component.form.discountPercent = 0;
      expect(component.canSave()).toBeFalse();
    });

    it('returns true for amount discount when amount is positive', () => {
      setValidForm();
      component.form.discountType = 'Amount';
      component.form.discountPercent = null;
      component.form.discountAmount = 5;
      expect(component.canSave()).toBeTrue();
    });

    it('returns false for amount discount when amount is zero', () => {
      setValidForm();
      component.form.discountType = 'Amount';
      component.form.discountPercent = null;
      component.form.discountAmount = 0;
      expect(component.canSave()).toBeFalse();
    });
  });

  describe('scopePreview', () => {
    it('includes product and client labels', () => {
      component.selectedProduct = { id: 'p1', label: 'P001 — Chaise' };
      component.selectedClient = { id: 'c1', label: 'Client VIP' };
      component.selectedCategoryId = null;
      component.categoryOptions.set([]);

      expect(component.scopePreview()).toBe('« P001 — Chaise » · Client « Client VIP »');
    });

    it('includes category label when no product is selected', () => {
      component.selectedProduct = null;
      component.selectedClient = null;
      component.selectedCategoryId = 'cat1';
      component.categoryOptions.set([{ label: 'Salon', value: 'cat1' }]);

      expect(component.scopePreview()).toBe('Catégorie « Salon » · Tous les clients');
    });
  });
});
