import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { Subject, of } from 'rxjs';
import { AiStreamService } from '@features/ai-assistant/services/ai-stream.service';
import { AiChatService } from '@features/ai-assistant/services/ai-chat.service';
import { ChatStreamEvent } from '@features/ai-assistant/models/ai-chat.models';
import { Confirmation, ConfirmationService } from '@core/services/confirmation.service';
import { ToastService } from '@core/services/toast.service';
import { StudioAiBuildService } from '../studio-ai-build.service';
import { StudioNavService } from '../studio-nav.service';
import { StudioAiPageComponent } from './studio-ai-page.component';
import { StudioAiCapabilitiesService } from './studio-ai-capabilities.service';
import { STUDIO_AI_LABELS } from './studio-ai-labels';
import { STUDIO_AI_CAPABILITIES_FALLBACK, StudioAiCapabilitiesDto } from './studio-ai.models';
import { studioAiSpecFixture } from './preview/testing/studio-ai-spec.fixture';

const ALL_ENABLED: StudioAiCapabilitiesDto = {
  ...STUDIO_AI_CAPABILITIES_FALLBACK,
  planPreviewEnabled: true,
  systemGenerationEnabled: true,
  modifyToolsEnabled: true,
  viewToolsEnabled: true,
  reportToolsEnabled: true,
  workbenchEnabled: true,
  templatesEnabled: true,
  advancedModelAvailable: true,
  standardModelLabel: 'GPT-4.1 mini',
  advancedModelLabel: 'GPT-4.1'
};

