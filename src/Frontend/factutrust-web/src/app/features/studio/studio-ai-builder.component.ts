import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { InputTextarea } from 'primeng/inputtextarea';
import { AiStreamService } from '@features/ai-assistant/services/ai-stream.service';
import { AssistantMode, ChatRequest, ChatStreamEvent } from '@features/ai-assistant/models/ai-chat.models';
import { StudioNavService } from './studio-nav.service';
import { StudioAiBuildService, StudioPlanEntity, StudioPlanEvent, StudioPlanSummary } from './studio-ai-build.service';
import { StudioPageShellComponent } from './shared/studio-page-shell.component';
import { STUDIO_BREADCRUMBS } from './shared/studio-breadcrumb.util';

interface NavAction { label: string; route: string; }
interface ChatLine { role: 'user' | 'assistant'; text: string; }
interface BuildStep { phase: string; label: string; status: string; entityRef?: string; }

/**
 * Étapes du flux : `idle` → `planning` (l'IA rédige) → `awaiting_confirmation` (aperçu affiché,
 * flux plan activé côté serveur) → `executing` (endpoint de confirmation, progression live).
 * Sans le flux d'aperçu, on passe directement de `planning` à `idle` (comportement historique).
 */
type BuilderState = 'idle' | 'planning' | 'awaiting_confirmation' | 'executing';

