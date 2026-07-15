import { TestBed } from '@angular/core/testing';
import { provideRouter, ActivatedRoute } from '@angular/router';
import { of } from 'rxjs';
import { AuthService } from '@core/services/auth.service';
import { InvoiceService } from '@core/services/invoice.service';
import { SupplierInvoiceService } from '@core/services/supplier-invoice.service';
import { PaymentsComponent } from './payments.component';

describe('PaymentsComponent — firm delegated readonly', () => {
  let component: PaymentsComponent;
  let authReadonly: boolean;

  const clientPayment = {
    id: 'inv-1',
    number: 'FAC-001',
    type: 'client' as const,
    status: 'Validée',
    counterpartyName: 'Client A',
    issueDate: '2026-01-01',
    dueDate: '2026-02-01',
    totalAmount: 100,
    totalPaid: 0,
    remainingAmount: 100,
    currency: 'TND',
    paidAt: null,
    isOverdue: false,
    detailRoute: ['/invoices', 'inv-1']
  };

  beforeEach(() => {
    authReadonly = false;
    TestBed.configureTestingModule({
      imports: [PaymentsComponent],
      providers: [
        provideRouter([]),
        {
          provide: ActivatedRoute,
          useValue: {
            data: of({ paymentType: 'client' }),
            snapshot: { data: { paymentType: 'client' } }
          }
        },
        {
          provide: InvoiceService,
          useValue: {
            getInvoices: () => of({ success: true, data: { items: [], totalCount: 0 } })
          }
        },
        {
          provide: SupplierInvoiceService,
          useValue: {
            getInvoices: () => of({ success: true, data: { items: [], totalCount: 0 } })
          }
        },
        {
          provide: AuthService,
          useValue: {
            isFirmDelegatedReadonly: () => authReadonly
          }
        }
      ]
    });

    const fixture = TestBed.createComponent(PaymentsComponent);
    component = fixture.componentInstance;
  });

  it('canRecordPayment returns true for eligible client invoice when not readonly', () => {
    authReadonly = false;
    expect(component.canRecordPayment(clientPayment)).toBe(true);
  });

  it('canRecordPayment returns false in firm delegated readonly mode', () => {
    authReadonly = true;
    expect(component.canRecordPayment(clientPayment)).toBe(false);
  });

  it('openRecordPaymentDialog does nothing when readonly', () => {
    authReadonly = true;
    component.openRecordPaymentDialog(clientPayment);
    expect(component.recordPaymentDialogVisible).toBe(false);
  });
});