describe('StudioAiPageComponent', () => {
  let fixture: ComponentFixture<StudioAiPageComponent>;
  let stream: jasmine.SpyObj<AiStreamService>;
  let builds: jasmine.SpyObj<StudioAiBuildService>;
  let confirmation: jasmine.SpyObj<ConfirmationService>;
  let toast: jasmine.SpyObj<ToastService>;
  let queryParams: Record<string, string>;
  let capabilitiesResponse: StudioAiCapabilitiesDto | null;

  beforeEach(() => {
    queryParams = {};
    capabilitiesResponse = ALL_ENABLED;
  });

  async function setup(): Promise<void> {
    stream = jasmine.createSpyObj<AiStreamService>('AiStreamService', ['streamChat']);
    stream.streamChat.and.returnValue(of());
    builds = jasmine.createSpyObj<StudioAiBuildService>('StudioAiBuildService', [
      'getPlanSpec', 'confirm', 'cancel', 'cancelPending', 'getCapabilities', 'listPlans', 'listTemplates', 'getPlan', 'createFromTemplate'
    ]);
    builds.getPlanSpec.and.returnValue(of());
    builds.cancel.and.returnValue(of({ success: true, data: null, message: null, errors: [] }) as never);
    builds.cancelPending.and.returnValue(of({ success: true, data: 1, message: null, errors: [] }) as never);
    builds.getCapabilities.and.returnValue(
      capabilitiesResponse ? (of({ success: true, data: capabilitiesResponse, message: null, errors: [] }) as never) : of()
    );
    builds.listPlans.and.returnValue(of({
      success: true, message: null, errors: [],
      data: { items: [], page: 1, pageSize: 5, totalCount: 0, totalPages: 0, hasNextPage: false, hasPreviousPage: false }
    }) as never);
    builds.listTemplates.and.returnValue(of({ success: true, data: [], message: null, errors: [] }) as never);
    builds.getPlan.and.returnValue(of());
    builds.createFromTemplate.and.returnValue(of());
    const nav = jasmine.createSpyObj<StudioNavService>('StudioNavService', ['refresh']);
    const chat = jasmine.createSpyObj<AiChatService>('AiChatService', ['extractDocument', 'deleteConversation']);
    chat.deleteConversation.and.returnValue(of(undefined));
    confirmation = jasmine.createSpyObj<ConfirmationService>('ConfirmationService', ['confirm']);
    toast = jasmine.createSpyObj<ToastService>('ToastService', ['add']);

    await TestBed.configureTestingModule({
      imports: [StudioAiPageComponent],
      providers: [
        provideRouter([]),
        provideNoopAnimations(),
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: AiStreamService, useValue: stream },
        { provide: StudioAiBuildService, useValue: builds },
        { provide: StudioNavService, useValue: nav },
        { provide: AiChatService, useValue: chat },
        { provide: ConfirmationService, useValue: confirmation },
        { provide: ToastService, useValue: toast },
        { provide: ActivatedRoute, useValue: { queryParamMap: of(convertToParamMap(queryParams)), snapshot: { queryParamMap: convertToParamMap(queryParams) } } }
      ]
    }).compileComponents();

    // La page n'est rendue par l'entrée `/studio/ai` qu'une fois les capacités chargées.
    TestBed.inject(StudioAiCapabilitiesService).ensureLoaded();
    fixture = TestBed.createComponent(StudioAiPageComponent);
    fixture.detectChanges();
  }

  function text(): string {
    return (fixture.nativeElement as HTMLElement).textContent ?? '';
  }

  function withPendingPlan(): void {
    const store = fixture.componentInstance.store;
    store.plan.set({
      planId: 'p1', kind: 'CreateSystem', expiresAt: null, rowVersion: 'v1',
      summary: { kind: 'CreateSystem', title: 'Congés', steps: [], entities: [], warnings: [] }
    });
    const spec = studioAiSpecFixture();
    store.spec.set(spec);
    store.draft.set(structuredClone(spec));
    store.phase.set('awaiting_confirmation');
    fixture.detectChanges();
  }

  function lastConfirmation(): Confirmation {
    return confirmation.confirm.calls.mostRecent().args[0];
  }

  describe('coquille', () => {
    beforeEach(async () => { await setup(); });

    it('affiche l’en-tête, le composer, les cartes et le rail complet au repos, sans conversation', () => {
      expect(fixture.componentInstance.mainView()).toBe('compose');
      expect(fixture.nativeElement.querySelector('app-studio-ai-header')).not.toBeNull();
      expect(fixture.nativeElement.querySelector('app-studio-ai-composer')).not.toBeNull();
      expect(fixture.nativeElement.querySelector('app-studio-ai-intent-cards')).not.toBeNull();
      expect(fixture.nativeElement.querySelector('app-studio-ai-conversation')).toBeNull();
      expect(fixture.nativeElement.querySelector('app-studio-ai-rail')).not.toBeNull();
      expect(fixture.nativeElement.querySelector('app-studio-ai-templates-card')).not.toBeNull();
      expect(fixture.nativeElement.querySelector('app-studio-ai-history-card')).not.toBeNull();
      expect(text()).toContain(STUDIO_AI_LABELS.page.heroTitle);
      expect(text()).toContain(STUDIO_AI_LABELS.page.title);
    });

    it('charge l’historique et les modèles du rail une fois les capacités prêtes', () => {
      expect(builds.listPlans).toHaveBeenCalledWith({ page: 1, pageSize: 5 });
      expect(builds.listTemplates).toHaveBeenCalledTimes(1);
    });

    it('préremplit le composer et présélectionne l’onglet quand une carte est choisie', () => {
      const card = STUDIO_AI_LABELS.intents.find(c => c.intent === 'report')!;
      fixture.componentInstance.pickIntent(card);
      expect(fixture.componentInstance.prefill()).toBe(card.prompt);
      expect(fixture.componentInstance.pendingIntent()).toBe('report');
      expect(fixture.componentInstance.previewTab()).toBe('reports');
    });

    it('ignore les cartes « Bientôt »', () => {
      const card = STUDIO_AI_LABELS.intents.find(c => !c.available)!;
      fixture.componentInstance.pickIntent(card);
      expect(fixture.componentInstance.prefill()).toBeNull();
    });

    it('envoie la demande au store avec l’intention courante, le modèle choisi, et affiche la conversation', () => {
      const card = STUDIO_AI_LABELS.intents.find(c => c.intent === 'table')!;
      fixture.componentInstance.pickIntent(card);
      fixture.componentInstance.setAdvancedModel(true);
      fixture.componentInstance.submit({ text: 'Créer une table contrats', attachments: [] });
      fixture.detectChanges();

      expect(confirmation.confirm).not.toHaveBeenCalled();
      expect(stream.streamChat).toHaveBeenCalledTimes(1);
      const request = stream.streamChat.calls.mostRecent().args[0];
      expect(request.message).toBe('Créer une table contrats');
      expect(request.options).toEqual(jasmine.objectContaining({ useAdvancedModel: true, studioIntent: 'table' }));
      expect(fixture.componentInstance.store.intent()).toBe('table');
      expect(fixture.componentInstance.showConversation()).toBeTrue();
      expect(fixture.nativeElement.querySelector('app-studio-ai-conversation')).not.toBeNull();
      localStorage.removeItem('studio.ai.advancedModel');
    });

    it('bascule sur l’aperçu quand un plan est présent (composer toujours actif), puis progression et résultat', () => {
      const store = fixture.componentInstance.store;
      withPendingPlan();
      expect(fixture.componentInstance.mainView()).toBe('preview');
      expect(fixture.nativeElement.querySelector('app-studio-ai-preview')).not.toBeNull();
      expect(fixture.nativeElement.querySelector('app-studio-ai-composer')).not.toBeNull();
      expect(fixture.componentInstance.composerDisabled()).toBeFalse();

      store.phase.set('executing');
      fixture.detectChanges();
      expect(fixture.componentInstance.mainView()).toBe('progress');
      expect(fixture.componentInstance.composerDisabled()).toBeTrue();

      store.result.set({
        success: true, systemKey: 'conges', systemUrl: '/studio/systems/conges', displayName: 'Congés',
        entityCount: 2, entities: [], warnings: [], message: ''
      });
      store.phase.set('completed');
      fixture.detectChanges();
      expect(fixture.componentInstance.mainView()).toBe('result');
      expect(fixture.nativeElement.querySelector('app-studio-ai-result-card')).not.toBeNull();
    });

    it('n’ouvre le dialogue de confirmation que si le plan est confirmable', () => {
      fixture.componentInstance.openConfirm();
      expect(fixture.componentInstance.confirmVisible()).toBeFalse();

      withPendingPlan();
      fixture.componentInstance.openConfirm();
      expect(fixture.componentInstance.confirmVisible()).toBeTrue();
    });
  });

  describe('A19 — message pendant un plan en attente', () => {
    beforeEach(async () => { await setup(); });

    it('demande confirmation ; accepter annule le plan puis envoie', () => {
      withPendingPlan();

      fixture.componentInstance.submit({ text: 'Plutôt des notes de frais', attachments: [] });

      expect(stream.streamChat).not.toHaveBeenCalled();
      const conf = lastConfirmation();
      expect(conf.header).toBe(STUDIO_AI_LABELS.page.pendingPlanTitle);
      expect(conf.acceptLabel).toBe(STUDIO_AI_LABELS.page.pendingPlanAccept);
      expect(conf.size).toBe('md');

      conf.accept!();
      fixture.detectChanges();

      expect(builds.cancel).toHaveBeenCalledWith('p1');
      expect(stream.streamChat).toHaveBeenCalledTimes(1);
      expect(stream.streamChat.calls.mostRecent().args[0].message).toBe('Plutôt des notes de frais');
      expect(fixture.componentInstance.store.plan()).toBeNull();
    });

    it('refuser ne fait rien : le plan reste affiché, rien n’est envoyé', () => {
      withPendingPlan();

      fixture.componentInstance.usePrompt('Autre chose');
      lastConfirmation().reject?.();
      fixture.detectChanges();

      expect(builds.cancel).not.toHaveBeenCalled();
      expect(stream.streamChat).not.toHaveBeenCalled();
      expect(fixture.componentInstance.store.plan()?.planId).toBe('p1');
      expect(fixture.componentInstance.mainView()).toBe('preview');
    });
  });

  describe('rail — réinitialiser, modèles, historique', () => {
    beforeEach(async () => { await setup(); });

    it('« Réinitialiser » demande confirmation puis annule les plans et affiche le décompte', () => {
      fixture.componentInstance.resetFromRail();
      expect(builds.cancelPending).not.toHaveBeenCalled();

      lastConfirmation().accept!();

      expect(builds.cancelPending).toHaveBeenCalledTimes(1);
      expect(toast.add).toHaveBeenCalledWith(jasmine.objectContaining({ severity: 'success', detail: '1 plan(s) en attente annulé(s).' }));
      expect(fixture.componentInstance.store.phase()).toBe('idle');
    });

    it('« Nouvelle demande » ne confirme que si un plan serait perdu', () => {
      fixture.componentInstance.newRequest();
      expect(confirmation.confirm).not.toHaveBeenCalled();
      expect(builds.cancelPending).toHaveBeenCalledTimes(1);

      withPendingPlan();
      fixture.componentInstance.newRequest();
      expect(confirmation.confirm).toHaveBeenCalledTimes(1);
      expect(builds.cancelPending).toHaveBeenCalledTimes(1);
    });

    it('« Utiliser » un modèle crée le plan directement, ou après confirmation si un plan est en cours', () => {
      fixture.componentInstance.useTemplate('crm');
      expect(builds.createFromTemplate).toHaveBeenCalledWith('crm');
      expect(confirmation.confirm).not.toHaveBeenCalled();

      withPendingPlan();
      fixture.componentInstance.useTemplate('paie');
      expect(builds.createFromTemplate).toHaveBeenCalledTimes(1);
      expect(lastConfirmation().message).toBe(STUDIO_AI_LABELS.rail.replaceCurrent);
      lastConfirmation().accept!();
      expect(builds.createFromTemplate).toHaveBeenCalledWith('paie');
    });

    it('rouvre un plan « À valider » de l’historique avec son résumé complet', () => {
      builds.getPlan.and.returnValue(of({
        success: true, message: null, errors: [],
        data: {
          id: 'p-7', kind: 'CreateSystem', status: 'Pending', expiresAt: '2026-09-13T10:00:00Z', createdAt: '2026-09-12T10:00:00Z',
          summaryJson: JSON.stringify({ kind: 'CreateSystem', title: 'Congés', steps: [], entities: [], warnings: [] })
        }
      }) as never);

      fixture.componentInstance.openHistoryPlan({
        id: 'p-7', kind: 'CreateSystem', status: 'Pending', title: 'Congés', entityCount: 2, createdAt: '2026-09-12T10:00:00Z', expiresAt: '2026-09-13T10:00:00Z'
      });

      expect(builds.getPlan).toHaveBeenCalledWith('p-7');
      expect(fixture.componentInstance.store.plan()?.planId).toBe('p-7');
      expect(fixture.componentInstance.store.phase()).toBe('awaiting_confirmation');
      expect(builds.getPlanSpec).toHaveBeenCalledWith('p-7');
    });
  });

  describe('modèle avancé (D3)', () => {
    beforeEach(async () => { await setup(); });

    it('affiche le bandeau quand l’avancé était demandé mais le serveur a répondu en standard', () => {
      const events = new Subject<ChatStreamEvent>();
      stream.streamChat.and.returnValue(events.asObservable());
      fixture.componentInstance.setAdvancedModel(true);
      fixture.componentInstance.submit({ text: 'Créer', attachments: [] });
      fixture.detectChanges();
      expect(fixture.nativeElement.querySelector('[data-testid="model-fallback"]')).toBeNull();

      events.next({ type: 'meta', content: JSON.stringify({ usedAdvancedModel: false, advancedModelFallbackReason: 'unavailable' }) });
      fixture.detectChanges();

      const banner = fixture.nativeElement.querySelector('[data-testid="model-fallback"]') as HTMLElement;
      expect(banner).not.toBeNull();
      expect(banner.textContent).toContain(STUDIO_AI_LABELS.model.fallbackTitle);
      expect(banner.textContent).toContain(STUDIO_AI_LABELS.model.fallbackReason['unavailable']);
      localStorage.removeItem('studio.ai.advancedModel');
    });

    it('n’affiche rien quand le modèle standard était demandé', () => {
      const events = new Subject<ChatStreamEvent>();
      stream.streamChat.and.returnValue(events.asObservable());
      fixture.componentInstance.submit({ text: 'Créer', attachments: [] });
      events.next({ type: 'meta', content: JSON.stringify({ usedAdvancedModel: false }) });
      fixture.detectChanges();

      expect(fixture.nativeElement.querySelector('[data-testid="model-fallback"]')).toBeNull();
    });
  });

  describe('paramètres d’URL', () => {
    it('`?intent=reference_data` présélectionne la carte puis nettoie l’URL', async () => {
      queryParams = { intent: 'reference_data' };
      await setup();

      expect(fixture.componentInstance.pendingIntent()).toBe('reference_data');
      expect(fixture.componentInstance.previewTab()).toBe('seed');
      expect(fixture.componentInstance.prefill()).toContain('valeurs initiales');
    });

    it('`?template=` ouvre le modèle', async () => {
      queryParams = { template: 'crm' };
      await setup();
      expect(builds.createFromTemplate).toHaveBeenCalledWith('crm');
    });

    it('`?plan=` recharge le plan', async () => {
      queryParams = { plan: 'p-42' };
      await setup();
      expect(builds.getPlan).toHaveBeenCalledWith('p-42');
    });
  });

  describe('capacités réduites', () => {
    it('sans capacités serveur, le rail ne montre ni modèles ni historique et le composer cache le toggle', async () => {
      capabilitiesResponse = null;
      await setup();

      expect(fixture.nativeElement.querySelector('app-studio-ai-rail')).not.toBeNull();
      expect(fixture.nativeElement.querySelector('app-studio-ai-templates-card')).toBeNull();
      expect(fixture.nativeElement.querySelector('app-studio-ai-history-card')).toBeNull();
      expect(fixture.nativeElement.querySelector('app-studio-ai-quick-actions')).not.toBeNull();
      expect(fixture.nativeElement.querySelector('p-toggleswitch')).toBeNull();
      expect(builds.listPlans).not.toHaveBeenCalled();
      expect(builds.listTemplates).not.toHaveBeenCalled();
    });
  });
});
