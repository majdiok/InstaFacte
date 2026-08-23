import {
  ChangeDetectionStrategy,
  Component,
  OnInit,
  computed,
  inject,
  signal
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TableModule } from 'primeng/table';
import { InputTextModule } from 'primeng/inputtext';
import { ButtonModule } from 'primeng/button';
import { SelectModule } from 'primeng/select';
import { TooltipModule } from 'primeng/tooltip';
import { MessageService } from 'primeng/api';

import { PlatformMigrationsService } from '@core/services/platform-migrations.service';
import type {
  MigrationStatsDto,
  MigrationStatusResultDto
} from '@core/models/platform.models';

import { FtPageHeaderComponent } from '@core/ui/page-header/ft-page-header.component';
import { FtKpiCardComponent } from '@core/ui/kpi-card/ft-kpi-card.component';
import { FtFilterToolbarComponent } from '@core/ui/filter-toolbar/ft-filter-toolbar.component';
import { FtBadgeComponent } from '@core/ui/badge/ft-badge.component';
import { FtAvatarComponent } from '@core/ui/avatar/ft-avatar.component';
import { FtEmptyStateComponent } from '@core/ui/empty-state/ft-empty-state.component';
import { FtSkeletonComponent } from '@core/ui/skeleton/ft-skeleton.component';
import { FtConfirmActionComponent } from '@core/ui/confirm-action/ft-confirm-action.component';
import { FtCellPlanComponent } from '@core/ui/table-cells/ft-cell-plan.component';

import { MIGRATIONS_FR } from './migrations.i18n.fr';
import { MigrationDetailDrawerComponent } from './migration-detail-drawer.component';

type MigrationFilter = 'all' | 'applied' | 'missing';

