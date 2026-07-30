import { TestBed } from '@angular/core/testing';
import { provideHttpClient, withInterceptorsFromDi } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { environment } from '@environments/environment';
import { FirmGovernanceService, SaveFirmActivityCodeBody } from './firm-governance.service';

describe('FirmGovernanceService', () => {
  let service: FirmGovernanceService;
  let httpMock: HttpTestingController;
  const base = `${environment.apiUrl}/firm/governance`;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(withInterceptorsFromDi()), provideHttpClientTesting()]
    });
    service = TestBed.inject(FirmGovernanceService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('lists activity codes with includeInactive query param', () => {
    service.listActivityCodes(true).subscribe();

    const req = httpMock.expectOne(r => r.url === `${base}/activity-codes`);
    expect(req.request.method).toBe('GET');
    expect(req.request.params.get('includeInactive')).toBe('true');
    req.flush({ success: true, data: [] });
  });

  it('seeds default activity codes', () => {
    service.seedDefaultActivityCodes().subscribe();

    const req = httpMock.expectOne(`${base}/activity-codes/seed-defaults`);
    expect(req.request.method).toBe('POST');
    req.flush({ success: true, data: [] });
  });

  it('creates an activity code', () => {
    const body: SaveFirmActivityCodeBody = {
      code: 'TEST',
      label: 'Test',
      category: 1,
      isBillableByDefault: true,
      sortOrder: 10
    };

    service.createActivityCode(body).subscribe();

    const req = httpMock.expectOne(`${base}/activity-codes`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual(body);
    req.flush({ success: true, data: null });
  });

  it('updates an activity code', () => {
    const body: SaveFirmActivityCodeBody = {
      code: 'TEST',
      label: 'Test modifie',
      category: 5,
      isBillableByDefault: false,
      sortOrder: 20
    };

    service.updateActivityCode('id-1', body).subscribe();

    const req = httpMock.expectOne(`${base}/activity-codes/id-1`);
    expect(req.request.method).toBe('PUT');
    expect(req.request.body).toEqual(body);
    req.flush({ success: true, data: null });
  });

  it('deactivates an activity code', () => {
    service.deactivateActivityCode('id-1').subscribe();

    const req = httpMock.expectOne(`${base}/activity-codes/id-1`);
    expect(req.request.method).toBe('DELETE');
    req.flush({ success: true, data: null });
  });

  it('activates an activity code', () => {
    service.activateActivityCode('id-1').subscribe();

    const req = httpMock.expectOne(`${base}/activity-codes/id-1/activate`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({});
    req.flush({ success: true, data: null });
  });
});
