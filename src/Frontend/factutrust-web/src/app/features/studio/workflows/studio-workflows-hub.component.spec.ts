import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { By } from '@angular/platform-browser';
import { MessageService } from 'primeng/api';
import { ConfirmationService } from '@core/services/confirmation.service';
import { environment } from '@environments/environment';
import { StudioWorkflowsHubComponent } from './studio-workflows-hub.component';
import { STUDIO_WORKFLOW_LABELS } from './studio-workflow-labels';
import { WorkflowDefinitionDto, WorkflowDefinitionListItemDto } from './studio-workflows.models';
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

/** Ligne de la liste globale (4.5c2) : définition + table porteuse. */
const item = (over: Partial<WorkflowDefinitionDto> = {}, entityKey = 'devis', entityDisplayName = 'Devis'): WorkflowDefinitionListItemDto =>
  ({ workflow: wf(over), entityKey, entityDisplayName });

/** Réponse `PagedResult` page unique de la liste globale (4.5f). */
const paged = (items: WorkflowDefinitionListItemDto[], totalCount = items.length) => ({
  success: true,
  data: { items, page: 1, pageSize: 200, totalCount, totalPages: 1, hasNextPage: false, hasPreviousPage: false },
  message: null, error: null
});

describe('StudioWorkflowsHubComponent', () => {
  let fixture: ComponentFixture<StudioWorkflowsHubComponent>;
  let component: StudioWorkflowsHubComponent;
  let httpMock: HttpTestingController;
  let toastSpy: jasmine.Spy;

  function setup(query: Record<string, string> = {}): void {
    TestBed.configureTestingModule({
      imports: [StudioWorkflowsHubComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        provideNoopAnimations(),
        MessageService,
        { provide: ConfirmationService, useValue: { confirm: jasmine.createSpy('confirm').and.callFake((c: { accept: () => void }) => c.accept()) } },
        { provide: ActivatedRoute, useValue: { snapshot: { queryParamMap: convertToParamMap(query) } } }
      ]
    });
    toastSpy = spyOn(TestBed.inject(MessageService), 'add');
    fixture = TestBed.createComponent(StudioWorkflowsHubComponent);
    component = fixture.componentInstance;
    httpMock = TestBed.inject(HttpTestingController);
    fixture.detectChanges();
    httpMock.expectOne(r => r.url === `${API}/entities`).flush({ success: true, data: entities, message: null, error: null });
  }

  /** Répond à la requête globale `GET workflows` (4.5f, PagedResult page unique) puis relance le rendu. */
  function flushAll(items: WorkflowDefinitionListItemDto[], totalCount = items.length): void {
    httpMock.expectOne(r => r.method === 'GET' && r.url === `${API}/workflows`).flush(paged(items, totalCount));
    fixture.detectChanges();
  }

  afterEach(() => httpMock.verify());

  it('liste tous les workflows du tenant en une requête GET workflows et affiche la table porteuse', () => {
    setup();
    flushAll([item({ id: 'w2', name: 'Relance facture' }, 'factures', 'Factures'), item()]);

    httpMock.expectNone(r => r.url.startsWith(`${API}/entities/`) && r.url.endsWith('/workflows'));   // plus de forkJoin par table
    expect(component.workflows().map(w => `${w.entityName}/${w.name}`)).toEqual(['Devis/Validation devis', 'Factures/Relance facture']);   // tri table puis nom
    expect(component.truncatedCount()).toBeNull();
    expect(fixture.debugElement.query(By.css('[data-testid="wf-hub-truncated"]'))).toBeNull();
    // data-testid figés (consommés par les tests Playwright de 4.4l1).
    expect(fixture.debugElement.query(By.css('[data-testid="wf-hub-new"]'))).not.toBeNull();
    expect(fixture.debugElement.query(By.css('[data-testid="wf-hub-row-w1"]'))).not.toBeNull();
    expect(fixture.debugElement.query(By.css('[data-testid="wf-hub-toggle-w1"]'))).not.toBeNull();
  });

  it('filtre par table via ?entity= avec GET entities/{id}/workflows (chemin conservé)', () => {
    setup({ entity: 'e1' });
    httpMock.expectNone(r => r.url === `${API}/workflows`);
    httpMock.expectOne(`${API}/entities/e1/workflows`).flush({ success: true, data: [wf()], message: null, error: null });
    fixture.detectChanges();

    expect(component.entityId()).toBe('e1');
    expect(component.workflows().length).toBe(1);
    expect(component.workflows()[0].entityName).toBe('Devis');
    expect(component.truncatedCount()).toBeNull();
  });

  it('signale la troncature quand totalCount dépasse la page renvoyée', () => {
    setup();
    flushAll([item()], 250);

    expect(component.truncatedCount()).toBe(1);
    const hint = fixture.debugElement.query(By.css('[data-testid="wf-hub-truncated"]'));
    expect(hint).not.toBeNull();
    expect((hint.nativeElement as HTMLElement).textContent).toContain('Seuls les 1 premiers workflows sont affichés');
    expect(fixture.debugElement.query(By.css('[data-testid="wf-hub-row-w1"]'))).not.toBeNull();   // la page reste affichée
  });

  it("affiche le toast d'erreur et une liste vide quand GET workflows échoue (500 ou 404 drapeau coupé)", () => {
    setup();
    httpMock.expectOne(r => r.method === 'GET' && r.url === `${API}/workflows`).flush(null, { status: 500, statusText: 'Server Error' });
    fixture.detectChanges();

    expect(component.workflows().length).toBe(0);
    expect(component.loading()).toBeFalse();
    expect(toastSpy).toHaveBeenCalledWith(jasmine.objectContaining({ severity: 'error', detail: STUDIO_WORKFLOW_LABELS.hub.loadError }));
  });

  it("active/désactive via POST toggle et restaure l'état sur 409", () => {
    setup();
    flushAll([item()]);

    component.toggle(component.workflows()[0], false);
    httpMock.expectOne(`${API}/workflows/w1/toggle`).flush(
      { success: false, error: 'conflit' },
      { status: 409, statusText: 'Conflict' }
    );

    expect(component.workflows()[0].isActive).toBe(true);
    expect(toastSpy).toHaveBeenCalledWith(jasmine.objectContaining({ severity: 'warn', detail: STUDIO_WORKFLOW_LABELS.designer.conflict }));
  });

  it("supprime après confirmation et affiche le nombre d'instances annulées", () => {
    setup();
    flushAll([item({ openInstances: 2 })]);

    component.remove(component.workflows()[0]);
    httpMock.expectOne(`${API}/workflows/w1`).flush({ success: true, data: { cancelledInstances: 2 }, message: null, error: null });
    // Rechargement après suppression.
    flushAll([]);

    expect(toastSpy).toHaveBeenCalledWith(jasmine.objectContaining({ severity: 'success', detail: '2 instance(s) annulée(s).' }));
    expect(component.workflows().length).toBe(0);
  });
});