@Component({
  selector: 'app-platform-migrations-page',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    FormsModule,
    TableModule,
    InputTextModule,
    ButtonModule,
    SelectModule,
    TooltipModule,
    FtPageHeaderComponent,
    FtKpiCardComponent,
    FtFilterToolbarComponent,
    FtBadgeComponent,
    FtAvatarComponent,
    FtEmptyStateComponent,
    FtSkeletonComponent,
    FtConfirmActionComponent,
    FtCellPlanComponent,
    MigrationDetailDrawerComponent
  ],
  template: `
    <ft-page-header [title]="t('list.title')" [subtitle]="t('list.subtitle')">
      <ng-container ftActions>
        <p-button
          [label]="t('action.refresh')"
          icon="pi pi-refresh"
          [outlined]="true"
          [disabled]="loading()"
          (onClick)="loadAll()"
        />
        <p-button
          [label]="t('action.applyAll')"
          icon="pi pi-database"
          severity="danger"
          [disabled]="loading() || applyingAll() || pendingCount() === 0"
          [pTooltip]="pendingCount() === 0 ? 'Tous les tenants sont à jour' : ''"
          tooltipPosition="bottom"
          (onClick)="openApplyAllDialog()"
        />
      </ng-container>
    </ft-page-header>

    <!-- Bandeau alerte pending -->
    @if (pendingCount() > 0) {
      <div class="alert alert--warning" role="alert">
        <i class="pi pi-exclamation-triangle" aria-hidden="true"></i>
        <div class="alert__body">
          <strong>{{ pendingCount() }} {{ t('alert.pendingPrefix') }}</strong>
          <p>{{ t('alert.pendingDesc') }}</p>
        </div>
        <p-button
          [label]="t('alert.pendingApplyAll')"
          icon="pi pi-database"
          severity="warn"
          size="small"
          (onClick)="openApplyAllDialog()"
        />
      </div>
    }

    <!-- KPIs row -->
    <section class="kpi-row" role="region" aria-label="Indicateurs migrations">
      <ft-kpi-card
        [label]="t('kpi.total')"
        [value]="stats()?.totalTenants ?? null"
        [hint]="t('kpi.total.hint')"
        tone="info"
        icon="pi pi-building"
        [loading]="statsLoading()"
      />
      <ft-kpi-card
        [label]="t('kpi.upToDate')"
        [value]="stats()?.upToDate ?? null"
        [hint]="t('kpi.upToDate.hint')"
        tone="success"
        icon="pi pi-check-circle"
        [clickable]="true"
        [active]="filterStatus() === 'applied'"
        [loading]="statsLoading()"
        (cardClick)="setStatusFilter('applied')"
      />
      <ft-kpi-card
        [label]="t('kpi.pending')"
        [value]="stats()?.pending ?? null"
        [hint]="t('kpi.pending.hint')"
        tone="warning"
        icon="pi pi-clock"
        [clickable]="true"
        [active]="filterStatus() === 'missing'"
        [loading]="statsLoading()"
        (cardClick)="setStatusFilter('missing')"
      />
      <ft-kpi-card
        [label]="t('kpi.failures24h')"
        [value]="stats()?.failures24h ?? null"
        [hint]="t('kpi.failures24h.hint')"
        tone="danger"
        icon="pi pi-times-circle"
        [loading]="statsLoading()"
      />
    </section>

    <!-- Toolbar -->
    <ft-filter-toolbar>
      <span class="search-wrap">
        <i class="pi pi-search" aria-hidden="true"></i>
        <input
          type="search"
          pInputText
          [ngModel]="searchDraft()"
          (ngModelChange)="searchDraft.set($event)"
          placeholder="Rechercher une entreprise…"
          aria-label="Recherche entreprise"
        />
      </span>
      <p-select
        [options]="statusOptions"
        [ngModel]="filterStatus()"
        (ngModelChange)="setStatusFilter($event)"
        optionLabel="label"
        optionValue="value"
        placeholder="Statut"
        styleClass="ft-dd"
      />

      <ng-container ftActions>
        <p-button
          label="Réinitialiser"
          icon="pi pi-filter-slash"
          [text]="true"
          [disabled]="!hasActiveFilters()"
          (onClick)="resetFilters()"
        />
      </ng-container>
    </ft-filter-toolbar>

    <!-- Table -->
    <p-table
      [value]="filteredRows()"
      [loading]="loading()"
      styleClass="p-datatable-sm ft-table"
      [tableStyle]="{ 'min-width': '52rem' }"
      responsiveLayout="scroll"
    >
      <ng-template pTemplate="header">
        <tr>
          <th scope="col">{{ t('col.tenant') }}</th>
          <th scope="col">{{ t('col.plan') }}</th>
          <th scope="col">{{ t('col.segment') }}</th>
          <th scope="col">{{ t('col.migrations') }}</th>
          <th scope="col" class="col-actions">{{ t('col.actions') }}</th>
        </tr>
      </ng-template>

      <ng-template pTemplate="body" let-row>
        <tr (click)="openDrawer(row)" class="clickable">
          <td>
            <div class="cell-tenant">
              <ft-avatar [name]="row.tenantName" [seed]="row.tenantId" size="sm" />
              <div class="cell-tenant__stack">
                <strong class="cell-tenant__name">{{ row.tenantName }}</strong>
                <code class="cell-tenant__id">{{ row.tenantId }}</code>
              </div>
            </div>
          </td>
          <td>
            <ft-cell-plan [planCode]="row.subscriptionPlan" [planDisplay]="row.subscriptionPlanDisplay" />
          </td>
          <td>
            <ft-badge [tone]="row.isPayingSubscriber ? 'success' : 'neutral'" size="sm">
              {{ row.isPayingSubscriber ? 'Abonné' : 'Non abonné' }}
            </ft-badge>
          </td>
          <td>
            @if (row.hasMigrationsApplied) {
              <ft-badge tone="success" [withDot]="true">{{ t('status.applied') }}</ft-badge>
            } @else {
              <ft-badge tone="warning" [withDot]="true">{{ t('status.missing') }}</ft-badge>
            }
          </td>
          <td class="col-actions" (click)="$event.stopPropagation()">
            @if (!row.hasMigrationsApplied) {
              <p-button
                [label]="t('action.applyOne')"
                icon="pi pi-play"
                size="small"
                [outlined]="true"
                [loading]="applyingId() === row.tenantId"
                [disabled]="applyingAll() || (applyingId() !== null && applyingId() !== row.tenantId)"
                (onClick)="openApplyOneDialog(row)"
              />
            } @else {
              <p-button
                [label]="t('action.viewDetails')"
                icon="pi pi-arrow-right"
                size="small"
                [text]="true"
                (onClick)="openDrawer(row)"
              />
            }
          </td>
        </tr>
      </ng-template>

      <ng-template pTemplate="emptymessage">
        <tr>
          <td colspan="5">
            <ft-empty-state
              [variant]="emptyVariant()"
              [title]="emptyTitle()"
              [description]="emptyDesc()"
            >
              @if (hasActiveFilters()) {
                <p-button label="Réinitialiser" icon="pi pi-filter-slash" [outlined]="true" (onClick)="resetFilters()" />
              }
            </ft-empty-state>
          </td>
        </tr>
      </ng-template>

      <ng-template pTemplate="loadingbody">
        @for (i of skeletonRows; track $index) {
          <tr>
            <td>
              <div class="skel-cell">
                <ft-skeleton shape="circle" width="1.75rem" height="1.75rem" />
                <div style="flex:1">
                  <ft-skeleton shape="line" width="60%" />
                  <ft-skeleton shape="line" width="40%" />
                </div>
              </div>
            </td>
            <td><ft-skeleton shape="line" width="4rem" /></td>
            <td><ft-skeleton shape="line" width="5rem" /></td>
            <td><ft-skeleton shape="line" width="6rem" /></td>
            <td class="col-actions"><ft-skeleton shape="line" width="5rem" /></td>
          </tr>
        }
      </ng-template>
    </p-table>

    <!-- Section Historique (placeholder Lot D4) -->
    <details class="history" [attr.open]="false">
      <summary class="history__summary">
        <i class="pi pi-history" aria-hidden="true"></i>
        {{ t('history.title') }}
      </summary>
      <div class="history__body">
        <ft-empty-state
          variant="all-clear"
          [title]="t('history.title')"
          [description]="t('history.placeholder')"
        />
      </div>
    </details>

    <!-- Modal "Appliquer sur tous" -->
    <ft-confirm-action
      [(visible)]="applyAllDialogOpen"
      variant="destructive"
      [title]="t('applyAll.title')"
      [confirmKeyword]="t('applyAll.confirmKeyword')"
      [confirmLabel]="applyingAll() ? t('applyAll.runningTitle') : t('applyAll.confirmLabel')"
      confirmIcon="pi pi-database"
      [busy]="applyingAll()"
      (confirmed)="applyAll()"
    >
      <p class="confirm-line">
        {{ t('applyAll.descPrefix') }} <strong>{{ stats()?.totalTenants ?? '—' }}</strong>
        {{ t('applyAll.descSuffix') }}
      </p>
      <p class="confirm-warning">
        <i class="pi pi-exclamation-triangle" aria-hidden="true"></i>
        {{ t('applyAll.warning') }}
      </p>
    </ft-confirm-action>

    <!-- Modal "Appliquer un tenant" -->
    <ft-confirm-action
      [(visible)]="applyOneDialogOpen"
      variant="soft"
      [title]="t('applyOne.title')"
      [description]="applyOneDescription()"
      [confirmLabel]="t('applyOne.confirmLabel')"
      confirmIcon="pi pi-play"
      [busy]="applyingId() !== null"
      (confirmed)="applyOne()"
    />

    <!-- Drawer détail -->
    <app-migration-detail-drawer
      [(visible)]="drawerOpen"
      [row]="drawerRow()"
      [busy]="applyingId() !== null"
      (applyOne)="onDrawerApply($event)"
    />
  `,
  styles: [
    `
      :host {
        display: block;
      }

      .alert {
        display: flex;
        gap: var(--gap-md);
        align-items: center;
        padding: var(--gap-sm) var(--gap-md);
        border-radius: var(--ft-radius);
        margin-bottom: var(--gap-md);
        background: var(--ft-warning-surface);
        border: 1px solid var(--ft-warning-border);
        color: var(--ft-warning-text);
      }

      .alert__body {
        flex: 1;
      }

      .alert__body strong {
        color: var(--ft-text);
        font-weight: 600;
      }

      .alert__body p {
        margin: 0.2rem 0 0;
        color: var(--ft-text-muted);
        font-size: 0.85rem;
      }

      .alert .pi-exclamation-triangle {
        font-size: 1.4rem;
        color: var(--ft-warning-text);
      }

      .search-wrap {
        position: relative;
        display: inline-flex;
        align-items: center;
      }

      .search-wrap .pi-search {
        position: absolute;
        left: 0.7rem;
        color: var(--ft-text-muted);
        pointer-events: none;
        font-size: 0.85rem;
      }

      .search-wrap input {
        padding-left: 2rem;
        min-width: 14rem;
        width: min(22rem, 100%);
        background: var(--ft-surface-2);
        color: var(--ft-text);
        border: 1px solid var(--ft-border);
        border-radius: var(--ft-radius);
      }

      :host ::ng-deep .ft-dd {
        min-width: 9.5rem;
      }

      .clickable {
        cursor: pointer;
      }

      .col-actions {
        text-align: end;
        white-space: nowrap;
        width: 9rem;
      }

      .cell-tenant {
        display: inline-flex;
        align-items: center;
        gap: var(--gap-sm);
        min-width: 0;
      }

      .cell-tenant__stack {
        display: flex;
        flex-direction: column;
        gap: 0.1rem;
        line-height: 1.25;
      }

      .cell-tenant__name {
        color: var(--ft-text);
        font-weight: 600;
        font-size: 0.92rem;
      }

      .cell-tenant__id {
        font-family: ui-monospace, SFMono-Regular, monospace;
        color: var(--ft-text-muted);
        font-size: 0.7rem;
      }

      .skel-cell {
        display: flex;
        align-items: center;
        gap: 0.6rem;
      }

      .history {
        margin-top: var(--gap-section);
        background: var(--ft-surface);
        border: 1px solid var(--ft-border);
        border-radius: var(--ft-radius);
        overflow: hidden;
      }

      .history__summary {
        list-style: none;
        cursor: pointer;
        padding: var(--gap-sm) var(--gap-md);
        display: flex;
        align-items: center;
        gap: var(--gap-sm);
        color: var(--ft-text);
        font-weight: 500;
        font-size: 0.92rem;
        user-select: none;
      }

      .history__summary::-webkit-details-marker {
        display: none;
      }

      .history__summary::before {
        content: '▶';
        color: var(--ft-text-muted);
        font-size: 0.7rem;
        transition: transform var(--duration-fast) var(--easing-standard);
      }

      .history[open] .history__summary::before {
        transform: rotate(90deg);
      }

      .history[open] .history__summary {
        border-bottom: 1px solid var(--ft-border);
      }

      .history__body {
        padding: var(--gap-md);
      }

      .confirm-line {
        margin: 0 0 var(--gap-sm);
        color: var(--ft-text);
        font-size: 0.92rem;
        line-height: 1.5;
      }

      .confirm-line strong {
        color: var(--ft-accent);
      }

      .confirm-warning {
        margin: 0;
        padding: var(--gap-sm) var(--gap-md);
        background: var(--ft-warning-surface);
        border: 1px solid var(--ft-warning-border);
        border-radius: var(--ft-radius);
        color: var(--ft-text);
        font-size: 0.85rem;
        line-height: 1.5;
        display: flex;
        gap: 0.6rem;
        align-items: flex-start;
      }

      .confirm-warning .pi-exclamation-triangle {
        color: var(--ft-warning-text);
        margin-top: 0.15rem;
      }
    `
  ]
})
export class PlatformMigrationsPageComponent implements OnInit {
  private readonly api = inject(PlatformMigrationsService);
  private readonly messages = inject(MessageService);