@Component({
  selector: 'app-studio-ai-builder',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, ButtonModule, InputTextarea, StudioPageShellComponent],
  template: `
    <app-studio-page-shell
      title="Assistant Studio (IA)"
      subtitle="Décrivez un système ou une table — l'IA crée tables, relations, formulaires et données de référence."
      [breadcrumbs]="breadcrumbs">

      <div class="sab-layout">
        <div class="sab-chat">
          @if (lines().length === 0 && !busy()) {
            <p class="sab-welcome">Comment puis-je vous aider aujourd'hui ?</p>
            <div class="sab-examples">
              @for (ex of examples; track ex) {
                <button type="button" class="sab-chip" (click)="useExample(ex)">{{ ex }}</button>
              }
            </div>
          }
          @for (line of lines(); track $index) {
            <div class="sab-line" [class.sab-line--user]="line.role === 'user'">{{ line.text }}</div>
          }

          @if (plan(); as p) {
            <div class="sab-plan">
              <div class="sab-plan__head">
                <i class="fa-solid fa-clipboard-check"></i>
                <div>
                  <h4>{{ p.summary.title }}</h4>
                  <p class="sab-plan__hint">Rien n'est encore créé — vérifiez puis validez.</p>
                </div>
              </div>
              <ul class="sab-plan__steps">
                @for (s of p.summary.steps; track s.key) {
                  <li>
                    <span class="sab-plan__label">{{ s.label }}</span>
                    <span class="sab-plan__detail">{{ s.detail }}</span>
                  </li>
                }
              </ul>
              @if (p.summary.entities.length) {
                <div class="sab-plan__entities">
                  @for (e of p.summary.entities; track e.displayName) {
                    <span class="sab-chip sab-chip--static">{{ entityChip(e) }}</span>
                  }
                </div>
              }
              @for (w of p.summary.warnings; track w) {
                <p class="sab-plan__warning"><i class="fa-solid fa-triangle-exclamation"></i> {{ w }}</p>
              }
              @if (state() === 'awaiting_confirmation') {
                <div class="sab-plan__actions">
                  <button pButton type="button" icon="fa-solid fa-check" label="Valider et créer"
                    (click)="confirmPlan()"></button>
                  <button pButton type="button" class="p-button-text" icon="fa-solid fa-xmark" label="Annuler"
                    (click)="cancelPlan()"></button>
                </div>
              }
            </div>
          }

          @if (buildSteps().length) {
            <div class="sab-progress">
              <h4>Construction</h4>
              @for (s of buildSteps(); track s.phase + s.label) {
                <div class="sab-step">
                  <i class="fa-solid" [class.fa-spinner]="s.status === 'running'" [class.fa-spin]="s.status === 'running'"
                    [class.fa-check]="s.status === 'done'" [class.fa-triangle-exclamation]="s.status === 'error'"></i>
                  {{ s.label }}
                </div>
              }
            </div>
          }
          @if (busy()) {
            <p class="sab-status"><i class="fa-solid fa-spinner fa-spin"></i> {{ status() }}</p>
          }
        </div>

        <div class="sab-compose">
          <textarea pInputTextarea [(ngModel)]="prompt" rows="2" class="sab-input" [disabled]="busy()"
            placeholder="Ex. : Créer un système de gestion de congés avec plusieurs tables..."
            (keydown.enter)="$event.preventDefault(); send()"></textarea>
          <div class="sab-compose__actions">
            <button pButton type="button" icon="fa-solid fa-paper-plane" label="Envoyer"
              [disabled]="busy() || !prompt.trim()" (click)="send()"></button>
          </div>
        </div>

        @if (actions().length) {
          <div class="sab-nav">
            @for (a of actions(); track a.route) {
              <button pButton type="button" class="p-button-outlined p-button-sm"
                icon="fa-solid fa-arrow-right" [label]="a.label" (click)="go(a)"></button>
            }
          </div>
        }
        @if (error()) {
          <p class="sab-error"><i class="fa-solid fa-triangle-exclamation"></i> {{ error() }}</p>
        }
      </div>
    </app-studio-page-shell>
  `,
  styles: [`
    .sab-layout { display: flex; flex-direction: column; gap: 1rem; max-width: 920px; min-height: 420px; }
    .sab-chat { flex: 1; display: flex; flex-direction: column; gap: .75rem; }
    .sab-welcome { font-size: 1.25rem; margin: 0; }
    .sab-examples { display: flex; flex-wrap: wrap; gap: .4rem; }
    .sab-chip { border: 1px solid var(--surface-300); background: var(--surface-50); border-radius: 999px;
      padding: .35rem .75rem; font-size: .82rem; cursor: pointer; text-align: left; }
    .sab-chip--static { cursor: default; }
    .sab-line { white-space: pre-wrap; padding: .75rem 1rem; border-radius: 10px; background: var(--surface-50);
      border: 1px solid var(--surface-200); line-height: 1.5; max-width: 95%; }
    .sab-line--user { align-self: flex-end; background: var(--primary-50); border-color: var(--primary-100); }
    .sab-plan { border: 1px solid var(--primary-200); background: var(--primary-50); border-radius: 10px; padding: 1rem; }
    .sab-plan__head { display: flex; gap: .75rem; align-items: flex-start; }
    .sab-plan__head h4 { margin: 0; font-size: 1rem; }
    .sab-plan__hint { margin: .15rem 0 0; font-size: .82rem; color: var(--text-color-secondary); }
    .sab-plan__steps { list-style: none; margin: .75rem 0 0; padding: 0; display: flex; flex-direction: column; gap: .35rem; }
    .sab-plan__steps li { display: flex; justify-content: space-between; gap: 1rem; font-size: .9rem;
      border-bottom: 1px dashed var(--surface-300); padding-bottom: .3rem; }
    .sab-plan__label { font-weight: 600; }
    .sab-plan__detail { color: var(--text-color-secondary); text-align: right; }
    .sab-plan__entities { display: flex; flex-wrap: wrap; gap: .4rem; margin-top: .75rem; }
    .sab-plan__warning { margin: .5rem 0 0; font-size: .85rem; color: var(--orange-600); }
    .sab-plan__actions { display: flex; gap: .5rem; margin-top: 1rem; }
    .sab-progress { border: 1px solid var(--surface-200); border-radius: 10px; padding: .75rem 1rem; }
    .sab-progress h4 { margin: 0 0 .5rem; font-size: .95rem; }
    .sab-step { display: flex; align-items: center; gap: .5rem; font-size: .9rem; padding: .2rem 0; }
    .sab-compose { display: flex; flex-direction: column; gap: .5rem; border-top: 1px solid var(--surface-200); padding-top: .75rem; }
    .sab-input { width: 100%; }
    .sab-compose__actions { display: flex; justify-content: flex-end; }
    .sab-status { color: var(--text-color-secondary); }
    .sab-nav { display: flex; flex-wrap: wrap; gap: .5rem; }
    .sab-error { color: var(--red-500); }
  `]
})
export class StudioAiBuilderComponent {
  private readonly stream = inject(AiStreamService);
  private readonly builds = inject(StudioAiBuildService);
  private readonly router = inject(Router);
  private readonly studioNav = inject(StudioNavService);

