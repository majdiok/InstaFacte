import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { By } from '@angular/platform-browser';
import { environment } from '@environments/environment';
import { StudioWorkflowInstancesPanelComponent } from './studio-workflow-instances-panel.component';
import { WorkflowInstanceDto } from './studio-workflows.models';

const API = `${environment.apiUrl}/studio`;

const inst = (over: Partial<WorkflowInstanceDto> = {}): WorkflowInstanceDto => ({
  id: 'i1', workflowDefinitionId: 'w1', entityDefinitionId: 'e1', workflowKey: 'validation_devis',
  workflowName: 'Validation devis', definitionVersion: 1, recordId: '9f1c2d3e-4b5a-6c7d-8e9f-0a1b2c3d4e5f',
  trigger: 'on_create', status: 'running', currentStepIndex: 0, currentStepKey: null, dueAt: null,
  startedBy: null, startedAt: '2026-09-17T10:00:00', completedAt: null, depth: 0,
  originInstanceId: null, error: null,
  ...over
});

describe('StudioWorkflowInstancesPanelComponent', () => {
  let fixture: ComponentFixture<StudioWorkflowInstancesPanelComponent>;
  let httpMock: HttpTestingController;

  function setup(workflowId: string | null = 'w1'): void {
    TestBed.configureTestingModule({
      imports: [StudioWorkflowInstancesPanelComponent],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([]), provideNoopAnimations()]
    });
    fixture = TestBed.createComponent(StudioWorkflowInstancesPanelComponent);
    fixture.componentRef.setInput('workflowId', workflowId);
    fixture.componentRef.setInput('entityKey', 'devis');
    httpMock = TestBed.inject(HttpTestingController);
    fixture.detectChanges();
  }

  afterEach(() => httpMock.verify());

  it('charge 50 instances au plus (borne API) quand workflowId est fourni et émet open au clic', () => {
    setup('w1');
    const req = httpMock.expectOne(r => r.url === `${API}/workflows/w1/instances` && r.params.get('max') === '50');
    expect(req.request.method).toBe('GET');
    req.flush({
      success: true,
      data: [inst({ id: 'i1', status: 'waiting_approval' }), inst({ id: 'i2', status: 'completed' })],
      message: null, error: null
    });
    fixture.detectChanges();

    // Badge d'instances ouvertes : waiting_approval = ouverte, completed = fermée ⇒ 1.
    const badge = fixture.debugElement.query(By.css('[data-testid="wf-instances-open-count"]'));
    expect(badge).not.toBeNull();
    expect((badge.nativeElement as HTMLElement).textContent?.trim()).toBe('1');

    const openSpy = jasmine.createSpy('open');
    fixture.componentInstance.open.subscribe(openSpy);
    const row = fixture.debugElement.query(By.css('[data-testid="wf-instance-i1"]'));
    expect(row).not.toBeNull();
    (row.nativeElement as HTMLElement).click();
    expect(openSpy).toHaveBeenCalledOnceWith(jasmine.objectContaining({ id: 'i1' }));

    // Identifiant tronqué (D-44-24) + lien « Ouvrir la fiche » vers la route de redirection.
    expect((row.nativeElement as HTMLElement).textContent).toContain('9f1c2d3e…');
    const link = fixture.debugElement.query(By.css('[data-testid="wf-instance-open-i1"]'));
    expect(link).not.toBeNull();
    expect((link.nativeElement as HTMLAnchorElement).getAttribute('href')).toBe('/studio/records/devis/9f1c2d3e-4b5a-6c7d-8e9f-0a1b2c3d4e5f');
  });

  it('recharge quand refreshToken change', () => {
    setup('w1');
    httpMock.expectOne(r => r.url === `${API}/workflows/w1/instances`)
      .flush({ success: true, data: [inst()], message: null, error: null });

    fixture.componentRef.setInput('refreshToken', 1);
    fixture.detectChanges();
    httpMock.expectOne(r => r.url === `${API}/workflows/w1/instances` && r.params.get('max') === '50')
      .flush({ success: true, data: [], message: null, error: null });
    fixture.detectChanges();
    expect(fixture.debugElement.query(By.css('[data-testid="wf-instances-empty"]'))).not.toBeNull();
  });

  it('signale le plafond quand la route renvoie 50 instances (D-46-01)', () => {
    setup('w1');
    httpMock.expectOne(r => r.url === `${API}/workflows/w1/instances`)
      .flush({ success: true, data: Array.from({ length: 50 }, (_, k) => inst({ id: `i${k}` })), message: null, error: null });
    fixture.detectChanges();

    const hint = fixture.debugElement.query(By.css('[data-testid="wf-instances-capped"]'));
    expect(hint).not.toBeNull();
    expect((hint.nativeElement as HTMLElement).textContent).toContain('Les 50 instances les plus récentes sont affichées');
  });

  it('ne signale pas le plafond sous la borne', () => {
    setup('w1');
    httpMock.expectOne(r => r.url === `${API}/workflows/w1/instances`)
      .flush({ success: true, data: [inst(), inst({ id: 'i2' })], message: null, error: null });
    fixture.detectChanges();

    expect(fixture.debugElement.query(By.css('[data-testid="wf-instances-capped"]'))).toBeNull();
  });

  it('affiche l\'état vide sans requête quand workflowId est nul', () => {
    setup(null);
    httpMock.expectNone(r => r.url.includes('/instances'));
    expect(fixture.debugElement.query(By.css('[data-testid="wf-instances-unsaved"]'))).not.toBeNull();
  });
});
