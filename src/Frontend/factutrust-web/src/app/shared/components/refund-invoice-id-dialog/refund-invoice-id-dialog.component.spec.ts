import { ComponentFixture, TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { NgbActiveModal } from '@ng-bootstrap/ng-bootstrap';
import { provideAnimations } from '@angular/platform-browser/animations';
import { RefundInvoiceIdDialogComponent } from './refund-invoice-id-dialog.component';
import { InvoiceReferenceResolverService } from '@core/services/invoice-reference-resolver.service';
import { InvoiceListItem } from '@core/services/invoice.service';

describe('RefundInvoiceIdDialogComponent', () => {
  let fixture: ComponentFixture<RefundInvoiceIdDialogComponent>;
  let component: RefundInvoiceIdDialogComponent;
  let modal: jasmine.SpyObj<NgbActiveModal>;
  let resolver: jasmine.SpyObj<InvoiceReferenceResolverService>;

  const sampleInvoice: InvoiceListItem = {
    id: 'inv-1',
    number: 'FAC-2026-000028',
    type: 'INVOICE',
    isCreditNote: false,
    issueDate: '2026-01-15',
    dueDate: '2026-02-15',
    status: 'Validée',
    statusCssClass: 'status-validated',
    clientName: 'Client passager',
    totalAmount: 6601.9,
    currency: 'TND',
    isOverdue: false,
    totalPaid: 0,
    remainingAmount: 6601.9,
  };

  beforeEach(async () => {
    modal = jasmine.createSpyObj<NgbActiveModal>('NgbActiveModal', ['close', 'dismiss']);
    resolver = jasmine.createSpyObj<InvoiceReferenceResolverService>(
      'InvoiceReferenceResolverService',
      ['searchInvoices', 'resolveReference']
    );
    resolver.searchInvoices.and.returnValue(of([sampleInvoice]));
    resolver.resolveReference.and.returnValue(of({
      id: sampleInvoice.id,
      number: sampleInvoice.number,
      clientName: sampleInvoice.clientName,
      status: sampleInvoice.status,
      totalTTC: sampleInvoice.totalAmount,
    }));

    await TestBed.configureTestingModule({
      imports: [RefundInvoiceIdDialogComponent],
      providers: [
        provideAnimations(),
        { provide: NgbActiveModal, useValue: modal },
        { provide: InvoiceReferenceResolverService, useValue: resolver },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(RefundInvoiceIdDialogComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('renders the invoice search label and disables submit initially', () => {
    const label = fixture.nativeElement.querySelector('label[for="refundInvoiceSearch"]');
    const submitBtn: HTMLButtonElement = fixture.nativeElement.querySelector('.refund-dialog-btn.accept');

    expect(label?.textContent).toContain('Facture à rembourser');
    expect(submitBtn.disabled).toBeTrue();
  });

  it('searches invoices and reserves space when suggestions are returned', () => {
    component.onSearch({ query: 'fac' });
    fixture.detectChanges();

    expect(resolver.searchInvoices).toHaveBeenCalledWith('fac');
    expect(component.suggestions).toEqual([sampleInvoice]);

    const searchWrap: HTMLElement = fixture.nativeElement.querySelector('.refund-dialog-search');
    expect(searchWrap.classList.contains('refund-dialog-search--open')).toBeTrue();
  });

  it('enables submit after selecting an invoice from suggestions', () => {
    component.onInvoiceSelect({ value: sampleInvoice });
    fixture.detectChanges();

    expect(component.searchText).toBe('FAC-2026-000028');
    expect(component.canSubmit()).toBeTrue();
  });

  it('closes the modal with the selected invoice on submit', () => {
    component.onInvoiceSelect({ value: sampleInvoice });
    component.onSubmit();

    expect(modal.close).toHaveBeenCalledWith(jasmine.objectContaining({
      id: 'inv-1',
      number: 'FAC-2026-000028',
      clientName: 'Client passager',
    }));
  });

  it('shows an error when submitting with an empty query', () => {
    component.searchText = '   ';
    component.onSubmit();
    fixture.detectChanges();

    expect(component.errorMessage()).toBe('Veuillez saisir ou sélectionner une facture.');
    expect(modal.close).not.toHaveBeenCalled();
  });

  it('resolves a typed invoice number when nothing was selected', () => {
    component.searchText = 'FAC-2026-000028';
    component.onSubmit();

    expect(resolver.resolveReference).toHaveBeenCalledWith('FAC-2026-000028');
    expect(modal.close).toHaveBeenCalledWith(jasmine.objectContaining({ id: 'inv-1' }));
  });

  it('shows resolver error when manual resolution fails', () => {
    resolver.resolveReference.and.returnValue(throwError(() => new Error('Facture introuvable.')));
    component.searchText = 'FAC-UNKNOWN';
    component.onSubmit();
    fixture.detectChanges();

    expect(component.isResolving()).toBeFalse();
    expect(component.errorMessage()).toBe('Facture introuvable.');
    expect(modal.close).not.toHaveBeenCalled();
  });

  it('dismisses the modal when cancel is clicked', () => {
    const cancelBtn: HTMLButtonElement = fixture.nativeElement.querySelector('.refund-dialog-btn.reject');
    cancelBtn.click();

    expect(modal.dismiss).toHaveBeenCalled();
  });
});
