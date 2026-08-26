import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { AuthService } from '@core/services/auth.service';
import { PERMISSIONS } from '@core/config/permission-keys';
import { StockFeaturesStore } from './stock-features-store.service';
import { StockFeatures, StockService } from './stock.service';

const enabledFeatures: StockFeatures = {
  lotTrackingEnabled: true,
  serialTrackingEnabled: false,
  expiryTrackingEnabled: false,
  productVariantsEnabled: true,
  fifoLifoValuationEnabled: false,
  blockExpiredLotsOnExit: false,
  strictTrackedAllocation: false
};

describe('StockFeaturesStore', () => {
  let store: StockFeaturesStore;
  let auth: jasmine.SpyObj<AuthService>;
  let stockService: jasmine.SpyObj<StockService>;

  beforeEach(() => {
    auth = jasmine.createSpyObj('AuthService', ['hasAnyPermission']);
    stockService = jasmine.createSpyObj('StockService', ['getFeatures']);
    stockService.getFeatures.and.returnValue(
      of({ success: true, data: enabledFeatures, message: null, errors: [] })
    );

    TestBed.configureTestingModule({
      providers: [
        StockFeaturesStore,
        { provide: AuthService, useValue: auth },
        { provide: StockService, useValue: stockService }
      ]
    });
    store = TestBed.inject(StockFeaturesStore);
  });

  it('does not call GET /stock/features without stock:read or products:read', () => {
    auth.hasAnyPermission.and.returnValue(false);

    store.ensureLoaded();

    expect(auth.hasAnyPermission).toHaveBeenCalledWith([
      PERMISSIONS.stock.read,
      PERMISSIONS.products.read
    ]);
    expect(stockService.getFeatures).not.toHaveBeenCalled();
    expect(store.productVariantsEnabled()).toBeFalse();
  });

  it('loads later when permissions appear after an initial skip (firm then delegated)', () => {
    auth.hasAnyPermission.and.returnValue(false);
    store.ensureLoaded();
    expect(stockService.getFeatures).not.toHaveBeenCalled();

    auth.hasAnyPermission.and.returnValue(true);
    store.ensureLoaded();

    expect(stockService.getFeatures).toHaveBeenCalledTimes(1);
    expect(store.productVariantsEnabled()).toBeTrue();
  });

  it('loads features when the user has stock:read or products:read', () => {
    auth.hasAnyPermission.and.returnValue(true);

    store.ensureLoaded();

    expect(stockService.getFeatures).toHaveBeenCalledTimes(1);
    expect(store.features()).toEqual(enabledFeatures);
    expect(store.productVariantsEnabled()).toBeTrue();
  });

  it('does not retry after a 403 (latch stays closed)', () => {
    auth.hasAnyPermission.and.returnValue(true);
    stockService.getFeatures.and.returnValue(throwError(() => ({ status: 403 })));

    store.ensureLoaded();
    store.ensureLoaded();

    expect(stockService.getFeatures).toHaveBeenCalledTimes(1);
    expect(store.features()).toBeNull();
    expect(store.productVariantsEnabled()).toBeFalse();
  });
});
