import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { By } from '@angular/platform-browser';
import { ConfirmationService, MessageService } from 'primeng/api';
import { environment } from '@environments/environment';
import { StudioWorkflowsHubComponent } from './studio-workflows-hub.component';
import { STUDIO_WORKFLOW_LABELS } from './studio-workflow-labels';
import { WorkflowDefinitionDto } from './studio-workflows.models';
import { CustomEntity } from '../studio.models';

const API = `${environment.apiUrl}/studio`;

const entities = [
  { id: 'e1', key: 'devis', displayName: 'Devis', kind: 'Standard' },
  { id: 'e2', key: 'factures', displayName: 'Factures', kind: 'Standard' },
  { id: 'e3', key: 'devis_factures', displayName: 'Liaison', kind: 'Junction' }
] as CustomEntity[];

const wf = (over: Partial<WorkflowDefinitionDto> = {}): WorkflowDefinitionDto => ({
  id: 'w1', entityDefinitionId: 'e1', key: 'validation_devis', name: 'Validation devis', description: null,
  trigger: 'on_create', triggerConfig: null, steps: { version: 1, steps: [] }, stepCount: 5, version: 1,
  isActive: true, openInstances: 0, createdAt: '2026-09-01', updatedAt: '2026-09-10', rowVersion: 'r1',
  ...over
});

describe('StudioWorkflowsHubComponent', () => {
  let fixture: ComponentFixture<StudioWorkflowsHubComponent>;
  let component: StudioWorkflowsHubComponent;
  let httpMock: HttpTestingController;
  let toastSpy: jasmine.Spy;

  function setup(): void {
    TestBed.configureTestingModule({
      imports: [StudioWorkflowsHubComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        provideNoopAnimations(),
        MessageService,
        { provide: ConfirmationService, useValue: { confirm: jasmine.createSpy('confirm').and.callFake((c: { accept: () => void }) => c.accept()) } },
        { provide: ActivatedRoute, useValue: { snapshot: { queryParamMap: convertToParamMap({}) } } }
      ]
    });
    toastSpy = spyOn(TestBed.inject(MessageService), 'add');
    fixture = TestBed.createComponent(StudioWorkflowsHubComponent);
    component = fixture.componentInstance;
    httpMock = TestBed.inject(HttpTestingController);
    fixture.detectChanges();
    httpMock.expectOne(r => r.url === `${API}/entities`).flush({ success: true, data: entities, message: null, error: null });
  }

  /** Répond aux chargements de workflows en cours (500 sur e2 possible) puis relance le rendu. */
  function flushWorkflows(payloads: Record<string, WorkflowDefinitionDto[] | 'erreur'>): void {
    for (const [entityId, payload] of Object.entries(payloads)) {
      const req = httpMock.expectOne(`${API}/entities/${entityId}/workflows`);
      if (payload === 'erreur') req.flush('erreur', { status: 500, statusText: 'Server Error' });
      else req.flush({ success: true, data: payload, message: null, error: null });
    }
    fixture.detectChanges();
  }

  beforeEach(() => setup());

  afterEach(() => httpMock.verify());

  it('agrège les workflows de toutes les tables actives non jonction et ignore une table en erreur', () => {
    flushWorkflows({ e1: [wf()], e2: 'erreur' });

    httpMock.expectNone(`${API}/entities/e3/workflows`);
    expect(component.workflows().length).toBe(1);
    expect(component.workflows()[0].entityName).toBe('Devis');
    // data-testid figés (consommés par les tests Playwright de 4.4l1).
    expect(fixture.debugElement.query(By.css('[data-testid="wf-hub-new"]'))).not.toBeNull();
    expect(fixture.debugElement.query(By.css('[data-testid="wf-hub-row-w1"]'))).not.toBeNull();
    expect(fixture.debugElement.query(By.css('[data-testid="wf-hub-toggle-w1"]'))).not.toBeNull();
  });

  it("active/désactive via POST toggle et restaure l'état sur 409", () => {
    flushWorkflows({ e1: [wf()], e2: [] });

    component.toggle(component.workflows()[0], false);
    httpMock.expectOne(`${API}/workflows/w1/toggle`).flush(
      { success: false, error: 'conflit' },
      { status: 409, statusText: 'Conflict' }
    );

    expect(component.workflows()[0].isActive).toBe(true);
    expect(toastSpy).toHaveBeenCalledWith(jasmine.objectContaining({ severity: 'warn', detail: STUDIO_WORKFLOW_LABELS.designer.conflict }));
  });

  it("supprime après confirmation et affiche le nombre d'instances annulées", () => {
    flushWorkflows({ e1: [wf({ openInstances: 2 })], e2: [] });

    component.remove(component.workflows()[0]);
    httpMock.expectOne(`${API}/workflows/w1`).flush({ success: true, data: { cancelledInstances: 2 }, message: null, error: null });
    // Rechargement après suppression.
    flushWorkflows({ e1: [], e2: [] });

    expect(toastSpy).toHaveBeenCalledWith(jasmine.objectContaining({ severity: 'success', detail: '2 instance(s) annulée(s).' }));
    expect(component.workflows().length).toBe(0);
  });
});
