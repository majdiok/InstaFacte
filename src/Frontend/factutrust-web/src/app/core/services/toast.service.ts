import { Injectable } from '@angular/core';
import { BehaviorSubject } from 'rxjs';

export interface ToastMessage {
  id?: number;
  key?: string;
  severity?: 'success' | 'info' | 'warn' | 'error';
  summary?: string;
  detail?: string;
  life?: number;
  closable?: boolean;
}

@Injectable({ providedIn: 'root' })
export class ToastService {
  private toasts$ = new BehaviorSubject<ToastMessage[]>([]);
  private idCounter = 0;

  get messages() {
    return this.toasts$.asObservable();
  }

  get messagesValue(): ToastMessage[] {
    return this.toasts$.value;
  }

  add(message: ToastMessage): void {
    const id = ++this.idCounter;
    const msg: ToastMessage = { ...message, id };
    this.toasts$.next([...this.toasts$.value, msg]);
    const life = message.life ?? 5000;
    if (life > 0) {
      setTimeout(() => this.remove(id), life);
    }
  }

  remove(id: number): void {
    this.toasts$.next(this.toasts$.value.filter(m => m.id !== id));
  }

  clear(): void {
    this.toasts$.next([]);
  }
}
