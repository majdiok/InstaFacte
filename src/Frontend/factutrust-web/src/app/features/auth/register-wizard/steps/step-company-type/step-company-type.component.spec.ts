import { ComponentFixture, TestBed } from '@angular/core/testing';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { StepCompanyTypeComponent } from './step-company-type.component';
import { SEGMENT_OPTIONS, DOMAIN_OPTIONS } from '../../registration-catalog';

describe('StepCompanyTypeComponent', () => {
  let component: StepCompanyTypeComponent;
  let fixture: ComponentFixture<StepCompanyTypeComponent>;
  let fb: FormBuilder;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [StepCompanyTypeComponent, ReactiveFormsModule],
      providers: [FormBuilder, provideNoopAnimations()]
    }).compileComponents();

    fb = TestBed.inject(FormBuilder);
    fixture = TestBed.createComponent(StepCompanyTypeComponent);
    component = fixture.componentInstance;
    component.form = fb.group({
      companySegment: ['', Validators.required],
      businessDomain: ['']
    });
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should expose the full catalog of segments and domains', () => {
    expect(component.segments.length).toBe(SEGMENT_OPTIONS.length);
    expect(component.domains.length).toBe(DOMAIN_OPTIONS.length);
  });

  describe('segment selection', () => {
    it('should report no segment selected initially', () => {
      expect(component.isSegmentSelected('commerce')).toBeFalse();
    });

    it('should set the companySegment control and mark it touched on selectSegment', () => {
      component.selectSegment('commerce');
      expect(component.form.get('companySegment')?.value).toBe('commerce');
      expect(component.form.get('companySegment')?.touched).toBeTrue();
      expect(component.isSegmentSelected('commerce')).toBeTrue();
      expect(component.isSegmentSelected('services')).toBeFalse();
    });

    it('should select a segment via keyboard Enter', () => {
      const event = new KeyboardEvent('keydown', { key: 'Enter' });
      spyOn(event, 'preventDefault');
      component.onSegmentKeydown(event, 'association');
      expect(event.preventDefault).toHaveBeenCalled();
      expect(component.isSegmentSelected('association')).toBeTrue();
    });

    it('should select a segment via keyboard Space', () => {
      const event = new KeyboardEvent('keydown', { key: ' ' });
      component.onSegmentKeydown(event, 'btp-construction');
      expect(component.isSegmentSelected('btp-construction')).toBeTrue();
    });

    it('should ignore other keys on the segment cards', () => {
      const event = new KeyboardEvent('keydown', { key: 'Tab' });
      component.onSegmentKeydown(event, 'commerce');
      expect(component.isSegmentSelected('commerce')).toBeFalse();
    });

    it('should render one radio card per segment in the DOM', () => {
      const cards = fixture.nativeElement.querySelectorAll('.seg-card');
      expect(cards.length).toBe(SEGMENT_OPTIONS.length);
    });
  });

  describe('domain selection', () => {
    it('should report no domain selected initially', () => {
      expect(component.isDomainSelected('technologie-informatique')).toBeFalse();
    });

    it('should set the businessDomain control on selectDomain', () => {
      component.selectDomain('technologie-informatique');
      expect(component.form.get('businessDomain')?.value).toBe('technologie-informatique');
      expect(component.form.get('businessDomain')?.touched).toBeTrue();
      expect(component.isDomainSelected('technologie-informatique')).toBeTrue();
    });

    it('should render one radio input per domain in the DOM', () => {
      const inputs = fixture.nativeElement.querySelectorAll('.dom-item input[type="radio"]');
      expect(inputs.length).toBe(DOMAIN_OPTIONS.length);
    });
  });

  it('should show the companySegment field error only once invalid and touched', () => {
    expect(fixture.nativeElement.querySelector('.field-error')).toBeNull();

    component.form.get('companySegment')?.markAsTouched();
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('.field-error')).not.toBeNull();
  });
});
