import {
  ChangeDetectionStrategy,
  Component,
  EventEmitter,
  Input,
  OnChanges,
  Output,
  SimpleChanges,
  inject,
  signal
} from '@angular/core';
import { DatePipe } from '@angular/common';
import { Router } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { TabViewModule } from 'primeng/tabview';
import { PlatformTenantService } from '@core/services/platform-tenant.service';
import type { PlatformTenantDetailDto } from '@core/models/platform.models';
import { FtDrawerComponent } from '@core/ui/drawer/ft-drawer.component';
import { FtSkeletonComponent } from '@core/ui/skeleton/ft-skeleton.component';
import { FtBadgeComponent } from '@core/ui/badge/ft-badge.component';
import { FtStatusDotComponent } from '@core/ui/status-dot/ft-status-dot.component';
import { FtAvatarComponent } from '@core/ui/avatar/ft-avatar.component';
import { FtEmptyStateComponent } from '@core/ui/empty-state/ft-empty-state.component';
import { FtTndCurrencyPipe } from '@core/pipes/ft-tnd-currency.pipe';
import { FtRelativeDatePipe } from '@core/pipes/ft-relative-date.pipe';
import type { FtTone } from '@core/ui/badge/ft-badge.component';
import { TENANTS_FR } from './tenants.i18n.fr';

/**
 * Drawer d'aperçu rapide d'une entreprise. Charge le détail tenant à l'ouverture
 * (lazy) et affiche 6 onglets :
 *  - Aperçu (champs d'identité + contact)
 *  - Abonnement (plan, statut, dates, prix)
 *  - Modules (placeholder Lot C1)
 *  - Activité (placeholder Lot D3)
 *  - Migrations (statut EF déjà disponible)
 *  - Vitrine (placeholder Lot D5)
 *
 * Footer : bouton "Voir page complète" navigue vers `/tenants/:id`.
 */
