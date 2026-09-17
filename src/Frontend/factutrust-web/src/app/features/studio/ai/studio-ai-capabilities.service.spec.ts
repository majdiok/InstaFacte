import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { environment } from '@environments/environment';
import { StudioAiCapabilitiesService } from './studio-ai-capabilities.service';
import { STUDIO_AI_CAPABILITIES_FALLBACK, StudioAiCapabilitiesDto } from './studio-ai.models';

describe('StudioAiCapabilitiesService', () => {
  let service: StudioAiCapabilitiesService;
  let http: HttpTestingController;

  const url = `${environment.apiUrl}/ai/studio/capabilities`;

  const capabilities: StudioAiCapabilitiesDto = {
    ...STUDIO_AI_CAPABILITIES_FALLBACK,
    planPreviewEnabled: true,
    systemGenerationEnabled: true,
    modifyToolsEnabled: true,
    viewToolsEnabled: false,
    reportToolsEnabled: true,
    workbenchEnabled: true,
    templatesEnabled: true,
    pagesEnabled: false,
    advancedModelAvailable: false,
    standardModelLabel: 'Standard',
    advancedModelLabel: null
  };

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])]
    });
    service = TestBed.inject(StudioAiCapabilitiesService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('starts unknown with the fallback capabilities', () => {
    expect(service.state()).toBe('unknown');
    expect(service.workbenchEnabled()).toBeFalse();
    expect(service.loading()).toBeTrue();
  });

  it('loads the capabilities once and exposes them', () => {
    service.ensureLoaded();
    service.ensureLoaded();

    const req = http.expectOne(url);
    expect(req.request.method).toBe('GET');
    req.flush({ success: true, data: capabilities, message: null, errors: [] });

    expect(service.state()).toBe('ready');
    expect(service.loading()).toBeFalse();
    expect(service.workbenchEnabled()).toBeTrue();
    expect(service.capabilities().standardModelLabel).toBe('Standard');
  });

  it('marks the workbench unavailable on error (legacy fallback)', () => {
    service.ensureLoaded();
    http.expectOne(url).flush('nope', { status: 404, statusText: 'Not Found' });

    expect(service.state()).toBe('unavailable');
    expect(service.workbenchEnabled()).toBeFalse();
    expect(service.capabilities().workbenchEnabled).toBeFalse();
  });

  it('marks the workbench unavailable when the envelope is not successful', () => {
    service.ensureLoaded();
    http.expectOne(url).flush({ success: false, data: null, message: 'ko', errors: [] });

    expect(service.state()).toBe('unavailable');
    expect(service.workbenchEnabled()).toBeFalse();
  });

  it('completes missing flags with the fallback values', () => {
    service.ensureLoaded();
    http.expectOne(url).flush({
      success: true,
      data: { workbenchEnabled: true, standardModelLabel: 'GPT' },
      message: null,
      errors: []
    });

    expect(service.capabilities().templatesEnabled).toBeFalse();
    expect(service.capabilities().workbenchEnabled).toBeTrue();
  });

  it('reloads after reset', () => {
    service.ensureLoaded();
    http.expectOne(url).flush({ success: true, data: capabilities, message: null, errors: [] });

    service.reset();
    expect(service.state()).toBe('unknown');
    expect(service.workbenchEnabled()).toBeFalse();

    service.ensureLoaded();
    http.expectOne(url).flush({ success: true, data: capabilities, message: null, errors: [] });
    expect(service.state()).toBe('ready');
  });

  it('expose workflowsEnabled à true seulement quand l\'état est prêt et la capacité vraie', () => {
    expect(service.workflowsEnabled()).toBeFalse();

    service.ensureLoaded();
    http.expectOne(url).flush({ success: true, data: { ...capabilities, workflowsEnabled: true }, message: null, errors: [] });
    expect(service.workflowsEnabled()).toBeTrue();

    service.reset();
    service.ensureLoaded();
    http.expectOne(url).flush({ success: true, data: { ...capabilities, workflowsEnabled: false }, message: null, errors: [] });
    expect(service.workflowsEnabled()).toBeFalse();
  });

  it('expose workflowToolsEnabled à false quand le serveur répond 403 (fail-closed)', () => {
    service.ensureLoaded();
    http.expectOne(url).flush('', { status: 403, statusText: 'Forbidden' });

    expect(service.state()).toBe('unavailable');
    expect(service.workflowToolsEnabled()).toBeFalse();
  });

  it('remet workflowsEnabled à false après reset()', () => {
    service.ensureLoaded();
    http.expectOne(url).flush({ success: true, data: { ...capabilities, workflowsEnabled: true }, message: null, errors: [] });
    expect(service.workflowsEnabled()).toBeTrue();

    service.reset();
    expect(service.workflowsEnabled()).toBeFalse();
  });
});