  protected readonly skeletonRows = Array.from({ length: 8 });

  protected t(key: keyof typeof MIGRATIONS_FR): string {
    return MIGRATIONS_FR[key];
  }

  // ----- State -----
  readonly stats = signal<MigrationStatsDto | null>(null);
  readonly statsLoading = signal(false);
  readonly rows = signal<MigrationStatusResultDto[]>([]);
  readonly loading = signal(false);
  readonly applyingAll = signal(false);
  readonly applyingId = signal<string | null>(null);

  // Filtres
  readonly searchDraft = signal('');
  readonly filterStatus = signal<MigrationFilter>('all');

  // Modaux + drawer
  applyAllDialogOpen = false;
  applyOneDialogOpen = false;
  drawerOpen = false;

  private readonly applyOneTarget = signal<MigrationStatusResultDto | null>(null);
  readonly drawerRow = signal<MigrationStatusResultDto | null>(null);

  // ----- Options -----
  readonly statusOptions: { label: string; value: MigrationFilter }[] = [
    { label: 'Tous', value: 'all' },
    { label: MIGRATIONS_FR['status.applied'], value: 'applied' },
    { label: MIGRATIONS_FR['status.missing'], value: 'missing' }
  ];

  // ----- Computed -----
  readonly hasActiveFilters = computed(
    () => !!this.searchDraft().trim() || this.filterStatus() !== 'all'
  );

