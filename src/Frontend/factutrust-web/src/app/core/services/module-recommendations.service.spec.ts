import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { environment } from '@environments/environment';
import { AppModule } from '@core/models/app-module';
import { ModuleRecommendationsService, ModuleRecommendationDto } from './module-recommendations.service';

describe('ModuleRecommendationsService (plan §3.3)', () => {
  let service: ModuleRecommendationsService;
  let httpMock: HttpTestingController;
  const API_URL = `${environment.apiUrl}/company/module-recommendations`;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        ModuleRecommendationsService,
        provideHttpClient(),
        provideHttpClientTesting()
      ]
    });
    service = TestBed.inject(ModuleRecommendationsService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('getRecommendations() sends a GET to /api/company/module-recommendations', () => {
    const mockRecos: ModuleRecommendationDto[] = [
      { moduleId: AppModule.CRM, reasonCode: 'high-quote-volume', reasonFr: 'Vous émettez beaucoup de devis — le CRM peut vous aider à suivre vos prospects.' }
    ];

    service.getRecommendations().subscribe((res) => {
      expect(res.success).toBeTrue();
      expect(res.data).toEqual(mockRecos);
    });

    const req = httpMock.expectOne(API_URL);
    expect(req.request.method).toBe('GET');
    req.flush({ success: true, data: mockRecos, message: null, errors: [] });
  });

  it('dismiss() sends a POST to /api/company/module-recommendations/{moduleId}/dismiss', () => {
    const moduleId = AppModule.CRM;

    service.dismiss(moduleId).subscribe((res) => {
      expect(res.success).toBeTrue();
      expect(res.data).toBeTrue();
    });

    const req = httpMock.expectOne(`${API_URL}/${moduleId}/dismiss`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({});
    req.flush({ success: true, data: true, message: 'Recommandation ignorée.', errors: [] });
  });
});
