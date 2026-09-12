import { Component } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { provideRouter } from '@angular/router';
import { Subject, of, throwError } from 'rxjs';
import { AiStreamService } from '@features/ai-assistant/services/ai-stream.service';
import { AiChatService } from '@features/ai-assistant/services/ai-chat.service';
import { ApiResponse } from '@core/services/client.service';
import { StudioAiBuildService } from '../studio-ai-build.service';
import { StudioNavService } from '../studio-nav.service';
import { StudioAiBuilderComponent } from '../studio-ai-builder.component';
import { StudioAiCapabilitiesService } from './studio-ai-capabilities.service';
import { StudioAiEntryComponent } from './studio-ai-entry.component';
import { StudioAiPageComponent } from './studio-ai-page.component';
import { STUDIO_AI_CAPABILITIES_FALLBACK, StudioAiCapabilitiesDto } from './studio-ai.models';

@Component({ selector: 'app-studio-ai-page', standalone: true, template: 'ATELIER' })
class StubPageComponent {}

@Component({ selector: 'app-studio-ai-builder', standalone: true, template: 'LEGACY' })
class StubLegacyComponent {}

describe('StudioAiEntryComponent', () => {
  let capabilities$: Subject<ApiResponse<StudioAiCapabilitiesDto>>;
  let fixture: ComponentFixture<StudioAiEntryComponent>;

  beforeEach(async () => {
    capabilities$ = new Subject();
    const builds = jasmine.createSpyObj<StudioAiBuildService>('StudioAiBuildService', ['getCapabilities']);
    builds.getCapabilities.and.returnValue(capabilities$.asObservable());
    const stream = jasmine.createSpyObj<AiStreamService>('AiStreamService', ['streamChat']);
    stream.streamChat.and.returnValue(of());

    await TestBed.configureTestingModule({
      imports: [StudioAiEntryComponent],
      providers: [
        provideRouter([]),
        provideNoopAnimations(),
        provideHttpClient(),
        provideHttpClientTesting(),
        StudioAiCapabilitiesService,
        { provide: StudioAiBuildService, useValue: builds },
        { provide: AiStreamService, useValue: stream },
        { provide: StudioNavService, useValue: jasmine.createSpyObj<StudioNavService>('StudioNavService', ['refresh', 'items']) },
        { provide: AiChatService, useValue: jasmine.createSpyObj<AiChatService>('AiChatService', ['extractDocument']) }
      ]
    })
      .overrideComponent(StudioAiEntryComponent, {
        remove: { imports: [StudioAiPageComponent, StudioAiBuilderComponent] },
        add: { imports: [StubPageComponent, StubLegacyComponent] }
      })
      .compileComponents();

    fixture = TestBed.createComponent(StudioAiEntryComponent);
    fixture.detectChanges();
  });

  function text(): string {
    return ((fixture.nativeElement as HTMLElement).textContent ?? '').trim();
  }

  it('affiche un squelette tant que les capacités ne sont pas connues', () => {
    expect(fixture.nativeElement.querySelector('p-skeleton')).not.toBeNull();
    expect(text()).not.toContain('ATELIER');
    expect(text()).not.toContain('LEGACY');
  });

  it('aiguille vers l’atelier quand le workbench est activé', () => {
    capabilities$.next({ success: true, data: { ...STUDIO_AI_CAPABILITIES_FALLBACK, workbenchEnabled: true }, message: null, errors: [] });
    fixture.detectChanges();
    expect(text()).toBe('ATELIER');
  });

  it('retombe sur la page legacy quand le workbench est désactivé', () => {
    capabilities$.next({ success: true, data: { ...STUDIO_AI_CAPABILITIES_FALLBACK, workbenchEnabled: false }, message: null, errors: [] });
    fixture.detectChanges();
    expect(text()).toBe('LEGACY');
  });

  it('retombe sur la page legacy en cas d’erreur (404 flag désactivé, réseau…)', () => {
    capabilities$.error(new Error('404'));
    fixture.detectChanges();
    expect(text()).toBe('LEGACY');
  });

  it('ne déclenche qu’un seul appel réseau par session', () => {
    const builds = TestBed.inject(StudioAiBuildService) as jasmine.SpyObj<StudioAiBuildService>;
    builds.getCapabilities.and.returnValue(throwError(() => new Error('x')));
    TestBed.inject(StudioAiCapabilitiesService).ensureLoaded();
    expect(builds.getCapabilities).toHaveBeenCalledTimes(1);
  });
});
