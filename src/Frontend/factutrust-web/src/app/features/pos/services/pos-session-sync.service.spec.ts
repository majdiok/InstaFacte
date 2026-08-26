import { fakeAsync, TestBed, tick } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { WarehouseContextService } from '@core/services/warehouse-context.service';
import { PosState, PosStateService } from './pos-state.service';
import { PosRegisterSessionService } from './pos-register-session.service';
import { PosSessionSyncService } from './pos-session-sync.service';

const warehouseId = '11111111-1111-1111-1111-111111111111';

function snapshot(lines: number): PosState {
  return {
    sessionId: 'ticket-1',
    client: null,
    lines: Array.from({ length: lines }, (_, i) => ({ id: `l${i}` })) as PosState['lines'],
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

describe('PosSessionSyncService', () => {
  let service: PosSessionSyncService;
  let http: HttpTestingController;
  let posState: { getSnapshot: jasmine.Spy };

  beforeEach(() => {
    localStorage.removeItem('factutrust_pos_cloud_draft');
    localStorage.removeItem('factutrust_pos_draft');
    posState = { getSnapshot: jasmine.createSpy('getSnapshot').and.returnValue(snapshot(1)) };

    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        PosSessionSyncService,
        { provide: PosStateService, useValue: posState },
        { provide: WarehouseContextService, useValue: { selectedWarehouseId: () => warehouseId } },
        { provide: PosRegisterSessionService, useValue: { selectedRegisterId: () => null } }
      ]
    });
    service = TestBed.inject(PosSessionSyncService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    http.verify();
    localStorage.removeItem('factutrust_pos_cloud_draft');
    localStorage.removeItem('factutrust_pos_draft');
  });

  function cartRequest(method: string) {
    return http.expectOne(
      r => r.method === method && r.url.includes('/pos/cart') && r.params.get('warehouseId') === warehouseId
    );
  }

  it('debounces cart PUT to /api/pos/cart', fakeAsync(() => {
    service.scheduleSave();
    service.scheduleSave();
    http.expectNone(r => r.url.includes('/pos/cart'));

    tick(1000);
    const req = cartRequest('PUT');
    expect(req.request.body.warehouseId).toBe(warehouseId);
    expect(req.request.body.state.sessionId).toBe('ticket-1');
    req.flush({ success: true });
  }));

  it('serializes overlapping cart PUTs instead of sending them in parallel', fakeAsync(() => {
    service.scheduleSave();
    tick(1000);

    const first = cartRequest('PUT');
    expect(first.request.body.state.sessionId).toBe('ticket-1');

    posState.getSnapshot.and.returnValue(snapshot(2));
    service.scheduleSave();
    tick(1000);
    http.expectNone(r => r.method === 'PUT' && r.url.includes('/pos/cart'));

    first.flush({ success: true });

    const second = cartRequest('PUT');
    expect(second.request.body.state.lines.length).toBe(2);
    second.flush({ success: true });
  }));

    it('prefers the server cart when it has lines', () => {
    const serverState = snapshot(2);
    serverState.sessionId = 'server-ticket';

    service.getFromCloud().subscribe(loaded => {
      expect(loaded?.sessionId).toBe('server-ticket');
      expect(loaded?.lines.length).toBe(2);
    });

    cartRequest('GET').flush({ data: { state: serverState } });
  });

  it('falls back to localStorage when the cart API fails', () => {
    const local = snapshot(1);
    local.sessionId = 'local-ticket';
    localStorage.setItem('factutrust_pos_cloud_draft', JSON.stringify(local));

    service.getFromCloud().subscribe(loaded => {
      expect(loaded?.sessionId).toBe('local-ticket');
    });

    cartRequest('GET').flush('error', { status: 500, statusText: 'Server Error' });
  });

  it('clears the server cart after the last line disappears', fakeAsync(() => {
    service.scheduleSave();
    tick(1000);
    cartRequest('PUT').flush({ success: true });

    posState.getSnapshot.and.returnValue(snapshot(0));
    service.scheduleSave();
    tick(1000);

    cartRequest('DELETE').flush({ success: true });
    expect(posState.getSnapshot().lines.length).toBe(0);
  }));
});
