import {
  ChangeDetectionStrategy,
  Component,
  OnDestroy,
  OnInit,
  computed,
  inject,
  signal
} from '@angular/core';
import { DecimalPipe } from '@angular/common';
import { ButtonModule } from 'primeng/button';
import { TooltipModule } from 'primeng/tooltip';
import { MessageService } from 'primeng/api';
import { environment } from '@environments/environment';
import { PlatformOpsService } from '@core/services/platform-ops.service';
import { PlatformPermissionsService } from '@core/services/platform-permissions.service';
import type { OpsHealthDto } from '@core/models/platform.models';
import { FtPageHeaderComponent } from '@core/ui/page-header/ft-page-header.component';
import { FtBadgeComponent } from '@core/ui/badge/ft-badge.component';
import { FtKpiCardComponent } from '@core/ui/kpi-card/ft-kpi-card.component';
import { FtSkeletonComponent } from '@core/ui/skeleton/ft-skeleton.component';
import { FtEmptyStateComponent } from '@core/ui/empty-state/ft-empty-state.component';
import { PlatformRole } from '@core/models/platform.models';
import type { FtTone } from '@core/ui/badge/ft-badge.component';

/**
 * Lot B5 — Page `/ops/health` : santé runtime du backend.
 *
 * - Statut global health (synthèse).
 * - Détail par check (master-db, ef-master, …).
 * - Statistiques Hangfire (serveurs, queues, jobs réussis/échoués/programmés).
 * - Lien direct vers le dashboard `/hangfire` côté API (super-admin uniquement).
 *
 * Auto-refresh toutes les 15 s pour suivre l'état en temps réel.
 */
