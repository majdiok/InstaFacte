import { Injectable, signal } from '@angular/core';

const CHANNEL_NAME = 'pos-customer-display';

export interface CustomerDisplayState {
  lines: { designation: string; quantity: number; totalTTC: number }[];
  totalTTC: number;
  clientName: string;
}

@Injectable({
  providedIn: 'root'
})
export class PosDualScreenService {
  private channel: BroadcastChannel | null = null;
  private win: Window | null = null;
  private closedCheckInterval: ReturnType<typeof setInterval> | null = null;

  readonly isOpen = signal(false);

  open(): void {
    if (this.win && !this.win.closed) return;
    const url = `${window.location.origin}/pos/customer-display`;
    this.win = window.open(url, 'pos-customer-display', 'width=800,height=600,menubar=no,toolbar=no');
    this.channel = new BroadcastChannel(CHANNEL_NAME);
    this.isOpen.set(true);
    this.startClosedCheck();
  }

  close(): void {
    this.stopClosedCheck();
    if (this.win && !this.win.closed) {
      this.win.close();
    }
    this.win = null;
    this.channel?.close();
    this.channel = null;
    this.isOpen.set(false);
  }

  broadcast(state: CustomerDisplayState): void {
    if (this.win?.closed) {
      this.close();
      return;
    }
    if (this.channel) {
      this.channel.postMessage(state);
    }
  }

  private startClosedCheck(): void {
    this.stopClosedCheck();
    this.closedCheckInterval = setInterval(() => {
      if (this.win?.closed) {
        this.close();
      }
    }, 1000);
  }

  private stopClosedCheck(): void {
    if (this.closedCheckInterval) {
      clearInterval(this.closedCheckInterval);
      this.closedCheckInterval = null;
    }
  }
}
