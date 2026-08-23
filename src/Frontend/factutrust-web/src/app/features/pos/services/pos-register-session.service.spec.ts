import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { environment } from '@environments/environment';
import {
  CashRegisterDto,
  CashRegisterSessionDto,
  PosRegisterSessionService,
  PosSessionReportDto
} from './pos-register-session.service';

const warehouseId = '11111111-1111-1111-1111-111111111111';

function register(overrides?: Partial<CashRegisterDto>): CashRegisterDto {
  return {
    id: 'reg-1',
    code: 'WH-MAIN',
    name: 'Caisse Principal',
    warehouseId,
    isActive: true,
    requireOpenSession: true,
    ...overrides
  };
}

function session(overrides?: Partial<CashRegisterSessionDto>): CashRegisterSessionDto {
  return {
    id: 'sess-1',
    cashRegisterId: 'reg-1',
    cashRegisterCode: 'WH-MAIN',
    cashRegisterName: 'Caisse Principal',
    warehouseId,
    status: 0,
    openedAt: '2026-08-18T08:00:00Z',
    openedByUserId: 'user-1',
    openingFloat: 50,
    ...overrides
  };
}

function xReport(overrides?: Partial<PosSessionReportDto>): PosSessionReportDto {
  return {
    sessionId: 'sess-1',
    cashRegisterId: 'reg-1',
    cashRegisterName: 'Caisse Principal',
    openedAt: '2026-08-18T08:00:00Z',
    openingFloat: 50,
    expectedCash: 50,
    invoiceCount: 0,
    creditNoteCount: 0,
    heldTicketCount: 0,
    totalsByMethod: [],
    invoiceIds: [],
    ...overrides
  };
}

describe('PosRegisterSessionService', () => {
  let service: PosRegisterSessionService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), PosRegisterSessionService]
    });
    service = TestBed.inject(PosRegisterSessionService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('blocks selling until an open session exists when the flag is on', () => {
    expect(service.canSell()).toBeFalse();

    service.hydrate(warehouseId).subscribe();
    const reqs = http.match(r => r.url.includes('/pos/register') || r.url.includes('/pos/open-session'));
    expect(reqs.length).toBe(2);
    reqs.find(r => r.request.url.includes('/pos/register'))!
      .flush({ success: true, data: register({ requireOpenSession: true }) });
    reqs.find(r => r.request.url.includes('/pos/open-session'))!
      .flush({ success: true, data: null });

    expect(service.canSell()).toBeFalse();
  });

  it('allows selling without a session when the flag is off', () => {
    service.hydrate(warehouseId).subscribe();
    const reqs = http.match(r => r.url.includes('/pos/register') || r.url.includes('/pos/open-session'));
    reqs.find(r => r.request.url.includes('/pos/register'))!
      .flush({ success: true, data: register({ requireOpenSession: false }) });
    reqs.find(r => r.request.url.includes('/pos/open-session'))!
      .flush({ success: true, data: null });

    expect(service.canSell()).toBeTrue();
    expect(service.currentSession()).toBeNull();
  });

  it('hydrates the open session and opens a new one', () => {
    const opened = session();
    service.open(warehouseId, 25).subscribe(result => {
      expect(result.id).toBe(opened.id);
    });

    const req = http.expectOne(`${environment.apiUrl}/pos/session/open`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ warehouseId, openingFloat: 25 });
    req.flush({ success: true, data: opened });

    expect(service.currentSession()?.id).toBe('sess-1');
    expect(service.canSell()).toBeTrue();
  });

  it('clears the session after a successful Z close', () => {
    service.open(warehouseId, 0).subscribe();
    http.expectOne(`${environment.apiUrl}/pos/session/open`).flush({ success: true, data: session() });
    expect(service.currentSession()).not.toBeNull();

    service.close(warehouseId, 12).subscribe(report => {
      expect(report.zReportNumber).toBe('Z-2026-000001');
    });

    const req = http.expectOne(
      r => r.method === 'POST' && r.url.startsWith(`${environment.apiUrl}/pos/session/close`)
    );
    expect(req.request.body).toEqual({ countedCash: 12, notes: null });
    req.flush({ success: true, data: xReport({ zReportNumber: 'Z-2026-000001', countedCash: 12 }) });

    expect(service.currentSession()).toBeNull();
  });
});