@Component({
  selector: 'app-platform-ops-health-page',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    DecimalPipe,
    ButtonModule,
    TooltipModule,
    FtPageHeaderComponent,
    FtBadgeComponent,
    FtKpiCardComponent,
    FtSkeletonComponent,
    FtEmptyStateComponent
  ],
  template: `
    <ft-page-header
      title="Santé du système"
      subtitle="État runtime du backend, vérification des dépendances (BD, Hangfire) et raccourci vers le dashboard des jobs.">
      <ng-container ftActions>
        <p-button
          label="Rafraîchir"
          icon="pi pi-refresh"
          [outlined]="true"
          [disabled]="loading()"
          (onClick)="load()" />
        @if (canSeeHangfire()) {
          <p-button
            label="Dashboard Hangfire"
            icon="pi pi-external-link"
            severity="primary"
            (onClick)="openHangfireDashboard()"
            pTooltip="Ouvre /hangfire dans un nouvel onglet (admin uniquement)"
            tooltipPosition="bottom" />
        }
      </ng-container>
    </ft-page-header>

    @if (loading()) {
      <div class="loading-stack">
        <ft-skeleton shape="rect" width="100%" height="6rem" />
        <ft-skeleton shape="line" />
        <ft-skeleton shape="line" />
        <ft-skeleton shape="rect" width="100%" height="10rem" />
      </div>
    } @else if (!data()) {
      <ft-empty-state
        variant="error"
        title="Impossible de récupérer l'état"
        description="L'API n'a pas répondu. Réessayez ou consultez les logs serveur."
      />
    } @else {
      <!-- Statut global -->
      <article class="status-banner" [class]="statusClass()">
        <div class="status-banner__icon"><i class="pi pi-heart-fill"></i></div>
        <div class="status-banner__body">
          <h2>{{ statusTitle() }}</h2>
          <p>
            {{ data()!.checks.length }} vérification(s) en
            <strong>{{ data()!.totalDurationMs | number: '1.0-0' }} ms</strong>.
            Dernière mise à jour : {{ lastUpdated() }}.
          </p>
        </div>
        <ft-badge [tone]="statusTone()" [withDot]="true">{{ data()!.status }}</ft-badge>
      </article>

      <!-- Health checks -->
      <section class="grid">
        @for (check of data()!.checks; track check.name) {
          <article class="check-card" [class]="checkCardClass(check.status)">
            <header>
              <h3>{{ check.name }}</h3>
              <ft-badge [tone]="resolveCheckTone(check.status)" [withDot]="true" size="sm">
                {{ check.status }}
              </ft-badge>
            </header>
            <p class="check-card__duration">
              <i class="pi pi-clock"></i>
              {{ check.durationMs | number: '1.0-0' }} ms
            </p>
            @if (check.description) {
              <p class="check-card__desc">{{ check.description }}</p>
            }
            @if (check.tags.length > 0) {
              <div class="tags">
                @for (tag of check.tags; track tag) {
                  <ft-badge tone="neutral" size="sm">{{ tag }}</ft-badge>
                }
              </div>
            }
          </article>
        }
      </section>

      <!-- Hangfire stats -->
      @if (data()!.hangfire; as hf) {
        <section class="hf-section">
          <h2>Background jobs (Hangfire)</h2>
          <div class="kpi-row kpi-row--dense">
            <ft-kpi-card
              label="Serveurs en ligne"
              [value]="hf.serversOnline"
              tone="info"
              icon="pi pi-server" />
            <ft-kpi-card
              label="En attente"
              [value]="hf.enqueued"
              [tone]="hf.enqueued > 100 ? 'warning' : 'neutral'"
              icon="pi pi-list" />
            <ft-kpi-card
              label="Programmés"
              [value]="hf.scheduled"
              tone="info"
              icon="pi pi-calendar" />
            <ft-kpi-card
              label="En cours"
              [value]="hf.processing"
              tone="accent"
              icon="pi pi-spin pi-cog" />
            <ft-kpi-card
              label="Réussis"
              [value]="hf.succeeded"
              tone="success"
              icon="pi pi-check-circle" />
            <ft-kpi-card
              label="Échoués"
              [value]="hf.failed"
              [tone]="hf.failed > 0 ? 'danger' : 'success'"
              icon="pi pi-exclamation-triangle" />
            <ft-kpi-card
              label="Récurrents"
              [value]="hf.recurring"
              tone="neutral"
              icon="pi pi-history" />
          </div>
        </section>
      } @else {
        <p class="muted">Hangfire n'est pas (encore) configuré ou indisponible.</p>
      }
    }
  `,
  styles: [
    `
      :host {
        display: block;
      }

      .loading-stack {
        display: flex;
        flex-direction: column;
        gap: var(--gap-md);
      }

      .status-banner {
        display: flex;
        align-items: center;
        gap: var(--gap-md);
        padding: var(--gap-md) var(--gap-lg);
        border-radius: var(--ft-radius-lg);
        margin-bottom: var(--gap-section);
        background: var(--ft-success-surface);
        border: 1px solid var(--ft-success-border);
        color: var(--ft-text);
      }

      .status-banner.degraded {
        background: var(--ft-warning-surface);
        border-color: var(--ft-warning-border);
      }

      .status-banner.unhealthy {
        background: var(--ft-danger-surface);
        border-color: var(--ft-danger-border);
      }

      .status-banner__icon {
        font-size: 1.6rem;
      }

      .status-banner.healthy .status-banner__icon {
        color: var(--ft-success-text);
      }

      .status-banner.degraded .status-banner__icon {
        color: var(--ft-warning-text);
      }

      .status-banner.unhealthy .status-banner__icon {
        color: var(--ft-danger-text);
      }

      .status-banner__body {
        flex: 1;
      }

      .status-banner__body h2 {
        margin: 0 0 0.2rem;
        font-size: 1.05rem;
      }

      .status-banner__body p {
        margin: 0;
        color: var(--ft-text-muted);
        font-size: 0.85rem;
      }

      .grid {
        display: grid;
        grid-template-columns: repeat(auto-fit, minmax(16rem, 1fr));
        gap: var(--gap-md);
        margin-bottom: var(--gap-section);
      }

      .check-card {
        background: var(--ft-surface);
        border: 1px solid var(--ft-border);
        border-radius: var(--ft-radius);
        padding: var(--gap-md);
        display: flex;
        flex-direction: column;
        gap: 0.4rem;
      }

      .check-card.healthy { border-left: 3px solid var(--ft-success-text); }
      .check-card.degraded { border-left: 3px solid var(--ft-warning-text); }
      .check-card.unhealthy { border-left: 3px solid var(--ft-danger-text); }

      .check-card header {
        display: flex;
        align-items: center;
        justify-content: space-between;
        gap: 0.5rem;
      }

      .check-card h3 {
        margin: 0;
        font-size: 0.92rem;
        color: var(--ft-text);
        font-family: ui-monospace, SFMono-Regular, monospace;
      }

      .check-card__duration {
        margin: 0;
        font-size: 0.78rem;
        color: var(--ft-text-muted);
        display: flex;
        gap: 0.4rem;
        align-items: center;
      }

      .check-card__desc {
        margin: 0;
        font-size: 0.82rem;
        color: var(--ft-text-muted);
        line-height: 1.4;
      }

      .tags {
        display: flex;
        flex-wrap: wrap;
        gap: 0.3rem;
      }

      .hf-section h2 {
        margin: 0 0 var(--gap-md);
        font-size: 1.05rem;
        color: var(--ft-text);
      }

      .muted {
        color: var(--ft-text-muted);
      }
    `
  ]
})
export class PlatformOpsHealthPageComponent implements OnInit, OnDestroy {
  private readonly api = inject(PlatformOpsService);
  private readonly permissions = inject(PlatformPermissionsService);
  private readonly toast = inject(MessageService);

