import { Injectable, computed, signal } from '@angular/core';

export interface SelectedAssistantMessage {
  conversationId: string;
  messageId: string;
  /** Snapshot of the assistant message title (auto-derived heading or fallback). */
  preview: string;
  /** ISO timestamp at the moment of selection — used for display in the picker. */
  createdAt: string;
  /** Convenience pre-flight info to avoid re-fetching the conversation in the dialog. */
  conversationTitle: string;
}

/**
 * Maximum number of assistant responses a user can include in a single PowerPoint deck.
 * Mirrors the backend `PowerPointDeckLimits.MaxResponses` value.
 */
export const MESSAGE_SELECTION_MAX = 50;

/**
 * Application-scoped, Signal-based store of the messages the user has selected for export.
 *
 * The service is intentionally framework-agnostic and Pinia-style: components subscribe to
 * the readonly signals (`selection`, `count`, `isSelectionMode`) and call mutation methods
 * (`toggle`, `clear`, …). Cross-conversation selection is supported because each entry is
 * keyed by `${conversationId}::${messageId}`.
 */
@Injectable({ providedIn: 'root' })
export class MessageSelectionService {
  private readonly _selectionMode = signal(false);
  private readonly _selections = signal<ReadonlyMap<string, SelectedAssistantMessage>>(new Map());

  /** Whether the chat is currently in multi-select mode. */
  readonly isSelectionMode = this._selectionMode.asReadonly();

  /** Immutable view of the current selection (preserves insertion order). */
  readonly selections = computed(() => Array.from(this._selections().values()));

  /** Number of currently selected messages (cached as computed). */
  readonly count = computed(() => this._selections().size);

  /** True when at least one message is selected. */
  readonly hasSelection = computed(() => this._selections().size > 0);

  /** True when the user has reached the hard limit (UI disables further selection). */
  readonly atLimit = computed(() => this._selections().size >= MESSAGE_SELECTION_MAX);

  /** Stable key used by the underlying Map. */
  private static key(conversationId: string, messageId: string): string {
    return `${conversationId}::${messageId}`;
  }

  enterSelectionMode(): void {
    if (this._selectionMode()) return;
    this._selectionMode.set(true);
  }

  exitSelectionMode(): void {
    if (!this._selectionMode() && this._selections().size === 0) return;
    this._selectionMode.set(false);
    this._selections.set(new Map());
  }

  toggleSelectionMode(): void {
    if (this._selectionMode()) {
      this.exitSelectionMode();
    } else {
      this.enterSelectionMode();
    }
  }

  isSelected(conversationId: string, messageId: string): boolean {
    return this._selections().has(MessageSelectionService.key(conversationId, messageId));
  }

  /**
   * Adds or removes a message from the selection. Returns the resulting selection size so the
   * caller can react (e.g. show a "limit reached" toast).
   */
  toggle(message: SelectedAssistantMessage): number {
    const key = MessageSelectionService.key(message.conversationId, message.messageId);
    const current = new Map(this._selections());
    if (current.has(key)) {
      current.delete(key);
    } else {
      if (current.size >= MESSAGE_SELECTION_MAX) {
        return current.size;
      }
      current.set(key, message);
    }
    this._selections.set(current);
    return current.size;
  }

  /** Adds a message without toggling — useful for "Select all" actions. */
  add(message: SelectedAssistantMessage): void {
    const key = MessageSelectionService.key(message.conversationId, message.messageId);
    const current = new Map(this._selections());
    if (current.has(key)) return;
    if (current.size >= MESSAGE_SELECTION_MAX) return;
    current.set(key, message);
    this._selections.set(current);
  }

  remove(conversationId: string, messageId: string): void {
    const key = MessageSelectionService.key(conversationId, messageId);
    if (!this._selections().has(key)) return;
    const current = new Map(this._selections());
    current.delete(key);
    this._selections.set(current);
  }

  clear(): void {
    if (this._selections().size === 0) return;
    this._selections.set(new Map());
  }

  /**
   * Updates selection entries when a local (client) message id is replaced by the server id
   * after SSE persistence reconciliation.
   */
  remapMessageId(oldMessageId: string, newMessageId: string): void {
    if (!oldMessageId || !newMessageId || oldMessageId === newMessageId) {
      return;
    }
    const current = this._selections();
    let changed = false;
    const next = new Map(current);
    for (const [key, entry] of current) {
      if (entry.messageId !== oldMessageId) {
        continue;
      }
      next.delete(key);
      next.set(MessageSelectionService.key(entry.conversationId, newMessageId), {
        ...entry,
        messageId: newMessageId
      });
      changed = true;
    }
    if (changed) {
      this._selections.set(next);
    }
  }

  /** Replaces the order of selected messages. The provided list must match current selection. */
  reorder(orderedKeys: ReadonlyArray<{ conversationId: string; messageId: string }>): void {
    const current = this._selections();
    if (orderedKeys.length !== current.size) return;
    const next = new Map<string, SelectedAssistantMessage>();
    for (const k of orderedKeys) {
      const key = MessageSelectionService.key(k.conversationId, k.messageId);
      const entry = current.get(key);
      if (!entry) return;
      next.set(key, entry);
    }
    this._selections.set(next);
  }
}
