import { ComponentFixture, TestBed } from '@angular/core/testing';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { StepInformationsComponent } from './step-informations.component';

describe('StepInformationsComponent', () => {
  let component: StepInformationsComponent;
  let fixture: ComponentFixture<StepInformationsComponent>;
  let fb: FormBuilder;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [StepInformationsComponent, ReactiveFormsModule],
      providers: [FormBuilder, provideNoopAnimations()]
    }).compileComponents();

    fb = TestBed.inject(FormBuilder);
    fixture = TestBed.createComponent(StepInformationsComponent);
    component = fixture.componentInstance;
    component.form = fb.group({
      firstName: [''],
      lastName: [''],
      email: [''],
      password: [''],
      confirmPassword: [''],
      partnerCode: [''],
      companyName: [''],
      nif: [''],
      taxRegime: [null],
      companyEmail: [''],
      phone: [''],
      website: [''],
      warehouseName: ['']
    });
    component.taxRegimes = [
      { label: 'Régime réel', value: 0 },
      { label: 'Régime forfaitaire', value: 1 },
      { label: 'Exonéré', value: 2 }
    ];
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should render one input for each simple text field', () => {
    expect(fixture.nativeElement.querySelector('#wiz-firstName')).not.toBeNull();
    expect(fixture.nativeElement.querySelector('#wiz-lastName')).not.toBeNull();
    expect(fixture.nativeElement.querySelector('#wiz-email')).not.toBeNull();
    expect(fixture.nativeElement.querySelector('#wiz-companyName')).not.toBeNull();
  });

  describe('isInvalid', () => {
    it('should be false when the field is untouched', () => {
      component.form.get('email')?.setErrors({ required: true });
      expect(component.isInvalid('email')).toBeFalse();
    });

    it('should be true only after the field is touched and invalid', () => {
      component.form.get('email')?.setErrors({ required: true });
      component.form.get('email')?.markAsTouched();
      expect(component.isInvalid('email')).toBeTrue();
    });
  });

  describe('partner code toggle', () => {
    it('should default to hidden', () => {
      expect(component.showPartnerCode).toBeFalse();
      expect(fixture.nativeElement.querySelector('#wiz-partnerCode')).toBeNull();
    });

    it('should reveal the partner code field on toggle', () => {
      component.togglePartnerCode();
      fixture.detectChanges();
      expect(component.showPartnerCode).toBeTrue();
      expect(fixture.nativeElement.querySelector('#wiz-partnerCode')).not.toBeNull();
    });
  });

  describe('password strength helpers', () => {
    it('should report "empty" strength with no password', () => {
      expect(component.passwordStrengthLevel()).toBe('empty');
      expect(component.passwordStrengthMetCount()).toBe(0);
    });

    it('should increase the met-criteria count as the password gets stronger', () => {
      component.form.get('password')?.setValue('Test1234@Password');
      const criteria = component.passwordCriteria();
      expect(criteria.minLength).toBeTrue();
      expect(criteria.hasUpper).toBeTrue();
      expect(criteria.hasLower).toBeTrue();
      expect(criteria.hasDigit).toBeTrue();
      expect(criteria.hasSpecial).toBeTrue();
      expect(component.passwordStrengthMetCount()).toBe(5);
    });
  });

  describe('password match state', () => {
    it('should report mismatch when passwords differ', () => {
      component.form.patchValue({ password: 'Test1234@Password', confirmPassword: 'Different' });
      expect(component.passwordMismatch()).toBeTrue();
      expect(component.passwordMatches()).toBeFalse();
    });

    it('should report a match when passwords are identical and non-empty', () => {
      component.form.patchValue({ password: 'Test1234@Password', confirmPassword: 'Test1234@Password' });
      expect(component.passwordMismatch()).toBeFalse();
      expect(component.passwordMatches()).toBeTrue();
    });
  });

  describe('aria-describedby helpers', () => {
    it('should include the error id in passwordFieldAriaDescribedBy when invalid', () => {
      component.form.get('password')?.setErrors({ required: true });
      component.form.get('password')?.markAsTouched();
      expect(component.passwordFieldAriaDescribedBy()).toContain('wiz-password-error');
    });

    it('should return null for confirmPasswordAriaDescribedBy when nothing to report', () => {
      expect(component.confirmPasswordAriaDescribedBy()).toBeNull();
    });

    it('should point to the mismatch error id when passwords differ', () => {
      component.form.patchValue({ password: 'Test1234@Password', confirmPassword: 'Different' });
      expect(component.confirmPasswordAriaDescribedBy()).toBe('wiz-confirmPassword-error');
    });

    it('should point to the success id when passwords match', () => {
      component.form.patchValue({ password: 'Test1234@Password', confirmPassword: 'Test1234@Password' });
      expect(component.confirmPasswordAriaDescribedBy()).toBe('wiz-confirmPassword-success');
    });
  });

  it('should emit nifBlur when onNifBlur is invoked', () => {
    let emitted = false;
    component.nifBlur.subscribe(() => (emitted = true));
    component.onNifBlur();
    expect(emitted).toBeTrue();
  });
});
