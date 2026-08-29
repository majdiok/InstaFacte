import { TestBed } from '@angular/core/testing';
import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
import { ProjectApiService } from './project-api.service';
import { environment } from '@environments/environment';

describe('ProjectApiService', () => {
  let service: ProjectApiService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [HttpClientTestingModule],
      providers: [ProjectApiService]
    });
    service = TestBed.inject(ProjectApiService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('lists projects with pagination params', () => {
    service.list({ page: 2, pageSize: 20, search: 'alpha', status: 'Active', kind: 'Esn' }).subscribe();
    const req = http.expectOne(r => r.url === `${environment.apiUrl}/projects`);
    expect(req.request.method).toBe('GET');
    expect(req.request.params.get('page')).toBe('2');
    expect(req.request.params.get('pageSize')).toBe('20');
    expect(req.request.params.get('search')).toBe('alpha');
    expect(req.request.params.get('status')).toBe('Active');
    expect(req.request.params.get('kind')).toBe('Esn');
    req.flush({ success: true, data: { items: [], page: 2, pageSize: 20, totalCount: 0 } });
  });

  it('lists projects with extended filter params', () => {
    service.list({
      page: 2,
      pageSize: 20,
      search: 'alpha',
      status: 'Active',
      kind: 'Esn',
      clientId: 'c1',
      ownerUserId: 'u1',
      billingMode: 'TimeAndMaterials',
      overdueOnly: true,
      endDateFrom: '2026-01-01',
      endDateTo: '2026-12-31'
    }).subscribe();
    const req = http.expectOne(r => r.url === `${environment.apiUrl}/projects`);
    expect(req.request.params.get('clientId')).toBe('c1');
    expect(req.request.params.get('ownerUserId')).toBe('u1');
    expect(req.request.params.get('billingMode')).toBe('TimeAndMaterials');
    expect(req.request.params.get('overdueOnly')).toBe('true');
    expect(req.request.params.get('endDateFrom')).toBe('2026-01-01');
    req.flush({ success: true, data: { items: [], page: 2, pageSize: 20, totalCount: 0 } });
  });

  it('exports projects as CSV blob', () => {
    service.exportCsv({ status: 'Active' }).subscribe();
    const req = http.expectOne(`${environment.apiUrl}/projects/export?status=Active`);
    expect(req.request.responseType).toBe('blob');
    req.flush(new Blob(['test']));
  });

  it('calls dashboard and extended dashboard endpoints', () => {
    service.dashboard().subscribe();
    http.expectOne(`${environment.apiUrl}/projects/dashboard`).flush({ success: true, data: {} });

    service.dashboardExtended('week').subscribe();
    const ext = http.expectOne(r => r.url === `${environment.apiUrl}/projects/dashboard/extended`);
    expect(ext.request.params.get('period')).toBe('week');
    ext.flush({ success: true, data: {} });
  });

  it('calls scoped project search endpoint', () => {
    service.search('mission', 8).subscribe();
    const req = http.expectOne(r => r.url === `${environment.apiUrl}/projects/search`);
    expect(req.request.method).toBe('GET');
    expect(req.request.params.get('q')).toBe('mission');
    expect(req.request.params.get('limit')).toBe('8');
    req.flush({ success: true, data: { query: 'mission', results: [] } });
  });

  it('loads task by id under tasks sub-path', () => {
    service.getTask('task-1').subscribe();
    const req = http.expectOne(`${environment.apiUrl}/projects/tasks/task-1`);
    expect(req.request.method).toBe('GET');
    req.flush({ success: true, data: {} });
  });

  it('moves task to another phase with optional status', () => {
    service.moveTask('task-1', 'phase-2', 'InProgress').subscribe();
    const req = http.expectOne(`${environment.apiUrl}/projects/tasks/task-1/move`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ phaseId: 'phase-2', status: 'InProgress' });
    req.flush({ success: true, data: true });
  });
});