// D-44-87 / D-44-92 : 4.4d importait `ConfirmationService` depuis `primeng/api` (jamais fourni) ⇒
// NullInjectorError au runtime sur /studio/workflows, alors que Karma restait vert grâce au stub
// `{ provide: ConfirmationService, useValue: … }` du describe ci-dessus. Ce describe n'en fournit
// volontairement AUCUN : le composant doit se créer avec le wrapper ng-bootstrap fourni à la racine.
describe('StudioWorkflowsHubComponent — injection réelle du ConfirmationService', () => {
  it('se crée avec le ConfirmationService du wrapper fourni à la racine et lui délègue la confirmation de suppression (régression D-44-87)', () => {
    TestBed.configureTestingModule({
      imports: [StudioWorkflowsHubComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        provideNoopAnimations(),
        MessageService,
        { provide: ActivatedRoute, useValue: { snapshot: { queryParamMap: convertToParamMap({}) } } }
      ]
    });
    const confirmSvc = TestBed.inject(ConfirmationService);
    expect(confirmSvc).toBeInstanceOf(ConfirmationService);
    const confirmSpy = spyOn(confirmSvc, 'confirm'); // pas de modale ng-bootstrap réelle dans le test

    const fixture = TestBed.createComponent(StudioWorkflowsHubComponent);
    const httpMock = TestBed.inject(HttpTestingController);
    fixture.detectChanges();
    httpMock.expectOne(r => r.url === `${API}/entities`).flush({ success: true, data: entities, message: null, error: null });
    httpMock.expectOne(r => r.method === 'GET' && r.url === `${API}/workflows`).flush(paged([item()]));   // 4.5f : liste globale
    fixture.detectChanges();

    fixture.componentInstance.remove(fixture.componentInstance.workflows()[0]);

    expect(confirmSpy).toHaveBeenCalledWith(jasmine.objectContaining({
      header: STUDIO_WORKFLOW_LABELS.hub.deleteTitle,
      acceptLabel: STUDIO_WORKFLOW_LABELS.hub.delete
    }));
    httpMock.expectNone(`${API}/workflows/w1`); // rien n'est supprimé tant que la confirmation n'est pas acceptée
    httpMock.verify();
  });
});
