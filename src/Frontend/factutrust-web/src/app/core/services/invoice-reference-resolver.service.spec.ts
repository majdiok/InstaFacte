import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import {
  InvoiceReferenceResolverService,
  isValidInvoiceGuid
} from './invoice-reference-resolver.service';
import { InvoiceService } from './invoice.service';

describe('isValidInvoiceGuid', () => {
  it('accepts valid GUID', () => {
    expect(isValidInvoiceGuid('550e8400-e29b-41d4-a716-446655440000')).toBeTrue();
  });

  it('rejects invoice number fragments', () => {
    expect(isValidInvoiceGuid('vvb')).toBeFalse();
    expect(isValidInvoiceGuid('FAC-2026-000042')).toBeFalse();
  });
});

describe('InvoiceReferenceResolverService', () => {
  let service: InvoiceReferenceResolverService;
  let invoiceService: jasmine.SpyObj<InvoiceService>;

  beforeEach(() => {
    invoiceService = jasmine.createSpyObj<InvoiceService>('InvoiceService', [
      'getInvoice',
      'getInvoices'
    ]);

    TestBed.configureTestingModule({
      providers: [
        InvoiceReferenceResolverService,
        { provide: InvoiceService, useValue: invoiceService }
      ]
    });

    service = TestBed.inject(InvoiceReferenceResolverService);
  });

  it('resolves a valid GUID via getInvoice', (done) => {
    invoiceService.getInvoice.and.returnValue(of({
      success: true,
      data: {
        id: '550e8400-e29b-41d4-a716-446655440000',
        number: 'FAC-2026-000042',
        isCreditNote: false,
        status: 'Validée',
        totalAmount: 120.5,
        client: { id: 'c1', name: 'Client A', nif: null, email: 'a@test.com', address: '' }
      },
      message: null,
      errors: []
    } as any));

    service.resolveReference('550e8400-e29b-41d4-a716-446655440000').subscribe({
      next: ref => {
        expect(ref.id).toBe('550e8400-e29b-41d4-a716-446655440000');
        expect(ref.number).toBe('FAC-2026-000042');
        expect(ref.clientName).toBe('Client A');
        done();
      },
      error: () => fail('should not error')
    });
  });

  it('resolves exact invoice number via search', (done) => {
    invoiceService.getInvoices.and.returnValue(of({
      success: true,
      data: {
        items: [{
          id: '550e8400-e29b-41d4-a716-446655440099',
          number: 'FAC-2026-000042',
          isCreditNote: false,
          status: 'Validée',
          clientName: 'Client B',
          totalAmount: 50
        }],
        totalCount: 1,
        page: 1,
        pageSize: 20
      },
      message: null,
      errors: []
    } as any));

    service.resolveReference('FAC-2026-000042').subscribe({
      next: ref => {
        expect(ref.id).toBe('550e8400-e29b-41d4-a716-446655440099');
        expect(ref.number).toBe('FAC-2026-000042');
        done();
      },
      error: () => fail('should not error')
    });
  });

  it('rejects ambiguous search results', (done) => {
    invoiceService.getInvoices.and.returnValue(of({
      success: true,
      data: {
        items: [
          {
            id: '1',
            number: 'FAC-2026-000041',
            isCreditNote: false,
            status: 'Validée',
            clientName: 'A',
            totalAmount: 10
          },
          {
            id: '2',
            number: 'FAC-2026-000043',
            isCreditNote: false,
            status: 'Validée',
            clientName: 'B',
            totalAmount: 20
          }
        ],
        totalCount: 2,
        page: 1,
        pageSize: 20
      },
      message: null,
      errors: []
    } as any));

    service.resolveReference('FAC').subscribe({
      next: () => fail('should error'),
      error: err => {
        expect(err.message).toContain('Plusieurs factures');
        done();
      }
    });
  });

  it('filters cancelled and draft invoices from search', (done) => {
    invoiceService.getInvoices.and.returnValue(of({
      success: true,
      data: {
        items: [
          {
            id: '1',
            number: 'FAC-2026-000010',
            isCreditNote: false,
            status: 'Annulée',
            clientName: 'A',
            totalAmount: 10
          }
        ],
        totalCount: 1,
        page: 1,
        pageSize: 20
      },
      message: null,
      errors: []
    } as any));

    service.resolveReference('FAC-2026-000010').subscribe({
      next: () => fail('should error'),
      error: err => {
        expect(err.message).toContain('introuvable');
        done();
      }
    });
  });

  it('searchInvoices returns empty for blank query', (done) => {
    service.searchInvoices('   ').subscribe({
      next: items => {
        expect(items).toEqual([]);
        expect(invoiceService.getInvoices).not.toHaveBeenCalled();
        done();
      }
    });
  });
});