  readonly data = signal<OpsHealthDto | null>(null);
  readonly loading = signal(false);
  readonly lastFetchAt = signal<Date | null>(null);

  private refreshTimer: ReturnType<typeof setInterval> | null = null;

  readonly canSeeHangfire = computed(
    () => this.permissions.hasRole(PlatformRole.PlatformAdmin)
  );

  readonly statusClass = computed(() => {
    const s = this.data()?.status?.toLowerCase() ?? 'unknown';
    if (s === 'healthy') return 'healthy';
    if (s === 'degraded') return 'degraded';
    if (s === 'unhealthy') return 'unhealthy';
    return 'unhealthy';
  });

  readonly statusTone = computed<FtTone>(() => {
    const s = this.data()?.status?.toLowerCase();
    if (s === 'healthy') return 'success';
    if (s === 'degraded') return 'warning';
    return 'danger';
  });

  readonly statusTitle = computed(() => {
    const s = this.data()?.status?.toLowerCase();
    if (s === 'healthy') return 'Tous les services sont opérationnels';
    if (s === 'degraded') return 'Service partiellement dégradé';
    return 'Service indisponible';
  });

  readonly lastUpdated = computed(() => {
    const at = this.lastFetchAt();
    return at ? at.toLocaleTimeString('fr-TN', { hour: '2-digit', minute: '2-digit', second: '2-digit' }) : '—';
  });

  ngOnInit(): void {
    this.load();
    this.refreshTimer = setInterval(() => this.load(true), 15_000);
  }

  ngOnDestroy(): void {
    if (this.refreshTimer) {
      clearInterval(this.refreshTimer);
      this.refreshTimer = null;
    }
  }

  load(silent = false): void {
    if (!silent) this.loading.set(true);
    this.api.health().subscribe({
      next: (res) => {
        this.loading.set(false);
        if (res.success && res.data) {
          this.data.set(res.data);
          this.lastFetchAt.set(new Date());
        }
      },
      error: () => {
        this.loading.set(false);
        if (!silent) {
          this.toast.add({
            severity: 'error',
            summary: 'Erreur',
            detail: 'Impossible de contacter /api/platform/ops/health'
          });
        }
      }
    });
  }

  resolveCheckTone(status: string): FtTone {
    const s = status?.toLowerCase();
    if (s === 'healthy') return 'success';
    if (s === 'degraded') return 'warning';
    return 'danger';
  }

  checkCardClass(status: string): string {
    const s = status?.toLowerCase();
    if (s === 'healthy') return 'healthy';
    if (s === 'degraded') return 'degraded';
    return 'unhealthy';
  }

  /**
   * Ouvre le dashboard Hangfire dans un nouvel onglet.
   * URL = environment.apiUrl SANS le suffixe `/api` + `/hangfire`.
   */
  openHangfireDashboard(): void {
    const apiUrl = environment.apiUrl;
    // L'URL des endpoints API termine par "/api" (ex: http://localhost:5000/api)
    // Le dashboard Hangfire est servi à la racine /hangfire (hors préfixe API).
    const root = apiUrl.replace(/\/api\/?$/, '');
    window.open(root + '/hangfire', '_blank', 'noopener,noreferrer');
  }
}
