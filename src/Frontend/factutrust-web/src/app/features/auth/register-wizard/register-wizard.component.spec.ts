import { ComponentFixture, TestBed, fakeAsync, tick, discardPeriodicTasks } from '@angular/core/testing';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { provideRouter, Router } from '@angular/router';
import { HttpErrorResponse, provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { of, throwError, NEVER } from 'rxjs';
import { RegisterWizardComponent } from './register-wizard.component';
import { AuthService, ApiResponse, AuthResponse, RegisterRequest } from '@core/services/auth.service';
import { ErrorHandlerService } from '@core/services/error-handler.service';
import { WarehouseContextService } from '@core/services/warehouse-context.service';
import { AppModule } from '@core/models/app-module';

/** Mock ApiResponse success pour register */
function validRegisterResponse(): ApiResponse<AuthResponse> {
  return {
    success: true,
    data: {} as AuthResponse,
    message: null,
    errors: []
  };
}

/** Mock ApiResponse failure pour register */
function errorRegisterResponse(errors: string[]): ApiResponse<AuthResponse> {
  return {
    success: false,
    data: {} as AuthResponse,
    message: null,
    errors
  };
}

describe('RegisterWizardComponent', () => {
  let component: RegisterWizardComponent;
  let fixture: ComponentFixture<RegisterWizardComponent>;
  let authService: jasmine.SpyObj<AuthService>;
  let router: Router;
  let warehouseContext: jasmine.SpyObj<WarehouseContextService>;
  let errorHandler: jasmine.SpyObj<ErrorHandlerService>;

  beforeEach(async () => {
    const authServiceSpy = jasmine.createSpyObj('AuthService', ['register']);
    const warehouseContextSpy = jasmine.createSpyObj('WarehouseContextService', [
      'navigateAfterSuccessfulAuth'
    ]);
    const errorHandlerSpy = jasmine.createSpyObj('ErrorHandlerService', [
      'extractErrorMessage',
      'logError'
    ]);

    await TestBed.configureTestingModule({
      imports: [RegisterWizardComponent, ReactiveFormsModule],
      providers: [
        FormBuilder,
        provideRouter([]),
        provideNoopAnimations(),
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: AuthService, useValue: authServiceSpy },
        { provide: WarehouseContextService, useValue: warehouseContextSpy },
        { provide: ErrorHandlerService, useValue: errorHandlerSpy }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(RegisterWizardComponent);
    component = fixture.componentInstance;
    authService = TestBed.inject(AuthService) as jasmine.SpyObj<AuthService>;
    router = TestBed.inject(Router);
    spyOn(router, 'navigate');
    warehouseContext = TestBed.inject(WarehouseContextService) as jasmine.SpyObj<WarehouseContextService>;
    errorHandler = TestBed.inject(ErrorHandlerService) as jasmine.SpyObj<ErrorHandlerService>;

    // ngOnInit() below fires catalog.load() — the sector-catalog request is registered
    // with HttpTestingController but intentionally left unflushed in tests that don't
    // exercise it: loadState stays 'loading' and the service keeps serving the static
    // fallback (remoteCatalog stays null), matching pre-Phase-2 behavior exactly.
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should start on step 0 with 4 steps total', () => {
    expect(component.currentStep()).toBe(0);
    expect(component.stepCount()).toBe(4);
    expect(component.stepHumanIndex()).toBe(1);
  });

  it('should default enabledModules to the core-only recommendation (no segment/domain yet)', () => {
    const coreOnly = component.catalog.recommendedModules(null, null);
    expect(component.form.get('enabledModules')?.value).toEqual(coreOnly);
  });

  describe('isCurrentStepValid / navigation gating', () => {
    it('should invalidate step 0 until segment and domain are both chosen', () => {
      expect(component.isCurrentStepValid()).toBeFalse();

      component.form.patchValue({ companySegment: 'commerce' });
      expect(component.isCurrentStepValid()).toBeFalse();

      component.form.patchValue({ businessDomain: 'artisanat' });
      expect(component.isCurrentStepValid()).toBeTrue();
    });

    it('should not advance nextStep() while the current step is invalid', () => {
      component.nextStep();
      expect(component.currentStep()).toBe(0);
    });

    it('should advance nextStep() once step 0 is valid', () => {
      component.form.patchValue({ companySegment: 'commerce', businessDomain: 'artisanat' });
      component.nextStep();
      expect(component.currentStep()).toBe(1);
    });

    it('should invalidate step 1 (account + company info) until required fields are valid', () => {
      component.currentStep.set(1);
      expect(component.isCurrentStepValid()).toBeFalse();

      component.form.patchValue({
        firstName: 'John',
        lastName: 'Doe',
        email: 'test@example.com',
        password: 'Test1234@Password',
        confirmPassword: 'Test1234@Password',
        companyName: 'Test Company',
        nif: '1234567A/B/C/000',
        taxRegime: 0,
        companyEmail: 'contact@test.tn',
        phone: '98455112'
      });
      expect(component.isCurrentStepValid()).toBeTrue();
    });

    it('should invalidate step 1 when passwords mismatch even if other fields are valid', () => {
      component.currentStep.set(1);
      component.form.patchValue({
        firstName: 'John',
        lastName: 'Doe',
        email: 'test@example.com',
        password: 'Test1234@Password',
        confirmPassword: 'DifferentPassword',
        companyName: 'Test Company',
        nif: '1234567A/B/C/000',
        taxRegime: 0,
        companyEmail: 'contact@test.tn',
        phone: '98455112'
      });
      expect(component.isCurrentStepValid()).toBeFalse();
    });

    it('should always validate step 2 (configuration has no required fields)', () => {
      component.currentStep.set(2);
      expect(component.isCurrentStepValid()).toBeTrue();
    });

    it('should invalidate step 3 until address + acceptTerms are provided', () => {
      component.currentStep.set(3);
      expect(component.isCurrentStepValid()).toBeFalse();

      component.form.patchValue({
        street: '155 Rue Test',
        city: 'Monastir',
        governorate: 'Monastir',
        acceptTerms: true
      });
      expect(component.isCurrentStepValid()).toBeTrue();
    });

    it('should allow goToStep to jump backward freely regardless of validity', () => {
      component.form.patchValue({ companySegment: 'commerce', businessDomain: 'artisanat' });
      component.currentStep.set(1);
      component.goToStep(0);
      expect(component.currentStep()).toBe(0);
    });

    it('should block goToStep from jumping forward when the current step is invalid', () => {
      component.goToStep(2);
      expect(component.currentStep()).toBe(0);
    });

    it('should allow goToStep to jump forward when the current step is valid', () => {
      component.form.patchValue({ companySegment: 'commerce', businessDomain: 'artisanat' });
      component.goToStep(1);
      expect(component.currentStep()).toBe(1);
    });

    it('should ignore goToStep with an out-of-range index', () => {
      component.goToStep(99);
      expect(component.currentStep()).toBe(0);
      component.goToStep(-1);
      expect(component.currentStep()).toBe(0);
    });

    it('should not go below step 0 with previousStep()', () => {
      component.previousStep();
      expect(component.currentStep()).toBe(0);
    });

    it('should go back one step with previousStep()', () => {
      component.currentStep.set(2);
      component.previousStep();
      expect(component.currentStep()).toBe(1);
    });
  });

  describe('module recommendation / manual toggle behavior', () => {
    it('should recompute recommended modules when segment/domain changes, until the user customizes', () => {
      component.form.patchValue({ companySegment: 'commerce', businessDomain: 'artisanat' });
      const expected = component.catalog.recommendedModules('commerce', 'artisanat');
      expect(component.form.get('enabledModules')?.value).toEqual(expected);
    });

    it('should stop overwriting the module selection once the user manually toggles a module', () => {
      component.form.patchValue({ companySegment: 'commerce', businessDomain: 'artisanat' });
      component.onModuleToggled(AppModule.CRM);
      expect(component.modulesTouched()).toBeTrue();

      // Changing the profile again must not clobber the user's manual choice.
      component.form.patchValue({ companySegment: 'services' });
      expect(component.form.get('enabledModules')?.value).toContain(AppModule.CRM);
    });

    it('should add a module on toggle when not already enabled', () => {
      const before: AppModule[] = component.form.get('enabledModules')?.value ?? [];
      expect(before).not.toContain(AppModule.CRM);
      component.onModuleToggled(AppModule.CRM);
      expect(component.form.get('enabledModules')?.value).toContain(AppModule.CRM);
    });

    it('should remove a module on toggle when already enabled', () => {
      component.form.patchValue({ companySegment: 'commerce', businessDomain: 'artisanat' });
      const enabled: AppModule[] = component.form.get('enabledModules')?.value ?? [];
      const target = enabled.find(id => id !== AppModule.Administration) ?? enabled[0];
      component.onModuleToggled(target);
      expect(component.form.get('enabledModules')?.value).not.toContain(target);
    });

    it('should reset the module selection to the recommendation and clear modulesTouched', () => {
      component.form.patchValue({ companySegment: 'commerce', businessDomain: 'artisanat' });
      component.onModuleToggled(AppModule.CRM);
      expect(component.modulesTouched()).toBeTrue();

      component.resetModulesToRecommendations();
      expect(component.modulesTouched()).toBeFalse();
      expect(component.form.get('enabledModules')?.value).toEqual(
        component.catalog.recommendedModules('commerce', 'artisanat')
      );
    });
  });

  describe('module dependency handling (plan WP-F3)', () => {
    function loadDependencyCatalog(): void {
      const httpMock = TestBed.inject(HttpTestingController);
      component.catalog.load();
      httpMock.expectOne(`${component.environment.apiUrl}/public/sector-catalog`).flush({
        success: true,
        data: {
          segments: [
            { code: 'commerce', labelFr: 'Commerce', descriptionFr: '', iconKey: 'x', sortOrder: 0, coreModuleIds: [], recommendedModuleIds: [], domainCodes: [] }
          ],
          domains: [],
          modules: [
            { id: AppModule.Stock, code: 'stock', labelFr: 'Stock', isCore: false },
            { id: AppModule.Forecasting, code: 'forecasting', labelFr: 'Prévisions IA', isCore: false }
          ],
          moduleDependencies: [
            { moduleId: AppModule.Forecasting, requiredModuleId: AppModule.Stock }
          ]
        }
      });
    }

    it('auto-enables a hard dependency when enabling a module that requires it, and records it in lastAutoEnabled', () => {
      loadDependencyCatalog();
      expect(component.form.get('enabledModules')?.value ?? []).not.toContain(AppModule.Stock);

      component.onModuleToggled(AppModule.Forecasting);

      const enabled: AppModule[] = component.form.get('enabledModules')?.value ?? [];
      expect(enabled).toContain(AppModule.Forecasting);
      expect(enabled).toContain(AppModule.Stock);
      expect(component.lastAutoEnabled()).toEqual([AppModule.Stock]);
    });

    it('does not re-add a required module that is already enabled, and reports no auto-enabled ids', () => {
      loadDependencyCatalog();
      component.form.get('enabledModules')?.setValue([AppModule.Stock]);

      component.onModuleToggled(AppModule.Forecasting);

      const enabled: AppModule[] = component.form.get('enabledModules')?.value ?? [];
      expect(enabled.filter(id => id === AppModule.Stock).length).toBe(1);
      expect(component.lastAutoEnabled()).toEqual([]);
    });

    it('refuses to disable a module while another enabled module still requires it', () => {
      loadDependencyCatalog();
      component.form.get('enabledModules')?.setValue([AppModule.Forecasting, AppModule.Stock]);

      component.onModuleToggled(AppModule.Stock);

      const enabled: AppModule[] = component.form.get('enabledModules')?.value ?? [];
      expect(enabled).toContain(AppModule.Stock);
    });

    it('allows disabling a module once its dependent has also been disabled', () => {
      loadDependencyCatalog();
      component.form.get('enabledModules')?.setValue([AppModule.Forecasting, AppModule.Stock]);

      component.onModuleToggled(AppModule.Forecasting);
      component.onModuleToggled(AppModule.Stock);

      const enabled: AppModule[] = component.form.get('enabledModules')?.value ?? [];
      expect(enabled).not.toContain(AppModule.Stock);
      expect(enabled).not.toContain(AppModule.Forecasting);
    });

    it('resetModulesToRecommendations clears the auto-enabled hint', () => {
      loadDependencyCatalog();
      component.onModuleToggled(AppModule.Forecasting);
      expect(component.lastAutoEnabled().length).toBeGreaterThan(0);

      component.resetModulesToRecommendations();
      expect(component.lastAutoEnabled()).toEqual([]);
    });
  });

  describe('domain clearing on segment change (plan WP-F2)', () => {
    it('clears businessDomain and sets domainClearedNotice when the new segment (remote catalog) no longer offers it', () => {
      const httpMock = TestBed.inject(HttpTestingController);
      component.catalog.load();
      httpMock.expectOne(`${component.environment.apiUrl}/public/sector-catalog`).flush({
        success: true,
        data: {
          segments: [
            { code: 'commerce', labelFr: 'Commerce', descriptionFr: '', iconKey: 'x', sortOrder: 0, coreModuleIds: [], recommendedModuleIds: [], domainCodes: ['artisanat'] },
            { code: 'association', labelFr: 'Association', descriptionFr: '', iconKey: 'x', sortOrder: 1, coreModuleIds: [], recommendedModuleIds: [], domainCodes: ['autre'] }
          ],
          domains: [
            { code: 'artisanat', labelFr: 'Artisanat', sortOrder: 0, additionalModuleIds: [] },
            { code: 'autre', labelFr: 'Autre domaine', sortOrder: 1, additionalModuleIds: [] }
          ],
          modules: [],
          moduleDependencies: []
        }
      });

      component.form.patchValue({ companySegment: 'commerce', businessDomain: 'artisanat' });
      expect(component.form.get('businessDomain')?.value).toBe('artisanat');
      expect(component.domainClearedNotice()).toBeFalse();

      component.form.get('companySegment')?.setValue('association');
      expect(component.form.get('businessDomain')?.value).toBe('');
      expect(component.form.get('businessDomain')?.touched).toBeFalse();
      expect(component.domainClearedNotice()).toBeTrue();
    });

    it('does not clear businessDomain when it stays valid for the new segment', () => {
      const httpMock = TestBed.inject(HttpTestingController);
      component.catalog.load();
      httpMock.expectOne(`${component.environment.apiUrl}/public/sector-catalog`).flush({
        success: true,
        data: {
          segments: [
            { code: 'commerce', labelFr: 'Commerce', descriptionFr: '', iconKey: 'x', sortOrder: 0, coreModuleIds: [], recommendedModuleIds: [], domainCodes: ['autre'] },
            { code: 'services', labelFr: 'Services', descriptionFr: '', iconKey: 'x', sortOrder: 1, coreModuleIds: [], recommendedModuleIds: [], domainCodes: ['autre'] }
          ],
          domains: [
            { code: 'autre', labelFr: 'Autre domaine', sortOrder: 0, additionalModuleIds: [] }
          ],
          modules: [],
          moduleDependencies: []
        }
      });

      component.form.patchValue({ companySegment: 'commerce', businessDomain: 'autre' });
      component.form.get('companySegment')?.setValue('services');

      expect(component.form.get('businessDomain')?.value).toBe('autre');
      expect(component.domainClearedNotice()).toBeFalse();
    });
  });

  describe('late remote catalog re-run (plan WP-F1)', () => {
    it('recomputes enabledModules once loadState becomes "remote", when the user has not touched module toggles', () => {
      component.form.patchValue({ companySegment: 'commerce', businessDomain: 'artisanat' });
      const httpMock = TestBed.inject(HttpTestingController);
      component.catalog.load();
      httpMock.expectOne(`${component.environment.apiUrl}/public/sector-catalog`).flush({
        success: true,
        data: {
          segments: [
            { code: 'commerce', labelFr: 'Commerce', descriptionFr: '', iconKey: 'x', sortOrder: 0, coreModuleIds: [], recommendedModuleIds: [AppModule.Stock], domainCodes: [] }
          ],
          domains: [],
          modules: [{ id: AppModule.Stock, code: 'stock', labelFr: 'Stock', isCore: false }],
          moduleDependencies: []
        }
      });
      fixture.detectChanges();

      expect(component.form.get('enabledModules')?.value).toEqual(
        component.catalog.recommendedModules('commerce', 'artisanat')
      );
      expect(component.form.get('enabledModules')?.value).toContain(AppModule.Stock);
    });

    it('does not overwrite a manually-touched module selection when the remote catalog arrives late', () => {
      component.form.patchValue({ companySegment: 'commerce', businessDomain: 'artisanat' });
      component.onModuleToggled(AppModule.CRM);
      const before = component.form.get('enabledModules')?.value;

      const httpMock = TestBed.inject(HttpTestingController);
      component.catalog.load();
      httpMock.expectOne(`${component.environment.apiUrl}/public/sector-catalog`).flush({
        success: true,
        data: {
          segments: [
            { code: 'commerce', labelFr: 'Commerce', descriptionFr: '', iconKey: 'x', sortOrder: 0, coreModuleIds: [], recommendedModuleIds: [AppModule.Stock], domainCodes: [] }
          ],
          domains: [],
          modules: [{ id: AppModule.Stock, code: 'stock', labelFr: 'Stock', isCore: false }],
          moduleDependencies: []
        }
      });
      fixture.detectChanges();

      expect(component.form.get('enabledModules')?.value).toEqual(before);
    });
  });

  describe('onNifBlur', () => {
    it('should clean and normalize the NIF value on blur', () => {
      component.form.get('nif')?.setValue('1234567a/b/c/000_');
      component.onNifBlur();
      expect(component.form.get('nif')?.value).toBe('1234567/A/B/C/000');
    });
  });

  describe('onSubmit', () => {
    beforeEach(() => {
      component.form.patchValue({
        companySegment: 'commerce',
        businessDomain: 'artisanat',
        firstName: 'John',
        lastName: 'Doe',
        email: 'test@example.com',
        password: 'Test1234@Password',
        confirmPassword: 'Test1234@Password',
        companyName: 'Test Company',
        nif: '1234567A/B/C/000',
        taxRegime: 0,
        companyEmail: 'contact@test.tn',
        phone: '98 455 112',
        street: '155 Rue Test',
        city: 'Monastir',
        governorate: 'Monastir',
        acceptTerms: true
      });
    });

    afterEach(() => {
      component.ngOnDestroy();
    });

    it('should not submit if the form is invalid', () => {
      component.form.patchValue({ email: '' });
      component.onSubmit();
      expect(authService.register).not.toHaveBeenCalled();
    });

    it('should not submit if acceptTerms is false', () => {
      component.form.patchValue({ acceptTerms: false });
      component.onSubmit();
      expect(authService.register).not.toHaveBeenCalled();
    });

    it('should not submit if passwords mismatch', () => {
      component.form.patchValue({ confirmPassword: 'DifferentPassword' });
      component.onSubmit();
      expect(authService.register).not.toHaveBeenCalled();
      expect(component.error()).toContain('mots de passe');
    });

    it('should build a RegisterRequest containing every legacy field plus the 3 new sector fields', () => {
      authService.register.and.returnValue(of(validRegisterResponse()));

      component.onModuleToggled(AppModule.CRM); // exercise a manual module choice too
      component.onSubmit();

      expect(authService.register).toHaveBeenCalled();
      const request = authService.register.calls.mostRecent().args[0] as RegisterRequest;

      // Legacy fields (zero field loss vs. the pre-wizard register request).
      expect(request.email).toBe('test@example.com');
      expect(request.password).toBe('Test1234@Password');
      expect(request.confirmPassword).toBe('Test1234@Password');
      expect(request.firstName).toBe('John');
      expect(request.lastName).toBe('Doe');
      expect(request.companyName).toBe('Test Company');
      expect(request.phone).toBe('98455112');
      expect(request.nif).toBe('1234567/A/B/C/000');
      expect(request.taxRegime).toBe(0);
      expect(request.companyEmail).toBe('contact@test.tn');
      expect(request.street).toBe('155 Rue Test');
      expect(request.city).toBe('Monastir');
      expect(request.governorate).toBe('Monastir');

      // New sector-aware fields.
      expect(request.companySegment).toBe('commerce');
      expect(request.businessDomain).toBe('artisanat');
      expect(request.enabledModules).toEqual(component.form.get('enabledModules')?.value);
      expect(request.enabledModules).toContain(AppModule.CRM);
    });

    it('should clean the NIF value before submission', () => {
      component.form.patchValue({ nif: '1234567A/B/C/000_' });
      authService.register.and.returnValue(of(validRegisterResponse()));

      component.onSubmit();

      const request = authService.register.calls.mostRecent().args[0] as RegisterRequest;
      expect(request.nif).toBe('1234567/A/B/C/000');
    });

    it('should surface pre-submit validation errors (e.g. invalid phone) without calling the API', () => {
      component.form.patchValue({ phone: '00000000' });
      component.onSubmit();

      expect(authService.register).not.toHaveBeenCalled();
      expect(component.error()).toContain('numéro de téléphone');
    });

    it('should navigate to /dashboard on success', () => {
      authService.register.and.returnValue(of(validRegisterResponse()));
      component.onSubmit();
      expect(warehouseContext.navigateAfterSuccessfulAuth).toHaveBeenCalledWith('/dashboard');
    });

    it('should set the error message on a failure response', () => {
      authService.register.and.returnValue(of(errorRegisterResponse(['Format du NIF invalide'])));
      component.onSubmit();
      expect(component.error()).toContain('Format du NIF invalide');
    });

    it('should handle an HTTP error and log it', () => {
      const httpError = new HttpErrorResponse({
        status: 400,
        error: { success: false, errors: ['Le NIF est obligatoire'] }
      });
      errorHandler.extractErrorMessage.and.returnValue('Le NIF est obligatoire');
      authService.register.and.returnValue(throwError(() => httpError));

      component.onSubmit();

      expect(component.error()).toBe('Le NIF est obligatoire');
      expect(errorHandler.logError).toHaveBeenCalled();
    });

    it('should handle a network error (status 0) with a friendly fallback message', () => {
      const httpError = new HttpErrorResponse({ status: 0, error: null });
      errorHandler.extractErrorMessage.and.returnValue('');
      authService.register.and.returnValue(throwError(() => httpError));

      component.onSubmit();

      expect(component.error()).toContain('se connecter au serveur');
    });

    it('should rotate loading messages while waiting for the API', fakeAsync(() => {
      authService.register.and.returnValue(NEVER);

      component.onSubmit();

      expect(component.loading()).toBeTrue();
      expect(component.loadingMessage()).toContain('Création de votre espace');

      tick(6_000);
      expect(component.loadingMessage()).toContain('Configuration de la base');

      tick(30_000);
      expect(component.loadingMessage()).toContain('Finalisation en cours');

      component.ngOnDestroy();
      discardPeriodicTasks();
    }));

    it('should surface a timeout message after 120 seconds and stop loading', fakeAsync(() => {
      authService.register.and.returnValue(NEVER);

      component.onSubmit();
      tick(120_000);

      expect(component.error()).toContain('plus de temps que prévu');
      expect(component.loading()).toBeFalse();
      discardPeriodicTasks();
    }));
  });
});
