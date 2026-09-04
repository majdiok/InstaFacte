import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { of, throwError } from 'rxjs';
import { ProjectApiService, ProjectLinkedInvoice } from '../project-api.service';
import { ProjectInvoicesSectionComponent } from './project-invoices-section.component';
import { InvoiceService } from '@core/services/invoice.service';
import { AuthService } from '@core/services/auth.service';
import { ToastService } from '@core/services/toast.service';
import { ErrorHandlerService } from '@core/services/error-handler.service';

const sampleInvoices: ProjectLinkedInvoice[] = [
  {
    invoiceId: 'inv-1',
    number: 'FAC-2026-00001',
    issueDate: '2026-02-15',
    clientName: 'Client test',
    amountHT: 200,
    amountVat: 38,
    amountTTC: 238,
    currency: 'TND',
    status: 'Validated',
    statusDisplay: 'Validée',
    isCreditNote: false,
    createdAt: '2026-02-15T10:00:00Z'
  }
];

describe('ProjectInvoicesSectionComponent', () => {
  let fixture: ComponentFixture<ProjectInvoicesSectionComponent>;
  let apiSpy: jasmine.SpyObj<ProjectApiService>;
  let authSpy: jasmine.SpyObj<AuthService>;

  async function setup(options: { invoices?: ProjectLinkedInvoice[] | null; canReadInvoices?: boolean; apiError?: boolean } = {}): Promise<void> {
    apiSpy = jasmine.createSpyObj<ProjectApiService>('ProjectApiService', ['linkedInvoices']);
    if (options.apiError) {
      apiSpy.linkedInvoices.and.returnValue(throwError(() => new Error('network')));
    } else {
      apiSpy.linkedInvoices.and.returnValue(of({
        success: true,
        data: options.invoices ?? sampleInvoices
      }));
    }

    authSpy = jasmine.createSpyObj<AuthService>('AuthService', ['hasPermission']);
    authSpy.hasPermission.and.callFake((key: string) =>
      key === 'invoices:read' ? (options.canReadInvoices ?? true) : false);

    await TestBed.configureTestingModule({
      imports: [ProjectInvoicesSectionComponent],
      providers: [
        provideRouter([]),
        provideNoopAnimations(),
        { provide: ProjectApiService, useValue: apiSpy },
        { provide: InvoiceService, useValue: jasmine.createSpyObj('InvoiceService', ['downloadPdf']) },
        { provide: AuthService, useValue: authSpy },
        { provide: ToastService, useValue: jasmine.createSpyObj('ToastService', ['add']) },
        { provide: ErrorHandlerService, useValue: jasmine.createSpyObj('ErrorHandlerService', ['logError', 'extractErrorMessage']) }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(ProjectInvoicesSectionComponent);
    fixture.componentInstance.projectId = 'proj-1';
    fixture.componentInstance.currency = 'TND';
    fixture.detectChanges();
  }

  it('loads linked invoices on init', async () => {
    await setup();
    expect(apiSpy.linkedInvoices).toHaveBeenCalledWith('proj-1');
    expect(fixture.nativeElement.textContent).toContain('FAC-2026-00001');
    expect(fixture.nativeElement.textContent).toContain('Client test');
  });

  it('shows empty state when no invoices', async () => {
    await setup({ invoices: [] });
    expect(fixture.nativeElement.textContent).toContain('Aucune facture');
  });

  it('hides action buttons when user lacks invoices:read', async () => {
    await setup({ canReadInvoices: false });
    expect(fixture.nativeElement.querySelector('.actions')).toBeNull();
    expect(fixture.nativeElement.querySelector('a.invoice-number')).toBeNull();
  });

  it('reloads when refreshToken changes', async () => {
    await setup();
    expect(apiSpy.linkedInvoices).toHaveBeenCalledTimes(1);
    fixture.componentInstance.refreshToken = 1;
    fixture.detectChanges();
    expect(apiSpy.linkedInvoices).toHaveBeenCalledTimes(2);
  });
});
