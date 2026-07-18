import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { InputTextarea } from 'primeng/inputtextarea';
import { AiStreamService } from '@features/ai-assistant/services/ai-stream.service';
import { AssistantMode, ChatRequest, ChatStreamEvent } from '@features/ai-assistant/models/ai-chat.models';
import { StudioNavService } from './studio-nav.service';
import { StudioPageShellComponent } from './shared/studio-page-shell.component';
import { STUDIO_BREADCRUMBS } from './shared/studio-breadcrumb.util';

interface NavAction { label: string; route: string; }
interface ChatLine { role: 'user' | 'assistant'; text: string; }
interface BuildStep { phase: string; label: string; status: string; entityRef?: string; }

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
          @if (lines().length === 0 && !loading()) {
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
          @if (loading()) {
            <p class="sab-status"><i class="fa-solid fa-spinner fa-spin"></i> {{ status() }}</p>
          }
        </div>

        <div class="sab-compose">
          <textarea pInputTextarea [(ngModel)]="prompt" rows="2" class="sab-input" [disabled]="loading()"
            placeholder="Ex. : Créer un système de gestion de congés avec plusieurs tables..."
            (keydown.enter)="$event.preventDefault(); send()"></textarea>
          <div class="sab-compose__actions">
            <button pButton type="button" icon="fa-solid fa-paper-plane" label="Envoyer"
              [disabled]="loading() || !prompt.trim()" (click)="send()"></button>
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
    .sab-line { white-space: pre-wrap; padding: .75rem 1rem; border-radius: 10px; background: var(--surface-50);
      border: 1px solid var(--surface-200); line-height: 1.5; max-width: 95%; }
    .sab-line--user { align-self: flex-end; background: var(--primary-50); border-color: var(--primary-100); }
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
  readonly loading = signal(false);
  readonly status = signal('Analyse…');
  readonly actions = signal<NavAction[]>([]);
  readonly error = signal<string | null>(null);

  useExample(ex: string): void { this.prompt = ex; }

  send(): void {
    const message = this.prompt.trim();
    if (!message || this.loading()) return;

    this.lines.update(l => [...l, { role: 'user', text: message }]);
    this.prompt = '';
    this.loading.set(true);
    this.status.set('Analyse de votre demande…');
    this.actions.set([]);
    this.error.set(null);
    this.buildSteps.set([]);
    this.assistantBuffer = '';

    const request: ChatRequest = {
      message,
      conversationId: this.conversationId,
      options: { assistantMode: AssistantMode.StudioBuilder }
    };

    this.stream.streamChat(request).subscribe({
      next: ev => this.handleEvent(ev),
      error: e => { this.error.set(e?.message ?? 'Échec de la génération.'); this.loading.set(false); },
      complete: () => this.loading.set(false)
    });
  }

  private handleEvent(ev: ChatStreamEvent): void {
    switch (ev.type) {
      case 'tool_call_start':
        this.status.set('Création en cours…');
        break;
      case 'content':
        if (ev.content) this.assistantBuffer += ev.content;
        break;
      case 'content_replace':
        if (ev.content != null) this.assistantBuffer = this.strip(ev.content);
        break;
      case 'studio_progress':
        if (ev.content) {
          try {
            const step = JSON.parse(ev.content) as BuildStep;
            this.buildSteps.update(steps => {
              const i = steps.findIndex(x => x.phase === step.phase && x.label === step.label);
              if (i >= 0) { const copy = [...steps]; copy[i] = step; return copy; }
              return [...steps, step];
            });
          } catch { /* ignore malformed progress */ }
        }
        break;
      case 'client_actions':
        this.actions.set(this.parseActions(ev.clientActions));
        break;
      case 'error':
        this.error.set(ev.error ?? 'Une erreur est survenue.');
        this.loading.set(false);
        break;
      case 'done':
        if (ev.conversationId) this.conversationId = ev.conversationId;
        const finalText = this.strip(this.assistantBuffer);
        if (finalText) this.lines.update(l => [...l, { role: 'assistant', text: finalText }]);
        this.studioNav.refresh();
        this.loading.set(false);
        break;
    }
  }

  go(a: NavAction): void { this.router.navigate([a.route]); }

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
