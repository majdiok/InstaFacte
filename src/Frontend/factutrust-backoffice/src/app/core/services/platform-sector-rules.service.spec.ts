import { TestBed } from '@angular/core/testing';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient } from '@angular/common/http';

import { PlatformSectorRulesService } from './platform-sector-rules.service';
import { environment } from '@environments/environment';

describe('PlatformSectorRulesService', () => {
  let service: PlatformSectorRulesService;
  let httpMock: HttpTestingController;
  const base = `${environment.apiUrl}/platform/sector-rules`;
  const ok = <T>(data: T) => ({ success: true, data, message: null, errors: [] });

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
    req.flush(ok(null));
  });

  it('segments CRUD hits the right URLs and verbs with GUID ids', () => {
    service.listSegments().subscribe();
    httpMock.expectOne(`${base}/segments`).flush(ok([]));

    service.createSegment({ code: 'commerce', labelFr: 'Commerce', descriptionFr: 'Desc', iconKey: 'shopping-cart', sortOrder: 1 }).subscribe();
    const createReq = httpMock.expectOne(`${base}/segments`);
    expect(createReq.request.method).toBe('POST');
    expect(createReq.request.body.code).toBe('commerce');
    expect(createReq.request.body.labelFr).toBe('Commerce');
    createReq.flush(ok(null));

    service.updateSegment('11111111-1111-1111-1111-111111111111', { labelFr: 'Commerce', descriptionFr: 'Desc', iconKey: 'shopping-cart', sortOrder: 1 }).subscribe();
    const updateReq = httpMock.expectOne(`${base}/segments/11111111-1111-1111-1111-111111111111`);
    expect(updateReq.request.method).toBe('PUT');
    updateReq.flush(ok(null));

    service.deactivateSegment('11111111-1111-1111-1111-111111111111').subscribe();
    const deleteReq = httpMock.expectOne(`${base}/segments/11111111-1111-1111-1111-111111111111`);
    expect(deleteReq.request.method).toBe('DELETE');
    deleteReq.flush(ok(null));
  });

  it('domains CRUD hits the right URLs and verbs', () => {
    service.listDomains().subscribe();
    httpMock.expectOne(`${base}/domains`).flush(ok([]));

    service.createDomain({ code: 'artisanat', labelFr: 'Artisanat', sortOrder: 1 }).subscribe();
    expect(httpMock.expectOne(`${base}/domains`).request.method).toBe('POST');

    service.updateDomain('22222222-2222-2222-2222-222222222222', { labelFr: 'Artisanat', sortOrder: 1 }).subscribe();
    expect(httpMock.expectOne(`${base}/domains/22222222-2222-2222-2222-222222222222`).request.method).toBe('PUT');

    service.deactivateDomain('22222222-2222-2222-2222-222222222222').subscribe();
    expect(httpMock.expectOne(`${base}/domains/22222222-2222-2222-2222-222222222222`).request.method).toBe('DELETE');
  });

  it('segment-domains CRUD hits the right URLs and verbs with GUID link ids', () => {
    service.listSegmentDomains().subscribe();
    httpMock.expectOne(`${base}/segment-domains`).flush(ok([]));

    service.createSegmentDomain({ segmentId: 's-1', domainId: 'd-1', sortOrder: 1 }).subscribe();
    const createReq = httpMock.expectOne(`${base}/segment-domains`);
    expect(createReq.request.method).toBe('POST');
    expect(createReq.request.body.segmentId).toBe('s-1');
    expect(createReq.request.body.domainId).toBe('d-1');
    createReq.flush(ok(null));

    service.updateSegmentDomain('sd-1', { sortOrder: 2 }).subscribe();
    expect(httpMock.expectOne(`${base}/segment-domains/sd-1`).request.method).toBe('PUT');

    service.deactivateSegmentDomain('sd-1').subscribe();
    expect(httpMock.expectOne(`${base}/segment-domains/sd-1`).request.method).toBe('DELETE');
  });

  it('module-rules CRUD hits the right URLs and verbs with ruleKind string', () => {
    service.listModuleRules().subscribe();
    httpMock.expectOne(`${base}/module-rules`).flush(ok([]));

    service.createModuleRule({ ruleKind: 'SegmentBase', segmentId: 's-1', domainId: null, moduleId: 7, sortOrder: 0 }).subscribe();
    const createReq = httpMock.expectOne(`${base}/module-rules`);
    expect(createReq.request.method).toBe('POST');
    expect(createReq.request.body.ruleKind).toBe('SegmentBase');
    createReq.flush(ok(null));

    service.updateModuleRule('mr-1', { sortOrder: 3 }).subscribe();
    expect(httpMock.expectOne(`${base}/module-rules/mr-1`).request.method).toBe('PUT');

    service.deactivateModuleRule('mr-1').subscribe();
    expect(httpMock.expectOne(`${base}/module-rules/mr-1`).request.method).toBe('DELETE');
  });

  it('module-dependencies expose POST + DELETE only (no PUT)', () => {
    service.listModuleDependencies().subscribe();
    httpMock.expectOne(`${base}/module-dependencies`).flush(ok([]));

    service.createModuleDependency({ moduleId: 6, requiredModuleId: 7 }).subscribe();
    const createReq = httpMock.expectOne(`${base}/module-dependencies`);
    expect(createReq.request.method).toBe('POST');
    expect(createReq.request.body.requiredModuleId).toBe(7);
    createReq.flush(ok(null));

    service.deactivateModuleDependency('dep-1').subscribe();
    expect(httpMock.expectOne(`${base}/module-dependencies/dep-1`).request.method).toBe('DELETE');
  });

  it('settings CRUD hits the right URLs and verbs with settingValue', () => {
    service.listSettings().subscribe();
    httpMock.expectOne(`${base}/settings`).flush(ok([]));

    service.createSetting({ segmentCode: 'commerce', settingKey: 'DefaultWarehouseName', settingValue: 'Magasin', valueType: 'string', sortOrder: 0 }).subscribe();
    const createReq = httpMock.expectOne(`${base}/settings`);
    expect(createReq.request.method).toBe('POST');
    expect(createReq.request.body.settingValue).toBe('Magasin');
    createReq.flush(ok(null));

    service.updateSetting('set-1', { settingValue: 'Dépôt', valueType: 'string', sortOrder: 0 }).subscribe();
    expect(httpMock.expectOne(`${base}/settings/set-1`).request.method).toBe('PUT');

    service.deactivateSetting('set-1').subscribe();
    expect(httpMock.expectOne(`${base}/settings/set-1`).request.method).toBe('DELETE');
  });

  it('templates CRUD hits the right URLs and verbs with labelFr', () => {
    service.listTemplates().subscribe();
    httpMock.expectOne(`${base}/templates`).flush(ok([]));

    service.createTemplate({ code: 'btp-docs', labelFr: 'Types de documents BTP', sortOrder: 1, items: [] }).subscribe();
    const createReq = httpMock.expectOne(`${base}/templates`);
    expect(createReq.request.method).toBe('POST');
    expect(createReq.request.body.labelFr).toBe('Types de documents BTP');
    createReq.flush(ok(null));

    service.updateTemplate('tpl-1', { labelFr: 'Types de documents BTP', sortOrder: 1, items: [] }).subscribe();
    expect(httpMock.expectOne(`${base}/templates/tpl-1`).request.method).toBe('PUT');

    service.deactivateTemplate('tpl-1').subscribe();
    expect(httpMock.expectOne(`${base}/templates/tpl-1`).request.method).toBe('DELETE');
  });

  it('seedFromCatalog() POSTs to seed-from-catalog with force query param', () => {
    service.seedFromCatalog(true).subscribe();
    const req = httpMock.expectOne(r => r.url === `${base}/seed-from-catalog` && r.params.get('force') === 'true');
    expect(req.request.method).toBe('POST');
    req.flush(ok(null));
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
