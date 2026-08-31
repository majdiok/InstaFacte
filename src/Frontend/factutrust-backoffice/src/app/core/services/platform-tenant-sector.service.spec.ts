import { TestBed } from '@angular/core/testing';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient } from '@angular/common/http';

import { PlatformTenantSectorService } from './platform-tenant-sector.service';
import { environment } from '@environments/environment';
import type { SectorReconfigurationRequestDto } from '@core/models/sector-rules.models';

describe('PlatformTenantSectorService', () => {
  let service: PlatformTenantSectorService;
  let httpMock: HttpTestingController;
  const base = `${environment.apiUrl}/platform/tenants`;
  const tenantId = '11111111-1111-1111-1111-111111111111';

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()]
    });
    service = TestBed.inject(PlatformTenantSectorService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('preview() POSTs to /sector-configuration/preview with the request payload', () => {
    const payload: SectorReconfigurationRequestDto = {
      companySegment: 'btp',
      businessDomain: 'construction',
      recomputeModuleGrants: true,
      applyDataTemplates: true
    };
    service.preview(tenantId, payload).subscribe();
    const req = httpMock.expectOne(`${base}/${tenantId}/sector-configuration/preview`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual(payload);
    req.flush({ success: true, data: null, message: null, errors: [] });
  });

  it('apply() POSTs to /sector-configuration/apply with the request payload', () => {
    const payload: SectorReconfigurationRequestDto = { companySegment: 'btp', businessDomain: 'construction' };
    service.apply(tenantId, payload).subscribe();
    const req = httpMock.expectOne(`${base}/${tenantId}/sector-configuration/apply`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual(payload);
    req.flush({ success: true, data: null, message: null, errors: [] });
  });
});
