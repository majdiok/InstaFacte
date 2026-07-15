import {
  ChangeDetectionStrategy,
  Component,
  OnInit,
  inject,
  signal
} from '@angular/core';
import { DatePipe, DecimalPipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { TableModule } from 'primeng/table';
import { ButtonModule } from 'primeng/button';
import { DropdownModule } from 'primeng/dropdown';
import { TooltipModule } from 'primeng/tooltip';
import { MessageService } from 'primeng/api';

import { PlatformPaymentProvidersService } from '@core/services/platform-payment-providers.service';
import type {
  PaymentIntentsPageDto
} from '@core/models/platform.models';

import { FtPageHeaderComponent } from '@core/ui/page-header/ft-page-header.component';
import { FtKpiCardComponent } from '@core/ui/kpi-card/ft-kpi-card.component';
import { FtFilterToolbarComponent } from '@core/ui/filter-toolbar/ft-filter-toolbar.component';
import { FtBadgeComponent } from '@core/ui/badge/ft-badge.component';
import { FtSkeletonComponent } from '@core/ui/skeleton/ft-skeleton.component';
import { FtEmptyStateComponent } from '@core/ui/empty-state/ft-empty-state.component';
import type { FtTone } from '@core/ui/badge/ft-badge.component';

import { PAYMENTS_FR } from './payments.i18n.fr';

/**
 * Lot C5 — Page audit des intentions de paiement (succès, échecs, attente).
 */
@Component({
  selector: 'app-platform-payment-intents-page',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    DatePipe,
    DecimalPipe,
    FormsModule,
    RouterLink,
    TableModule,
    ButtonModule,
    DropdownModule,
    TooltipModule,
    FtPageHeaderComponent,
    FtKpiCardComponent,
    FtFilterToolbarComponent,
    FtBadgeComponent,
    FtSkeletonComponent,
    FtEmptyStateComponent
  ],
  template: `
    <ft-page-header [title]="t('intents.title')" [subtitle]="t('intents.subtitle')">
      <ng-container ftActions>
        <p-button label="Retour" icon="pi pi-arrow-left" [outlined]="true" severity="secondary" routerLink="/payments/providers" />
        <p-button [label]="t('list.actions.refresh')" icon="pi pi-refresh" [outlined]="true" [disabled]="loading()" (onClick)="load()" />
      </ng-container>
    </ft-page-header>

    <section class="kpi-row">
      <ft-kpi-card [label]="t('intents.kpi.total')" [value]="page()?.totalCount ?? null" tone="info" icon="pi pi-list" [loading]="loading()" />
      <ft-kpi-card [label]="t('intents.kpi.succeeded')" [value]="page()?.succeededCount ?? null" tone="success" icon="pi pi-check-circle" [loading]="loading()" />
      <ft-kpi-card [label]="t('intents.kpi.pending')" [value]="page()?.pendingCount ?? null" tone="warning" icon="pi pi-clock" [loading]="loading()" />
      <ft-kpi-card [label]="t('intents.kpi.failed')" [value]="page()?.failedCount ?? null" tone="danger" icon="pi pi-times-circle" [loading]="loading()" />
      <ft-kpi-card [label]="t('intents.kpi.amount')" [value]="page()?.totalAmountSucceededTnd ?? null" tone="accent" icon="pi pi-wallet" [loading]="loading()" />
    </section>

    <ft-filter-toolbar>
      <p-dropdown
        [options]="providerOptions"
        [(ngModel)]="providerFilter"
        optionLabel="label"
        optionValue="value"
        [showClear]="true"
        [placeholder]="t('intents.filter.providerAll')"
        (onChange)="load()"
        styleClass="filter-dropdown" />
      <p-dropdown
        [options]="statusOptions"
        [(ngModel)]="statusFilter"
        optionLabel="label"
        optionValue="value"
        [showClear]="true"
        [placeholder]="t('intents.filter.statusAll')"
        (onChange)="load()"
        styleClass="filter-dropdown" />
    </ft-filter-toolbar>

    @if (loading()) {
      <ft-skeleton kind="line" count="6" />
    } @else if ((page()?.items?.length ?? 0) === 0) {
      <ft-empty-state variant="table-empty" [title]="t('empty.title')" [description]="t('empty.desc')" />
    } @else {
      <p-table [value]="page()!.items" styleClass="ft-table">
        <ng-template pTemplate="header">
          <tr>
            <th>{{ t('intents.col.invoice') }}</th>
            <th>{{ t('intents.col.tenant') }}</th>
            <th>{{ t('intents.col.provider') }}</th>
            <th class="num">{{ t('intents.col.amount') }}</th>
            <th>{{ t('intents.col.status') }}</th>
            <th>{{ t('intents.col.providerRef') }}</th>
            <th>{{ t('intents.col.createdAt') }}</th>
            <th>{{ t('intents.col.completedAt') }}</th>
          </tr>
        </ng-template>
        <ng-template pTemplate="body" let-row>
          <tr>
            <td>
              <a [routerLink]="['/invoices', row.invoiceId]" class="link num-link">{{ row.invoiceNumber ?? '—' }}</a>
            </td>
            <td>{{ row.tenantName }}</td>
            <td>
              <span class="provider-badge">{{ row.providerCode }}</span>
            </td>
            <td class="num">{{ row.amountTND | number:'1.3-3' }} TND</td>
            <td>
              <ft-badge [tone]="statusTone(row.status)">{{ row.statusDisplay }}</ft-badge>
            </td>
            <td class="ref">{{ row.providerRef ?? '—' }}</td>
            <td>{{ row.createdAt | date:'dd/MM/yyyy HH:mm' }}</td>
            <td>{{ row.completedAt ? (row.completedAt | date:'dd/MM/yyyy HH:mm') : '—' }}</td>
          </tr>
        </ng-template>
      </p-table>
    }
  `,
  styles: [
    `
      .kpi-row {
        display: grid;
        grid-template-columns: repeat(5, minmax(0, 1fr));
        gap: var(--gap-md);
        margin-bottom: var(--gap-lg);
      }
      @media (max-width: 1100px) { .kpi-row { grid-template-columns: repeat(3, minmax(0, 1fr)); } }
      @media (max-width: 720px) { .kpi-row { grid-template-columns: 1fr; } }

      .num { text-align: right; font-variant-numeric: tabular-nums; }
      .ref { font-family: var(--font-mono, ui-monospace); font-size: 0.85rem; color: var(--ft-text-muted); }
      .provider-badge {
        display: inline-block;
        padding: 0.18rem 0.55rem;
        border-radius: var(--ft-radius-sm, 6px);
        background: var(--ft-accent-muted);
        color: var(--ft-accent);
        font-size: 0.78rem;
        font-weight: 500;
        text-transform: uppercase;
      }
      .link { color: var(--ft-accent); text-decoration: none; font-variant-numeric: tabular-nums; font-weight: 500; }
      .link:hover { text-decoration: underline; }

      :host ::ng-deep .filter-dropdown { min-width: 12rem; }
    `
  ]
})
export class PlatformPaymentIntentsPageComponent implements OnInit {
  private readonly api = inject(PlatformPaymentProvidersService);
  private readonly toast = inject(MessageService);

