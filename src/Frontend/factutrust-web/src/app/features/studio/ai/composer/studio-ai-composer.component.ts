import { ChangeDetectionStrategy, Component, ElementRef, effect, inject, input, output, signal, viewChild } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { Textarea } from 'primeng/textarea';
import { TooltipModule } from 'primeng/tooltip';
import { createClientUuid } from '@core/utils/safe-random-uuid.util';
import { ChatAttachment } from '@features/ai-assistant/models/ai-chat.models';
import { AiChatService } from '@features/ai-assistant/services/ai-chat.service';
import { toChatAttachment } from '@features/ai-assistant/utils/chat-attachment-payload.util';
import { ChatAttachmentCardComponent } from '@features/ai-assistant/components/chat-attachment-card/chat-attachment-card.component';
import { STUDIO_AI_LABELS, formatLabel } from '../studio-ai-labels';

/** Ce que le composer remonte à la page : le prompt affiché tel quel et les pièces jointes extraites. */
export interface StudioAiComposerSubmit {
  text: string;
  attachments: ChatAttachment[];
}

/** Budget de pièces jointes par demande (au-delà, message en ligne, aucun envoi). */
const MAX_ATTACHMENTS = 5;

/** Extraction en cours : l'identifiant local permet de retirer la bonne ligne même en cas d'homonymes. */
interface PendingExtraction { id: string; name: string; }

/**
 * Zone de saisie de l'atelier (plan P1 §4.1) : `textarea` (Entrée envoie, Maj+Entrée insère un
 * retour à la ligne — même geste que la page legacy), bouton trombone et bouton d'envoi.
 *
 * Les pièces jointes suivent EXACTEMENT le flux du chat (`ChatInputComponent`) : le fichier est
 * envoyé à `POST ai/document-extract` et c'est le texte extrait par le serveur qui partira avec la
 * demande. Le composer n'encode donc rien lui-même : il émet des `ChatAttachment` et la page
 * compose le message backend (`composeBackendMessage` / `buildAttachmentRequests`).
 */
