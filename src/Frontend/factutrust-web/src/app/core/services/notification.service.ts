import { Injectable, effect, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Subscription, timer } from 'rxjs';
import { environment } from '@environments/environment';
import { ApiResponse, AuthService } from './auth.service';
import { FirmBadgeService } from './firm-badge.service';

export interface AppNotification {
  id: string;
  type: number;
  title: string;
  body: string;
  linkUrl?: string;
  createdAt: string;
  readAt?: string;
}

export interface NotificationList {
  items: AppNotification[];
  totalCount: number;
  unreadCount: number;
  page: number;
  pageSize: number;
}

const POLL_INTERVAL_MS = 60_000;

/**
 * Notifications in-app (cloche du header) : compteur non-lues + dernières
 * notifications, rafraîchis par polling léger tant que l'utilisateur est
 * connecté. Le tick rafraîchit aussi le badge invitations du cabinet.
 */
@Injectable({ providedIn: 'root' })
export class NotificationService {
  private readonly http = inject(HttpClient);
  private readonly auth = inject(AuthService);
  private readonly firmBadge = inject(FirmBadgeService);
  private readonly baseUrl = `${environment.apiUrl}/notifications`;

  readonly unreadCount = signal(0);
  readonly latest = signal<AppNotification[]>([]);

  private pollSub: Subscription | null = null;

  constructor() {
    effect(() => {
      if (this.auth.isAuthenticated()) {
        this.startPolling();
      } else {
        this.stopPolling();
        this.unreadCount.set(0);
        this.latest.set([]);
      }
    });
  }

  refresh(): void {
    this.http
      .get<ApiResponse<NotificationList>>(this.baseUrl, { params: { page: 1, pageSize: 10 } })
      .subscribe({
        next: r => {
          if (r.success) {
            this.latest.set(r.data.items);
            this.unreadCount.set(r.data.unreadCount);
          }
        },
        error: () => {
          /* silencieux : le prochain tick réessaiera */
        }
      });
  }

  markRead(id: string): void {
    this.http.post<ApiResponse<unknown>>(`${this.baseUrl}/${id}/read`, {}).subscribe({
      next: () => this.refresh(),
      error: () => {}
    });
  }

  markAllRead(): void {
    this.http.post<ApiResponse<unknown>>(`${this.baseUrl}/read-all`, {}).subscribe({
      next: () => this.refresh(),
      error: () => {}
    });
  }

  private startPolling(): void {
    if (this.pollSub) return;
    this.pollSub = timer(0, POLL_INTERVAL_MS).subscribe(() => {
      this.refresh();
      this.firmBadge.refresh();
    });
  }

  private stopPolling(): void {
    this.pollSub?.unsubscribe();
    this.pollSub = null;
  }
}