@Component({
  selector: 'app-tenant-quick-view-drawer',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    DatePipe,
    ButtonModule,
    TabViewModule,
    FtDrawerComponent,
    FtSkeletonComponent,
    FtBadgeComponent,
    FtStatusDotComponent,
    FtAvatarComponent,
    FtEmptyStateComponent,
    FtTndCurrencyPipe,
    FtRelativeDatePipe
  ],
  template: `
    <ft-drawer
      [visible]="visible"
      (visibleChange)="onVisibleChange($event)"
      [title]="title()"
      [subtitle]="subtitle()"
      [width]="'520px'"
    >
      @if (loading()) {
        <div class="qv-loading">
          <ft-skeleton shape="rect" width="100%" height="3rem" />
          <ft-skeleton shape="line" />
          <ft-skeleton shape="line" />
          <ft-skeleton shape="rect" width="100%" height="8rem" />
        </div>
      }
      @if (!loading() && error()) {
        <ft-empty-state
          variant="error"
          title="Impossible de charger ce tenant"
          description="L'API n'a pas répondu. Réessayez ou ouvrez la page complète."
        />
      }
      @if (!loading() && !error() && detail(); as d) {
        <div class="qv-head">
          <ft-avatar [name]="d.companyName" [seed]="d.tenantId" size="lg" />
          <div class="qv-head__titles">
            <h3 class="qv-head__name">{{ d.companyName }}</h3>
            <p class="qv-head__email">{{ d.companyEmail }}</p>
            <div class="qv-head__chips">
              <ft-badge [tone]="d.isActive ? 'success' : 'danger'" size="sm">
                {{ d.isActive ? 'Active' : 'Inactive' }}
              </ft-badge>
              @if (d.subscriptionStatus) {
                <ft-status-dot [tone]="resolveStatusTone(d.subscriptionStatus)" [label]="d.subscriptionStatusDisplay ?? d.subscriptionStatus" />
              }
            </div>
          </div>
        </div>

        <p-tabView styleClass="qv-tabs">
          <p-tabPanel [header]="t('drawer.tab.overview')">
            <dl class="qv-fields">
              <div><dt>{{ t('drawer.field.nif') }}</dt><dd><code>{{ d.nif }}</code></dd></div>
              <div><dt>{{ t('drawer.field.email') }}</dt><dd>{{ d.companyEmail }}</dd></div>
              <div><dt>{{ t('drawer.field.phone') }}</dt><dd>{{ d.phone || '—' }}</dd></div>
              <div>
                <dt>{{ t('drawer.field.address') }}</dt>
                <dd>{{ d.city }}{{ d.governorate ? ', ' + d.governorate : '' }}</dd>
              </div>
              <div><dt>{{ t('drawer.field.taxRegime') }}</dt><dd>{{ d.taxRegimeDisplay }}</dd></div>
              @if (d.website) {
                <div><dt>{{ t('drawer.field.website') }}</dt><dd><a [href]="d.website" target="_blank" rel="noopener">{{ d.website }}</a></dd></div>
              }
              @if (d.deactivatedAt) {
                <div>
                  <dt>{{ t('drawer.field.deactivatedAt') }}</dt>
                  <dd>{{ d.deactivatedAt | date: 'dd/MM/yyyy HH:mm' }}</dd>
                </div>
              }
              <div>
                <dt>{{ t('drawer.field.databaseName') }}</dt>
                <dd><code class="db-name">{{ d.databaseName }}</code></dd>
              </div>
            </dl>
          </p-tabPanel>

          <p-tabPanel [header]="t('drawer.tab.subscription')">
            <dl class="qv-fields">
              <div>
                <dt>{{ t('drawer.field.plan') }}</dt>
                <dd>
                  @if (d.subscriptionPlanDisplay) {
                    <ft-badge [tone]="resolvePlanTone(d.subscriptionPlan)">
                      {{ d.subscriptionPlanDisplay }}
                    </ft-badge>
                  } @else {
                    —
                  }
                </dd>
              </div>
              <div>
                <dt>{{ t('drawer.field.status') }}</dt>
                <dd>
                  @if (d.subscriptionStatus) {
                    <ft-status-dot [tone]="resolveStatusTone(d.subscriptionStatus)" [label]="d.subscriptionStatusDisplay ?? d.subscriptionStatus" />
                  } @else {
                    —
                  }
                </dd>
              </div>
              <div>
                <dt>{{ t('drawer.field.endDate') }}</dt>
                <dd>
                  @if (d.subscriptionEndDate) {
                    <span [title]="d.subscriptionEndDate">
                      {{ d.subscriptionEndDate | date: 'dd/MM/yyyy' }}
                      <span class="muted">·</span>
                      <span class="muted">{{ d.subscriptionEndDate | ftRelativeDate }}</span>
                    </span>
                  } @else {
                    —
                  }
                </dd>
              </div>
            </dl>
          </p-tabPanel>

          <p-tabPanel [header]="t('drawer.tab.modules')">
            <ft-empty-state
              variant="all-clear"
              title="Modules"
              [description]="t('drawer.placeholder.modules')"
            />
          </p-tabPanel>

          <p-tabPanel [header]="t('drawer.tab.activity')">
            <ft-empty-state
              variant="all-clear"
              title="Activité"
              [description]="t('drawer.placeholder.activity')"
            />
          </p-tabPanel>

          <p-tabPanel [header]="t('drawer.tab.migrations')">
            <div class="qv-migrations">
              <div class="qv-migrations__row">
                <span>{{ t('drawer.field.migrationStatus') }}</span>
                @if (d.hasMigrationsApplied) {
                  <ft-badge tone="success" [withDot]="true">{{ t('drawer.migrations.applied') }}</ft-badge>
                } @else {
                  <ft-badge tone="warning" [withDot]="true">{{ t('drawer.migrations.missing') }}</ft-badge>
                }
              </div>
              <p class="qv-migrations__hint">
                Pour appliquer les migrations sur cette base, ouvrez la page <em>Migrations</em>
                ou cliquez sur "Voir page complète".
              </p>
            </div>
          </p-tabPanel>

          <p-tabPanel [header]="t('drawer.tab.storefront')">
            <ft-empty-state
              variant="all-clear"
              title="Vitrine"
              [description]="t('drawer.placeholder.storefront')"
            />
          </p-tabPanel>
        </p-tabView>
      }

      <ng-container ftFooter>
        <p-button
          [label]="t('drawer.action.openFullPage')"
          icon="pi pi-arrow-right"
          [text]="false"
          [outlined]="false"
          severity="primary"
          (onClick)="onOpenFullPage()"
          [disabled]="!detail()?.tenantId"
        />
      </ng-container>
    </ft-drawer>
  `,
  styles: [
    `
      :host {
        display: contents;
      }

      .qv-loading {
        display: flex;
        flex-direction: column;
        gap: var(--gap-sm);
      }

      .qv-head {
        display: flex;
        gap: var(--gap-md);
        align-items: flex-start;
        margin-bottom: var(--gap-md);
        padding-bottom: var(--gap-md);
        border-bottom: 1px solid var(--ft-border);
      }

      .qv-head__titles {
        display: flex;
        flex-direction: column;
        gap: 0.25rem;
        flex: 1;
        min-width: 0;
      }

      .qv-head__name {
        margin: 0;
        font-size: 1.1rem;
        font-weight: 600;
        color: var(--ft-text);
      }

      .qv-head__email {
        margin: 0;
        color: var(--ft-text-muted);
        font-size: 0.85rem;
      }

      .qv-head__chips {
        display: flex;
        gap: 0.5rem;
        flex-wrap: wrap;
        align-items: center;
        margin-top: 0.4rem;
      }

      .qv-fields {
        margin: 0;
        padding: 0;
        display: flex;
        flex-direction: column;
        gap: 0.7rem;
      }

      .qv-fields > div {
        display: grid;
        grid-template-columns: 9rem 1fr;
        gap: 0.75rem;
        align-items: baseline;
      }

      .qv-fields dt {
        margin: 0;
        font-size: 0.78rem;
        color: var(--ft-text-muted);
        text-transform: uppercase;
        letter-spacing: 0.04em;
        font-weight: 500;
      }

      .qv-fields dd {
        margin: 0;
        color: var(--ft-text);
        font-size: 0.9rem;
        word-break: break-word;
      }

      .qv-fields code,
      .db-name {
        font-family: ui-monospace, SFMono-Regular, monospace;
        font-size: 0.85em;
        background: var(--ft-surface-2);
        padding: 0.1rem 0.4rem;
        border-radius: var(--ft-radius-sm);
        color: var(--ft-accent);
      }

      .muted {
        color: var(--ft-text-muted);
      }

      .qv-migrations {
        display: flex;
        flex-direction: column;
        gap: var(--gap-sm);
      }

      .qv-migrations__row {
        display: flex;
        align-items: center;
        justify-content: space-between;
        padding: var(--gap-sm) var(--gap-md);
        background: var(--ft-surface-2);
        border: 1px solid var(--ft-border);
        border-radius: var(--ft-radius);
      }

      .qv-migrations__hint {
        margin: 0;
        color: var(--ft-text-muted);
        font-size: 0.85rem;
        line-height: 1.5;
      }

      :host ::ng-deep .qv-tabs .p-tabview-nav {
        background: transparent;
        border-bottom: 1px solid var(--ft-border);
      }

      :host ::ng-deep .qv-tabs .p-tabview-nav li .p-tabview-nav-link {
        background: transparent;
        color: var(--ft-text-muted);
        border-color: transparent;
      }

      :host ::ng-deep .qv-tabs .p-tabview-nav li.p-highlight .p-tabview-nav-link {
        color: var(--ft-accent);
        border-color: var(--ft-accent);
      }

      :host ::ng-deep .qv-tabs .p-tabview-panels {
        background: transparent;
        padding: var(--gap-md) 0 0;
      }
    `
  ]
})
export class TenantQuickViewDrawerComponent implements OnChanges {
  private readonly api = inject(PlatformTenantService);
  private readonly router = inject(Router);

