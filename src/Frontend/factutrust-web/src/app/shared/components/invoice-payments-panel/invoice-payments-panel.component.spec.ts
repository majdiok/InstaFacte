import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { AuthService } from '@core/services/auth.service';
import { InvoiceService } from '@core/services/invoice.service';
import { SupplierInvoiceService } from '@core/services/supplier-invoice.service';
import { InvoicePaymentsPanelComponent } from './invoice-payments-panel.component';

describe('InvoicePaymentsPanelComponent — firm delegated readonly', () => {
  const invoice = {
    id: 'inv-1',
    number: 'FAC-001',
    type: 'client' as const,
    totalAmount: 100,
    totalPaid: 0,
    remainingAmount: 100,
    currency: 'TND'
  };

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [InvoicePaymentsPanelComponent],
      providers: [
        {
          provide: InvoiceService,
          useValue: {
            getInvoicePayments: () => of({ success: true, data: [] })
          }
        },
        {
          provide: SupplierInvoiceService,
          useValue: {
            getPayments: () => of({ success: true, data: [] })
          }
        }
      ]
    }).compileComponents();
  });

  function createWithReadonly(readonly: boolean): InvoicePaymentsPanelComponent {
    TestBed.overrideProvider(AuthService, {
      useValue: { isFirmDelegatedReadonly: () => readonly }
    });
    const fixture = TestBed.createComponent(InvoicePaymentsPanelComponent);
    fixture.componentInstance.invoice = invoice;
    fixture.detectChanges();
    return fixture.componentInstance;
  }

  it('showRecordPayment is true when recording is allowed', () => {
    const component = createWithReadonly(false);
    expect(component.showRecordPayment()).toBe(true);
  });

  it('showRecordPayment is false in firm delegated readonly mode', () => {
    const component = createWithReadonly(true);
    expect(component.showRecordPayment()).toBe(false);
  });
});
