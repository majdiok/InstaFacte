import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  ElementRef,
  EventEmitter,
  Input,
  OnChanges,
  Output,
  PLATFORM_ID,
  SimpleChanges,
  ViewChild,
  inject
} from '@angular/core';
import { CommonModule, isPlatformBrowser } from '@angular/common';
import { Router } from '@angular/router';
import { MarkdownModule } from 'ngx-markdown';
import { ChatMessage, ClientNavAction, MessageRole } from '../../models/ai-chat.models';
import { TypingIndicatorComponent } from '../typing-indicator/typing-indicator.component';
import { AiDashboardRendererComponent } from '../ai-dashboard-renderer/ai-dashboard-renderer.component';
import { AssistantProgressTimelineComponent } from '../assistant-progress-timeline/assistant-progress-timeline.component';
import { ChatAttachmentCardComponent } from '../chat-attachment-card/chat-attachment-card.component';
import { AiChatSessionService } from '../../services/ai-chat-session.service';
import { MessageSelectionService } from '../../services/message-selection.service';
import {
  buildAssistantMarkdownForDisplay,
  closePartialFences,
  resolveAssistantDisplayState
} from '../../utils/assistant-message-display';
import { environment } from '@environments/environment';
import { getAiToolDisplayLabel, dedupeToolSources, DedupedToolSource } from '../../utils/assistant-progress-display';
import { writeTextToClipboard } from '../../utils/clipboard.util';
import { downloadCsv } from '../../utils/dashboard-table-csv';
import { htmlTablesToCsv } from '../../utils/markdown-html-tables-csv';
import { exportDashboardToXlsx, exportElementToPdf } from '../../utils/ai-assistant-export';

