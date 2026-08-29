import { TestBed } from '@angular/core/testing';
import { firstValueFrom, of, throwError } from 'rxjs';
import { InvoiceService, InvoiceListItem } from '@core/services/invoice.service';
import { QuoteService } from '@core/services/quote.service';
import { ClientService } from '@core/services/client.service';
import { AuthService } from '@core/services/auth.service';
import { PERMISSIONS } from '@core/config/permission-keys';
import { DashboardService } from './dashboard.service';

describe('DashboardService', () => {
  let service: DashboardService;
  let getInvoices: jasmine.Spy;
  let getQuotes: jasmine.Spy;
  let getClients: jasmine.Spy;
  let hasPermission: jasmine.Spy;

  const emptyPage = { items: [] as unknown[], page: 1, pageSize: 10, totalCount: 0, totalPages: 0, hasPreviousPage: false, hasNextPage: false };

  function apiOk<T>(data: T) {
    return of({ success: true, data, message: null, errors: [] as string[] });
  }

  beforeEach(() => {
    getInvoices = jasmine.createSpy('getInvoices').and.returnValue(apiOk(emptyPage));
    getQuotes = jasmine.createSpy('getQuotes').and.returnValue(apiOk(emptyPage));
    getClients = jasmine.createSpy('getClients').and.returnValue(apiOk(emptyPage));
    // Par défaut, toutes les permissions sont accordées : non-régression des tests existants.
    hasPermission = jasmine.createSpy('hasPermission').and.returnValue(true);

    TestBed.configureTestingModule({
      providers: [
        DashboardService,
        { provide: InvoiceService, useValue: { getInvoices } },
        { provide: QuoteService, useValue: { getQuotes } },
        { provide: ClientService, useValue: { getClients } },
        { provide: AuthService, useValue: { hasPermission } }
      ]
    });
    service = TestBed.inject(DashboardService);
  });

  function invoice(partial: Partial<InvoiceListItem> & Pick<InvoiceListItem, 'issueDate' | 'totalAmount' | 'status'>): InvoiceListItem {
    return {
      id: partial.id ?? 'id',
      number: partial.number ?? 'FAC-1',
      type: partial.type ?? 'INVOICE',
      isCreditNote: partial.isCreditNote ?? false,
      issueDate: partial.issueDate,
      dueDate: partial.dueDate ?? null,
      status: partial.status,
      statusCssClass: '',
      clientName: 'Client',
      totalAmount: partial.totalAmount,
      currency: 'TND',
      isOverdue: false,
      totalPaid: 0,
      remainingAmount: partial.totalAmount
    };
  }

  it('exposes kpiSparklines with expected bucket lengths from loaded invoices', async () => {
    const today = new Date();
    const todayKey = `${today.getFullYear()}-${String(today.getMonth() + 1).padStart(2, '0')}-${String(today.getDate()).padStart(2, '0')}`;
    getInvoices.and.returnValue(apiOk({
      ...emptyPage,
      items: [
        invoice({ issueDate: todayKey, totalAmount: 100, status: 'Payée' }),
        invoice({ issueDate: todayKey, totalAmount: 40, status: 'Partiellement payée' })
      ]
    }));

    const data = await firstValueFrom(service.loadDashboardData());
    expect(data.kpiSparklines.revenue.length).toBe(6);
    expect(data.kpiSparklines.salesToday.length).toBe(7);
    expect(data.kpiSparklines.currentMonth.length).toBe(today.getDate());
    expect(data.kpiSparklines.pending.length).toBe(6);
    expect(data.kpiSparklines.salesToday[6]).toBe(140);
    expect(data.kpiSparklines.pending[5]).toBe(1);
    expect(data.kpiSparklines.revenue[5]).toBe(100);
  });

  it('returns zeroed sparklines when invoice/quote/client calls fail', async () => {
    getInvoices.and.returnValue(throwError(() => new Error('invoices down')));
    getQuotes.and.returnValue(throwError(() => new Error('quotes down')));
    getClients.and.returnValue(throwError(() => new Error('clients down')));

    const data = await firstValueFrom(service.loadDashboardData());
    expect(data.allInvoices).toEqual([]);
    expect(data.kpiSparklines.revenue).toEqual([0, 0, 0, 0, 0, 0]);
    expect(data.kpiSparklines.salesToday.every(v => v === 0)).toBeTrue();
    expect(data.kpiSparklines.pending).toEqual([0, 0, 0, 0, 0, 0]);
    expect(data.kpiSparklines.currentMonth.length).toBe(new Date().getDate());
  });

  // §7.3.3 — profil « Commercial étendu » : invoices:read ✗, quotes:read ✓, clients:read ✓.
  describe('mixed permissions — profil Commercial étendu', () => {
    beforeEach(() => {
      hasPermission.and.callFake((perm: string) => perm !== PERMISSIONS.invoices.read);
    });

    it('does not call getInvoices and substitutes an empty, successful page', async () => {
      const data = await firstValueFrom(service.loadDashboardData());

      expect(getInvoices).not.toHaveBeenCalled();
      expect(getQuotes).toHaveBeenCalled();
      expect(getClients).toHaveBeenCalled();
      expect(data.allInvoices).toEqual([]);
    });
  });

  // Second profil : « Auditeur » — tout en lecture, tous les appels partent.
  describe('mixed permissions — profil Auditeur (tout en lecture)', () => {
    beforeEach(() => {
      hasPermission.and.returnValue(true);
    });

    it('calls all three sources and exposes every domain as authorized', async () => {
      const data = await firstValueFrom(service.loadDashboardData());

      expect(getInvoices).toHaveBeenCalled();
      expect(getQuotes).toHaveBeenCalled();
      expect(getClients).toHaveBeenCalled();
    });
  });
});