  @Input() visible = false;
  @Input() tenantId: string | null = null;
  @Output() visibleChange = new EventEmitter<boolean>();

  readonly detail = signal<PlatformTenantDetailDto | null>(null);
  readonly loading = signal(false);
  readonly error = signal(false);

  ngOnChanges(changes: SimpleChanges): void {
    // Reload only when becoming visible with a (potentially) new tenantId
    if ((changes['visible'] || changes['tenantId']) && this.visible && this.tenantId) {
      this.loadDetail(this.tenantId);
    }
  }

  title(): string {
    return this.detail()?.companyName ?? TENANTS_FR['drawer.title'];
  }

  subtitle(): string | null {
    const d = this.detail();
    return d?.companyEmail ?? null;
  }

  t(key: keyof typeof TENANTS_FR): string {
    return TENANTS_FR[key];
  }

  resolveStatusTone(status: string | null | undefined): FtTone {
    switch ((status ?? '').toLowerCase()) {
      case 'active':
        return 'success';
      case 'trial':
        return 'info';
      case 'pastdue':
      case 'past_due':
        return 'warning';
      case 'suspended':
        return 'danger';
      case 'cancelled':
      case 'canceled':
      case 'expired':
        return 'neutral';
      default:
        return 'neutral';
    }
  }

  resolvePlanTone(plan: string | null | undefined): FtTone {
    switch ((plan ?? '').toLowerCase()) {
      case 'annual':
        return 'accent';
      case 'monthly':
        return 'info';
      default:
        return 'neutral';
    }
  }

  onVisibleChange(value: boolean): void {
    this.visible = value;
    this.visibleChange.emit(value);
    if (!value) {
      this.detail.set(null);
      this.error.set(false);
    }
  }

  onOpenFullPage(): void {
    const id = this.detail()?.tenantId ?? this.tenantId;
    if (id) {
      this.onVisibleChange(false);
      void this.router.navigate(['/tenants', id]);
    }
  }

  private loadDetail(tenantId: string): void {
    this.loading.set(true);
    this.error.set(false);
    this.detail.set(null);
    this.api.get(tenantId).subscribe({
      next: (res) => {
        this.loading.set(false);
        if (res.success && res.data) {
          this.detail.set(res.data);
        } else {
          this.error.set(true);
        }
      },
      error: () => {
        this.loading.set(false);
        this.error.set(true);
      }
    });
  }
}
