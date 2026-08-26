import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { environment } from '@environments/environment';
import { WarehouseContextService } from '@core/services/warehouse-context.service';
import { PosState, PosStateService } from './pos-state.service';
import { PosHeldOrdersService } from './pos-held-orders.service';

const warehouseId = '11111111-1111-1111-1111-111111111111';
const ticketId = '22222222-2222-2222-2222-222222222222';

function snapshot(): PosState {
  return {
    sessionId: 'ticket-held',
    client: {
      id: 'c1',
      name: 'Client test',
      email: '',
      phone: '',
      nif: '',
      isWalkIn: true
    },
    lines: [{ id: 'l1' }] as PosState['lines'],
    paymentMethod: 'CASH' as PosState['paymentMethod'],
    currency: 'TND' as PosState['currency'],
    isDirty: true,
    isProcessing: false,
    lastError: null,
    globalDiscountType: null,
    globalDiscountValue: null,
    globalDiscountAmount: 0,
    isSplitPayment: false,
    paymentSplits: [],
    orderNotes: '',
    isQuickMode: false,
    isCreditNote: false,
    linkedInvoice: null,
    printMode: 'receipt',
    isDemoMode: false,
    paymentSchedule: 'immediate',
    firstPurchaseDiscountPercent: null,
    clientOutstanding: null,
    overLimitAcknowledged: false
  };
}

describe('PosHeldOrdersService', () => {
  let service: PosHeldOrdersService;
  let http: HttpTestingController;
  let restoreSnapshot: jasmine.Spy;
  let resetOrder: jasmine.Spy;

  beforeEach(() => {
    restoreSnapshot = jasmine.createSpy('restoreSnapshot');
    resetOrder = jasmine.createSpy('resetOrder');

    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        PosHeldOrdersService,
        {
          provide: PosStateService,
          useValue: {
            getSnapshot: () => snapshot(),
            totals: () => ({ totalTTC: 12.5 }),
            resetOrder,
            restoreSnapshot
          }
        },
        { provide: WarehouseContextService, useValue: { selectedWarehouseId: () => warehouseId } }
      ]
    });
    service = TestBed.inject(PosHeldOrdersService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  async function waitForRequest(method: string, urlPart: string) {
    const deadline = Date.now() + 2000;
    while (Date.now() < deadline) {
      const match = http.match(r => r.method === method && r.url.includes(urlPart));
      if (match.length === 1) {
        return match[0];
      }
      await new Promise(resolve => setTimeout(resolve, 10));
    }
    throw new Error(`Timed out waiting for ${method} ${urlPart}`);
  }

  it('loads remote held tickets and skips IndexedDB import when the server already has some', async () => {
    const pending = service.initialize();
    const req = await waitForRequest('GET', '/pos/held-tickets');
    req.flush({
      success: true,
      data: [
        {
          id: ticketId,
          label: 'Client test - 1 article',
          totalTtc: 12.5,
          lineCount: 1,
          heldAt: '2026-08-18T10:00:00Z',
          state: snapshot()
        }
      ]
    });

    await pending;
    expect(service.heldOrderCount()).toBe(1);
    expect(service.heldOrdersList()[0].label).toContain('Client test');
  });

  it('holds the current order via POST then resets the cart', async () => {
    const pending = service.holdCurrentOrder();
    const req = http.expectOne(`${environment.apiUrl}/pos/held-tickets`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body.warehouseId).toBe(warehouseId);
    expect(req.request.body.label).toContain('Client test');
    req.flush({
      success: true,
      data: {
        id: ticketId,
        label: req.request.body.label,
        totalTtc: 12.5,
        lineCount: 1,
        heldAt: '2026-08-18T10:00:00Z',
        state: snapshot()
      }
    });

    const id = await pending;
    expect(id).toBe(ticketId);
    expect(resetOrder).toHaveBeenCalled();
    expect(service.heldOrderCount()).toBe(1);
  });

  it('recalls a ticket, restores the snapshot and removes it from the list', async () => {
    const held = service.holdCurrentOrder();
    http.expectOne(`${environment.apiUrl}/pos/held-tickets`).flush({
      success: true,
      data: {
        id: ticketId,
        label: 'held',
        totalTtc: 12.5,
        lineCount: 1,
        heldAt: '2026-08-18T10:00:00Z',
        state: snapshot()
      }
    });
    await held;

    const pending = service.recallOrder(ticketId);
    const req = http.expectOne(`${environment.apiUrl}/pos/held-tickets/${ticketId}/recall`);
    expect(req.request.method).toBe('POST');
    req.flush({
      success: true,
      data: {
        id: ticketId,
        label: 'held',
        totalTtc: 12.5,
        lineCount: 1,
        heldAt: '2026-08-18T10:00:00Z',
        state: snapshot()
      }
    });

    await expectAsync(pending).toBeResolvedTo(true);
    expect(restoreSnapshot).toHaveBeenCalled();
    expect(service.heldOrderCount()).toBe(0);
  });
});
