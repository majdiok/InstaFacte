import { Injectable, inject, signal, OnDestroy } from '@angular/core';
import { Subject } from 'rxjs';

export type VoiceCommandType = 'undo_last_line';

@Injectable({
  providedIn: 'root'
})
export class PosVoiceCommandService implements OnDestroy {
  readonly listening = signal(false);
  private recognition: { start(): void; stop(): void } | null = null;
  private timeoutId: ReturnType<typeof setTimeout> | null = null;
  private readonly commandSubject = new Subject<VoiceCommandType>();

  readonly onCommand = this.commandSubject.asObservable();

  private static isSupported(): boolean {
    if (typeof window === 'undefined') return false;
    const w = window as unknown as { SpeechRecognition?: unknown; webkitSpeechRecognition?: unknown };
    return !!(w.SpeechRecognition ?? w.webkitSpeechRecognition);
  }

  isSupported(): boolean {
    return PosVoiceCommandService.isSupported();
  }

  startListeningForUndo(): void {
    if (!PosVoiceCommandService.isSupported() || this.listening()) return;
    const w = window as unknown as { SpeechRecognition?: new () => unknown; webkitSpeechRecognition?: new () => unknown };
    const API = w.SpeechRecognition ?? w.webkitSpeechRecognition;
    if (!API) return;
    const rec = new API() as {
      continuous: boolean;
      interimResults: boolean;
      lang: string;
      onresult: (e: { results: { 0?: { 0?: { transcript?: string } } } }) => void;
      onerror: () => void;
      onend: () => void;
      start(): void;
      stop(): void;
    };
    rec.continuous = false;
    rec.interimResults = false;
    rec.lang = 'fr-FR';
    rec.onresult = (event: unknown) => {
      const e = event as { results: { [i: number]: { [j: number]: { transcript?: string } } } };
      const transcript = (e.results?.[0]?.[0]?.transcript ?? '').trim().toLowerCase();
      if (/annuler\s*(la\s*)?derni[eè]re\s*ligne|undo|retirer\s*derni[eè]re/.test(transcript)) {
        this.commandSubject.next('undo_last_line');
      }
      this.stop();
    };
    rec.onerror = () => this.stop();
    rec.onend = () => this.stop();
    this.recognition = rec;
    this.listening.set(true);
    rec.start();
    this.timeoutId = setTimeout(() => this.stop(), 6000);
  }

  stop(): void {
    if (this.timeoutId) {
      clearTimeout(this.timeoutId);
      this.timeoutId = null;
    }
    if (this.recognition) {
      try {
        this.recognition.stop();
      } catch {
        // ignore
      }
      this.recognition = null;
    }
    this.listening.set(false);
  }

  ngOnDestroy(): void {
    this.stop();
  }
}
