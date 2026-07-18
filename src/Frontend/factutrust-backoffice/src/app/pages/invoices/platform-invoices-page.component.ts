import {
  ChangeDetectionStrategy,
  Component,
  OnInit,
  computed,
  inject,
  signal
} from '@angular/core';
import { DatePipe, DecimalPipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { TableModule } from 'primeng/table';
import { ButtonModule } from 'primeng/button';
import { DropdownModule } from 'primeng/dropdown';
import { CalendarModule } from 'primeng/calendar';
import { TooltipModule } from 'primeng/tooltip';
import { MessageService } from 'primeng/api';

import { PlatformInvoicesService } from '@core/services/platform-invoices.service';
import { PlatformTenantService } from '@core/services/platform-tenant.service';
import type {
  PlatformInvoiceSummaryDto,
  PlatformInvoicesPageDto
} from '@core/models/platform.models';

import { FtPageHeaderComponent } from '@core/ui/page-header/ft-page-header.component';
import { FtKpiCardComponent } from '@core/ui/kpi-card/ft-kpi-card.component';
import { FtFilterToolbarComponent } from '@core/ui/filter-toolbar/ft-filter-toolbar.component';
import { FtBadgeComponent } from '@core/ui/badge/ft-badge.component';
import { FtSkeletonComponent } from '@core/ui/skeleton/ft-skeleton.component';
import { FtEmptyStateComponent } from '@core/ui/empty-state/ft-empty-state.component';
import type { FtTone } from '@core/ui/badge/ft-badge.component';

import { INVOICES_FR } from './invoices.i18n.fr';

interface TenantOption {
  label: string;
  value: string | null;
}

interface StatusOption {
  label: string;
  value: string | null;
}

/**
 * Lot C4 — Page liste des factures plateforme.
 *
 * KPIs : draft / issued / paid / overdue + montants émis/payés/restant.
 * Filtres : tenant + statut + plage de dates.
 * Click ligne → /invoices/:id (détail).
 */
@Component({
  selector: 'app-platform-invoices-page',
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
    CalendarModule,
    TooltipModule,
    FtPageHeaderComponent,
    FtKpiCardComponent,
    FtFilterToolbarComponent,
    FtBadgeComponent,
    FtSkeletonComponent,
    FtEmptyStateComponent
  ],
  template: `
    <ft-page-header [title]="t('list.title')" [subtitle]="t('list.subtitle')">
      <ng-container ftActions>
        <p-button
          [label]="t('list.actions.fiscalSettings')"
          icon="pi pi-cog"
          [outlined]="true"
          severity="secondary"
          routerLink="/invoices/fiscal-settings" />
        <p-button
          [label]="t('list.actions.refresh')"
          icon="pi pi-refresh"
          [outlined]="true"
          [disabled]="loading()"
          (onClick)="load()" />
        <p-button
          [label]="t('list.actions.create')"
          icon="pi pi-plus"
          severity="primary"
          routerLink="/invoices/new" />
      </ng-container>
    </ft-page-header>

    <section class="kpi-row" role="region">
      <ft-kpi-card
        [label]="t('kpi.total')"
        [value]="page()?.totalCount ?? null"
        tone="info" icon="pi pi-file" [loading]="loading()" />
      <ft-kpi-card
        [label]="t('kpi.draft')"
        [value]="page()?.draftCount ?? null"
        tone="neutral" icon="pi pi-pencil" [loading]="loading()" />
      <ft-kpi-card
        [label]="t('kpi.issued')"
        [value]="page()?.issuedCount ?? null"
        tone="accent" icon="pi pi-send" [loading]="loading()" />
      <ft-kpi-card
        [label]="t('kpi.paid')"
        [value]="page()?.paidCount ?? null"
        tone="success" icon="pi pi-check-circle" [loading]="loading()" />
      <ft-kpi-card
        [label]="t('kpi.overdue')"
        [value]="page()?.overdueCount ?? null"
        tone="danger" icon="pi pi-exclamation-triangle" [loading]="loading()" />
      <ft-kpi-card
        [label]="t('kpi.outstanding') + ' (TND)'"
        [value]="page()?.totalOutstandingTtc ?? null"
        tone="warning" icon="pi pi-wallet" [loading]="loading()" />
    </section>

    <ft-filter-toolbar>
      <p-dropdown
        [options]="tenantOptions()"
        [(ngModel)]="tenantFilter"
        optionLabel="label"
        optionValue="value"
        [showClear]="true"
        [filter]="true"
        [placeholder]="t('filter.tenantAll')"
        (onChange)="load()"
        styleClass="filter-dropdown" />

      <p-dropdown
        [options]="statusOptions"
        [(ngModel)]="statusFilter"
        optionLabel="label"
        optionValue="value"
        [showClear]="true"
        [placeholder]="t('filter.statusAll')"
        (onChange)="load()"
        styleClass="filter-dropdown" />

      <p-calendar
        [(ngModel)]="fromDate"
        dateFormat="dd/mm/yy"
        [showIcon]="true"
        [placeholder]="t('filter.from')"
        (onSelect)="load()"
        (onClear)="load()"
        [showClear]="true"
        styleClass="filter-calendar" />

      <p-calendar
        [(ngModel)]="toDate"
        dateFormat="dd/mm/yy"
        [showIcon]="true"
        [placeholder]="t('filter.to')"
        (onSelect)="load()"
        (onClear)="load()"
        [showClear]="true"
        styleClass="filter-calendar" />
    </ft-filter-toolbar>

    @if (loading()) {
      <div class="skeleton-block">
        <ft-skeleton kind="line" count="6" />
      </div>
    } @else if ((page()?.items?.length ?? 0) === 0) {
      <ft-empty-state
        variant="table-empty"
        [title]="t('empty.title')"
        [description]="t('empty.desc')" />
    } @else {
      <p-table
        [value]="page()!.items"
        [rowHover]="true"
        [responsiveLayout]="'scroll'"
        styleClass="ft-table"
        (onRowSelect)="onRowSelect($event)"
        selectionMode="single">
        <ng-template pTemplate="header">
          <tr>
            <th>{{ t('col.number') }}</th>
            <th>{{ t('col.tenant') }}</th>
            <th>{{ t('col.invoiceDate') }}</th>
            <th>{{ t('col.dueDate') }}</th>
            <th>{{ t('col.billingType') }}</th>
            <th>{{ t('col.status') }}</th>
            <th class="num">{{ t('col.totalTtc') }}</th>
            <th class="num">{{ t('col.received') }}</th>
            <th class="num">{{ t('col.remaining') }}</th>
          </tr>
        </ng-template>

        <ng-template pTemplate="body" let-row>
          <tr [pSelectableRow]="row">
            <td>
              <a [routerLink]="['/invoices', row.id]" class="num-link">
                {{ row.number ?? '—' }}
              </a>
            </td>
            <td class="cell-strong">{{ row.tenantName }}</td>
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
      .kpi-row {
        display: grid;
        grid-template-columns: repeat(6, minmax(0, 1fr));
        gap: var(--gap-md);
        margin-bottom: var(--gap-lg);
      }
      @media (max-width: 1280px) {
        .kpi-row { grid-template-columns: repeat(3, minmax(0, 1fr)); }
      }
      @media (max-width: 720px) {
        .kpi-row { grid-template-columns: repeat(2, minmax(0, 1fr)); }
      }

      .skeleton-block { padding: 1rem 0; }
      .num { text-align: right; font-variant-numeric: tabular-nums; }
      .cell-strong { font-weight: 500; color: var(--ft-text); }
      .muted { color: var(--ft-text-subtle); }
      .num-link {
        color: var(--ft-accent);
        text-decoration: none;
        font-variant-numeric: tabular-nums;
        font-weight: 500;
      }
      .num-link:hover { text-decoration: underline; }
      .due-overdue { color: var(--ft-danger-text); font-weight: 500; }
      .remaining-positive { color: var(--ft-warning-text); font-weight: 500; }

      :host ::ng-deep .filter-dropdown,
      :host ::ng-deep .filter-calendar {
        min-width: 12rem;
      }
    `
  ]
})
export class PlatformInvoicesPageComponent implements OnInit {
  private readonly api = inject(PlatformInvoicesService);
  private readonly tenantsApi = inject(PlatformTenantService);
  private readonly toast = inject(MessageService);
  private readonly router = inject(Router);