@Component({
  selector: 'app-studio-ai-composer',
  standalone: true,
  imports: [FormsModule, ButtonModule, Textarea, TooltipModule, ChatAttachmentCardComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="sac">
      <input
        #fileInput
        type="file"
        class="sac__file"
        multiple
        tabindex="-1"
        aria-hidden="true"
        accept=".txt,.csv,.pdf,.png,.jpg,.jpeg,.webp,.docx,.xlsx,text/plain,text/csv,application/pdf,image/png,image/jpeg,image/webp"
        (change)="onFilesSelected($event)" />

      @if (attachments().length) {
        <div class="sac__attachments" role="list" [attr.aria-label]="labels.composer.attachments">
          @for (att of attachments(); track att.id) {
            <app-chat-attachment-card
              role="listitem"
              [attachment]="att"
              [editable]="true"
              (removed)="removeAttachment($event)" />
          }
        </div>
      }

      @for (pending of extracting(); track pending.id) {
        <p class="sac__extracting" role="status" aria-live="polite">
          <i class="fa-solid fa-spinner fa-spin" aria-hidden="true"></i>
          {{ extractingLabel(pending.name) }}
        </p>
      }

      <div class="sac__box">
        <textarea
          #input
          pTextarea
          rows="3"
          class="sac__input"
          [ngModel]="text()"
          (ngModelChange)="text.set($event)"
          [disabled]="disabled()"
          [placeholder]="placeholder()"
          [attr.aria-label]="labels.page.heroTitle"
          (keydown.enter)="onEnter($event)"></textarea>

        <div class="sac__actions">
          <button
            pButton
            type="button"
            class="p-button-text p-button-sm sac__attach"
            icon="fa-solid fa-paperclip"
            [attr.aria-label]="labels.page.attach"
            [pTooltip]="labels.page.attach"
            [disabled]="disabled()"
            (click)="openFilePicker()"></button>
          <button
            pButton
            type="button"
            class="p-button-sm sac__send"
            icon="fa-solid fa-paper-plane"
            [label]="disabled() ? labels.page.generating : labels.page.generate"
            [disabled]="disabled() || !canSubmit()"
            (click)="submit()"></button>
        </div>
      </div>

      @if (attachError(); as err) {
        <p class="sac__error" role="alert"><i class="fa-solid fa-triangle-exclamation" aria-hidden="true"></i> {{ err }}</p>
      }
      <p class="sac__hint">{{ labels.composer.enterHint }}</p>
    </div>
  `,
  styles: [`
    .sac { display: flex; flex-direction: column; gap: var(--spacing-2); }
    .sac__file { position: absolute; width: 1px; height: 1px; overflow: hidden; clip: rect(0 0 0 0); }
    .sac__attachments { display: flex; flex-wrap: wrap; gap: var(--spacing-2); }
    .sac__extracting, .sac__hint, .sac__error {
      margin: 0;
      font-size: var(--font-size-sm);
      color: var(--color-neutral-500);
    }
    .sac__error { color: var(--color-danger-600, #dc2626); }
    .sac__box {
      display: flex;
      flex-direction: column;
      gap: var(--spacing-2);
      padding: var(--spacing-3);
      border: 1px solid var(--color-border-subtle, #e2e8f0);
      border-radius: var(--radius-lg);
      background: var(--color-background-elevated, #fff);
    }
    .sac__input { width: 100%; resize: vertical; }
    .sac__actions { display: flex; justify-content: flex-end; gap: var(--spacing-2); }
  `]
})
export class StudioAiComposerComponent {
  private readonly chat = inject(AiChatService);

  /** Désactive la saisie et l'envoi (génération ou intégration en cours). */
  readonly disabled = input(false);
  readonly placeholder = input(STUDIO_AI_LABELS.page.composerPlaceholder);
  /**
   * Texte poussé par la page quand une carte d'intention est choisie : il remplace le contenu de la
   * zone de saisie et prend le focus, sans rien envoyer (§4.2).
   */
  readonly prefill = input<string | null>(null);

  readonly submitted = output<StudioAiComposerSubmit>();

  protected readonly labels = STUDIO_AI_LABELS;
  protected readonly text = signal('');
  protected readonly attachments = signal<ChatAttachment[]>([]);
  protected readonly extracting = signal<PendingExtraction[]>([]);
  protected readonly attachError = signal<string | null>(null);

  private readonly inputRef = viewChild<ElementRef<HTMLTextAreaElement>>('input');
  private readonly fileInputRef = viewChild<ElementRef<HTMLInputElement>>('fileInput');

  constructor() {
    effect(() => {
      const prefill = this.prefill();
      if (!prefill) return;
      this.text.set(prefill);
      this.focusInput();
    });
  }

  protected canSubmit(): boolean {
    return this.text().trim().length > 0 || this.attachments().length > 0;
  }

  protected extractingLabel(name: string): string {
    return formatLabel(STUDIO_AI_LABELS.composer.extracting, { name });
  }

  /** Entrée envoie ; Maj+Entrée laisse le comportement natif (retour à la ligne). */
  protected onEnter(event: Event): void {
    if ((event as KeyboardEvent).shiftKey) return;
    event.preventDefault();
    this.submit();
  }

  submit(): void {
    if (this.disabled() || !this.canSubmit()) return;
    this.submitted.emit({ text: this.text().trim(), attachments: this.attachments() });
    this.text.set('');
    this.attachments.set([]);
    this.attachError.set(null);
  }

  protected openFilePicker(): void {
    this.attachError.set(null);
    this.fileInputRef()?.nativeElement.click();
  }

  protected onFilesSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    const files = Array.from(input.files ?? []);
    input.value = '';
    if (!files.length || this.disabled()) return;
    this.attachError.set(null);
    for (const file of files) this.extract(file);
  }

  protected removeAttachment(id: string): void {
    this.attachments.update(list => list.filter(a => a.id !== id));
    this.attachError.set(null);
  }

  /**
   * Extraction serveur d'un fichier. Le budget compte aussi les extractions en cours : sans cela,
   * une sélection multiple de dix fichiers en laisserait passer dix avant que la limite ne parle.
   */
  private extract(file: File): void {
    if (this.attachments().length + this.extracting().length >= MAX_ATTACHMENTS) {
      this.attachError.set(formatLabel(STUDIO_AI_LABELS.composer.maxAttachments, { max: MAX_ATTACHMENTS }));
      return;
    }
    const pending: PendingExtraction = { id: createClientUuid(), name: file.name };
    this.extracting.update(list => [...list, pending]);

    // Pas de rendu d'images : le mode StudioBuilder n'expose aucune sélection de modèle vision (§4.1).
    this.chat.extractDocument(file, { renderImages: false }).subscribe({
      next: res => {
        this.settle(pending.id);
        this.attachments.update(list => [...list, toChatAttachment(res, file, createClientUuid())]);
      },
      error: () => {
        this.settle(pending.id);
        this.attachError.set(STUDIO_AI_LABELS.composer.extractError);
      }
    });
  }

  private settle(id: string): void {
    this.extracting.update(list => list.filter(p => p.id !== id));
  }

  private focusInput(): void {
    const el = this.inputRef()?.nativeElement;
    if (!el) return;
    el.focus();
    el.setSelectionRange(el.value.length, el.value.length);
  }
}
