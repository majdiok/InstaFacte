import { TestBed } from '@angular/core/testing';
import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
import { SKIP_ERROR_TOAST } from '@core/http-context';
import { FirmLeavesService } from './firm-leaves.service';
import { environment } from '@environments/environment';

describe('FirmLeavesService', () => {
  let service: FirmLeavesService;
  let http: HttpTestingController;
  const base = `${environment.apiUrl}/firm/governance`;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [HttpClientTestingModule]
    });
    service = TestBed.inject(FirmLeavesService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('lists requests with filters', () => {
    service.listRequests({ year: 2026, status: 1 }).subscribe(r => {
      expect(r.success).toBeTrue();
      expect(r.data?.length).toBe(1);
    });
    const req = http.expectOne(r => r.url === `${base}/leaves` && r.params.get('year') === '2026');
    req.flush({ success: true, data: [{ id: '1', status: 1 }] });
  });

  it('processes approve', () => {
    service.process('abc', true).subscribe(r => expect(r.success).toBeTrue());
    const req = http.expectOne(`${base}/leaves/abc/process`);
    expect(req.request.body).toEqual({ approve: true, rejectionReason: undefined });
    req.flush({ success: true, data: { id: 'abc', status: 2 } });
  });

  it('submits a draft request', () => {
    service.submit('abc').subscribe(r => expect(r.success).toBeTrue());
    const req = http.expectOne(`${base}/leaves/abc/submit`);
    expect(req.request.method).toBe('POST');
    req.flush({ success: true, data: { id: 'abc', status: 1 } });
  });

  it('create posts with SKIP_ERROR_TOAST (dialog handles validation errors)', () => {
    service.create({
      leaveTypeId: 'type-1',
      startDate: '2026-08-18',
      endDate: '2026-08-23',
      startUnit: 0,
      endUnit: 0
    }).subscribe({ error: () => undefined });
    const req = http.expectOne(`${base}/leaves`);
    expect(req.request.context.get(SKIP_ERROR_TOAST)).toBe(true);
    req.flush(
      { success: false, error: 'Une demande existe déjà sur cette période.' },
      { status: 400, statusText: 'Bad Request' }
    );
  });

  it('update puts with SKIP_ERROR_TOAST (dialog handles validation errors)', () => {
    service.update('abc', {
      leaveTypeId: 'type-1',
      startDate: '2026-08-18',
      endDate: '2026-08-23',
      startUnit: 0,
      endUnit: 0
    }).subscribe({ error: () => undefined });
    const req = http.expectOne(`${base}/leaves/abc`);
    expect(req.request.method).toBe('PUT');
    expect(req.request.context.get(SKIP_ERROR_TOAST)).toBe(true);
    req.flush(
      { success: false, error: 'Une demande existe déjà sur cette période.' },
      { status: 400, statusText: 'Bad Request' }
    );
  });
});
