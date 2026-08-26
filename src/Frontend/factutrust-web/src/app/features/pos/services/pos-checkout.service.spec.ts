import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { InvoiceService } from '@core/services/invoice.service';
import { WarehouseContextService } from '@core/services/warehouse-context.service';
import { InvoiceWizardService } from '../../invoices/invoice-wizard/services/invoice-wizard.service';
import { PosCheckoutService } from './pos-checkout.service';
import { PosRegisterSessionService } from './pos-register-session.service';
import { PosStateService } from './pos-state.service';

describe('PosCheckoutService', () => {
  let service: PosCheckoutService;
  let posState: PosStateService;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        PosCheckoutService,
        PosStateService,
        { provide: InvoiceWizardService, useValue: {} },
        { provide: InvoiceService, useValue: {} },
        { provide: WarehouseContextService, useValue: { selectedWarehouseId: () => null } },
        { provide: PosRegisterSessionService, useValue: { currentSession: () => null } }
      ]
    });
    service = TestBed.inject(PosCheckoutService);
    posState = TestBed.inject(PosStateService);
  });

  it('records payment after submit for cash, not for on-account', () => {
    expect(service.shouldRecordPayment()).toBeTrue();
    posState.setPaymentSchedule('onAccount');
    expect(service.shouldRecordPayment()).toBeFalse();
  });
});
