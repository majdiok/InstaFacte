import {
  ChangeDetectionStrategy,
  Component,
  Input,
  OnChanges,
  SimpleChanges,
  computed,
  inject,
  signal
} from '@angular/core';
import { DatePipe, DecimalPipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { TableModule } from 'primeng/table';
import { ButtonModule } from 'primeng/button';
import { TooltipModule } from 'primeng/tooltip';
import { MessageService } from 'primeng/api';

import { PlatformInvoicesService } from '@core/services/platform-invoices.service';
import type { PlatformInvoicesPageDto } from '@core/models/platform.models';

import { FtKpiCardComponent } from '@core/ui/kpi-card/ft-kpi-card.component';
import { FtBadgeComponent } from '@core/ui/badge/ft-badge.component';
import { FtSkeletonComponent } from '@core/ui/skeleton/ft-skeleton.component';
import { FtEmptyStateComponent } from '@core/ui/empty-state/ft-empty-state.component';
import type { FtTone } from '@core/ui/badge/ft-badge.component';

/**
 * Sous-lot C4.5 — Tab « Factures plateforme » dans le détail tenant.
 *
 * Réutilise <see cref="PlatformInvoicesService.list"/> avec un filtre <c>tenantId</c>
 * imposé. Affiche 4 KPIs (Total, Émises, Réglées, Restant dû) puis une table
 * read-only qui pointe vers le détail facture <c>/invoices/:id</c>.
 *
 * Le composant est <em>autonome</em> : il charge sa propre donnée à chaque
 * changement de l'input <c>tenantId</c>, sans toucher au state du parent.
 */
@Component({
  selector: 'app-tenant-invoices-tab',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    DatePipe,
    DecimalPipe,
    RouterLink,
    TableModule,
    ButtonModule,
    TooltipModule,
    FtKpiCardComponent,
    FtBadgeComponent,
    FtSkeletonComponent,
    FtEmptyStateComponent
  ],
  template: `
    <section class="kpi-row">
      <ft-kpi-card
        [label]="'Total factures'"
        [value]="page()?.totalCount ?? null"
        tone="info" icon="pi pi-file" [loading]="loading()" />
      <ft-kpi-card
        [label]="'Émises (TND)'"
        [value]="page()?.totalIssuedTtc ?? null"
        tone="accent" icon="pi pi-send" [loading]="loading()" />
      <ft-kpi-card
        [label]="'Encaissé (TND)'"
        [value]="page()?.totalPaidTtc ?? null"
        tone="success" icon="pi pi-check-circle" [loading]="loading()" />
      <ft-kpi-card
        [label]="'Restant dû (TND)'"
        [value]="page()?.totalOutstandingTtc ?? null"
        tone="warning" icon="pi pi-wallet" [loading]="loading()" />
    </section>

    <div class="tab-actions">
      <p-button
        label="Rafraîchir"
        icon="pi pi-refresh"
        [outlined]="true"
        size="small"
        [disabled]="loading()"
        (onClick)="reload()" />
      <p-button
        label="Voir toutes les factures plateforme"
        icon="pi pi-external-link"
        [outlined]="true"
        size="small"
        severity="secondary"
        routerLink="/invoices"
        [queryParams]="{ tenantId: tenantId }" />
    </div>

    @if (loading()) {
      <ft-skeleton kind="line" count="6" />
    } @else if (errored()) {
      <p class="error">Impossible de charger les factures de ce tenant.</p>
    } @else if ((page()?.items?.length ?? 0) === 0) {
      <ft-empty-state
        variant="table-empty"
        [title]="'Aucune facture plateforme'"
        [description]="'Aucun document fiscal n’a encore été émis à cette entreprise.'" />
    } @else {
      <p-table [value]="page()!.items" styleClass="ft-table">
        <ng-template pTemplate="header">
          <tr>
            <th>N°</th>
            <th>Date</th>
            <th>Échéance</th>
            <th>Type</th>
            <th>Statut</th>
            <th class="num">TTC</th>
            <th class="num">Reçu</th>
            <th class="num">Solde</th>
          </tr>
        </ng-template>
        <ng-template pTemplate="body" let-row>
          <tr>
            <td>
              <a [routerLink]="['/invoices', row.id]" class="num-link">{{ row.number ?? '—' }}</a>
            </td>
            <td>{{ row.invoiceDate | date:'dd/MM/yyyy' }}</td>
            <td>
              @if (row.dueDate) {
                <span [class.due-overdue]="row.isOverdue">{{ row.dueDate | date:'dd/MM/yyyy' }}</span>
              } @else {
                <span class="muted">—</span>
              }
            </td>
            <td>{{ row.billingTypeDisplay }}</td>
            <td>
              <ft-badge [tone]="statusTone(row.status)">{{ row.statusDisplay }}</ft-badge>
            </td>
            <td class="num">{{ row.totalTTC | number:'1.3-3' }}</td>
            <td class="num">{{ row.totalReceived | number:'1.3-3' }}</td>
            <td class="num" [class.remaining-positive]="row.remainingAmount > 0">
              {{ row.remainingAmount | number:'1.3-3' }}
            </td>
          </tr>
        </ng-template>
      </p-table>
    }
  `,
  styles: [
    `
      :host {
        display: block;
        padding-top: 0.6rem;
      }
      .kpi-row {
        display: grid;
        grid-template-columns: repeat(4, minmax(0, 1fr));
        gap: var(--gap-md, 1rem);
        margin-bottom: var(--gap-md, 1rem);
      }
      @media (max-width: 980px) {
        .kpi-row { grid-template-columns: repeat(2, minmax(0, 1fr)); }
      }
      @media (max-width: 540px) {
        .kpi-row { grid-template-columns: 1fr; }
      }
      .tab-actions {
        display: flex;
        gap: 0.5rem;
        margin-bottom: 0.75rem;
        flex-wrap: wrap;
      }
      .num { text-align: right; font-variant-numeric: tabular-nums; }
      .muted { color: var(--ft-text-subtle, #6e7681); }
      .num-link {
        color: var(--ft-accent, #58a6ff);
        text-decoration: none;
        font-variant-numeric: tabular-nums;
        font-weight: 500;
      }
      .num-link:hover { text-decoration: underline; }
      .due-overdue { color: var(--ft-danger-text, #f85149); font-weight: 500; }
      .remaining-positive { color: var(--ft-warning-text, #d29922); font-weight: 500; }
      .error {
        color: var(--ft-danger-text, #f85149);
        background: var(--ft-danger-surface, rgba(248, 81, 73, 0.14));
        border: 1px solid var(--ft-danger-border, rgba(248, 81, 73, 0.4));
        border-radius: var(--ft-radius, 8px);
        padding: 0.6rem 0.85rem;
        margin: 0;
      }
    `
  ]
})
export class TenantInvoicesTabComponent implements OnChanges {
  private readonly api = inject(PlatformInvoicesService);
  private readonly toast = inject(MessageService);

