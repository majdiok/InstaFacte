import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HttpErrorResponse, provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { StorefrontSettingsComponent } from './storefront-settings.component';
import { ErrorHandlerService } from '@core/services/error-handler.service';

function draftProfilePayload() {
  return {
    id: '11111111-1111-1111-1111-111111111111',
    tenantId: '22222222-2222-2222-2222-222222222222',
    slug: 'acme',
    displayName: 'Acme',
    status: 'draft',
    consentVersion: '1.0.0',
    tagline: '',
    descriptionMarkdown: '',
    brandPrimaryColorHex: '#2563EB',
    brandSecondaryColorHex: '#0EA5E9',
    publicLogoUrl: '',
    publicCoverImageUrl: '',
    category: 0,
    facadeTheme: 0,
    publicContactEmail: 'pub@acme.test',
    publicContactPhone: '',
    publicContactWhatsApp: '',
    orderSubmissionEnabled: true
  };
}

describe('StorefrontSettingsComponent', () => {
  let fixture: ComponentFixture<StorefrontSettingsComponent>;
  let component: StorefrontSettingsComponent;
  let httpMock: HttpTestingController;

  beforeEach(async () => {
    const activatedRouteStub = {
      snapshot: { params: {}, queryParams: {}, fragment: null, data: {} },
      params: of({}),
      queryParams: of({}),
      fragment: of(null),
      data: of({}),
      outlet: 'primary',
      parent: null,
      firstChild: null,
      children: [],
      pathFromRoot: [],
      paramMap: of(convertToParamMap({})),
      queryParamMap: of(convertToParamMap({})),
      url: of([]),
      title: of(undefined)
    };

    await TestBed.configureTestingModule({
      imports: [StorefrontSettingsComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideNoopAnimations(),
        provideRouter([]),
        ErrorHandlerService,
        { provide: ActivatedRoute, useValue: activatedRouteStub }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(StorefrontSettingsComponent);
    component = fixture.componentInstance;
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('should create', () => {
    fixture.detectChanges();
    httpMock.expectOne(req => req.url.includes('/storefront/tenant/profile')).flush({
      success: true,
      data: draftProfilePayload()
    });
    expect(component).toBeTruthy();
  });

  it('should surface API message on submit-for-review HTTP 400', () => {
    fixture.detectChanges();
    httpMock.expectOne(req => req.url.includes('/storefront/tenant/profile')).flush({
      success: true,
      data: draftProfilePayload()
    });
    fixture.detectChanges();

    component.submitForReview();
    const submitReq = httpMock.expectOne(req => req.url.includes('/submit-for-review'));
    const msg = 'Une description est obligatoire avant soumission';
    submitReq.flush(
      { success: false, data: null, message: msg, errors: [msg] },
      { status: 400, statusText: 'Bad Request' }
    );

    expect(component.error()).toBe(msg);
    expect(component.workflowBusy()).toBe(false);
  });

  it('extractErrorMessage fallback works for HttpErrorResponse shape used by HttpClient', () => {
    const errorHandler = TestBed.inject(ErrorHandlerService);
    const err = new HttpErrorResponse({
      status: 400,
      url: 'https://localhost:7001/api/storefront/tenant/submit-for-review',
      error: { success: false, data: null, message: 'Un logo public est obligatoire avant soumission', errors: [] }
    });
    expect(errorHandler.extractErrorMessage(err)).toContain('logo');
  });
});
