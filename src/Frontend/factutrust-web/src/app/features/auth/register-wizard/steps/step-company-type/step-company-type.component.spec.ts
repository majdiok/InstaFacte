import { ComponentFixture, TestBed } from '@angular/core/testing';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { environment } from '@environments/environment';
import { AppModule } from '@core/models/app-module';
import { StepCompanyTypeComponent } from './step-company-type.component';
import { SEGMENT_OPTIONS, DOMAIN_OPTIONS, SectorCatalogDto, ApiResponse } from '../../registration-catalog';

describe('StepCompanyTypeComponent', () => {
  let component: StepCompanyTypeComponent;
  let fixture: ComponentFixture<StepCompanyTypeComponent>;
  let fb: FormBuilder;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [StepCompanyTypeComponent, ReactiveFormsModule],
      providers: [FormBuilder, provideNoopAnimations(), provideHttpClient(), provideHttpClientTesting()]
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

  describe('remote catalog filtering (plan WP-F2)', () => {
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
            recommendedModuleIds: [],
            defaultWarehouseName: 'Magasin principal',
            domainCodes: ['alimentation-agroalimentaire', 'artisanat']
          },
          {
            code: 'association',
            labelFr: 'Association',
            descriptionFr: 'Organismes à but non lucratif.',
            iconKey: 'heart-handshake',
            sortOrder: 1,
            coreModuleIds: [],
            recommendedModuleIds: [],
            defaultWarehouseName: null,
            domainCodes: []
          }
        ],
        domains: [
          { code: 'alimentation-agroalimentaire', labelFr: 'Alimentation & Agroalimentaire', sortOrder: 0, additionalModuleIds: [] },
          { code: 'artisanat', labelFr: 'Artisanat', sortOrder: 1, additionalModuleIds: [] },
          { code: 'autre', labelFr: 'Autre domaine', sortOrder: 2, additionalModuleIds: [] }
        ],
        modules: [{ id: AppModule.Stock, code: 'stock', labelFr: 'Stock', isCore: false }],
        moduleDependencies: []
      };
    }

    function loadRemoteCatalog(): void {
      const httpMock = TestBed.inject(HttpTestingController);
      component.catalog.load();
      httpMock.expectOne(CATALOG_URL).flush({ success: true, data: fakeCatalog() } as ApiResponse<SectorCatalogDto>);
      fixture.detectChanges();
    }

    it('shows the disabled placeholder when no segment is selected yet', () => {
      loadRemoteCatalog();
      expect(component.domainsDisabled).toBeTrue();
      expect(fixture.nativeElement.querySelector('.dom-grid--placeholder')).not.toBeNull();
      expect(fixture.nativeElement.querySelector('.dom-grid')).toBeNull();
    });

    it('filters the domain list to the selected segment, ordered, with "autre" appended', () => {
      loadRemoteCatalog();
      component.selectSegment('commerce');
      fixture.detectChanges();

      expect(component.domainsDisabled).toBeFalse();
      expect(component.domains.map(d => d.code)).toEqual([
        'alimentation-agroalimentaire', 'artisanat', 'autre'
      ]);
      const items = fixture.nativeElement.querySelectorAll('.dom-item');
      expect(items.length).toBe(3);
    });

    it('shows the "no specific domain" hint when a segment has an empty domainCodes list', () => {
      loadRemoteCatalog();
      component.selectSegment('association');
      fixture.detectChanges();

      expect(component.showNoSpecificDomainHint).toBeTrue();
      expect(component.domains.map(d => d.code)).toEqual(['autre']);
    });

    it('shows the domain-cleared notice when @Input domainClearedNotice is true', () => {
      loadRemoteCatalog();
      component.domainClearedNotice = true;
      fixture.detectChanges();

      expect(fixture.nativeElement.querySelector('.domain-notice')).not.toBeNull();
    });
  });
});