  readonly pendingCount = computed(
    () => this.stats()?.pending ?? this.rows().filter((r) => !r.hasMigrationsApplied).length
  );

  readonly filteredRows = computed(() => {
    let list = this.rows();
    const status = this.filterStatus();
    if (status === 'applied') list = list.filter((r) => r.hasMigrationsApplied);
    else if (status === 'missing') list = list.filter((r) => !r.hasMigrationsApplied);

    const search = this.searchDraft().trim().toLowerCase();
    if (search) {
      list = list.filter(
        (r) =>
          r.tenantName.toLowerCase().includes(search) ||
          r.tenantId.toLowerCase().includes(search)
      );
    }
    return list;
  });

  readonly emptyVariant = computed<'search-no-result' | 'all-clear' | 'table-empty'>(() => {
    if (this.hasActiveFilters()) return 'search-no-result';
    if (this.rows().length > 0 && this.pendingCount() === 0) return 'all-clear';
    return 'table-empty';
  });

  readonly emptyTitle = computed(() => {
    if (this.hasActiveFilters()) return 'Aucun résultat';
    if (this.rows().length > 0 && this.pendingCount() === 0) {
      return MIGRATIONS_FR['empty.allUpToDate.title'];
    }
    return MIGRATIONS_FR['empty.noTenant.title'];
  });

