import { Component, input, model, output } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { By } from '@angular/platform-browser';
import { MessageService } from 'primeng/api';
import { environment } from '@environments/environment';
import { StudioRecordWorkflowsTabComponent } from './studio-record-workflows-tab.component';
import { StudioWorkflowInstanceDetailComponent } from './studio-workflow-instance-detail.component';
import { STUDIO_WORKFLOW_LABELS } from './studio-workflow-labels';
import type { RunnableWorkflowDto, WorkflowInstanceDto } from './studio-workflows.models';

const API = `${environment.apiUrl}/studio`;
const labels = STUDIO_WORKFLOW_LABELS.recordTab;

/**
 * Bouchon du drawer d'instance 4.4f (même contrat figé H-8 que le stub de 4.4h1, + `recordId`
 * 4.5d2) : le vrai drawer appelle la route runtime à l'ouverture — neutralisé ici.
 */
@Component({ selector: 'app-studio-workflow-instance-detail', standalone: true, template: '' })
class InstanceDetailStubComponent {
  readonly instanceId = model<string | null>(null);
  readonly entityKey = input<string | null>(null);
  readonly recordId = input<string | null>(null);
  readonly changed = output<WorkflowInstanceDto>();
  readonly closed = output<void>();
}

function instance(id: string, status: WorkflowInstanceDto['status'], overrides: Partial<WorkflowInstanceDto> = {}): WorkflowInstanceDto {
  return {
    id, workflowDefinitionId: 'd1', entityDefinitionId: 'e1', workflowKey: 'relance', workflowName: 'Relance devis',
    definitionVersion: 1, recordId: 'r1', trigger: 'manual', status, currentStepIndex: 1,
    currentStepKey: null, dueAt: null, startedBy: null, startedAt: '2026-09-16T09:00:00Z',
    completedAt: null, depth: 0, originInstanceId: null, error: null, ...overrides
  };
}

const runnable: RunnableWorkflowDto = { id: 'w1', key: 'relance', name: 'Relance', description: null, stepCount: 2 };