@Component({
  selector: 'app-chat-message',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    CommonModule,
    MarkdownModule,
    TypingIndicatorComponent,
    AiDashboardRendererComponent,
    AssistantProgressTimelineComponent,
    ChatAttachmentCardComponent
  ],
  template: `
    <div
      class="chat-message"
      [class.user]="message.role === MessageRole.User"
      [class.assistant]="message.role === MessageRole.Assistant"
      [class.tool]="message.role === MessageRole.Tool"
      [class.selectable]="canBeSelected()"
      [class.selected]="isMessageSelected()">
      @if (selection.isSelectionMode() && canBeSelected()) {
        <label class="message-select" [attr.aria-label]="'Sélectionner cette réponse'">
          <input
            type="checkbox"
            [checked]="isMessageSelected()"
            (change)="onSelectionToggle($event)" />
        </label>
      }
      <div class="message-avatar" aria-hidden="true">
        <i [class]="message.role === MessageRole.User ? 'fa-solid fa-user' : 'fa-solid fa-robot'"></i>
      </div>

      <div class="message-body">
        @if (message.role === MessageRole.User) {
          @if (message.attachments && message.attachments.length > 0) {
            <div class="message-attachments" role="list">
              @for (att of message.attachments; track att.id) {
                <app-chat-attachment-card role="listitem" [attachment]="att" [editable]="false" />
              }
            </div>
          }
          @if (message.content) {
            <div class="message-content user-content">{{ message.content }}</div>
          }
        } @else if (message.role === MessageRole.Tool) {
          <div class="tool-badge">
            <i class="fa-solid fa-wrench" aria-hidden="true"></i>
            <span>{{ message.toolName || 'Outil' }}</span>
          </div>
        } @else {

          <app-assistant-progress-timeline
            [progress]="message.progress"
            [toolCalls]="message.toolCalls || []"
            [isStreaming]="message.isStreaming === true"
            [connectingToModel]="connectingToModel"
          />

          @if (message.generationInterrupted) {
            <div class="generation-interrupted" role="status" aria-live="polite">
              <i class="fa-solid fa-circle-exclamation" aria-hidden="true"></i>
              <span>G&#233;n&#233;ration interrompue. Vous pouvez renvoyer votre message.</span>
            </div>
          }

          <div class="assistant-export-root" #exportRoot>
            @if (message.parsedDashboard && !message.hideInlineDashboard) {
              <app-ai-dashboard-renderer
                [config]="message.parsedDashboard"
                variant="inline"
                (closed)="onDashboardClosed()"
              />
            }

            @if (message.hideInlineDashboard && message.parsedDashboard) {
              <button
                type="button"
                class="show-dashboard-btn"
                (click)="session.showInlineDashboard(message.id)"
                aria-label="Afficher le tableau de bord structur&#233;">
                Afficher le tableau de bord
              </button>
            }

            @if (showAssistantActions()) {
              <div class="assistant-toolbar">
                @if (markdownForDisplay.trim()) {
                  <button
                    type="button"
                    class="toolbar-btn"
                    (click)="copyFullMessage()"
                    [disabled]="copyInProgress"
                    aria-label="Copier la r&#233;ponse (texte brut)">
                    <i class="fa-regular fa-copy" aria-hidden="true"></i>
                    Copier
                  </button>
                  <button
                    type="button"
                    class="toolbar-btn"
                    (click)="copyAsTsv()"
                    [disabled]="copyInProgress"
                    aria-label="Copier pour Excel (TSV)">
                    TSV
                  </button>
                }
                @if (canExportMarkdownTables()) {
                  <button
                    type="button"
                    class="toolbar-btn"
                    (click)="exportMarkdownTablesCsv()"
                    aria-label="Exporter les tableaux Markdown en CSV">
                    CSV (tableaux)
                  </button>
                }
                @if (message.parsedDashboard) {
                  <button
                    type="button"
                    class="toolbar-btn"
                    (click)="exportDashboardExcel()"
                    [disabled]="exportBusy"
                    aria-label="Exporter le tableau de bord en Excel">
                    Excel
                  </button>
                }
                <button
                  type="button"
                  class="toolbar-btn"
                  (click)="exportAssistantPdf()"
                  [disabled]="exportBusy"
                  aria-label="Exporter en PDF">
                  PDF
                </button>
                @if (canExportSingleToPowerPoint()) {
                  <button
                    type="button"
                    class="toolbar-btn"
                    (click)="exportSingleToPowerPoint()"
                    [disabled]="exportBusy"
                    aria-label="Exporter en PowerPoint (cette réponse uniquement)">
                    <i class="fa-solid fa-file-powerpoint" aria-hidden="true"></i>
                    PPT
                  </button>
                }
                @if (markdownForDisplay.trim()) {
                  <button
                    type="button"
                    class="toolbar-btn"
                    (click)="shareByEmail()"
                    aria-label="Partager par e-mail">
                    E-mail
                  </button>
                }
                <button
                  type="button"
                  class="toolbar-btn"
                  (click)="copyConversationLink()"
                  aria-label="Copier le lien de la conversation">
                  Lien
                </button>
                @if (markdownForDisplay.trim() || message.parsedDashboard) {
                  <button
                    type="button"
                    class="toolbar-btn"
                    (click)="suggestChartFollowUp()"
                    aria-label="Demander un graphique &#224; partir de cette r&#233;ponse">
                    Graphique
                  </button>
                }
              </div>
            }

            @if (message.isStreaming && message.content.trim()) {
              <div class="assistant-bubble-wrap">
                <div class="message-content assistant-markdown streaming">
                  <markdown [data]="streamingMarkdown"></markdown>
                </div>
              </div>
            } @else if (renderedMarkdown) {
              <div class="assistant-bubble-wrap">
                <div class="message-content assistant-markdown" #mdRoot>
                  <markdown [data]="renderedMarkdown" (load)="onMarkdownRendered()"></markdown>
                </div>
              </div>
            } @else if (displayFallbackText) {
              <div class="assistant-bubble-wrap">
                <div class="message-content assistant-streaming-text">{{ displayFallbackText }}</div>
              </div>
            }
          </div>

          @if (message.suggestedPrompts?.length) {
            <div class="suggested-prompts" role="group" aria-label="Suggestions de suite">
              @for (p of message.suggestedPrompts; track $index) {
                <button type="button" class="nav-chip" (click)="session.sendSuggestedPrompt(p)">
                  {{ p }}
                </button>
              }
            </div>
          }

          @if (message.clientActions?.length) {
            <div class="client-actions" role="group" aria-label="Navigation sugg&#233;r&#233;e">
              @for (a of message.clientActions; track $index) {
                <button type="button" class="nav-chip" (click)="navigateClientAction(a)">
                  {{ a.label }}
                </button>
              }
            </div>
          }

          @if (message.sources?.length) {
            <div class="sources-line" role="note">
              <span class="sources-label">Sources (outils)</span>
              @for (s of dedupedSources; track s.toolName) {
                <span class="src-tag">{{ s.label }}@if (s.count > 1) { ×{{ s.count }}}</span>
              }
            </div>
          }

          @if (!message.content.trim() && message.isStreaming) {
            <app-typing-indicator />
          }
        }
      </div>
    </div>
  `,
  styles: [`
    .chat-message {
      display: flex;
      gap: 12px;
      padding: 12px 16px;
      animation: fadeIn 0.2s ease-out;
    }

    @keyframes fadeIn {
      from { opacity: 0; transform: translateY(4px); }
      to { opacity: 1; transform: translateY(0); }
    }

    .chat-message.user {
      flex-direction: row-reverse;
    }

    .message-avatar {
      width: 32px;
      height: 32px;
      border-radius: 50%;
      display: flex;
      align-items: center;
      justify-content: center;
      flex-shrink: 0;
      font-size: 14px;
    }

    .user .message-avatar {
      background: var(--color-primary-100, #dbeafe);
      color: var(--color-primary-600, #2563eb);
    }

    .assistant .message-avatar {
      background: var(--ai-gradient, linear-gradient(135deg, #8b5cf6 0%, #d946ef 100%));
      color: #fff;
    }

    .tool .message-avatar {
      background: var(--color-neutral-100, #f3f4f6);
      color: var(--color-neutral-600, #4b5563);
    }

    .message-body {
      max-width: min(100%, 42rem);
      min-width: 0;
    }

    .assistant-bubble-wrap {
      position: relative;
    }

    .assistant-export-root {
      margin-bottom: 4px;
    }

    .suggested-prompts {
      display: flex;
      flex-wrap: wrap;
      gap: 8px;
      margin-bottom: 10px;
    }

    .assistant-toolbar {
      display: flex;
      flex-wrap: wrap;
      justify-content: flex-end;
      gap: 6px;
      margin-bottom: 4px;
      opacity: 0;
      transition: opacity 0.15s;
    }

    .assistant-export-root:hover .assistant-toolbar,
    .assistant-export-root:focus-within .assistant-toolbar {
      opacity: 1;
    }

    .toolbar-btn {
      display: inline-flex;
      align-items: center;
      gap: 6px;
      padding: 4px 10px;
      font-size: 12px;
      font-weight: 500;
      color: var(--color-neutral-600, #4b5563);
      background: transparent;
      border: 1px solid var(--color-neutral-200, #e5e7eb);
      border-radius: 8px;
      cursor: pointer;
    }

    .toolbar-btn:hover:not(:disabled) {
      background: var(--color-neutral-50, #f9fafb);
    }

    .toolbar-btn:focus-visible {
      outline: 2px solid var(--color-primary-500, #3b82f6);
      outline-offset: 2px;
    }

    .toolbar-btn:disabled {
      opacity: 0.6;
      cursor: default;
    }

    .message-content {
      padding: 10px 14px;
      border-radius: 12px;
      font-size: 14px;
      line-height: 1.55;
      word-wrap: break-word;
    }

    .user-content {
      background: linear-gradient(135deg, var(--color-primary-600, #2563eb) 0%, var(--color-primary-700, #1d4ed8) 100%);
      color: #fff;
      border-bottom-right-radius: 4px;
      box-shadow: 0 4px 12px rgba(37, 99, 235, 0.18);
    }

    .assistant-streaming-text {
      white-space: pre-wrap;
      word-break: break-word;
    }

    .message-attachments {
      display: flex;
      flex-direction: column;
      gap: 6px;
      margin-bottom: 6px;
    }

    .assistant .message-content {
      background: var(--color-neutral-50, #f9fafb);
      border: 1px solid var(--color-neutral-200, #e5e7eb);
      border-bottom-left-radius: 4px;
    }

    :host ::ng-deep .assistant-markdown markdown {
      display: block;
    }

    :host ::ng-deep .assistant-markdown markdown pre {
      position: relative;
      margin: 8px 0;
      padding: 12px 12px 36px;
      border-radius: 8px;
      background: var(--color-neutral-900, #111827);
      color: var(--color-neutral-100, #f3f4f6);
      overflow-x: auto;
    }

    :host ::ng-deep .assistant-markdown markdown pre code {
      background: transparent;
      padding: 0;
      font-size: 13px;
    }

    :host ::ng-deep .assistant-markdown markdown .md-code-copy {
      position: absolute;
      right: 8px;
      bottom: 6px;
      padding: 4px 10px;
      font-size: 11px;
      font-weight: 500;
      color: var(--color-neutral-200, #e5e7eb);
      background: rgba(255, 255, 255, 0.12);
      border: 1px solid rgba(255, 255, 255, 0.2);
      border-radius: 6px;
      cursor: pointer;
    }

    :host ::ng-deep .assistant-markdown markdown .md-code-copy:hover {
      background: rgba(255, 255, 255, 0.2);
    }

    :host ::ng-deep .assistant-markdown markdown .md-code-copy:focus-visible {
      outline: 2px solid var(--color-primary-400, #60a5fa);
      outline-offset: 2px;
    }

    :host ::ng-deep .assistant-markdown markdown .md-table-scroll {
      display: block;
      width: 100%;
      overflow-x: auto;
      margin: 8px 0;
      -webkit-overflow-scrolling: touch;
      border-radius: 8px;
      border: 1px solid var(--color-neutral-200, #e5e7eb);
    }

    :host ::ng-deep .assistant-markdown markdown .md-table-scroll:focus-visible {
      outline: 2px solid var(--color-primary-500, #3b82f6);
      outline-offset: 2px;
    }

    :host ::ng-deep .assistant-markdown markdown table {
      width: 100%;
      border-collapse: collapse;
      margin: 0;
      font-size: 13px;
    }

    :host ::ng-deep .assistant-markdown markdown th,
    :host ::ng-deep .assistant-markdown markdown td {
      padding: 8px 10px;
      border: 1px solid var(--color-neutral-200, #e5e7eb);
      text-align: left;
    }

    :host ::ng-deep .assistant-markdown markdown th {
      background: var(--color-neutral-100, #f3f4f6);
      font-weight: 600;
    }

    :host ::ng-deep .assistant-markdown markdown h1,
    :host ::ng-deep .assistant-markdown markdown h2,
    :host ::ng-deep .assistant-markdown markdown h3 {
      margin: 12px 0 8px;
      font-weight: 600;
      color: var(--color-neutral-900, #111827);
      line-height: 1.3;
    }

    :host ::ng-deep .assistant-markdown markdown h1 { font-size: 1.25rem; }
    :host ::ng-deep .assistant-markdown markdown h2 { font-size: 1.1rem; }
    :host ::ng-deep .assistant-markdown markdown h3 { font-size: 1rem; }

    :host ::ng-deep .assistant-markdown markdown blockquote {
      margin: 8px 0;
      padding: 8px 12px;
      border-left: 3px solid var(--color-neutral-300, #d1d5db);
      color: var(--color-neutral-600, #4b5563);
      background: var(--color-neutral-50, #f9fafb);
      border-radius: 0 8px 8px 0;
    }

    :host ::ng-deep .assistant-markdown markdown .severity-critical {
      color: var(--color-danger-700, #b91c1c);
      font-weight: 700;
    }

    :host ::ng-deep .assistant-markdown markdown .severity-warning {
      color: var(--color-warning-800, #92400e);
      font-weight: 700;
    }

    :host ::ng-deep .assistant-markdown markdown .severity-ok {
      color: var(--color-success-700, #15803d);
      font-weight: 700;
    }

    :host ::ng-deep .assistant-markdown markdown hr {
      border: none;
      border-top: 1px solid var(--color-neutral-200, #e5e7eb);
      margin: 12px 0;
    }

    :host ::ng-deep .assistant-markdown markdown p { margin: 0 0 8px; }
    :host ::ng-deep .assistant-markdown markdown p:last-child { margin-bottom: 0; }

    :host ::ng-deep .assistant-markdown markdown code {
      background: var(--color-neutral-100, #f3f4f6);
      padding: 1px 4px;
      border-radius: 4px;
      font-size: 13px;
    }

    :host ::ng-deep .assistant-markdown markdown ul,
    :host ::ng-deep .assistant-markdown markdown ol {
      margin: 4px 0;
      padding-left: 20px;
    }

    :host ::ng-deep .assistant-markdown markdown strong { font-weight: 600; }

    /* Caret clignotant en fin de texte pendant le streaming */
    :host ::ng-deep .assistant-markdown.streaming markdown p:last-child::after,
    :host ::ng-deep .assistant-markdown.streaming markdown li:last-child::after,
    :host ::ng-deep .assistant-markdown.streaming markdown h1:last-child::after,
    :host ::ng-deep .assistant-markdown.streaming markdown h2:last-child::after,
    :host ::ng-deep .assistant-markdown.streaming markdown h3:last-child::after {
      content: '▍';
      display: inline-block;
      margin-left: 2px;
      color: var(--color-primary-500, #6366f1);
      animation: ft-caret-blink 1s steps(1) infinite;
    }
    @keyframes ft-caret-blink { 50% { opacity: 0; } }


    .generation-interrupted {
      display: flex;
      align-items: flex-start;
      gap: 8px;
      padding: 8px 12px;
      margin-bottom: 8px;
      border-radius: 10px;
      font-size: 13px;
      line-height: 1.45;
      color: var(--color-neutral-700, #374151);
      background: var(--color-warning-50, #fffbeb);
      border: 1px solid var(--color-warning-200, #fde68a);
    }

    .generation-interrupted i {
      margin-top: 2px;
      color: var(--color-warning-600, #d97706);
      flex-shrink: 0;
    }

    .tool-badge {
      display: inline-flex;
      align-items: center;
      gap: 6px;
      padding: 4px 10px;
      border-radius: 8px;
      font-size: 12px;
      background: var(--color-neutral-50, #f9fafb);
      color: var(--color-neutral-500, #6b7280);
      font-style: italic;
    }


    .sources-line {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: 6px;
      margin-bottom: 8px;
      font-size: 11px;
      color: var(--color-neutral-500, #6b7280);
    }

    .sources-label {
      font-weight: 600;
      margin-right: 4px;
    }

    .src-tag {
      padding: 2px 8px;
      border-radius: 6px;
      background: var(--color-neutral-100, #f3f4f6);
      color: var(--color-neutral-700, #374151);
    }

    .message-select {
      display: inline-flex;
      align-items: flex-start;
      padding-top: 6px;
      padding-right: 4px;
      cursor: pointer;
      user-select: none;
    }

    .message-select input[type='checkbox'] {
      width: 16px;
      height: 16px;
      accent-color: var(--color-primary-600, #2563eb);
      cursor: pointer;
    }

    .chat-message.selectable {
      border-radius: 12px;
      transition: background 0.15s, box-shadow 0.15s;
    }

    .chat-message.selectable:hover {
      background: var(--color-neutral-50, #f9fafb);
    }

    .chat-message.selected {
      background: var(--color-primary-50, #eff6ff);
      box-shadow: inset 2px 0 0 var(--color-primary-500, #3b82f6);
    }
  `]
})
export class ChatMessageComponent implements OnChanges {
  @Input({ required: true }) message!: ChatMessage;
  @Input() connectingToModel = false;

