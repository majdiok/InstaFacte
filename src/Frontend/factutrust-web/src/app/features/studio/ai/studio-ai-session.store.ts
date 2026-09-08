import { Injectable, OnDestroy, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { Subscription } from 'rxjs';
import { AiStreamService } from '@features/ai-assistant/services/ai-stream.service';
import {
  AssistantMode, ChatAttachment, ChatRequest, ChatStreamEvent
} from '@features/ai-assistant/models/ai-chat.models';
import {
  buildAttachmentRequests,
  composeBackendMessage
} from '@features/ai-assistant/utils/chat-attachment-payload.util';
import { StudioNavService } from '../studio-nav.service';
import {
  StudioAiBuildService, StudioPlanEvent, StudioPlanSummary,
  StudioReportFailureEvent, StudioReportResultEvent
} from '../studio-ai-build.service';
import { stripStudioAssistantText } from '../studio-ai-builder.component';
import { STUDIO_AI_LABELS } from './studio-ai-labels';
import { cloneSpec, parseSpecPayload, serializeSpec, studioAiHttpError } from './studio-ai-spec.util';
import {
  StudioAiIntent,
  StudioAiSessionPhase,
  StudioBuildResult,
  StudioBuildStep,
  StudioSpecCounters,
  StudioSystemSpec,
  countSpec,
  isSystemBuildResult,
  specPayloadForKind,
  toSystemSpecView
} from './studio-ai.models';

/** Action de navigation proposée par le backend (`client_actions`). */
export interface StudioAiNavAction { label: string; route: string; }

/**
 * Élément du fil de conversation. Les tableaux et les cartes d'échec en font PARTIE : chaque
 * résultat reste attaché à la question qui l'a produit (voir la page legacy pour l'historique).
 */
export interface StudioAiTimelineItem {
  kind: 'text' | 'report' | 'failure' | 'system';
  role?: 'user' | 'assistant';
  text?: string;
  report?: StudioReportResultEvent;
  failure?: StudioReportFailureEvent;
}

/** Plan en attente côté serveur, tel que l'atelier le suit (résumé + spec versionnée). */
export interface StudioAiActivePlan {
  planId: string;
  kind: string;
  expiresAt?: string | null;
  /** Jeton de concurrence optimiste renvoyé par `GET/PUT {id}/spec` ; requis pour `saveDraft`. */
  rowVersion: string | null;
  summary: StudioPlanSummary;
}

export interface StudioAiValidationState {
  warnings: string[];
  errors: string[];
  pending: boolean;
}

/**
 * Ce que le compositeur envoie : le prompt tel que tapé, l'intention choisie et les pièces jointes
 * déjà extraites. Le store compose lui-même le message backend (texte des annexes entre marqueurs) :
 * la bulle utilisateur et `lastPrompt` ne montrent que le prompt (plan P1 §4.1).
 */
export interface StudioAiSendOptions {
  intent?: StudioAiIntent | null;
  attachments?: ChatAttachment[];
}

/**
 * État de session de l'atelier `/studio/ai` (fourni au niveau de `StudioAiPageComponent`, PAS en
 * root : quitter la page réinitialise l'atelier, comme la page legacy).
 *
 * Transitions (plan P1 §2.3) :
 *   idle ──send──▶ planning ──studio_plan──▶ awaiting_confirmation ──startEditing──▶ editing
 *   awaiting_confirmation | editing ──confirm──▶ executing ──studio_result──▶ completed
 *   * ──error──▶ failed (plan conservé si l'échec survient pendant le chat, consommé sinon)
 *   completed | failed ──send / resetConversation──▶ idle | planning
 *
 * Le composant reste « bête » : il lit les signaux et appelle les méthodes ; toute la logique de flux
 * (SSE chat, SSE confirmation, spec versionnée) vit ici pour être testable sans DOM.
 */
@Injectable()
export class StudioAiSessionStore implements OnDestroy {
  private readonly stream = inject(AiStreamService);
  private readonly builds = inject(StudioAiBuildService);
  private readonly studioNav = inject(StudioNavService);
  private readonly router = inject(Router);

  private assistantBuffer = '';
  /** Une carte d'échec a été émise pendant le tour courant (miroir de `reportFailure` sur la page legacy). */
  private failureReported = false;
  private streamSub: Subscription | null = null;
  private confirmSub: Subscription | null = null;
  /** Pièces jointes de la dernière demande, pour que « Réessayer » les renvoie aussi. */
  private lastAttachments: ChatAttachment[] = [];

  // ---- État ---------------------------------------------------------------------------------------
  readonly phase = signal<StudioAiSessionPhase>('idle');
  readonly status = signal<string>('');
  readonly conversationId = signal<string | undefined>(undefined);
  readonly lastPrompt = signal<string>('');
  readonly intent = signal<StudioAiIntent | null>(null);
  readonly timeline = signal<StudioAiTimelineItem[]>([]);
  readonly suggestions = signal<string[]>([]);
  readonly actions = signal<StudioAiNavAction[]>([]);
  readonly plan = signal<StudioAiActivePlan | null>(null);
  /** Spec telle que le serveur la connaît (référence pour `dirty`). `null` tant que l'aperçu détaillé n'est pas chargé. */
  readonly spec = signal<StudioSystemSpec | null>(null);
  /** Copie de travail éditée localement (P1b). Identique à `spec` hors édition. */
  readonly draft = signal<StudioSystemSpec | null>(null);
  readonly specLoading = signal(false);
  readonly validation = signal<StudioAiValidationState>({ warnings: [], errors: [], pending: false });
  readonly buildSteps = signal<StudioBuildStep[]>([]);
  readonly result = signal<StudioBuildResult | null>(null);
  readonly error = signal<string | null>(null);

  // ---- Dérivés ------------------------------------------------------------------------------------
  readonly busy = computed(() => {
    const p = this.phase();
    return p === 'planning' || p === 'executing';
  });
  readonly hasPlan = computed(() => this.plan() !== null);
  readonly counters = computed<StudioSpecCounters>(() => countSpec(this.draft() ?? this.spec()));
  readonly dirty = computed(() => {
    const base = this.spec();
    const draft = this.draft();
    if (!base || !draft) return false;
    return serializeSpec(base) !== serializeSpec(draft);
  });
  readonly canConfirm = computed(() => {
    const p = this.phase();
    return this.hasPlan() && (p === 'awaiting_confirmation' || p === 'editing') && !this.dirty() && !this.validation().pending;
  });
  readonly canEdit = computed(() => {
    const p = this.phase();
    return this.hasPlan() && this.spec() !== null && (p === 'awaiting_confirmation' || p === 'editing');
  });
  /** Avertissements affichés dans l'aperçu : résumé serveur + validation locale. */
  readonly warnings = computed(() => {
    const fromPlan = this.plan()?.summary.warnings ?? [];
    const fromValidation = this.validation().warnings;
    return Array.from(new Set([...fromPlan, ...fromValidation]));
  });

  // ---- Conversation -------------------------------------------------------------------------------

  /** Envoie une demande à l'assistant (mode StudioBuilder). Ignoré si vide ou déjà occupé. */
  send(prompt: string, options: StudioAiSendOptions = {}): void {
    const text = prompt.trim();
    if (!text || this.busy()) return;
    const attachments = options.attachments ?? [];

    this.timeline.update(l => [...l, { kind: 'text', role: 'user', text }]);
    this.lastPrompt.set(text);
    this.lastAttachments = attachments;
    if (options.intent !== undefined) this.intent.set(options.intent);
    this.phase.set('planning');
    this.status.set(STUDIO_AI_LABELS.status.analyzing);
    this.actions.set([]);
    this.error.set(null);
    this.buildSteps.set([]);
    this.result.set(null);
    this.suggestions.set([]);
    this.clearPlanState();
    this.assistantBuffer = '';
    this.failureReported = false;

    const attachmentRequests = buildAttachmentRequests(attachments, false);
    const request: ChatRequest = {
      message: composeBackendMessage(text, attachments),
      conversationId: this.conversationId(),
      options: { assistantMode: AssistantMode.StudioBuilder },
      attachments: attachmentRequests.length ? attachmentRequests : undefined
    };

    this.streamSub?.unsubscribe();
    this.streamSub = this.stream.streamChat(request).subscribe({
      next: ev => this.handleChatEvent(ev),
      error: e => this.failPlanning(e?.message ?? STUDIO_AI_LABELS.errors.generationFailed),
      complete: () => this.settleAfterChat()
    });
  }

  /** Rejoue la dernière demande à l'identique (bouton « Réessayer »). */
  retry(): void {
    const last = this.lastPrompt();
    if (!last) return;
    this.send(last, { intent: this.intent(), attachments: this.lastAttachments });
  }

  /** Le store meurt avec la page : on coupe les flux SSE encore ouverts (le serveur termine seul). */
  ngOnDestroy(): void {
    this.streamSub?.unsubscribe();
    this.confirmSub?.unsubscribe();
    this.streamSub = null;
    this.confirmSub = null;
  }

  /**
   * « Réinitialiser la conversation » : annule côté serveur les plans en attente (jamais supprimés :
   * ils restent visibles dans l'historique en `Annulé`), puis repart d'une conversation neuve.
   */
  resetConversation(): void {
    this.streamSub?.unsubscribe();
    this.streamSub = null;
    const finish = () => {
      this.conversationId.set(undefined);
      this.lastPrompt.set('');
      this.lastAttachments = [];
      this.intent.set(null);
      this.timeline.set([]);
      this.suggestions.set([]);
      this.actions.set([]);
      this.buildSteps.set([]);
      this.result.set(null);
      this.error.set(null);
      this.status.set('');
      this.clearPlanState();
      this.phase.set('idle');
    };
    if (this.plan()) {
      this.builds.cancelPending().subscribe({ next: finish, error: finish });
    } else {
      finish();
    }
  }

  // ---- Plan : aperçu, édition, validation ---------------------------------------------------------

  /**
   * Charge la spec détaillée d'un plan (`GET {id}/spec`). Appelé dès la réception d'un `studio_plan`
   * quand l'atelier est actif, et par « Mes projets » pour rouvrir un plan en attente.
   */
  loadSpec(planId: string): void {
    this.specLoading.set(true);
    this.builds.getPlanSpec(planId).subscribe({
      next: res => {
        this.specLoading.set(false);
        const parsed = res?.success ? parseSpecPayload(res.data?.spec) : null;
        if (!parsed) { this.error.set(STUDIO_AI_LABELS.errors.invalidSpec); return; }
        const view = toSystemSpecView(parsed);
        this.spec.set(view);
        this.draft.set(cloneSpec(view));
        this.plan.update(p => p
          ? { ...p, kind: res.data.kind || p.kind, rowVersion: res.data.rowVersion, expiresAt: res.data.expiresAt }
          : p);
      },
      error: err => {
        this.specLoading.set(false);
        this.error.set(studioAiHttpError(err, 'plan'));
      }
    });
  }

  /** Rouvre un plan `Pending` existant (depuis « Mes projets ») sans repasser par le chat. */
  openPlan(planId: string, kind: string, summary: StudioPlanSummary, expiresAt?: string | null): void {
    if (this.busy()) return;
    this.error.set(null);
    this.result.set(null);
    this.buildSteps.set([]);
    this.plan.set({ planId, kind, expiresAt: expiresAt ?? null, rowVersion: null, summary: normalizeSummary(summary) });
    this.spec.set(null);
    this.draft.set(null);
    this.validation.set({ warnings: [], errors: [], pending: false });
    this.phase.set('awaiting_confirmation');
    this.status.set(STUDIO_AI_LABELS.status.awaitingValidation);
    this.loadSpec(planId);
  }

  /** Passe l'aperçu en mode édition (P1b : onglets éditables). */
  startEditing(): void {
    if (!this.canEdit()) return;
    this.phase.set('editing');
  }

  /** Remplace la copie de travail (les onglets éditables émettent la spec complète mise à jour). */
  updateDraft(next: StudioSystemSpec): void {
    if (this.phase() !== 'editing') return;
    this.draft.set(next);
  }

  /** Abandonne les modifications locales et revient à la spec serveur. */
  resetDraft(): void {
    const base = this.spec();
    this.draft.set(base ? cloneSpec(base) : null);
    this.validation.set({ warnings: [], errors: [], pending: false });
    if (this.phase() === 'editing') this.phase.set('awaiting_confirmation');
  }

  /**
   * Enregistre la copie de travail sur le plan (`PUT {id}/spec`, `rowVersion` obligatoire). Le
   * serveur re-parse et renvoie le nouveau résumé + la spec canonique + un nouveau `rowVersion`.
   * Un 409 signifie qu'un autre onglet a modifié le plan : on invite à recharger, sans écraser.
   */
  saveDraft(): void {
    const current = this.plan();
    const draft = this.draft();
    if (!current || !draft || !this.dirty() || this.validation().pending) return;
    if (!current.rowVersion) { this.error.set(STUDIO_AI_LABELS.errors.conflict); return; }

    this.validation.update(v => ({ ...v, pending: true, errors: [] }));
    this.error.set(null);
    const payload = serializeSpec(specPayloadForKind(current.kind, draft));
    this.builds.updatePlanSpec(current.planId, payload, current.rowVersion).subscribe({
      next: res => {
        if (!res?.success || !res.data) {
          this.validation.set({ warnings: [], errors: [STUDIO_AI_LABELS.errors.generic], pending: false });
          return;
        }
        const parsed = parseSpecPayload(res.data.spec?.spec);
        const view = parsed ? toSystemSpecView(parsed) : draft;
        const summary = parsePlanSummary(res.data.plan?.summaryJson) ?? current.summary;
        this.spec.set(view);
        this.draft.set(cloneSpec(view));
        this.plan.set({
          ...current,
          rowVersion: res.data.spec?.rowVersion ?? current.rowVersion,
          expiresAt: res.data.spec?.expiresAt ?? current.expiresAt,
          summary
        });
        this.validation.set({ warnings: summary.warnings ?? [], errors: [], pending: false });
        this.phase.set('awaiting_confirmation');
        this.status.set(STUDIO_AI_LABELS.status.awaitingValidation);
        this.timeline.update(l => [...l, { kind: 'system', text: STUDIO_AI_LABELS.status.draftSaved }]);
      },
      error: err => {
        const message = studioAiHttpError(err, 'plan');
        this.validation.set({ warnings: [], errors: [message], pending: false });
      }
    });
  }

  /**
   * « Régénérer avec ces modifications » (P1b) : renvoie au chat la demande initiale enrichie d'un
   * résumé lisible des changements ; le serveur produit un nouveau plan qui remplace l'actuel.
   */
  regenerate(changeSummary: string): void {
    const last = this.lastPrompt();
    if (!last || this.busy()) return;
    const message = changeSummary.trim()
      ? `${last}\n\nModifications demandées :\n${changeSummary.trim()}`
      : last;
    this.send(message, { intent: this.intent(), attachments: this.lastAttachments });
  }

  // ---- Plan : confirmation / annulation ------------------------------------------------------------

  /** Confirme le plan en attente : flux SSE `studio_progress` puis `studio_result`. */
  confirm(): void {
    const current = this.plan();
    if (!current || !this.canConfirm()) return;

    this.phase.set('executing');
    this.status.set(STUDIO_AI_LABELS.status.creating);
    this.error.set(null);
    this.buildSteps.set([]);
    this.result.set(null);

    this.confirmSub?.unsubscribe();
    this.confirmSub = this.builds.confirm(current.planId).subscribe({
      next: ev => this.handleConfirmEvent(ev),
      error: e => this.failExecution(e?.message ?? STUDIO_AI_LABELS.errors.executionFailed),
      complete: () => {
        if (this.phase() === 'executing') this.phase.set(this.result() ? 'completed' : 'idle');
        this.studioNav.refresh();
      }
    });
  }

  /** Annule le plan en attente côté serveur ; l'aperçu local se referme dans tous les cas. */
  cancelPlan(): void {
    const current = this.plan();
    if (!current || this.busy()) return;
    const close = () => {
      this.clearPlanState();
      this.phase.set('idle');
      this.status.set('');
      this.timeline.update(l => [...l, { kind: 'system', text: STUDIO_AI_LABELS.status.planCancelled }]);
    };
    this.builds.cancel(current.planId).subscribe({ next: close, error: close });
  }

  // ---- Après création -----------------------------------------------------------------------------

  /** Ouvre le système / la table créé(e). */
  openCreated(url?: string): void {
    const target = url ?? this.resultUrl();
    if (target) void this.router.navigateByUrl(target);
  }

  /** URL principale du résultat (système ou table). */
  resultUrl(): string | null {
    const r = this.result();
    if (!r) return null;
    return (isSystemBuildResult(r) ? r.systemUrl : r.openUrl) || null;
  }

  /** Suit une action de navigation proposée par l'assistant. */
  go(action: StudioAiNavAction): void {
    void this.router.navigate([action.route]);
  }

  // ---- SSE : chat -----------------------------------------------------------------------------------

  private handleChatEvent(ev: ChatStreamEvent): void {
    switch (ev.type) {
      case 'tool_call_start':
        this.status.set(STUDIO_AI_LABELS.status.preparing);
        break;
      case 'content':
        if (ev.content) this.assistantBuffer += ev.content;
        break;
      case 'content_replace':
        if (ev.content != null && !this.failureReported) this.assistantBuffer = stripStudioAssistantText(ev.content);
        break;
      case 'studio_plan':
        this.applyPlan(ev.content);
        break;
      case 'studio_report_result':
        this.applyReportResult(ev.content);
        break;
      case 'studio_report_error':
        this.applyReportFailure(ev.content);
        break;
      case 'suggested_prompts':
        this.applySuggestions(ev.suggestedPrompts);
        break;
      case 'studio_progress':
        this.applyProgress(ev.content);
        break;
      case 'client_actions':
        this.actions.set(parseActions(ev.clientActions));
        break;
      case 'error':
        this.failPlanning(ev.error ?? STUDIO_AI_LABELS.errors.generic);
        break;
      case 'done':
        if (ev.conversationId) this.conversationId.set(ev.conversationId);
        this.flushAssistantText();
        // Sans flux d'aperçu (préversion désactivée), la création a déjà eu lieu : on rafraîchit la nav.
        if (!this.plan()) this.studioNav.refresh();
        break;
    }
  }

  private handleConfirmEvent(ev: ChatStreamEvent): void {
    switch (ev.type) {
      case 'studio_progress':
        this.applyProgress(ev.content);
        break;
      case 'studio_result':
        this.applyResult(ev.content);
        break;
      case 'error':
        this.failExecution(ev.error ?? STUDIO_AI_LABELS.errors.executionFailed);
        break;
      case 'done':
        if (this.phase() === 'executing') this.phase.set(this.result() ? 'completed' : 'idle');
        break;
    }
  }

  private applyPlan(json: string | undefined): void {
    if (!json) return;
    try {
      const payload = JSON.parse(json) as StudioPlanEvent;
      if (!payload?.planId || !payload.summary) return;
      const summary = normalizeSummary(
        typeof payload.summary === 'string' ? JSON.parse(payload.summary as unknown as string) : payload.summary
      );
      this.plan.set({ planId: payload.planId, kind: summary.kind, expiresAt: null, rowVersion: null, summary });
      this.validation.set({ warnings: [], errors: [], pending: false });
      this.phase.set('awaiting_confirmation');
      this.status.set(STUDIO_AI_LABELS.status.awaitingValidation);
      this.loadSpec(payload.planId);
    } catch {
      // Payload de plan illisible : on laisse le flux se terminer normalement.
    }
  }

  private applyResult(json: string | undefined): void {
    if (!json) return;
    try {
      const payload = JSON.parse(json) as StudioBuildResult;
      if (!payload || typeof payload !== 'object') return;
      this.result.set(payload);
      this.timeline.update(l => [...l, { kind: 'system', text: STUDIO_AI_LABELS.status.created }]);
    } catch {
      // Résultat illisible : le plan est bien exécuté côté serveur, la nav est rafraîchie au `done`.
      this.timeline.update(l => [...l, { kind: 'system', text: STUDIO_AI_LABELS.status.created }]);
    }
  }

  private applyReportResult(json: string | undefined): void {
    if (!json) return;
    try {
      const payload = JSON.parse(json) as StudioReportResultEvent;
      if (!payload?.result?.columns) return;
      payload.warnings ??= [];
      this.timeline.update(l => [...l, { kind: 'report', report: payload }]);
    } catch { /* payload illisible : le texte de l'assistant suffit */ }
  }

  private applyReportFailure(json: string | undefined): void {
    if (!json) return;
    try {
      const payload = JSON.parse(json) as StudioReportFailureEvent;
      if (!payload?.message) return;
      payload.suggestions ??= [];
      this.timeline.update(l => [...l, { kind: 'failure', failure: payload }]);
      this.failureReported = true;
      // La carte remplace la bulle : ce qui avait déjà été accumulé n'a plus lieu d'être affiché.
      this.assistantBuffer = '';
    } catch { /* payload illisible */ }
  }

  private applySuggestions(json: string | undefined): void {
    if (!json) return;
    try {
      const parsed = JSON.parse(json);
      if (Array.isArray(parsed)) this.suggestions.set(parsed.filter((s): s is string => typeof s === 'string'));
    } catch { /* pas de puces */ }
  }

  private applyProgress(json: string | undefined): void {
    if (!json) return;
    try {
      const step = JSON.parse(json) as StudioBuildStep;
      if (!step?.phase) return;
      this.buildSteps.update(steps => {
        const i = steps.findIndex(x => x.phase === step.phase && x.label === step.label);
        if (i >= 0) { const copy = [...steps]; copy[i] = step; return copy; }
        return [...steps, step];
      });
    } catch { /* progression illisible */ }
  }

  private settleAfterChat(): void {
    if (this.phase() === 'planning') this.phase.set('idle');
    if (this.phase() === 'idle') this.status.set('');
  }

  private flushAssistantText(): void {
    const finalText = stripStudioAssistantText(this.assistantBuffer);
    if (finalText) this.timeline.update(l => [...l, { kind: 'text', role: 'assistant', text: finalText }]);
    this.assistantBuffer = '';
  }

  /** Échec pendant la phase de chat : un plan déjà reçu reste validable. */
  private failPlanning(message: string): void {
    this.error.set(message);
    this.phase.set(this.plan() ? 'awaiting_confirmation' : 'failed');
    this.status.set('');
  }

  /** Échec pendant l'exécution : le plan est consommé côté serveur (statut Failed). */
  private failExecution(message: string): void {
    this.error.set(message);
    this.clearPlanState();
    this.phase.set('failed');
    this.status.set('');
  }

  private clearPlanState(): void {
    this.plan.set(null);
    this.spec.set(null);
    this.draft.set(null);
    this.specLoading.set(false);
    this.validation.set({ warnings: [], errors: [], pending: false });
  }

}

// ---- Helpers purs (exportés pour les tests) ---------------------------------------------------------

export function normalizeSummary(summary: StudioPlanSummary): StudioPlanSummary {
  return {
    ...summary,
    kind: summary.kind ?? '',
    title: summary.title ?? '',
    steps: summary.steps ?? [],
    entities: summary.entities ?? [],
    warnings: summary.warnings ?? []
  };
}

export function parsePlanSummary(summaryJson: string | null | undefined): StudioPlanSummary | null {
  if (!summaryJson) return null;
  try {
    const parsed = JSON.parse(summaryJson) as StudioPlanSummary;
    return parsed && typeof parsed === 'object' ? normalizeSummary(parsed) : null;
  } catch { return null; }
}

export function parseActions(json: string | undefined): StudioAiNavAction[] {
  if (!json) return [];
  try {
    const arr = JSON.parse(json);
    return Array.isArray(arr)
      ? arr.filter((a): a is StudioAiNavAction =>
          typeof a?.label === 'string' && typeof a?.route === 'string' && a.route.startsWith('/'))
      : [];
  } catch { return []; }
}
