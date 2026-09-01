import { ComponentFixture, TestBed } from '@angular/core/testing';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { StepConfigurationComponent } from './step-configuration.component';
import { AppModule } from '@core/models/app-module';
import { environment } from '@environments/environment';
import { SectorCatalogDto, ApiResponse } from '../../registration-catalog';

describe('StepConfigurationComponent', () => {
  let component: StepConfigurationComponent;
  let fixture: ComponentFixture<StepConfigurationComponent>;
  let fb: FormBuilder;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [StepConfigurationComponent, ReactiveFormsModule],
      providers: [provideHttpClient(), provideHttpClientTesting()]
    }).compileComponents();

    fb = TestBed.inject(FormBuilder);
    fixture = TestBed.createComponent(StepConfigurationComponent);
    component = fixture.componentInstance;
    component.form = fb.group({
      enabledModules: [[...component.catalog.recommendedModules(null, null)]]
    });
    component.segment = null;
    component.domain = null;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should list every core module as locked-on and never as optional/recommended', () => {
    const coreIds = component.coreModules.map(m => m.id);
    expect(coreIds.length).toBeGreaterThan(0);
    coreIds.forEach(id => {
      expect(component.recommendedModules.some(m => m.id === id)).toBeFalse();
      expect(component.optionalModules.some(m => m.id === id)).toBeFalse();
    });
  });

  it('should render one locked module card per core module', () => {
    const lockedCards = fixture.nativeElement.querySelectorAll('.mod-card.locked');
    expect(lockedCards.length).toBe(component.coreModules.length);
  });

  it('should have no profileLabel and no recommended modules when neither segment nor domain is set', () => {
    expect(component.profileLabel).toBe('');
    expect(component.recommendedModules.length).toBe(0);
  });

  it('should compute recommended/optional modules and a profile label once a segment+domain are set', () => {
    component.segment = 'commerce';
    component.domain = 'artisanat';
    fixture.detectChanges();

    expect(component.profileLabel).toContain('Commerce');
    expect(component.recommendedModules.length).toBeGreaterThan(0);
    // A module cannot be both recommended and optional at the same time.
    const recommendedIds = new Set(component.recommendedModules.map(m => m.id));
    component.optionalModules.forEach(m => expect(recommendedIds.has(m.id)).toBeFalse());
  });

  describe('module enabled state', () => {
    it('should report enabled for ids present in the enabledModules control', () => {
      const enabled: AppModule[] = component.form.get('enabledModules')?.value ?? [];
      expect(enabled.length).toBeGreaterThan(0);
      expect(component.isModuleEnabled(enabled[0])).toBeTrue();
    });

    it('should report disabled for an id not present in the enabledModules control', () => {
      component.form.get('enabledModules')?.setValue([]);
      expect(component.isModuleEnabled(AppModule.CRM)).toBeFalse();
    });
  });

  it('should emit moduleToggled with the module id on toggleModule', () => {
    let emittedId: AppModule | undefined;
    component.moduleToggled.subscribe(id => (emittedId = id));
    component.toggleModule(AppModule.CRM);
    expect(emittedId).toBe(AppModule.CRM);
  });

  it('should emit resetToRecommendations on onReset', () => {
    let emitted = false;
    component.resetToRecommendations.subscribe(() => (emitted = true));
    component.onReset();
    expect(emitted).toBeTrue();
  });

  it('should emit resetToRecommendations when the reset button is clicked', () => {
    let emitted = false;
    component.resetToRecommendations.subscribe(() => (emitted = true));
    const button: HTMLButtonElement = fixture.nativeElement.querySelector('.btn-reset');
    button.click();
    expect(emitted).toBeTrue();
  });

  describe('premium module gating — "Plan supérieur" (Free plan, static-fallback path)', () => {
    const PREMIUM = [AppModule.AI, AppModule.Forecasting, AppModule.Studio, AppModule.Payroll];

    beforeEach(() => {
      component.segment = 'commerce';
      component.domain = 'artisanat';
      fixture.detectChanges();
    });

    it('premium modules are never listed as optional or recommended', () => {
      for (const premium of PREMIUM) {
        expect(component.optionalModules.some(m => m.id === premium)).toBeFalse();
        expect(component.recommendedModules.some(m => m.id === premium)).toBeFalse();
      }
    });

    it('premiumModules getter returns the 4 premium modules present in the static catalog, sorted', () => {
      expect(component.premiumModules.map(m => m.id)).toEqual(PREMIUM);
    });

    it('renders one locked "Plan supérieur" card per premium module, with a lock badge and no toggle', () => {
      const premiumCards: HTMLElement[] = Array.from(fixture.nativeElement.querySelectorAll('.mod-card.locked-plan'));
      expect(premiumCards.length).toBe(component.premiumModules.length);
      for (const card of premiumCards) {
        expect(card.getAttribute('aria-disabled')).toBe('true');
        expect(card.querySelector('.mod-locked-badge--plan')?.textContent).toContain('Plan supérieur');
        // A locked-plan card must not carry an input switch (not toggleable).
        expect(card.querySelector('p-inputswitch, .p-inputswitch')).toBeNull();
      }
    });

    it('locked-plan cards are distinct from core "Inclus" cards', () => {
      const coreLocked = fixture.nativeElement.querySelectorAll('.mod-card.locked').length;
      const planLocked = fixture.nativeElement.querySelectorAll('.mod-card.locked-plan').length;
      expect(coreLocked).toBe(component.coreModules.length);
      expect(planLocked).toBe(component.premiumModules.length);
    });

    it('premium modules are not present in the enabledModules control (never submitted)', () => {
      const enabled: AppModule[] = component.form.get('enabledModules')?.value ?? [];
      for (const premium of PREMIUM) {
        expect(enabled).not.toContain(premium);
      }
    });
  });

  describe('module dependency locking (plan WP-F3)', () => {
    const CATALOG_URL = `${environment.apiUrl}/public/sector-catalog`;

    function fakeCatalog(): SectorCatalogDto {
      return {
        segments: [
          {
            code: 'commerce',
            labelFr: 'Commerce',
            descriptionFr: 'Négoce et distribution.',
            iconKey: 'shopping-cart',
            sortOrder: 0,
            coreModuleIds: [],
            recommendedModuleIds: [AppModule.Forecasting, AppModule.Stock],
            domainCodes: []
          }
        ],
        domains: [],
        modules: [
          { id: AppModule.Stock, code: 'stock', labelFr: 'Stock', isCore: false },
          { id: AppModule.Forecasting, code: 'forecasting', labelFr: 'Prévisions IA', isCore: false }
        ],
        moduleDependencies: [
          { moduleId: AppModule.Forecasting, requiredModuleId: AppModule.Stock }
        ]
      };
    }

    function loadRemoteCatalog(): void {
      const httpMock = TestBed.inject(HttpTestingController);
      component.catalog.load();
      httpMock.expectOne(CATALOG_URL).flush({ success: true, data: fakeCatalog() } as ApiResponse<SectorCatalogDto>);
      fixture.detectChanges();
    }

    it('isLockedByDependency is true for a module required by another currently-enabled module', () => {
      loadRemoteCatalog();
      component.form.get('enabledModules')?.setValue([AppModule.Forecasting, AppModule.Stock]);

      expect(component.isLockedByDependency(AppModule.Stock)).toBeTrue();
      expect(component.isLockedByDependency(AppModule.Forecasting)).toBeFalse();
    });

    it('dependencyLockLabel names the enabled module(s) that require it', () => {
      loadRemoteCatalog();
      component.form.get('enabledModules')?.setValue([AppModule.Forecasting, AppModule.Stock]);

      expect(component.dependencyLockLabel(AppModule.Stock)).toBe('Requis par Prévisions IA');
    });

    it('renders a disabled, tooltip-bearing switch for a locked module', () => {
      loadRemoteCatalog();
      component.form.get('enabledModules')?.setValue([AppModule.Forecasting, AppModule.Stock]);
      fixture.detectChanges();

      const lockedCard: HTMLElement = fixture.nativeElement.querySelector('.mod-card.locked-dep');
      expect(lockedCard).toBeTruthy();
      expect(lockedCard.querySelector('.mod-badge.dep')?.textContent).toContain('Requis par');
    });

    it('wasAutoEnabled/autoEnabledHintLabel surface the dependency hint for a just-auto-enabled module', () => {
      loadRemoteCatalog();
      component.autoEnabledIds = [AppModule.Stock];
      component.form.get('enabledModules')?.setValue([AppModule.Forecasting, AppModule.Stock]);
      fixture.detectChanges();

      expect(component.wasAutoEnabled(AppModule.Stock)).toBeTrue();
      expect(component.autoEnabledHintLabel(AppModule.Stock)).toContain('Activé automatiquement');

      const hint: HTMLElement = fixture.nativeElement.querySelector('.dep-hint');
      expect(hint).toBeTruthy();
    });
  });
});