  readonly emptyDesc = computed(() => {
    if (this.hasActiveFilters()) return 'Essayez de modifier les filtres ou la recherche.';
    if (this.rows().length > 0 && this.pendingCount() === 0) {
      return MIGRATIONS_FR['empty.allUpToDate.desc'];
    }
    return MIGRATIONS_FR['empty.noTenant.desc'];
  });

  readonly applyOneDescription = computed(() => {
    const tgt = this.applyOneTarget();
    if (!tgt) return MIGRATIONS_FR['applyOne.desc'];
    return `« ${tgt.tenantName} » — ${MIGRATIONS_FR['applyOne.desc']}`;
  });

  ngOnInit(): void {
    this.loadAll();
  }

  loadAll(): void {
    this.loadStats();
    this.loadList();
  }

  private loadStats(): void {
    this.statsLoading.set(true);
    this.api.stats().subscribe({
      next: (res) => {
        this.statsLoading.set(false);
        if (res.success && res.data) this.stats.set(res.data);
      },
      error: () => {
        this.statsLoading.set(false);
      }
    });
  }

  private loadList(): void {
    this.loading.set(true);
    this.api.listStatus().subscribe({
      next: (res) => {
        this.loading.set(false);
        if (res.success) {
          this.rows.set(res.data ?? []);
        } else {
          this.toastError(res.message);
        }
      },
      error: () => {
        this.loading.set(false);
        this.toastError();
      }
    });
  }

