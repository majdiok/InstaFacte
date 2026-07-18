import { TestBed } from '@angular/core/testing';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { PowerPointExportService } from './powerpoint-export.service';
import { SlideContentBlock, SlideOrientation, PowerPointTemplate } from '../models/ai-chat.models';
import { environment } from '@environments/environment';
import { provideHttpClient, withInterceptorsFromDi } from '@angular/common/http';

describe('PowerPointExportService', () => {
  let service: PowerPointExportService;
  let httpMock: HttpTestingController;
  const baseUrl = `${environment.apiUrl}/ai/exports/powerpoint`;

  beforeEach(() => {
    TestBed.configureTestingModule({
    imports: [],
    providers: [PowerPointExportService, provideHttpClient(withInterceptorsFromDi()), provideHttpClientTesting()]
});
    service = TestBed.inject(PowerPointExportService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('generate returns inline blob on 200', done => {
    const blob = new Blob(['pptx'], {
      type: 'application/vnd.openxmlformats-officedocument.presentationml.presentation'
    });
    service
      .generate({
        title: 'Test',
        template: PowerPointTemplate.Standard,
        orientation: SlideOrientation.Widescreen16x9,
        includeCoverSlide: true,
        includeAgenda: false,
        includeTableOfContents: false,
        includeSpeakerNotes: false,
        includeSources: false,
        includeAppendix: false,
        responses: []
      })
      .subscribe(result => {
        expect(result.kind).toBe('inline');
        if (result.kind === 'inline') {
          expect(result.slideCount).toBe(5);
        }
        done();
      });

    const req = httpMock.expectOne(baseUrl);
    expect(req.request.method).toBe('POST');
    expect(req.request.body.template).toBe('Standard');
    expect(req.request.body.orientation).toBe('Widescreen16x9');
    req.flush(blob, {
      status: 200,
      statusText: 'OK',
      headers: {
        'X-Export-Id': 'id-1',
        'X-Export-Slides': '5',
        'X-Export-Download-Url': '/download',
        'X-Export-Expires': new Date().toISOString()
      }
    });
  });

  it('listTemplates normalizes string enum ids from API', done => {
    service.listTemplates().subscribe(list => {
      expect(list.length).toBe(1);
      expect(list[0].id).toBe(PowerPointTemplate.Executive);
      expect(list[0].name).toBe('Executive');
      done();
    });
    const req = httpMock.expectOne(`${baseUrl}/templates`);
    req.flush({
      success: true,
      data: [{
        id: 'Executive',
        key: 'Executive',
        name: 'Executive',
        description: 'd',
        category: 2,
        isDark: false,
        sortOrder: 2,
        primaryColorHex: '#111',
        accentColorHex: '#222',
        backgroundColorHex: '#fff',
        surfaceColorHex: '#f9',
        onSurfaceColorHex: '#111',
        linkColorHex: '#222',
        titleFontFamily: 'Inter',
        bodyFontFamily: 'Inter',
        engine: 'Legacy',
        previewThumbnailUrl: '/assets/powerpoint/themes/Executive/preview-16x9.svg',
        requiresAttribution: false,
        coverLayoutStyle: 'ClassicBar'
      }]
    });
  });

  it('listTemplates resolves preview thumbnail URLs to API origin', done => {
    const apiOrigin = new URL(environment.apiUrl).origin;
    service.listTemplates().subscribe(list => {
      expect(list.length).toBe(1);
      expect(list[0].previewThumbnailUrl).toBe(
        `${apiOrigin}/assets/powerpoint/themes/Vortex/preview-16x9.svg`
      );
      expect(list[0].previewThumbnailUrl4x3).toBe(
        `${apiOrigin}/assets/powerpoint/themes/Vortex/preview-4x3.svg`
      );
      done();
    });
    const req = httpMock.expectOne(`${baseUrl}/templates`);
    req.flush({
      success: true,
      data: [{
        id: 'Vortex',
        key: 'Vortex',
        name: 'Vortex',
        description: 'd',
        category: 3,
        isDark: true,
        sortOrder: 10,
        primaryColorHex: '#fff',
        accentColorHex: '#A78BFA',
        backgroundColorHex: '#1E1B4B',
        surfaceColorHex: '#312E81',
        onSurfaceColorHex: '#EDE9FE',
        linkColorHex: '#A78BFA',
        titleFontFamily: 'Inter',
        bodyFontFamily: 'Inter',
        engine: 'Hybrid',
        previewThumbnailUrl: '/assets/powerpoint/themes/Vortex/preview-16x9.svg',
        previewThumbnailUrl4x3: '/assets/powerpoint/themes/Vortex/preview-4x3.svg',
        requiresAttribution: false,
        coverLayoutStyle: 'ClassicBar'
      }]
    });
  });

  it('generate surfaces validation errors from 400 JSON blob', done => {
    service
      .generate({
        title: '',
        template: PowerPointTemplate.Vortex,
        orientation: SlideOrientation.Widescreen16x9,
        includeCoverSlide: true,
        includeAgenda: false,
        includeTableOfContents: false,
        includeSpeakerNotes: false,
        includeSources: false,
        includeAppendix: false,
        responses: []
      })
      .subscribe({
        next: () => fail('expected error'),
        error: err => {
          expect(err.status).toBe(400);
          expect(err.code).toBe('Validation');
          expect(err.message).toContain('titre');
          done();
        }
      });

    const req = httpMock.expectOne(baseUrl);
    const payload = JSON.stringify({
      success: false,
      message: 'Validation failed',
      code: 'Validation',
      errors: ['Le titre de la présentation est obligatoire.']
    });
    req.flush(new Blob([payload], { type: 'application/json' }), {
      status: 400,
      statusText: 'Bad Request'
    });
  });

  it('serializeExportRequest maps includeOnly flags to API string', () => {
    const wire = service.serializeExportRequest({
      title: 'Deck',
      template: PowerPointTemplate.Vortex,
      orientation: SlideOrientation.Widescreen16x9,
      includeCoverSlide: true,
      includeAgenda: false,
      includeTableOfContents: false,
      includeSpeakerNotes: false,
      includeSources: false,
      includeAppendix: false,
      responses: [{
        conversationId: '00000000-0000-0000-0000-000000000001',
        messageId: '00000000-0000-0000-0000-000000000002',
        includeOnly: SlideContentBlock.Text | SlideContentBlock.KpiCards
      }]
    });

    expect(wire.template).toBe('Vortex');
    expect(wire.responses[0].includeOnly).toBe('Text, KpiCards');
  });

  it('previewResponse returns structural metadata', done => {
    service.previewResponse('conv-1', 'msg-1', 'Custom').subscribe(preview => {
      expect(preview.kpiCount).toBe(4);
      expect(preview.slideOutline.length).toBeGreaterThan(0);
      done();
    });
    const req = httpMock.expectOne(r => r.url === `${baseUrl}/preview`);
    expect(req.request.params.get('conversationId')).toBe('conv-1');
    expect(req.request.params.get('messageId')).toBe('msg-1');
    req.flush({
      success: true,
      data: {
        conversationId: 'conv-1',
        messageId: 'msg-1',
        title: 'Analyse',
        kpiCount: 4,
        tableCount: 0,
        chartCount: 0,
        sectionCount: 5,
        hasText: true,
        slideOutline: ['Synthèse exécutive', 'Indicateurs clés (4)']
      }
    });
  });
});

describe('SlideContentBlock', () => {
  it('All combines every export block flag', () => {
    expect(SlideContentBlock.All).toBe(
      SlideContentBlock.Text |
        SlideContentBlock.KpiCards |
        SlideContentBlock.Tables |
        SlideContentBlock.Charts |
        SlideContentBlock.Sources
    );
  });
});
