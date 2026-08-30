import { ComponentFixture, TestBed } from '@angular/core/testing';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { provideRouter } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { StepFinalisationComponent } from './step-finalisation.component';
import { AppModule } from '@core/models/app-module';

describe('StepFinalisationComponent', () => {
  let component: StepFinalisationComponent;
  let fixture: ComponentFixture<StepFinalisationComponent>;
  let fb: FormBuilder;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [StepFinalisationComponent, ReactiveFormsModule],
      providers: [FormBuilder, provideRouter([]), provideNoopAnimations()]
    }).compileComponents();

    fb = TestBed.inject(FormBuilder);
    fixture = TestBed.createComponent(StepFinalisationComponent);
    component = fixture.componentInstance;
    component.form = fb.group({
      companySegment: [''],
      businessDomain: [''],
      taxRegime: [null],
      enabledModules: [[]],
      street: [''],
      streetLine2: [''],
      city: [''],
      postalCode: [''],
      governorate: [''],
      acceptTerms: [false, Validators.requiredTrue]
    });
    component.taxRegimes = [
      { label: 'Régime réel', value: 0 },
      { label: 'Régime forfaitaire', value: 1 },
      { label: 'Exonéré', value: 2 }
    ];
    component.governorates = [
      { label: 'Monastir', value: 'Monastir' },
      { label: 'Tunis', value: 'Tunis' }
    ];
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  describe('isInvalid', () => {
    it('should be false when the field is untouched', () => {
      component.form.get('street')?.setErrors({ required: true });
      expect(component.isInvalid('street')).toBeFalse();
    });

    it('should be true when the field is invalid and touched', () => {
      component.form.get('street')?.setErrors({ required: true });
      component.form.get('street')?.markAsTouched();
      expect(component.isInvalid('street')).toBeTrue();
    });
  });

  describe('recap labels', () => {
    it('should render a dash placeholder when nothing was chosen yet', () => {
      expect(component.recapSegment()).toBe('—');
      expect(component.recapDomain()).toBe('—');
      expect(component.recapTaxRegime()).toBe('—');
      expect(component.recapModuleLabels()).toEqual([]);
    });

    it('should resolve the segment/domain/tax-regime labels from the catalog and taxRegimes input', () => {
      component.form.patchValue({
        companySegment: 'commerce',
        businessDomain: 'artisanat',
        taxRegime: 1
      });

      expect(component.recapSegment()).toBe(component.catalog.segmentLabel('commerce'));
      expect(component.recapDomain()).toBe(component.catalog.domainLabel('artisanat'));
      expect(component.recapTaxRegime()).toBe('Régime forfaitaire');
    });

    it('should map enabledModules ids to their catalog labels', () => {
      component.form.patchValue({ enabledModules: [AppModule.CRM, AppModule.Stock] });
      expect(component.recapModuleLabels()).toEqual([
        component.catalog.moduleLabel(AppModule.CRM),
        component.catalog.moduleLabel(AppModule.Stock)
      ]);
    });
  });

  it('should emit editStep with the target step index on goTo', () => {
    let emittedStep: number | undefined;
    component.editStep.subscribe(step => (emittedStep = step));
    component.goTo(2);
    expect(emittedStep).toBe(2);
  });

  it('should emit editStep when a recap edit button is clicked', () => {
    let emittedStep: number | undefined;
    component.editStep.subscribe(step => (emittedStep = step));
    const editButtons: NodeListOf<HTMLButtonElement> = fixture.nativeElement.querySelectorAll('.rec-edit');
    expect(editButtons.length).toBeGreaterThan(0);
    editButtons[0].click();
    expect(emittedStep).toBeDefined();
  });

  it('should require acceptance of the CGU checkbox before the field is considered valid', () => {
    expect(component.form.get('acceptTerms')?.valid).toBeFalse();
    component.form.get('acceptTerms')?.setValue(true);
    expect(component.form.get('acceptTerms')?.value).toBeTrue();
  });
});
