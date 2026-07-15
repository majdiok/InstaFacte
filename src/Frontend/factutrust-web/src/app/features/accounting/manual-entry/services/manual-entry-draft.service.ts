import { Injectable } from '@angular/core';

export interface ManualEntryDraftLine {
  accountNumber: string;
  lineLabel: string;
  debit: number | null;
  credit: number | null;
}

export interface ManualEntryDraft {
  journalCode: string;
  entryDate: string;
  entryLabel: string;
  lines: ManualEntryDraftLine[];
  savedAt: string;
}

/**
 * Persists a single in-progress manual journal entry as JSON in localStorage.
 *
 * - Storage is per-browser (not per-user); the draft is namespaced by a fixed key.
 * - The draft is cleared after a successful submission (called from the host component).
 * - Cleared automatically when the saved payload is older than 7 days to prevent stale drafts.
 */
@Injectable({ providedIn: 'root' })
export class ManualEntryDraftService {
  private static readonly STORAGE_KEY = 'factutrust:manual-entry:draft:v1';
  private static readonly TTL_DAYS = 7;

  save(draft: Omit<ManualEntryDraft, 'savedAt'>): void {
    if (!this.isBrowser()) return;
    try {
      const payload: ManualEntryDraft = { ...draft, savedAt: new Date().toISOString() };
      window.localStorage.setItem(ManualEntryDraftService.STORAGE_KEY, JSON.stringify(payload));
    } catch {
      // Quota exceeded or unavailable — silently ignore (auto-save is best-effort).
    }
  }

  load(): ManualEntryDraft | null {
    if (!this.isBrowser()) return null;
    try {
      const raw = window.localStorage.getItem(ManualEntryDraftService.STORAGE_KEY);
      if (!raw) return null;
      const parsed = JSON.parse(raw) as ManualEntryDraft;
      if (!parsed || !parsed.savedAt || !Array.isArray(parsed.lines)) {
        this.clear();
        return null;
      }
      // Drop expired drafts
      const savedAt = new Date(parsed.savedAt);
      const ageMs = Date.now() - savedAt.getTime();
      const ttlMs = ManualEntryDraftService.TTL_DAYS * 24 * 60 * 60 * 1000;
      if (Number.isNaN(savedAt.getTime()) || ageMs > ttlMs) {
        this.clear();
        return null;
      }
      return parsed;
    } catch {
      this.clear();
      return null;
    }
  }

  clear(): void {
    if (!this.isBrowser()) return;
    try {
      window.localStorage.removeItem(ManualEntryDraftService.STORAGE_KEY);
    } catch {
      // Ignore — clearing a draft is best-effort.
    }
  }

  /**
   * Returns true when the draft contains at least one piece of user input
   * (label or any line with account/amount). Empty drafts are treated as "no draft".
   */
  isMeaningful(draft: ManualEntryDraft): boolean {
    if (draft.entryLabel.trim().length > 0) return true;
    return draft.lines.some(l =>
      l.accountNumber.trim().length > 0 ||
      l.lineLabel.trim().length > 0 ||
      (l.debit ?? 0) > 0 ||
      (l.credit ?? 0) > 0
    );
  }

  private isBrowser(): boolean {
    return typeof window !== 'undefined' && typeof window.localStorage !== 'undefined';
  }
}
