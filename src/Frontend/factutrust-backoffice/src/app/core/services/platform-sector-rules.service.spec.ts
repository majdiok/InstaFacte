import { TestBed } from '@angular/core/testing';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient } from '@angular/common/http';

import { PlatformSectorRulesService } from './platform-sector-rules.service';
import { environment } from '@environments/environment';

describe('PlatformSectorRulesService', () => {
  let service: PlatformSectorRulesService;
  let httpMock: HttpTestingController;
  const base = `${environment.apiUrl}/platform/sector-rules`;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()]
    });
    service = TestBed.inject(PlatformSectorRulesService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('getAll() GETs the dump endpoint', () => {
    service.getAll().subscribe();
    const req = httpMock.expectOne(base);
    expect(req.request.method).toBe('GET');
    req.flush({ success: true, data: null, message: null, errors: [] });
  });

  it('listSegments()/createSegment()/updateSegment()/deactivateSegment() hit the right URLs and verbs', () => {
    service.listSegments().subscribe();
    httpMock.expectOne(`${base}/segments`).flush({ success: true, data: [], message: null, errors: [] });

    service.createSegment({ code: 'commerce', label: 'Commerce', sortOrder: 1 }).subscribe();
    const createReq = httpMock.expectOne(`${base}/segments`);
    expect(createReq.request.method).toBe('POST');
    expect(createReq.request.body.code).toBe('commerce');
    createReq.flush({ success: true, data: null, message: null, errors: [] });

    service.updateSegment('id-1', { label: 'Commerce', sortOrder: 1, isActive: true }).subscribe();
    const updateReq = httpMock.expectOne(`${base}/segments/id-1`);
    expect(updateReq.request.method).toBe('PUT');
    updateReq.flush({ success: true, data: null, message: null, errors: [] });

    service.deactivateSegment('id-1').subscribe();
    const deleteReq = httpMock.expectOne(`${base}/segments/id-1`);
    expect(deleteReq.request.method).toBe('DELETE');
    deleteReq.flush({ success: true, data: null, message: 'Segment désactivé.', errors: [] });
  });

  it('listDomains()/createDomain()/updateDomain()/deactivateDomain() hit the right URLs and verbs', () => {
    service.listDomains().subscribe();
    httpMock.expectOne(`${base}/domains`).flush({ success: true, data: [], message: null, errors: [] });

    service.createDomain({ code: 'vente-detail', label: 'Vente au détail', sortOrder: 1 }).subscribe();
    const createReq = httpMock.expectOne(`${base}/domains`);
    expect(createReq.request.method).toBe('POST');
    createReq.flush({ success: true, data: null, message: null, errors: [] });

    service.updateDomain('id-2', { label: 'Vente au détail', sortOrder: 1, isActive: true }).subscribe();
    expect(httpMock.expectOne(`${base}/domains/id-2`).request.method).toBe('PUT');

    service.deactivateDomain('id-2').subscribe();
    expect(httpMock.expectOne(`${base}/domains/id-2`).request.method).toBe('DELETE');
  });

  it('listSegmentDomains()/setSegmentDomains() PUTs the domain codes for the segment', () => {
    service.listSegmentDomains().subscribe();
    httpMock.expectOne(`${base}/segment-domains`).flush({ success: true, data: [], message: null, errors: [] });

    service.setSegmentDomains('commerce', ['vente-detail', 'autre']).subscribe();
    const req = httpMock.expectOne(`${base}/segment-domains/commerce`);
    expect(req.request.method).toBe('PUT');
    expect(req.request.body.domainCodes).toEqual(['vente-detail', 'autre']);
  });

  it('listModuleRules()/saveSegmentRules()/saveDomainOverlay() hit the right URLs', () => {
    service.listModuleRules().subscribe();
    httpMock.expectOne(`${base}/module-rules`).flush({ success: true, data: [], message: null, errors: [] });

    service.saveSegmentRules('commerce', [7, 6]).subscribe();
    const segReq = httpMock.expectOne(`${base}/module-rules/segments/commerce`);
    expect(segReq.request.method).toBe('PUT');
    expect(segReq.request.body.moduleIds).toEqual([7, 6]);

    service.saveDomainOverlay('vente-detail', [9]).subscribe();
    const domReq = httpMock.expectOne(`${base}/module-rules/domains/vente-detail`);
    expect(domReq.request.method).toBe('PUT');
  });

  it('listDependencies()/createDependency()/deleteDependency() hit the right URLs', () => {
    service.listDependencies().subscribe();
    httpMock.expectOne(`${base}/module-dependencies`).flush({ success: true, data: [], message: null, errors: [] });

    service.createDependency({ moduleId: 6, requiresModuleId: 7 }).subscribe();
    const createReq = httpMock.expectOne(`${base}/module-dependencies`);
    expect(createReq.request.method).toBe('POST');

    service.deleteDependency('dep-1').subscribe();
    expect(httpMock.expectOne(`${base}/module-dependencies/dep-1`).request.method).toBe('DELETE');
  });

  it('listDefaultSettings()/saveDefaultSetting() hit the right URLs', () => {
    service.listDefaultSettings().subscribe();
    httpMock.expectOne(`${base}/settings`).flush({ success: true, data: [], message: null, errors: [] });

    service.saveDefaultSetting({ segmentCode: 'commerce', settingKey: 'DefaultWarehouseName', valueType: 'string', value: 'Magasin' }).subscribe();
    expect(httpMock.expectOne(`${base}/settings`).request.method).toBe('POST');
  });

  it('listDataTemplates()/saveDataTemplate() hit the right URLs', () => {
    service.listDataTemplates().subscribe();
    httpMock.expectOne(`${base}/templates`).flush({ success: true, data: [], message: null, errors: [] });

    service.saveDataTemplate({ code: 'btp-docs', label: 'Types de documents BTP', segmentCode: 'btp', domainCode: null, sortOrder: 1, items: [] }).subscribe();
    expect(httpMock.expectOne(`${base}/templates`).request.method).toBe('POST');
  });

  it('seedFromCatalog() POSTs to seed-from-catalog with force query param', () => {
    service.seedFromCatalog(true).subscribe();
    const req = httpMock.expectOne(r => r.url === `${base}/seed-from-catalog` && r.params.get('force') === 'true');
    expect(req.request.method).toBe('POST');
    req.flush({ success: true, data: null, message: null, errors: [] });
  });

  it('seedFromCatalog() without force omits the query param', () => {
    service.seedFromCatalog().subscribe();
    const req = httpMock.expectOne(r => r.url === `${base}/seed-from-catalog`);
    expect(req.request.params.has('force')).toBeFalse();
  });

  it('getParity() GETs the parity endpoint', () => {
    service.getParity().subscribe();
    const req = httpMock.expectOne(`${base}/parity`);
    expect(req.request.method).toBe('GET');
  });
});