  /** Emitted when the user clicks the PPT toolbar button for this single message. */
  @Output() exportSingleAsPowerPoint = new EventEmitter<ChatMessage>();

  @ViewChild('mdRoot') mdRoot?: ElementRef<HTMLElement>;
  @ViewChild('exportRoot') exportRoot?: ElementRef<HTMLElement>;

  readonly MessageRole = MessageRole;
  readonly session = inject(AiChatSessionService);
  readonly selection = inject(MessageSelectionService);
  private readonly platformId = inject(PLATFORM_ID);
  private readonly router = inject(Router);
  private readonly cdr = inject(ChangeDetectorRef);
  copyInProgress = false;
  exportBusy = false;
  markdownHasTables = false;
  renderedMarkdown = '';
  displayFallbackText = '';
  /** Markdown rendu EN DIRECT pendant le streaming (fences partielles fermées temporairement). */
  streamingMarkdown = '';

  ngOnChanges(changes: SimpleChanges): void {
    const messageChange = changes['message'];
    if (!messageChange) {
      return;
    }
    if (this.message.isStreaming) {
      // Rendu markdown progressif : le backend streame des segments sûrs déjà assainis ;
      // on se contente de fermer une éventuelle fence ``` ouverte pour un rendu stable.
      this.streamingMarkdown = closePartialFences(this.message.content ?? '');
      this.cdr.markForCheck();
      return;
    }
    this.streamingMarkdown = '';
    const prev = messageChange.previousValue as ChatMessage | undefined;
    const contentChanged = prev?.content !== this.message.content;
    // Réponses livrées via `content_replace` (synthèse / fallback / fast-path) : le contenu est
    // posé pendant le streaming puis `done` bascule isStreaming sans le changer. Sans ce test,
    // refreshAssistantDisplayContent() ne tournerait jamais → renderedMarkdown/displayFallbackText
    // resteraient vides → corps blanc. On recalcule donc aussi à la finalisation du streaming.
    // On n'arrive ici que si this.message.isStreaming est faux (cf. early return ci-dessus) :
    // si le précédent état streamait, c'est que le streaming vient de se terminer.
    const streamingJustEnded = prev?.isStreaming === true;
    if (messageChange.firstChange || contentChanged || streamingJustEnded) {
      this.refreshAssistantDisplayContent();
      this.cdr.markForCheck();
    }
  }

