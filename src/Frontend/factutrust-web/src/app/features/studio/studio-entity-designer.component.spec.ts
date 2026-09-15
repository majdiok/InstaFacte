import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient } from '@angular/common/http';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { By } from '@angular/platform-browser';
import { MessageService } from 'primeng/api';
import { signal } from '@angular/core';
import { environment } from '@environments/environment';
import { ConfirmationService } from '@core/services/confirmation.service';
import { StudioEntityDesignerComponent } from './studio-entity-designer.component';
import { StudioAiCapabilitiesService } from './ai/studio-ai-capabilities.service';
import { STUDIO_AI_CAPABILITIES_FALLBACK, StudioAiCapabilitiesDto } from './ai/studio-ai.models';

const API = `${environment.apiUrl}/studio`;

const e1 = {
  id: 'e1', key: 'interventions', displayName: 'Interventions', displayNamePlural: 'Interventions',
  icon: null, description: null, isActive: true, fieldCount: 1, createdAt: '', updatedAt: '', kind: 'Standard'
};
const relations = [{
  kind: 'many_to_many', sourceEntityId: 'e1', sourceEntityKey: 'interventions', sourceLabel: 'Interventions',
  targetEntityId: 'e2', targetEntityKey: 'techniciens', targetLabel: 'Techniciens',
  fieldId: 'f1', fieldKey: 'intervention_id', isRequired: true, isUnique: false,
  junctionEntityId: 'j1', junctionEntityKey: 'intervention_technicien', junctionTargetFieldKey: 'technicien_id'
}];

function capabilitiesStub(overrides: Partial<StudioAiCapabilitiesDto>, state: 'ready' | 'unavailable' = 'ready') {
  return {
    ensureLoaded: () => {},
    state: signal(state).asReadonly(),
    capabilities: signal({ ...STUDIO_AI_CAPABILITIES_FALLBACK, ...overrides }).asReadonly()
  };
}

describe('StudioEntityDesignerComponent — relations N-N (2.5f)', () => {
  let fixture: ComponentFixture<StudioEntityDesignerComponent>;
  let component: StudioEntityDesignerComponent;
  let httpMock: HttpTestingController;

  function setup(manyToManyEnabled: boolean): void {
    TestBed.configureTestingModule({
      imports: [StudioEntityDesignerComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        provideNoopAnimations(),
        MessageService,
        { provide: ConfirmationService, useValue: { confirm: () => {} } },
        { provide: StudioAiCapabilitiesService, useValue: capabilitiesStub({ manyToManyEnabled }) },
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: convertToParamMap({ id: 'e1' }) } } }
      ]
    });
    fixture = TestBed.createComponent(StudioEntityDesignerComponent);
    component = fixture.componentInstance;
    httpMock = TestBed.inject(HttpTestingController);
    fixture.detectChanges();
    httpMock.expectOne(`${API}/entities/e1`).flush({ success: true, data: e1, message: null, errors: [] });
    httpMock.expectOne(req => req.url === `${API}/entities/e1/fields`).flush({ success: true, data: [], message: null, errors: [] });
    httpMock.expectOne(req => req.url === `${API}/entities` && req.method === 'GET').flush({ success: true, data: [e1], message: null, errors: [] });
    fixture.detectChanges();
  }

  afterEach(() => httpMock.verify());

  it('sans manyToManyEnabled ⇒ ni bouton ni section Relations, aucun GET relations', () => {
    setup(false);
    expect(fixture.debugElement.query(By.css('[data-testid="m2m-open"]'))).toBeNull();
    expect(fixture.debugElement.query(By.css('[data-testid="relations-section"]'))).toBeNull();
    httpMock.expectNone(`${API}/entities/e1/relations`);
  });

  it('avec manyToManyEnabled ⇒ bouton après Pont ERP et GET entities/{id}/relations', () => {
    setup(true);
    httpMock.expectOne(`${API}/entities/e1/relations`).flush({ success: true, data: relations, message: null, errors: [] });
    fixture.detectChanges();
    const buttons = fixture.debugElement.queryAll(By.css('.studio-head-actions button'));
    const idx = buttons.findIndex(b => b.nativeElement.getAttribute('data-testid') === 'm2m-open');
    expect(idx).toBeGreaterThanOrEqual(0);
    expect(buttons[idx - 1].nativeElement.textContent).toContain('Pont ERP');
    const section = fixture.debugElement.query(By.css('[data-testid="relations-section"]'));
    expect(section.nativeElement.textContent).toContain('Plusieurs-à-plusieurs');
    expect(section.nativeElement.textContent).toContain('Techniciens');
    expect(section.nativeElement.textContent).toContain('intervention_technicien');
  });

  it('created ⇒ recharge relations + fields et toast', () => {
    setup(true);
    httpMock.expectOne(`${API}/entities/e1/relations`).flush({ success: true, data: relations, message: null, errors: [] });
    fixture.detectChanges();
    const toast = TestBed.inject(MessageService);
    spyOn(toast, 'add');

    component.onRelationCreated();
    httpMock.expectOne(`${API}/entities/e1/relations`).flush({ success: true, data: [...relations, ...relations], message: null, errors: [] });
    httpMock.expectOne(req => req.url === `${API}/entities/e1/fields`).flush({ success: true, data: [], message: null, errors: [] });
    expect(toast.add).toHaveBeenCalled();
    expect(component.relations().length).toBe(2);
  });
});