  readonly breadcrumbs = STUDIO_BREADCRUMBS.aiBuilder();
  readonly examples = [
    'Créer un système de gestion de congés avec plusieurs tables liées',
    'Crée une table pour suivre les contrats clients avec date de début, date de fin, montant et statut',
    'Table de suivi des congés : employé, type, date début, date fin, nombre de jours, approuvé'
  ];

  prompt = '';
  private conversationId?: string;
  private assistantBuffer = '';

  readonly lines = signal<ChatLine[]>([]);
  readonly buildSteps = signal<BuildStep[]>([]);
  readonly state = signal<BuilderState>('idle');
  readonly status = signal('Analyse…');
  readonly actions = signal<NavAction[]>([]);
  readonly error = signal<string | null>(null);
  readonly plan = signal<StudioPlanEvent | null>(null);

  /** Le compositeur est verrouillé tant qu'un plan attend une décision ou qu'un travail est en cours. */
  busy(): boolean { return this.state() !== 'idle'; }

  useExample(ex: string): void { this.prompt = ex; }

  send(): void {
    const message = this.prompt.trim();
    if (!message || this.busy()) return;

    this.lines.update(l => [...l, { role: 'user', text: message }]);
    this.prompt = '';
    this.state.set('planning');
    this.status.set('Analyse de votre demande…');
    this.actions.set([]);
    this.error.set(null);
    this.buildSteps.set([]);
    this.plan.set(null);
    this.assistantBuffer = '';

    const request: ChatRequest = {
      message,
      conversationId: this.conversationId,
      options: { assistantMode: AssistantMode.StudioBuilder }
    };

    this.stream.streamChat(request).subscribe({
      next: ev => this.handleEvent(ev),
      error: e => this.fail(e?.message ?? 'Échec de la génération.'),
      complete: () => this.settleAfterChat()
    });
  }

  confirmPlan(): void {
    const current = this.plan();
    if (!current || this.state() !== 'awaiting_confirmation') return;

    this.state.set('executing');
    this.status.set('Création en cours…');
    this.error.set(null);
    this.buildSteps.set([]);

    this.builds.confirm(current.planId).subscribe({
      next: ev => this.handleConfirmEvent(ev),
      error: e => this.failExecution(e?.message ?? "Échec de l'exécution du plan."),
      complete: () => {
        if (this.state() === 'executing') this.state.set('idle');
        this.studioNav.refresh();
      }
    });
  }

  cancelPlan(): void {
    const current = this.plan();
    if (!current) return;

    this.builds.cancel(current.planId).subscribe({
      next: () => {
        this.plan.set(null);
        this.state.set('idle');
        this.lines.update(l => [...l, { role: 'assistant', text: 'Plan annulé. Rien n\'a été créé.' }]);
      },
      error: () => {
        // L'annulation serveur a échoué (plan déjà traité) : on referme quand même l'aperçu local.
        this.plan.set(null);
        this.state.set('idle');
      }
    });
  }

  go(a: NavAction): void { this.router.navigate([a.route]); }

  private handleEvent(ev: ChatStreamEvent): void {
    switch (ev.type) {
      case 'tool_call_start':
        this.status.set('Préparation en cours…');
        break;
      case 'content':
        if (ev.content) this.assistantBuffer += ev.content;
        break;
      case 'content_replace':
        if (ev.content != null) this.assistantBuffer = this.strip(ev.content);
        break;
      case 'studio_plan':
        this.applyPlan(ev.content);
        break;
      case 'studio_progress':
        this.applyProgress(ev.content);
        break;
      case 'client_actions':
        this.actions.set(this.parseActions(ev.clientActions));
        break;
      case 'error':
        this.fail(ev.error ?? 'Une erreur est survenue.');
        break;
      case 'done':
        if (ev.conversationId) this.conversationId = ev.conversationId;
        this.flushAssistantText();
        // Sans flux d'aperçu, la création a déjà eu lieu pendant le chat : on rafraîchit la nav.
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
        this.plan.set(null);
        this.lines.update(l => [...l, { role: 'assistant', text: 'Création terminée.' }]);
        break;
      case 'error':
        this.failExecution(ev.error ?? "Échec de l'exécution du plan.");
        break;
      case 'done':
        if (this.state() === 'executing') this.state.set('idle');
        break;
    }
  }