  protected readonly page = signal<PlatformInvoicesPageDto | null>(null);
  protected readonly loading = signal<boolean>(false);
  protected readonly tenantOptions = signal<TenantOption[]>([{ label: this.t('filter.tenantAll'), value: null }]);

  protected tenantFilter: string | null = null;
  protected statusFilter: string | null = null;
  protected fromDate: Date | null = null;
  protected toDate: Date | null = null;

  protected readonly statusOptions: StatusOption[] = [
    { label: this.t('filter.statusAll'), value: null },
    { label: this.t('status.draft'), value: 'Draft' },
    { label: this.t('status.issued'), value: 'Issued' },
    { label: this.t('status.paid'), value: 'Paid' },
    { label: this.t('status.partiallyPaid'), value: 'PartiallyPaid' },
    { label: this.t('status.overdue'), value: 'Overdue' },
    { label: this.t('status.cancelled'), value: 'Cancelled' }
  ];

  protected t(key: keyof typeof INVOICES_FR): string {
    return INVOICES_FR[key];
  }

  ngOnInit(): void {
    this.loadTenants();
    this.load();
  }

  load(): void {
    this.loading.set(true);
    const fromIso = this.fromDate ? this.fromDate.toISOString() : null;
    const toIso = this.toDate ? this.toDate.toISOString() : null;
    this.api.list(this.tenantFilter, this.statusFilter, fromIso, toIso, 1, 50).subscribe({
      next: (res) => {
        if (res.success && res.data) this.page.set(res.data);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.toast.add({
          severity: 'error',
          summary: this.t('toast.error.title'),
          detail: this.t('toast.error.detail')
        });
      }
    });
  }

  private loadTenants(): void {
    this.tenantsApi
      .list({ pageSize: 200 })
      .subscribe({
        next: (res) => {
          if (res.success && res.data) {
            const opts: TenantOption[] = [{ label: this.t('filter.tenantAll'), value: null }];
            for (const t of res.data.items) {
              opts.push({ label: t.companyName, value: t.tenantId });
            }
            this.tenantOptions.set(opts);
          }
        },
        error: () => {
          // silencieux
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

  protected onRowSelect(event: { data?: PlatformInvoiceSummaryDto | PlatformInvoiceSummaryDto[] | null }): void {
    const row = Array.isArray(event.data) ? event.data[0] : event.data;
    if (row?.id) this.router.navigate(['/invoices', row.id]);
  }
}
