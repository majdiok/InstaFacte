import { BreakpointObserver } from '@angular/cdk/layout';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ActivatedRoute, Router, convertToParamMap, provideRouter } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { By } from '@angular/platform-browser';
import { of } from 'rxjs';
import { MessageService } from 'primeng/api';
import { environment } from '@environments/environment';
import { AuthService } from '@core/services/auth.service';
import { ConfirmationService } from '@core/services/confirmation.service';
import { CustomField } from '@shared/studio-runtime/studio-runtime.models';
import { StudioWorkflowDesignerComponent } from './studio-workflow-designer.component';
import { StepCatalogEntryDto, WorkflowDefinitionDto } from './studio-workflows.models';
import { CustomEntity } from '../studio.models';

const API = `${environment.apiUrl}/studio`;

const entities = [
  { id: 'e1', key: 'clients', displayName: 'Clients', kind: 'Standard' }
] as CustomEntity[];

const fields = [
  { id: 'f1', key: 'nom', label: 'Nom', fieldType: 0, isRequired: false, isActive: true },
  { id: 'f2', key: 'montant', label: 'Montant', fieldType: 2, isRequired: false, isActive: true }
] as CustomField[];

/** Catalogue minimal : `condition` avec sa propriété `filters` (affiche le filter-builder), `notify` nue. */
const catalog: StepCatalogEntryDto[] = [
  { type: 'condition', label: 'Condition', description: '', properties: [{ name: 'filters', kind: 'filters', required: false, help: '' }] },
  { type: 'notify', label: 'Notifier', description: '', properties: [] }
];

const workflow: WorkflowDefinitionDto = {
  id: 'w1', entityDefinitionId: 'e1', key: 'validation_devis', name: 'Validation devis', description: null,
  trigger: 'on_create', triggerConfig: null,
  steps: { version: 1, steps: [{ key: 'condition_1', type: 'condition', label: 'Montant élevé', filters: [], match: 'all', onFalse: 'stop' }] },
  stepCount: 1, version: 1, isActive: false, openInstances: 0,
  createdAt: '2026-09-01', updatedAt: '2026-09-10', rowVersion: 'rv1'
};

describe('StudioWorkflowDesignerComponent', () => {
  let fixture: ComponentFixture<StudioWorkflowDesignerComponent>;
  let component: StudioWorkflowDesignerComponent;
  let httpMock: HttpTestingController;
  let confirmSpy: jasmine.Spy;

  /** Motif `studio-record-view-designer.component.spec.ts` l.45–75 ; BreakpointObserver stubbé (écran large). */
  function setup(): void {
    confirmSpy = jasmine.createSpy('confirm');
    TestBed.configureTestingModule({
      imports: [StudioWorkflowDesignerComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        provideNoopAnimations(),
        MessageService,
        { provide: ConfirmationService, useValue: { confirm: confirmSpy } },
        { provide: AuthService, useValue: { hasPermission: () => true } },
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: { paramMap: convertToParamMap({ id: 'w1' }), queryParamMap: convertToParamMap({}) },
            // 4.4e2 : le concepteur lit `?instance=` en continu (toSignal) pour le drawer de 4.4f.
            queryParamMap: of(convertToParamMap({}))
          }
        },
        { provide: BreakpointObserver, useValue: { observe: () => of({ matches: false, breakpoints: {} }) } }
      ]
    });
    const router = TestBed.inject(Router);
    spyOn(router, 'navigate').and.resolveTo(true);
    fixture = TestBed.createComponent(StudioWorkflowDesignerComponent);
    component = fixture.componentInstance;
    httpMock = TestBed.inject(HttpTestingController);
    fixture.detectChanges();
    httpMock.expectOne(`${API}/workflows/step-catalog`).flush({ success: true, data: { entries: catalog }, message: null, error: null });
    httpMock.expectOne(`${API}/entities?includeInactive=false`).flush({ success: true, data: entities, message: null, error: null });
    httpMock.expectOne(`${API}/automations/actions`).flush({ success: true, data: [], message: null, error: null });
    httpMock.expectOne(`${API}/workflows/w1`).flush({ success: true, data: workflow, message: null, error: null });
    httpMock.expectOne(`${API}/records/clients/schema`).flush({
      success: true,
      data: { entity: entities[0], fields, form: { sections: [] }, relations: [], views: [] },
      message: null, error: null
    });
    fixture.detectChanges();
    // 4.4e2 / 4.6a2 : le panneau d'instances de la colonne 3 charge les 50 dernières instances (borne API).
    httpMock.expectOne(r => r.url === `${API}/workflows/w1/instances` && r.params.get('max') === '50')
      .flush({ success: true, data: [], message: null, error: null });
    fixture.detectChanges();
  }

  beforeEach(() => setup());

  afterEach(() => httpMock.verify());

  it('charge catalogue, tables, actions et schéma puis affiche la première étape', () => {
    expect(component.loading()).toBeFalse();
    expect(component.entity()?.key).toBe('clients');
    expect(component.selectedIndex()).toBe(0);
    // Ligne de la liste d'étapes (testid figé de 4.4c2) + éditeur monté.
    expect(fixture.debugElement.query(By.css('[data-testid="wf-step-condition_1"]'))).not.toBeNull();
    expect(fixture.debugElement.query(By.css('[data-testid="step-editor"]'))).not.toBeNull();
    // Actions d'en-tête figées (Playwright 4.4l1).
    expect(fixture.debugElement.query(By.css('[data-testid="wf-validate"]'))).not.toBeNull();
    expect(fixture.debugElement.query(By.css('[data-testid="wf-save"]'))).not.toBeNull();
    expect(fixture.debugElement.query(By.css('[data-testid="wf-toggle"]'))).not.toBeNull();
  });

  it('enchaîne validate puis update avec rowVersion et remonte les erreurs par étape sans enregistrer si invalide', () => {
    expect(component.canSave()).toBeTrue();

    // 1er essai : le serveur déclare le workflow invalide ⇒ aucune écriture.
    component.save();
    httpMock.expectOne(r => r.method === 'POST' && r.url === `${API}/entities/e1/workflows/validate`)
      .flush({ success: true, data: { isValid: false, errors: [{ path: 'steps[0].filters', message: 'x' }], warnings: [], stepCount: 1 }, message: null, error: null });
    httpMock.expectNone(`${API}/workflows/w1`);
    expect(component.saving()).toBeFalse();
    expect(component.issues()).toEqual([{ path: 'steps[0].filters', message: 'x' }]);
    fixture.detectChanges();
    expect(fixture.debugElement.query(By.css('[data-testid="wf-validation"]'))).not.toBeNull();

    // 2d essai : valide ⇒ PUT avec le rowVersion courant.
    component.save();
    httpMock.expectOne(r => r.method === 'POST' && r.url === `${API}/entities/e1/workflows/validate`)
      .flush({ success: true, data: { isValid: true, errors: [], warnings: [], stepCount: 1 }, message: null, error: null });
    const put = httpMock.expectOne(r => r.method === 'PUT' && r.url === `${API}/workflows/w1`);
    expect(put.request.body.rowVersion).toBe('rv1');
    expect(put.request.body.key).toBe('validation_devis');
    put.flush({ success: true, data: { ...workflow, version: 2, rowVersion: 'rv2' }, message: null, error: null });
    expect(component.saving()).toBeFalse();
    expect(component.version()).toBe(2);
    expect(component.dirty()).toBeFalse();
  });
});
