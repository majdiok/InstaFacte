import { ComponentFixture, TestBed } from '@angular/core/testing';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { StepConfigurationComponent } from './step-configuration.component';
import { AppModule } from '@core/models/app-module';

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
});
