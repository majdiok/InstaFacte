import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { MessageService } from 'primeng/api';
import { environment } from '@environments/environment';
import { PERMISSIONS } from '@core/config/permission-keys';
import { AuthService } from '@core/services/auth.service';
import { StudioApprovalsPageComponent } from './studio-approvals-page.component';
import { StudioApprovalsBadgeService } from './studio-approvals-badge.service';
import { STUDIO_WORKFLOW_LABELS } from '../workflows/studio-workflow-labels';
import type { WorkflowApprovalInboxItemDto } from '../workflows/studio-workflows.models';

const API = `${environment.apiUrl}/studio`;
const labels = STUDIO_WORKFLOW_LABELS.approvals;

/** Item de boîte de réception au format H-1 (forme imbriquée, annexe 4.4g2). */
function inboxItem(id = 'a1', dueAt: string | null = null): WorkflowApprovalInboxItemDto {
  return {
    approval: {
      id, instanceId: 'i1', stepKey: 'approval_1', title: 'Valider le devis', status: 'pending',
      dueAt, createdAt: '2026-09-17T08:00:00Z', comment: null, message: 'Merci de relire',
      assigneeUserId: null, assigneeRole: 'Administrator', decidedBy: null, decidedAt: null, rowVersion: 'v1'
    },
    instanceId: 'i1', workflowKey: 'validation_devis', workflowName: 'Validation devis',
    entityKey: 'devis', entityName: 'Devis', recordId: 'r1', recordLabel: 'DEV-001',
    startedBy: null, startedAt: '2026-09-16T09:00:00Z'
  };
}

