import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { of, throwError } from 'rxjs';
import { ToastService } from '@core/services/toast.service';
import { DrawerOverlayService } from '@core/services/drawer-overlay.service';
import { HonorairesService } from '../../services/honoraires.service';
import { HonorairesRecordPaymentDialogComponent } from './honoraires-record-payment-dialog.component';

describe('HonorairesRecordPaymentDialogComponent', () => {
  let fixture: ComponentFixture<HonorairesRecordPaymentDialogComponent>;
  let component: HonorairesRecordPaymentDialogComponent;
  let toastAdd: jasmine.Spy;
  let recordPayment: jasmine.Spy;
  let drawerOverlay: DrawerOverlayService;

  beforeEach(async () => {
    toastAdd = jasmine.createSpy('add');
    recordPayment = jasmine.createSpy('recordPayment').and.returnValue(of('pay-1'));

    await TestBed.configureTestingModule({
      imports: [HonorairesRecordPaymentDialogComponent],
      providers: [
        provideNoopAnimations(),
        {
          provide: HonorairesService,
          useValue: { recordPayment }
        },
        {
          provide: ToastService,
          useValue: { add: toastAdd }
        }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(HonorairesRecordPaymentDialogComponent);
    component = fixture.componentInstance;
    drawerOverlay = TestBed.inject(DrawerOverlayService);
    component.invoiceId = 'inv-1';
    component.invoiceNumber = 'FAC-2026-000001';
    component.currency = 'TND';
    component.totalAmount = 190.4;
    component.amountDue = 190.4;
    component.invoiceWithholdingAmount = 0;
    component.paymentsClientWithholdingTotal = 0;
    component.defaultBankAccountLabel = 'Compte pro';
  });

  it('renders panel overlay and content when visible (no p-dialog)', () => {
    component.visible = true;
    fixture.detectChanges();

    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('.panel-overlay')).toBeTruthy();
    expect(el.querySelector('.panel-content')).toBeTruthy();
    expect(el.querySelector('p-dialog')).toBeFalsy();
  });

  it('renders all form fields and footer actions', () => {
    component.visible = true;
    fixture.detectChanges();

    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('#hon-pay-date')).toBeTruthy();
    expect(el.querySelector('#hon-pay-amount')).toBeTruthy();
    expect(el.querySelector('#hon-pay-rs')).toBeTruthy();
    expect(el.querySelector('#hon-pay-method')).toBeTruthy();
    expect(el.querySelector('#hon-pay-ref')).toBeTruthy();
    expect(el.querySelector('#hon-pay-bank')).toBeTruthy();
    expect(el.querySelector('#hon-pay-notes')).toBeTruthy();

    const title = el.querySelector('#hon-pay-panel-title') as HTMLElement;
    expect(title.textContent?.trim()).toBe('Encaissement honoraires');

    const buttons = Array.from(el.querySelectorAll('.panel-footer button'));
    const labels = buttons.map((b) => (b.textContent || '').trim());
    expect(labels.some((l) => l.includes('Annuler'))).toBeTrue();
    expect(labels.some((l) => l.includes("Enregistrer l'encaissement"))).toBeTrue();
  });

  it('registers drawer overlay when panel opens and closes', () => {
    component.visible = true;
    fixture.detectChanges();
    expect(drawerOverlay.isOpen()).toBeTrue();

    component.visible = false;
    fixture.detectChanges();
    expect(drawerOverlay.isOpen()).toBeFalse();
  });

  it('closes on Escape and emits visibleChange(false)', () => {
    component.visible = true;
    fixture.detectChanges();

    const emitSpy = spyOn(component.visibleChange, 'emit');
    component.onEscape();

    expect(emitSpy).toHaveBeenCalledWith(false);
    expect(component.visible).toBeFalse();
  });

  it('resets form when opened', () => {
    component.amount = 10;
    component.clientWithholdingAmount = 5;
    component.method = 99;
    component.reference = 'stale';
    component.notes = 'stale notes';
    component.bankAccountLabel = 'stale bank';
    component.amountDue = 100;
    component.invoiceWithholdingAmount = 20;
    component.paymentsClientWithholdingTotal = 0;
    component.defaultBankAccountLabel = 'Compte pro';

    component.visible = true;
    fixture.detectChanges();

    expect(component.amount).toBe(80);
    expect(component.clientWithholdingAmount).toBe(20);
    expect(component.method).toBe(1);
    expect(component.reference).toBe('');
    expect(component.notes).toBe('');
    expect(component.bankAccountLabel).toBe('Compte pro');
  });

  it('submits successfully, emits paymentRecorded and closes', () => {
    component.visible = true;
    fixture.detectChanges();

    const recordedSpy = spyOn(component.paymentRecorded, 'emit');
    const visibleSpy = spyOn(component.visibleChange, 'emit');

    component.amount = 50;
    component.clientWithholdingAmount = 0;
    component.method = 1;
    component.paymentDate = new Date(2026, 7, 4);
    component.submit();

    expect(recordPayment).toHaveBeenCalled();
    expect(toastAdd).toHaveBeenCalledWith(
      jasmine.objectContaining({ severity: 'success', detail: 'Encaissement enregistré' })
    );
    expect(recordedSpy).toHaveBeenCalled();
    expect(visibleSpy).toHaveBeenCalledWith(false);
    expect(component.visible).toBeFalse();
  });

  it('blocks submit on validation error without calling API', () => {
    component.visible = true;
    fixture.detectChanges();

    component.amount = 200;
    component.clientWithholdingAmount = 0;
    component.amountDue = 100;
    component.method = 1;
    component.paymentDate = new Date();
    component.submit();

    expect(recordPayment).not.toHaveBeenCalled();
    expect(component.errorMessage()).toBe('Le montant dépasse le reste dû');
  });

  it('unregisters drawer overlay on destroy while open', () => {
    component.visible = true;
    fixture.detectChanges();
    expect(drawerOverlay.isOpen()).toBeTrue();

    fixture.destroy();
    expect(drawerOverlay.isOpen()).toBeFalse();
  });

  it('shows API error message on failed submit', () => {
    recordPayment.and.returnValue(
      throwError(() => ({ error: { message: 'Solde insuffisant' } }))
    );
    component.visible = true;
    fixture.detectChanges();

    component.amount = 50;
    component.clientWithholdingAmount = 0;
    component.method = 1;
    component.paymentDate = new Date();
    component.submit();

    expect(component.errorMessage()).toBe('Solde insuffisant');
    expect(toastAdd).toHaveBeenCalledWith(
      jasmine.objectContaining({ severity: 'error', detail: 'Solde insuffisant' })
    );
    expect(component.visible).toBeTrue();
  });
});