describe('StudioRecordWorkflowsTabComponent — onglet « Workflows » de la fiche (4.4h2)', () => {
  let fixture: ComponentFixture<StudioRecordWorkflowsTabComponent>;
  let component: StudioRecordWorkflowsTabComponent;
  let httpMock: HttpTestingController;
  let toastSpy: jasmine.Spy;
  let changedSpy: jasmine.Spy;

  function setup(opts: { canWrite?: boolean; instances?: WorkflowInstanceDto[] } = {}): void {
    TestBed.configureTestingModule({
      imports: [StudioRecordWorkflowsTabComponent],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([]), provideNoopAnimations(), MessageService]
    });
    TestBed.overrideComponent(StudioRecordWorkflowsTabComponent, {
      remove: { imports: [StudioWorkflowInstanceDetailComponent] },
      add: { imports: [InstanceDetailStubComponent] }
    });
    toastSpy = spyOn(TestBed.inject(MessageService), 'add');
    fixture = TestBed.createComponent(StudioRecordWorkflowsTabComponent);
    component = fixture.componentInstance;
    httpMock = TestBed.inject(HttpTestingController);
    fixture.componentRef.setInput('entityKey', 'interventions');
    fixture.componentRef.setInput('recordId', 'r1');
    fixture.componentRef.setInput('instances', opts.instances ?? []);
    fixture.componentRef.setInput('canWrite', opts.canWrite ?? true);
    changedSpy = jasmine.createSpy('changed');
    component.changed.subscribe(changedSpy);
    fixture.detectChanges();
  }

  /** Clic sur un p-button repéré par data-testid (hôte p-button ⇒ bouton interne ; le dialog est dans body). */
  function clickButton(testid: string): void {
    const host = (fixture.nativeElement as HTMLElement).querySelector(`[data-testid="${testid}"]`)
      ?? document.querySelector(`[data-testid="${testid}"]`);
    expect(host).withContext(`bouton ${testid} présent`).not.toBeNull();
    (((host as HTMLElement).querySelector('button') as HTMLElement | null) ?? (host as HTMLElement)).click();
    fixture.detectChanges();
  }

  afterEach(async () => {
    httpMock.verify();
    // p-dialog monté avec appendTo="body" : refermer et laisser l'animation de sortie finir AVANT
    // la destruction du TestBed (NG0205 sinon), puis purge défensive (cf. note 4.4f / 4.4h1).
    component.runOpen.set(false);
    component.openInstanceId.set(null);
    fixture.detectChanges();
    await fixture.whenStable();
    document.querySelectorAll('.p-dialog, .p-dialog-mask, .p-drawer, .p-drawer-mask').forEach(el => el.remove());
  });

  it("ne rend pas de p-toast : la fiche hôte porte l'unique toast (D-44-89)", () => {
    setup();
    expect((fixture.nativeElement as HTMLElement).querySelector('p-toast')).toBeNull();
  });

  it('liste les instances avec leur statut et leur étape courante', () => {
    setup({
      instances: [
        instance('i1', 'waiting_approval', { currentStepKey: 'approbation_direction', workflowName: 'Validation devis', dueAt: '2026-09-20T09:00:00Z' }),
        instance('i2', 'running')
      ]
    });

    const row1 = fixture.nativeElement.querySelector('[data-testid="srw-row-i1"]') as HTMLElement;
    // Étiquette 4.4e2 : l'attribut data-status est porté par le p-tag interne au composant.
    const tag = row1.querySelector('p-tag[data-status="waiting_approval"]');
    expect(tag).withContext('tag de statut 4.4e2 rendu').not.toBeNull();
    expect(tag!.textContent).toContain('À valider');
    expect(row1.textContent).toContain('Validation devis');
    expect(row1.textContent).toContain('approbation_direction');
    expect(row1.textContent).toContain('20/09/2026');                    // échéance renseignée

    const row2 = fixture.nativeElement.querySelector('[data-testid="srw-row-i2"]') as HTMLElement;
    expect(row2.textContent).toContain('Relance devis');
    expect(row2.textContent).toContain('—');                             // currentStepKey / dueAt null ⇒ tiret
    expect(row2.querySelector('p-tag[data-status="running"]')).not.toBeNull();
  });

  it('masque Lancer / Annuler / Relancer sans custom_records:write', () => {
    setup({ canWrite: false, instances: [instance('i1', 'running')] });

    expect(fixture.nativeElement.querySelector('[data-testid="srw-run"]')).toBeNull();
    expect(fixture.nativeElement.querySelector('[data-testid="srw-remind-i1"]')).toBeNull();
    expect(fixture.nativeElement.querySelector('[data-testid="srw-cancel-i1"]')).toBeNull();
  });

  it('affiche Détail pour tout lecteur et ouvre le drawer avec entityKey + recordId (portée fiche, 4.5b)', () => {
    setup({ canWrite: false, instances: [instance('i1', 'running')] });
    const stubDebug = fixture.debugElement.query(By.css('app-studio-workflow-instance-detail'));
    expect(stubDebug).withContext('drawer rendu sans studio:design_entities (4.5d3)').not.toBeNull();
    const stub = stubDebug.componentInstance as InstanceDetailStubComponent;
    expect(stub.instanceId()).toBeNull();   // fermé tant qu'aucun « Détail » n'est cliqué

    clickButton('srw-detail-i1');

    expect(stub.instanceId()).toBe('i1');
    expect(stub.entityKey()).toBe('interventions');
    expect(stub.recordId()).toBe('r1');
  });

  it('charge les workflows exécutables puis lance le choisi et émet changed', () => {
    setup();
    clickButton('srw-run');

    const listReq = httpMock.expectOne(`${API}/records/interventions/workflows`);
    expect(listReq.request.method).toBe('GET');
    listReq.flush({ success: true, data: [runnable], message: null, error: null });
    fixture.detectChanges();
    expect(component.runOpen()).toBeTrue();
    expect(component.runnable()).toEqual([runnable]);

    component.runKey.set('relance');
    fixture.detectChanges();
    clickButton('srw-run-confirm');

    const runReq = httpMock.expectOne(`${API}/records/interventions/r1/workflows/relance/run`);
    expect(runReq.request.method).toBe('POST');
    expect(runReq.request.body).toEqual({});
    runReq.flush({ success: true, data: instance('i9', 'running'), message: null, error: null }, { status: 201, statusText: 'Created' });
    fixture.detectChanges();

    expect(changedSpy).toHaveBeenCalledTimes(1);
    expect(component.runOpen()).toBeFalse();                            // dialog refermé après 201
    expect(toastSpy).toHaveBeenCalledWith(jasmine.objectContaining({ severity: 'success', detail: labels.started }));
  });

  it('annule une instance ouverte et émet changed', () => {
    setup({ instances: [instance('i1', 'waiting_approval')] });

    clickButton('srw-cancel-i1');
    const cancelReq = httpMock.expectOne(`${API}/workflows/instances/i1/cancel`);
    expect(cancelReq.request.method).toBe('POST');
    expect(cancelReq.request.body).toEqual({ reason: null });
    cancelReq.flush({ success: true, data: instance('i1', 'cancelled'), message: null, error: null });
    fixture.detectChanges();
    expect(changedSpy).toHaveBeenCalledTimes(1);
    expect(toastSpy).toHaveBeenCalledWith(jasmine.objectContaining({ severity: 'success', detail: labels.cancelled }));

    // Variante erreur : instance déjà terminée côté serveur (409 ⇒ message serveur, D-44-02).
    clickButton('srw-cancel-i1');
    httpMock.expectOne(`${API}/workflows/instances/i1/cancel`)
      .flush({ success: false, error: 'Instance déjà terminée.' }, { status: 409, statusText: 'Conflict' });
    fixture.detectChanges();
    expect(changedSpy).toHaveBeenCalledTimes(1);                        // pas de second changed
    expect(toastSpy).toHaveBeenCalledWith(jasmine.objectContaining({ severity: 'warn', detail: 'Instance déjà terminée.' }));
  });
});