  entityChip(e: StudioPlanEntity): string {
    const base = `${e.displayName} · ${e.fieldCount} champ(s)`;
    return e.relationCount > 0 ? `${base}, ${e.relationCount} relation(s)` : base;
  }

  private applyPlan(json: string | undefined): void {
    if (!json) return;
    try {
      const payload = JSON.parse(json) as StudioPlanEvent;
      if (!payload?.planId || !payload.summary) return;
      // Tolérance : le backend peut sérialiser `summary` en objet ou en chaîne JSON.
      payload.summary = (typeof payload.summary === 'string'
        ? JSON.parse(payload.summary as unknown as string)
        : payload.summary) as StudioPlanSummary;
      payload.summary.steps ??= [];
      payload.summary.entities ??= [];
      payload.summary.warnings ??= [];
      this.plan.set(payload);
      this.state.set('awaiting_confirmation');
      this.status.set('En attente de votre validation…');
    } catch {
      // Payload de plan illisible : on laisse le flux se terminer normalement.
    }
  }

  private applyProgress(json: string | undefined): void {
    if (!json) return;
    try {
      const step = JSON.parse(json) as BuildStep;
      this.buildSteps.update(steps => {
        const i = steps.findIndex(x => x.phase === step.phase && x.label === step.label);
        if (i >= 0) { const copy = [...steps]; copy[i] = step; return copy; }
        return [...steps, step];
      });
    } catch { /* ignore malformed progress */ }
  }

  /** Fin du flux de chat : on ne rend la main que si aucun plan n'attend de décision. */
  private settleAfterChat(): void {
    if (this.state() === 'planning') this.state.set('idle');
  }

  private flushAssistantText(): void {
    const finalText = this.strip(this.assistantBuffer);
    if (finalText) this.lines.update(l => [...l, { role: 'assistant', text: finalText }]);
    this.assistantBuffer = '';
  }

  /** Échec pendant la phase de chat : un plan déjà reçu reste validable. */
  private fail(message: string): void {
    this.error.set(message);
    this.state.set(this.plan() ? 'awaiting_confirmation' : 'idle');
  }

  /**
   * Échec pendant l'exécution : le plan est consommé côté serveur (statut Failed) — le proposer à
   * nouveau renverrait un conflit. On referme l'aperçu et on invite implicitement à reformuler.
   */
  private failExecution(message: string): void {
    this.error.set(message);
    this.plan.set(null);
    this.state.set('idle');
  }

  private parseActions(json: string | undefined): NavAction[] {
    if (!json) return [];
    try {
      const arr = JSON.parse(json);
      return Array.isArray(arr)
        ? arr.filter(a => typeof a?.label === 'string' && typeof a?.route === 'string' && a.route.startsWith('/'))
        : [];
    } catch { return []; }
  }

  private strip(text: string): string {
    // Hide any internal detail the model may leak: ft-meta/dashboard meta fences, ```json blocks, any
    // fenced block that contains a system/entities spec object, and BARE tool-call envelopes
    // ({"name":"studio_…","arguments":…}). Then drop any remaining PROSE line that names an internal
    // tool (studio_*) or mentions the JSON spec — the builder must summarise in plain French only.
    const noFences = text
      .replace(/```ft-meta[\s\S]*?```/g, '')
      .replace(/```dashboard[\s\S]*?```/g, '')
      .replace(/```json[\s\S]*?```/gi, '')
      .replace(/```[\s\S]*?"(?:system|entities)"[\s\S]*?```/gi, '')
      .replace(/\{\s*"(?:type|name)"\s*:[\s\S]*?"name"\s*:\s*"studio_[\s\S]*?"arguments"[\s\S]*?\}\s*\}/gi, '')
      .replace(/\{\s*"name"\s*:\s*"studio_[\s\S]*?"arguments"\s*:\s*\{[\s\S]*?\}\s*\}/gi, '');
    return noFences
      .split('\n')
      .filter(line => !/studio_[a-z_]+/i.test(line) && !/\bjson\b/i.test(line))
      .join('\n')
      .replace(/\n{3,}/g, '\n\n')
      .trim();
  }
}