  // ----- Filtres -----
  setStatusFilter(value: MigrationFilter): void {
    this.filterStatus.set(value);
  }

  resetFilters(): void {
    this.searchDraft.set('');
    this.filterStatus.set('all');
  }

  // ----- Drawer -----
  openDrawer(row: MigrationStatusResultDto): void {
    this.drawerRow.set(row);
    this.drawerOpen = true;
  }

  onDrawerApply(row: MigrationStatusResultDto): void {
    this.openApplyOneDialog(row);
  }

  // ----- Modals -----
  openApplyAllDialog(): void {
    this.applyAllDialogOpen = true;
  }

  openApplyOneDialog(row: MigrationStatusResultDto): void {
    this.applyOneTarget.set(row);
    this.applyOneDialogOpen = true;
  }

  applyAll(): void {
    this.applyingAll.set(true);
    this.api.applyAll().subscribe({
      next: (res) => {
        this.applyingAll.set(false);
        this.applyAllDialogOpen = false;
        if (res.success) {
          this.messages.add({
            severity: 'success',
            summary: 'Terminé',
            detail:
              res.message ??
              `${res.data?.successCount ?? 0} migration(s) appliquée(s) sur ${
                res.data?.totalTenants ?? 0
              }.`
          });
          this.loadAll();
        } else {
          this.toastError(res.message);
        }
      },
      error: () => {
        this.applyingAll.set(false);
        this.applyAllDialogOpen = false;
        this.toastError();
      }
    });
  }

  applyOne(): void {
    const target = this.applyOneTarget();
    if (!target) return;
    this.applyingId.set(target.tenantId);
    this.api.applyOne(target.tenantId).subscribe({
      next: (res) => {
        this.applyingId.set(null);
        this.applyOneDialogOpen = false;
        if (res.success) {
          this.messages.add({
            severity: 'success',
            summary: 'OK',
            detail: res.message ?? MIGRATIONS_FR['toast.applyOne.success']
          });
          this.refreshTenant(target.tenantId);
        } else {
          this.toastError(res.message);
        }
      },
      error: () => {
        this.applyingId.set(null);
        this.applyOneDialogOpen = false;
        this.toastError();
      }
    });
  }

  /** Rafraîchit uniquement le tenant ciblé (et les stats). */
  private refreshTenant(tenantId: string): void {
    this.api.statusFor(tenantId).subscribe({
      next: (res) => {
        if (res.success && res.data) {
          const updated = res.data;
          this.rows.update((rows) =>
            rows.map((r) => (r.tenantId === tenantId ? updated : r))
          );
          // Mettre à jour le drawer s'il pointe sur ce tenant
          if (this.drawerRow()?.tenantId === tenantId) {
            this.drawerRow.set(updated);
          }
        }
        this.loadStats(); // KPIs à rafraîchir
      },
      error: () => {
        // silencieux : la liste reste cohérente, l'utilisateur peut Rafraîchir manuellement
      }
    });
  }

  private toastError(detail?: string | null): void {
    this.messages.add({
      severity: 'error',
      summary: MIGRATIONS_FR['toast.error.title'],
      detail: detail ?? MIGRATIONS_FR['toast.error.detail']
    });
  }
}