  protected readonly page = signal<PaymentIntentsPageDto | null>(null);
  protected readonly loading = signal<boolean>(false);

  protected providerFilter: string | null = null;
  protected statusFilter: string | null = null;

  protected readonly providerOptions = [
    { label: this.t('intents.filter.providerAll'), value: null },
    { label: 'Konnect', value: 'konnect' },
    { label: 'Paymee', value: 'paymee' },
    { label: 'Virement', value: 'wire' }
  ];

  protected readonly statusOptions = [
    { label: this.t('intents.filter.statusAll'), value: null },
    { label: this.t('status.created'), value: 'Created' },
    { label: this.t('status.redirect'), value: 'RedirectIssued' },
    { label: this.t('status.pending'), value: 'Pending' },
    { label: this.t('status.succeeded'), value: 'Succeeded' },
    { label: this.t('status.failed'), value: 'Failed' },
    { label: this.t('status.cancelled'), value: 'Cancelled' }
  ];

  protected t(key: keyof typeof PAYMENTS_FR): string {
    return PAYMENTS_FR[key];
  }

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.api.listIntents(this.providerFilter, this.statusFilter, null, null, null, 1, 50).subscribe({
      next: (res) => {
        if (res.success && res.data) this.page.set(res.data);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.toast.add({ severity: 'error', summary: this.t('toast.error.title'), detail: this.t('toast.error.detail') });
      }
    });
  }

  protected statusTone(status: number): FtTone {
    switch (status) {
      case 0: return 'neutral'; // Created
      case 1: return 'accent';  // RedirectIssued
      case 2: return 'warning'; // Pending
      case 3: return 'success'; // Succeeded
      case 4: return 'danger';  // Failed
      case 5: return 'neutral'; // Cancelled
      case 6: return 'neutral'; // Refunded
      default: return 'neutral';
    }
  }
}