  private refreshAssistantDisplayContent(): void {
    const raw = this.message.content ?? '';
    const state = resolveAssistantDisplayState(raw, {
      smartJsonFallback: environment.aiAssistantSmartJsonFallbackEnabled !== false,
      hasParsedDashboard: !!this.message.parsedDashboard
    });
    this.renderedMarkdown = state.renderedMarkdown;
    this.displayFallbackText = state.displayFallbackText;
  }

  canBeSelected(): boolean {
    return this.message.role === MessageRole.Assistant && !this.message.isStreaming;
  }

  isMessageSelected(): boolean {
    const id = this.session.activeConversationId();
    if (!id) return false;
    return this.selection.isSelected(id, this.message.id);
  }

  canExportSingleToPowerPoint(): boolean {
    return this.message.role === MessageRole.Assistant
      && !this.message.isStreaming
      && (this.markdownForDisplay.trim().length > 0 || !!this.message.parsedDashboard);
  }

  onSelectionToggle(event: Event): void {
    const id = this.session.activeConversationId();
    if (!id) return;
    const conversationTitle = this.session.conversations().find(c => c.id === id)?.title ?? 'Conversation';
    const preview = (this.markdownForDisplay || this.message.content || '')
      .replace(/\s+/g, ' ')
      .trim()
      .slice(0, 140);
    const checked = (event.target as HTMLInputElement)?.checked === true;
    if (checked) {
      this.selection.add({
        conversationId: id,
        messageId: this.message.id,
        conversationTitle,
        preview,
        createdAt: this.message.createdAt instanceof Date
          ? this.message.createdAt.toISOString()
          : new Date().toISOString()
      });
    } else {
      this.selection.remove(id, this.message.id);
    }
  }

