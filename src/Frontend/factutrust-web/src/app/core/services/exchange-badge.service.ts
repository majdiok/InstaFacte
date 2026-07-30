import { Injectable, inject, signal } from '@angular/core';
import { AuthService } from './auth.service';
import { ExchangeService } from './exchange.service';
import { environment } from '@environments/environment';

@Injectable({ providedIn: 'root' })
export class ExchangeBadgeService {
  private readonly auth = inject(AuthService);
  private readonly exchange = inject(ExchangeService);

  readonly unreadCount = signal(0);
  readonly openRequests = signal(0);

  refresh(): void {
    if (!environment.accountingFirmsEnabled || !this.auth.isAuthenticated()) {
      this.unreadCount.set(0);
      this.openRequests.set(0);
      return;
    }

    const user = this.auth.user();
    const role = user?.role;
    const allowed =
      role === 'Administrator' || role === 'FirmManager' || role === 'FirmAccountant';
    if (!allowed || this.auth.isDelegatedMode()) {
      this.unreadCount.set(0);
      this.openRequests.set(0);
      return;
    }

    this.exchange.getUnreadSummary().subscribe({
      next: r => {
        if (r.success) {
          this.unreadCount.set(r.data.totalUnreadMessages);
          this.openRequests.set(r.data.openRequests);
        }
      },
      error: () => {
        /* silent */
      }
    });
  }

  invalidate(): void {
    this.refresh();
  }
}
