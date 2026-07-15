import { Injectable, signal } from '@angular/core';
import { PLATFORM_ID, inject } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';

const STORAGE_KEY = 'pos-sound-enabled';

/**
 * Service de feedback sonore pour le POS.
 * Utilise Web Audio API pour générer des bips sans fichiers audio.
 */
@Injectable({
  providedIn: 'root'
})
export class PosAudioService {
  private readonly platformId = inject(PLATFORM_ID);
  private readonly _isSoundEnabled = signal(this.loadStoredPreference());

  readonly isSoundEnabled = this._isSoundEnabled.asReadonly();

  private loadStoredPreference(): boolean {
    if (!isPlatformBrowser(this.platformId)) return true;
    try {
      const stored = localStorage.getItem(STORAGE_KEY);
      return stored === null ? true : stored === 'true';
    } catch {
      return true;
    }
  }

  private persistPreference(): void {
    if (!isPlatformBrowser(this.platformId)) return;
    try {
      localStorage.setItem(STORAGE_KEY, String(this._isSoundEnabled()));
    } catch {
      // ignore
    }
  }

  toggleSound(): void {
    this._isSoundEnabled.update(v => !v);
    this.persistPreference();
  }

  beepSuccess(): void {
    if (!this._isSoundEnabled()) return;
    this.playTone(880, 0.08, 'sine');
  }

  beepError(): void {
    if (!this._isSoundEnabled()) return;
    this.playTone(220, 0.15, 'sawtooth');
  }

  beepScan(): void {
    if (!this._isSoundEnabled()) return;
    this.playTone(660, 0.05, 'square');
  }

  private playTone(frequency: number, durationSeconds: number, type: OscillatorType): void {
    if (!isPlatformBrowser(this.platformId)) return;
    try {
      const ctx = new (window.AudioContext ?? (window as unknown as { webkitAudioContext?: new () => AudioContext }).webkitAudioContext)();
      const osc = ctx.createOscillator();
      const gain = ctx.createGain();
      osc.connect(gain);
      gain.connect(ctx.destination);
      osc.type = type;
      osc.frequency.value = frequency;
      gain.gain.setValueAtTime(0.15, ctx.currentTime);
      gain.gain.exponentialRampToValueAtTime(0.001, ctx.currentTime + durationSeconds);
      osc.start(ctx.currentTime);
      osc.stop(ctx.currentTime + durationSeconds);
    } catch {
      // Web Audio API non supporté ou contexte bloqué
    }
  }
}