  exportSingleToPowerPoint(): void {
    if (!this.canExportSingleToPowerPoint() || this.exportBusy) return;
    this.exportSingleAsPowerPoint.emit(this.message);
  }

  get markdownForDisplay(): string {
    return (
      this.renderedMarkdown
      || this.displayFallbackText
      || buildAssistantMarkdownForDisplay(this.message.content, {
        smartJsonFallback: environment.aiAssistantSmartJsonFallbackEnabled !== false
      })
    );
  }


  showAssistantActions(): boolean {
    if (this.message.isStreaming) {
      return false;
    }
    return !!(
      this.markdownForDisplay?.trim() ||
      this.message.parsedDashboard
    );
  }

  canExportMarkdownTables(): boolean {
    return !this.message.isStreaming && this.markdownHasTables;
  }

  onDashboardClosed(): void {
    this.session.dismissInlineDashboard(this.message.id);
  }

  async copyFullMessage(): Promise<void> {
    const text = this.markdownForDisplay?.trim();
    if (!text || this.copyInProgress) {
      return;
    }
    this.copyInProgress = true;
    try {
      await writeTextToClipboard(text, this.platformId);
    } finally {
      this.copyInProgress = false;
    }
  }

  async copyAsTsv(): Promise<void> {
    const host = this.mdRoot?.nativeElement;
    const text = this.markdownForDisplay?.trim();
    if (!text || this.copyInProgress) {
      return;
    }
    this.copyInProgress = true;
    try {
      if (host) {
        const csv = htmlTablesToCsv(host);
        if (csv) {
          const tsv = csv
            .split(/\r?\n/)
            .map(line => (line.startsWith('#') ? line : line.split(';').join('\t')))
            .join('\n');
          await writeTextToClipboard(tsv, this.platformId);
          return;
        }
      }
      await writeTextToClipboard(text, this.platformId);
    } finally {
      this.copyInProgress = false;
    }
  }

