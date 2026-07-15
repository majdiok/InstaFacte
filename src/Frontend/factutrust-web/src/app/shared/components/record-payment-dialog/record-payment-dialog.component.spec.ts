import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { AuthService } from '@core/services/auth.service';
import { InvoiceService } from '@core/services/invoice.service';
import { SupplierInvoiceService } from '@core/services/supplier-invoice.service';
import { ToastService } from '@core/services/toast.service';
import { DrawerOverlayService } from '@core/services/drawer-overlay.service';
import { RecordPaymentDialogComponent } from './record-payment-dialog.component';

describe('RecordPaymentDialogComponent', () => {
  let fixture: ComponentFixture<RecordPaymentDialogComponent>;
  let component: RecordPaymentDialogComponent;
  let authReadonly: boolean;
  let toastAdd: jasmine.Spy;
  let drawerOverlay: DrawerOverlayService;

  beforeEach(async () => {
    authReadonly = false;
    toastAdd = jasmine.createSpy('add');
    await TestBed.configureTestingModule({
      imports: [RecordPaymentDialogComponent],
      providers: [
        provideNoopAnimations(),
        {
          provide: InvoiceService,
          useValue: { recordPayment: jasmine.createSpy('recordPayment') }
        },
        {
          provide: SupplierInvoiceService,
          useValue: { recordPayment: jasmine.createSpy('recordPayment') }
        },
        {
          provide: AuthService,
          useValue: {
            isFirmDelegatedReadonly: () => authReadonly
          }
        },
        {
          provide: ToastService,
          useValue: { add: toastAdd }
        }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(RecordPaymentDialogComponent);
    component = fixture.componentInstance;
    drawerOverlay = TestBed.inject(DrawerOverlayService);
    component.invoiceId = 'inv-1';
    component.invoiceNumber = 'FAC-001';
    component.totalAmount = 100;
    component.remainingAmount = 75;
    component.invoiceType = 'client';
    component.selectedMethod = 0;
    component.amount = 50;
    component.paymentDate = new Date();
  });

  describe('panel mode', () => {
    beforeEach(() => {
      component.panelMode = true;
    });

    it('renders panel overlay and content when visible', () => {
      component.visible = true;
      fixture.detectChanges();

      const el = fixture.nativeElement as HTMLElement;
      expect(el.querySelector('.panel-overlay')).toBeTruthy();
      expect(el.querySelector('.panel-content')).toBeTruthy();
      expect(el.querySelector('p-dialog')).toBeFalsy();
    });

    it('renders p-dialog when panelMode is false', () => {
      component.panelMode = false;
      component.visible = true;
      fixture.detectChanges();

      const el = fixture.nativeElement as HTMLElement;
      expect(el.querySelector('.panel-overlay')).toBeFalsy();
      expect(el.querySelector('p-dialog')).toBeTruthy();
    });

    it('registers drawer overlay when panel opens and closes', () => {
      component.visible = true;
      fixture.detectChanges();
      expect(drawerOverlay.isOpen()).toBeTrue();

      component.visible = false;
      fixture.detectChanges();
      expect(drawerOverlay.isOpen()).toBeFalse();
    });

    it('closes on Escape in panel mode', () => {
      component.visible = true;
      fixture.detectChanges();

      const emitSpy = spyOn(component.visibleChange, 'emit');
      component.onEscape();

      expect(emitSpy).toHaveBeenCalledWith(false);
    });

    it('resets form when opened', () => {
      component.amount = 10;
      component.reference = 'stale';
      component.notes = 'stale notes';
      component.visible = true;
      fixture.detectChanges();

      expect(component.amount).toBe(75);
      expect(component.selectedMethod).toBe(0);
      expect(component.reference).toBe('');
      expect(component.notes).toBe('');
    });

    it('shows client title in panel mode', () => {
      component.visible = true;
      fixture.detectChanges();

      const title = fixture.nativeElement.querySelector('.panel-title') as HTMLElement;
      expect(title.textContent?.trim()).toBe('Enregistrer un paiement');
    });

    it('shows supplier title in panel mode', () => {
      component.invoiceType = 'supplier';
      component.visible = true;
      fixture.detectChanges();

      const title = fixture.nativeElement.querySelector('.panel-title') as HTMLElement;
      expect(title.textContent?.trim()).toBe('Payer la facture');
    });
  });

  describe('firm delegated readonly', () => {
    it('blocks submit and shows toast in firm delegated readonly mode', () => {
      authReadonly = true;
      component.visible = true;
      fixture.detectChanges();
      const invoiceService = TestBed.inject(InvoiceService);

      component.submit();

      expect(invoiceService.recordPayment).not.toHaveBeenCalled();
      expect(toastAdd).toHaveBeenCalledWith(
        jasmine.objectContaining({ severity: 'warn', summary: 'Lecture seule' })
      );
    });
  });
});