describe('StudioApprovalsPageComponent', () => {
  let fixture: ComponentFixture<StudioApprovalsPageComponent>;
  let component: StudioApprovalsPageComponent;
  let httpMock: HttpTestingController;
  let toastSpy: jasmine.Spy;
  let badgeRefresh: jasmine.Spy;
  let perms: Set<string>;

  function setup(): void {
    TestBed.configureTestingModule({
      imports: [StudioApprovalsPageComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        provideNoopAnimations(),
        MessageService,
        { provide: AuthService, useValue: { hasPermission: (p: string) => perms.has(p) } },
        { provide: StudioApprovalsBadgeService, useValue: { refresh: (badgeRefresh = jasmine.createSpy('refresh')) } }
      ]
    });
    toastSpy = spyOn(TestBed.inject(MessageService), 'add');
    fixture = TestBed.createComponent(StudioApprovalsPageComponent);
    component = fixture.componentInstance;
    httpMock = TestBed.inject(HttpTestingController);
    fixture.detectChanges();   // ngOnInit ⇒ GET approvals/mine
  }

  afterEach(async () => {
    httpMock.verify();
    // Le dialog est monté avec appendTo="body" : on le referme et on laisse son animation de
    // sortie se terminer AVANT la destruction du TestBed (sinon NG0205 « injecteur détruit »
    // en console), puis purge défensive des résidus éventuels (cf. note 4.4f).
    component.decision.set(null);
    fixture.detectChanges();
    await fixture.whenStable();
    document.querySelectorAll('.p-dialog, .p-dialog-mask').forEach(el => el.remove());
  });

  /** Répond au GET de la boîte de réception (prédicat : URL sans les query params). */
  function flushInbox(items: WorkflowApprovalInboxItemDto[]): void {
    httpMock.expectOne(r => r.method === 'GET' && r.url === `${API}/workflows/approvals/mine`)
      .flush({ success: true, data: items, message: null, error: null });
    fixture.detectChanges();
  }

  /** Clic sur un bouton de ligne de la table (hôte p-button ⇒ bouton interne). */
  function clickRowButton(testid: string): void {
    const host = fixture.nativeElement.querySelector(`[data-testid="${testid}"]`) as HTMLElement | null;
    expect(host).withContext(`bouton ${testid} présent`).not.toBeNull();
    ((host!.querySelector('button') as HTMLElement | null) ?? host!).click();
    fixture.detectChanges();
  }

  /** Bouton de confirmation du dialog (monté dans document.body via appendTo). */
  function confirmButton(): HTMLButtonElement | null {
    return document.querySelector<HTMLButtonElement>('[data-testid="sap-confirm"] button');
  }

  it('charge la boîte et calcule les KPI À traiter / En retard / Sous 24 h', () => {
    perms = new Set([PERMISSIONS.customData.recordsRead, PERMISSIONS.customData.recordsWrite]);
    setup();
    flushInbox([
      inboxItem('a1', new Date(Date.now() - 3_600_000).toISOString()),        // en retard
      inboxItem('a2', new Date(Date.now() + 2 * 3_600_000).toISOString()),    // sous 24 h
      inboxItem('a3', new Date(Date.now() + 3 * 86_400_000).toISOString())    // plus tard
    ]);

    expect(component.kpis()).toEqual({ pending: 3, late: 1, soon: 1 });
    const text = (testid: string) => ((fixture.nativeElement.querySelector(`[data-testid="${testid}"] .sap-kpi__value`) as HTMLElement).textContent ?? '').trim();
    expect(text('sap-kpi-pending')).toBe('3');
    expect(text('sap-kpi-late')).toBe('1');
    expect(text('sap-kpi-soon')).toBe('1');
    expect(fixture.nativeElement.querySelector('[data-testid="sap-row-a1"]')).not.toBeNull();
  });

  it("affiche l'état vide quand aucune approbation n'est en attente", () => {
    perms = new Set([PERMISSIONS.customData.recordsRead, PERMISSIONS.customData.recordsWrite]);
    setup();
    flushInbox([]);

    const empty = fixture.nativeElement.querySelector('.ft-empty') as HTMLElement;
    expect(empty).not.toBeNull();
    expect(empty.textContent).toContain(labels.empty);
    expect(empty.textContent).toContain(labels.emptyHint);
  });

  it('masque Approuver / Refuser et affiche la note lecture seule sans custom_records:write', () => {
    perms = new Set([PERMISSIONS.customData.recordsRead]);
    setup();
    flushInbox([inboxItem('a1')]);

    expect(component.canDecide()).toBe(false);
    expect(fixture.nativeElement.querySelector('.sap-readonly')).not.toBeNull();
    expect(fixture.nativeElement.querySelector('[data-testid="sap-approve-a1"]')).toBeNull();
    expect(fixture.nativeElement.querySelector('[data-testid="sap-reject-a1"]')).toBeNull();
    // D-44-53 : pas de lien vers la fiche (la route edit exige recordsWrite)
    expect(fixture.nativeElement.querySelector('[data-testid="sap-row-a1"] a')).toBeNull();
  });

  it('exige un motif pour refuser', () => {
    perms = new Set([PERMISSIONS.customData.recordsRead, PERMISSIONS.customData.recordsWrite]);
    setup();
    flushInbox([inboxItem('a1')]);

    clickRowButton('sap-reject-a1');
    expect(component.decision()?.kind).toBe('reject');
    expect(confirmButton()).not.toBeNull();
    expect(confirmButton()!.disabled).toBeTrue();          // commentaire vide ⇒ confirmation bloquée

    component.comment.set('Trop cher');
    fixture.detectChanges();
    expect(confirmButton()!.disabled).toBeFalse();
  });

  it('approuve, retire la ligne et rafraîchit le badge', () => {
    perms = new Set([PERMISSIONS.customData.recordsRead, PERMISSIONS.customData.recordsWrite]);
    setup();
    flushInbox([inboxItem('a1'), inboxItem('a2')]);

    clickRowButton('sap-approve-a1');
    expect(component.decision()?.kind).toBe('approve');
    confirmButton()!.click();

    const req = httpMock.expectOne(`${API}/workflows/approvals/a1/approve`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ comment: null });
    req.flush({ success: true, data: null, message: null, error: null });
    fixture.detectChanges();

    expect(component.items().map(i => i.id)).toEqual(['a2']);
    expect(badgeRefresh).toHaveBeenCalled();
    expect(toastSpy).toHaveBeenCalledWith(jasmine.objectContaining({ severity: 'success', detail: labels.approved }));
    expect(component.decision()).toBeNull();
  });

  it('signale une demande déjà traitée (409) et recharge la liste', () => {
    perms = new Set([PERMISSIONS.customData.recordsRead, PERMISSIONS.customData.recordsWrite]);
    setup();
    flushInbox([inboxItem('a1')]);

    clickRowButton('sap-reject-a1');
    component.comment.set('Trop cher');
    fixture.detectChanges();
    confirmButton()!.click();

    const req = httpMock.expectOne(`${API}/workflows/approvals/a1/reject`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ comment: 'Trop cher' });
    req.flush({ success: false, data: null, message: null, error: 'déjà traitée' }, { status: 409, statusText: 'Conflict' });
    fixture.detectChanges();

    expect(toastSpy).toHaveBeenCalledWith(jasmine.objectContaining({ severity: 'warn', detail: labels.alreadyDecided }));
    // D-44-55 : rechargement complet (vérité serveur) plutôt que retrait local
    httpMock.expectOne(r => r.method === 'GET' && r.url === `${API}/workflows/approvals/mine`)
      .flush({ success: true, data: [], message: null, error: null });
    fixture.detectChanges();
    expect(component.items()).toEqual([]);
  });

  it("affiche l'erreur et Réessayer quand le chargement échoue", () => {
    perms = new Set([PERMISSIONS.customData.recordsRead, PERMISSIONS.customData.recordsWrite]);
    setup();
    httpMock.expectOne(r => r.method === 'GET' && r.url === `${API}/workflows/approvals/mine`)
      .flush('panne', { status: 500, statusText: 'Server Error' });
    fixture.detectChanges();

    const banner = fixture.nativeElement.querySelector('.sai-banner--error') as HTMLElement;
    expect(banner).not.toBeNull();
    expect(banner.textContent).toContain(labels.loadError);

    const retry = fixture.nativeElement.querySelector('[data-testid="sap-retry"] button') as HTMLButtonElement;
    expect(retry).not.toBeNull();
    retry.click();
    flushInbox([inboxItem('a1')]);

    expect(fixture.nativeElement.querySelector('.sai-banner--error')).toBeNull();
    expect(fixture.nativeElement.querySelector('[data-testid="sap-row-a1"]')).not.toBeNull();
  });
});