  exportMarkdownTablesCsv(): void {
    if (!isPlatformBrowser(this.platformId)) {
      return;
    }
    const host = this.mdRoot?.nativeElement;
    if (!host) {
      return;
    }
    const csv = htmlTablesToCsv(host);
    if (!csv) {
      return;
    }
    downloadCsv(`tableaux-${this.slugifyShort(this.message.id)}`, csv);
  }

  async exportDashboardExcel(): Promise<void> {
    const d = this.message.parsedDashboard;
    if (!d || this.exportBusy) {
      return;
    }
    this.exportBusy = true;
    try {
      await exportDashboardToXlsx(d, `export-${this.slugifyShort(d.title)}`);
    } finally {
      this.exportBusy = false;
    }
  }

  async exportAssistantPdf(): Promise<void> {
    const el = this.exportRoot?.nativeElement;
    if (!el || this.exportBusy) {
      return;
    }
    this.exportBusy = true;
    try {
      await exportElementToPdf(el, `assistant-${this.slugifyShort(this.message.id)}`);
    } finally {
      this.exportBusy = false;
    }
  }

  shareByEmail(): void {
    const body = encodeURIComponent(this.markdownForDisplay?.trim() ?? '');
    const subject = encodeURIComponent(`${environment.appName} - Assistant IA`);
    window.location.href = `mailto:?subject=${subject}&body=${body}`;
  }

