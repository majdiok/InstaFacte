import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { ButtonModule } from 'primeng/button';
import { TooltipModule } from 'primeng/tooltip';
import { ChatAttachment } from '@features/ai-assistant/models/ai-chat.models';
import { StudioPageShellComponent } from '../shared/studio-page-shell.component';
import { STUDIO_BREADCRUMBS } from '../shared/studio-breadcrumb.util';
import { StudioAiCapabilitiesService } from './studio-ai-capabilities.service';
import { STUDIO_AI_LABELS, StudioAiIntentCardDef } from './studio-ai-labels';
import { StudioAiSessionStore } from './studio-ai-session.store';
import { StudioAiIntent, StudioAiPreviewTab } from './studio-ai.models';
import { StudioAiComposerComponent } from './composer/studio-ai-composer.component';
import { StudioAiIntentCardsComponent } from './composer/studio-ai-intent-cards.component';
import { StudioAiConversationComponent } from './conversation/studio-ai-conversation.component';
import { StudioAiConfirmDialogComponent } from './preview/studio-ai-confirm-dialog.component';
import { StudioAiPreviewComponent } from './preview/studio-ai-preview.component';
import { StudioAiProgressComponent } from './preview/studio-ai-progress.component';
import { StudioAiResultCardComponent } from './preview/studio-ai-result-card.component';

/** Onglet d'aperçu ouvert par défaut selon l'intention choisie dans les cartes. */
const INTENT_TO_TAB: Partial<Record<StudioAiIntent, StudioAiPreviewTab>> = {
  table: 'tables',
  relations: 'relations',
  form: 'forms',
  reference_data: 'seed',
  report: 'reports'
};

/**
 * Atelier Studio IA (`/studio/ai` quand le workbench est activé).
 *
 * Composition « bête » : le store porte tout l'état de session ; cette page choisit seulement quoi
 * afficher dans la colonne principale (composer + cartes ↔ aperçu ↔ progression ↔ résultat) et relie
 * les sorties des composants aux méthodes du store. Le store est fourni ICI (pas en root) : quitter
 * la page réinitialise l'atelier.
 */
@Component({
  selector: 'app-studio-ai-page',
  standalone: true,
  imports: [
    ButtonModule,
    TooltipModule,
    StudioPageShellComponent,
    StudioAiComposerComponent,
    StudioAiIntentCardsComponent,
    StudioAiConversationComponent,
    StudioAiConfirmDialogComponent,
    StudioAiPreviewComponent,
    StudioAiProgressComponent,
    StudioAiResultCardComponent
  ],
  providers: [StudioAiSessionStore],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './studio-ai-page.component.html',
  styleUrl: './studio-ai-page.component.scss'
})
export class StudioAiPageComponent {
  readonly store = inject(StudioAiSessionStore);
  readonly capabilities = inject(StudioAiCapabilitiesService);
  readonly labels = STUDIO_AI_LABELS;
  readonly breadcrumbs = STUDIO_BREADCRUMBS.aiBuilder();

  /** Texte injecté dans le composer au clic d'une carte d'intention. */
  readonly prefill = signal<string | null>(null);
  readonly pendingIntent = signal<StudioAiIntent | null>(null);
  readonly previewTab = signal<StudioAiPreviewTab>('overview');
  readonly confirmVisible = signal(false);
  readonly conversationCollapsed = signal(false);

  /** Ce que la colonne principale affiche. */
  readonly mainView = computed<'compose' | 'preview' | 'progress' | 'result'>(() => {
    const phase = this.store.phase();
    if (phase === 'executing') return 'progress';
    if (phase === 'completed' && this.store.result()) return 'result';
    if (this.store.hasPlan()) return 'preview';
    return 'compose';
  });

  /** La conversation n'a d'intérêt qu'une fois la première demande envoyée. */
  readonly showConversation = computed(() => this.store.timeline().length > 0 || this.store.busy());

  readonly composerDisabled = computed(() => this.store.busy() || this.store.hasPlan());

  pickIntent(card: StudioAiIntentCardDef): void {
    if (!card.available) return;
    this.pendingIntent.set(card.intent);
    this.previewTab.set(INTENT_TO_TAB[card.intent] ?? 'overview');
    // Nouvelle référence à chaque clic pour que le composer réagisse même si le texte est identique.
    this.prefill.set(card.prompt);
  }

  submit(payload: { text: string; attachments: ChatAttachment[] }): void {
    this.store.send(payload.text, { intent: this.pendingIntent(), attachments: payload.attachments });
    this.prefill.set(null);
  }

  usePrompt(text: string): void {
    if (this.store.hasPlan()) return;
    this.store.send(text, { intent: this.pendingIntent() });
  }

  openConfirm(): void {
    if (this.store.canConfirm()) this.confirmVisible.set(true);
  }

  confirm(): void {
    this.confirmVisible.set(false);
    this.store.confirm();
  }

  newRequest(): void {
    this.store.resetConversation();
    this.pendingIntent.set(null);
    this.previewTab.set('overview');
  }
}