  @Input({ required: true }) tenantId: string | null = null;

  protected readonly page = signal<PlatformInvoicesPageDto | null>(null);
  protected readonly loading = signal<boolean>(false);
  protected readonly errored = signal<boolean>(false);

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['tenantId']) {
      this.reload();
    }
  }

  reload(): void {
    if (!this.tenantId) {
      this.page.set(null);
      return;
    }
    this.loading.set(true);
    this.errored.set(false);
    // 100 = ample pour la majorité des tenants ; pagination cliquable via le lien "Voir toutes".
    this.api.list(this.tenantId, null, null, null, 1, 100).subscribe({
      next: (res) => {
        if (res.success && res.data) {
          this.page.set(res.data);
        } else {
          this.errored.set(true);
        }
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.errored.set(true);
        this.toast.add({
          severity: 'error',
          summary: 'Erreur',
          detail: 'Impossible de charger les factures.'
        });
      }
    });
  }

  protected statusTone(status: number): FtTone {
    switch (status) {
      case 0: return 'neutral'; // Draft
      case 1: return 'accent';  // Issued
      case 2: return 'success'; // Paid
      case 3: return 'warning'; // PartiallyPaid
      case 4: return 'danger';  // Overdue
      case 5: return 'neutral'; // Cancelled
      case 6: return 'neutral'; // Refunded
      default: return 'neutral';
    }
  }
}