  async copyConversationLink(): Promise<void> {
    const id = this.session.activeConversationId();
    if (!id || !isPlatformBrowser(this.platformId)) {
      return;
    }
    const url = `${window.location.origin}/ai-assistant?conversationId=${encodeURIComponent(id)}`;
    await writeTextToClipboard(url, this.platformId);
  }

  suggestChartFollowUp(): void {
    // Formulation volontairement sans « ajouter » (déclencheur de l'heuristique de mutation backend)
    // et avec « graphique » + generate_dashboard_config → route vers l'intent Chart (lecture seule).
    this.session.sendMessage(
      "A partir des chiffres ci-dessus, genere un graphique pertinent (barres, lignes ou camembert) avec generate_dashboard_config."
    );
  }

  private slugifyShort(s: string): string {
    return (
      s
        .normalize('NFD')
        .replace(/\p{M}/gu, '')
        .replace(/[^\w\s-]/g, '')
        .trim()
        .replace(/\s+/g, '-')
        .slice(0, 48) || 'export'
    );
  }

  onMarkdownRendered(): void {
    if (!isPlatformBrowser(this.platformId)) {
      return;
    }
    queueMicrotask(() => {
      const host = this.mdRoot?.nativeElement;
      this.markdownHasTables = !!host?.querySelector('markdown table');
      this.wrapMarkdownTables();
      this.attachCodeCopyButtons();
    });
  }

  private wrapMarkdownTables(): void {
    const host = this.mdRoot?.nativeElement;
    if (!host) {
      return;
    }
    const tables = host.querySelectorAll<HTMLTableElement>(
      'markdown table:not(.md-table-scroll table)'
    );
    tables.forEach(table => {
      if (table.closest('.md-table-scroll')) {
        return;
      }
      const wrap = document.createElement('div');
      wrap.className = 'md-table-scroll';
      wrap.tabIndex = 0;
      wrap.setAttribute('role', 'region');
      wrap.setAttribute('aria-label', 'Tableau defilable');
      table.parentNode?.insertBefore(wrap, table);
      wrap.appendChild(table);
    });
  }

  private attachCodeCopyButtons(): void {
    const host = this.mdRoot?.nativeElement;
    if (!host) {
      return;
    }
    const pres = host.querySelectorAll<HTMLPreElement>('markdown pre:not([data-ft-code-copy])');
    pres.forEach(pre => {
      pre.setAttribute('data-ft-code-copy', '1');
      const code = pre.querySelector('code');
      const text = code?.textContent ?? '';
      const btn = document.createElement('button');
      btn.type = 'button';
      btn.className = 'md-code-copy';
      btn.setAttribute('aria-label', 'Copier le code');
      btn.textContent = 'Copier';
      btn.addEventListener('click', () => {
        void writeTextToClipboard(text, this.platformId);
      });
      pre.appendChild(btn);
    });
  }

  getToolLabel(name: string): string {
    return getAiToolDisplayLabel(name);
  }

  /** Chips sources agrégées par outil (« Prévision du CA ×3 » au lieu de 3 chips identiques). */
  get dedupedSources(): DedupedToolSource[] {
    return dedupeToolSources(this.message.sources);
  }

  navigateClientAction(action: ClientNavAction): void {
    let url = action.route.startsWith('/') ? action.route : `/${action.route}`;
    if (action.queryParams && Object.keys(action.queryParams).length > 0) {
      const q = new URLSearchParams(action.queryParams).toString();
      url += url.includes('?') ? `&${q}` : `?${q}`;
    }
    void this.router.navigateByUrl(url);
  }
}

