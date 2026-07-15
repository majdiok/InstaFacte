import {
  ChangeDetectionStrategy,
  Component,
  OnInit,
  computed,
  inject,
  signal
} from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { TableModule, TableLazyLoadEvent } from 'primeng/table';
import { InputTextModule } from 'primeng/inputtext';
import { ButtonModule } from 'primeng/button';
import { DropdownModule } from 'primeng/dropdown';
import { CalendarModule } from 'primeng/calendar';
import { TooltipModule } from 'primeng/tooltip';
import { MessageService } from 'primeng/api';

import { PlatformAuditService } from '@core/services/platform-audit.service';
import { PlatformTenantService } from '@core/services/platform-tenant.service';
import type {
  AuditChainVerificationDto,
  AuditLogDetailDto,
  AuditLogEntryDto,
  AuditLogPageDto,
  PlatformTenantListItemDto
} from '@core/models/platform.models';

import { FtPageHeaderComponent } from '@core/ui/page-header/ft-page-header.component';
import { FtKpiCardComponent } from '@core/ui/kpi-card/ft-kpi-card.component';
import { FtFilterToolbarComponent } from '@core/ui/filter-toolbar/ft-filter-toolbar.component';
import { FtBadgeComponent } from '@core/ui/badge/ft-badge.component';
import { FtEmptyStateComponent } from '@core/ui/empty-state/ft-empty-state.component';
import { FtSkeletonComponent } from '@core/ui/skeleton/ft-skeleton.component';

import { AUDIT_FR } from './audit.i18n.fr';
import { AuditDetailDialogComponent } from './audit-detail-dialog.component';

@Component({
  selector: 'app-platform-audit-page',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    DatePipe,
    FormsModule,
    TableModule,
    InputTextModule,
    ButtonModule,
    DropdownModule,
    CalendarModule,
    TooltipModule,
    FtPageHeaderComponent,
    FtKpiCardComponent,
    FtFilterToolbarComponent,
    FtBadgeComponent,
    FtEmptyStateComponent,
    FtSkeletonComponent,
    AuditDetailDialogComponent
  ],
  template: `
    <ft-page-header [title]="t('list.title')" [subtitle]="t('list.subtitle')">
      <ng-container ftActions>
        <p-button
          [label]="t('list.actions.refresh')"
          icon="pi pi-refresh"
          [outlined]="true"
          [disabled]="loading() || !selectedTenantId()"
          (onClick)="reload()" />
        <p-button
          [label]="t('list.actions.exportCsv')"
          icon="pi pi-file"
          [outlined]="true"
          [disabled]="loading() || !selectedTenantId() || (page()?.totalCount ?? 0) === 0"
          (onClick)="exportFile('csv')" />
        <p-button
          [label]="t('list.actions.exportPdf')"
          icon="pi pi-file-pdf"
          [outlined]="true"
          [disabled]="loading() || !selectedTenantId() || (page()?.totalCount ?? 0) === 0"
          (onClick)="exportFile('pdf')" />
      </ng-container>
    </ft-page-header>

    <!-- Tenant picker -->
    <div class="picker-card">
      <label class="picker-label" for="tenant-picker">{{ t('picker.label') }}</label>
      <p-dropdown
        inputId="tenant-picker"
        [options]="tenantOptions()"
        [ngModel]="selectedTenantId()"
        (ngModelChange)="onTenantChange($event)"
        optionLabel="label"
        optionValue="value"
        [placeholder]="t('picker.placeholder')"
        [filter]="true"
        filterBy="label"
        [showClear]="true"
        styleClass="ft-dd"
        [loading]="tenantsLoading()" />
    </div>

    @if (!selectedTenantId()) {
      <ft-empty-state
        variant="search-no-result"
        [title]="t('picker.empty.title')"
        [description]="t('picker.empty.desc')" />
    } @else {
      <!-- KPIs -->
      <section class="kpi-row" role="region" aria-label="Indicateurs audit">
        <ft-kpi-card
          [label]="t('kpi.total')"
          [value]="page()?.totalCount ?? null"
          [hint]="t('kpi.total.hint')"
          tone="info"
          icon="pi pi-list"
          [loading]="loading()" />
        <ft-kpi-card
          [label]="t('kpi.integrity')"
          [value]="integrity()?.entryCount ?? null"
          [hint]="integrityHint()"
          [tone]="integrityTone()"
          icon="pi pi-shield"
          [loading]="integrityLoading()" />
        <ft-kpi-card
          [label]="t('kpi.duplicates')"
          [value]="integrity()?.duplicatePreviousHashGroupCount ?? null"
          [hint]="t('kpi.duplicates.hint')"
          [tone]="(integrity()?.duplicatePreviousHashGroupCount ?? 0) > 0 ? 'warning' : 'neutral'"
          icon="pi pi-clone"
          [loading]="integrityLoading()" />
      </section>

      <!-- Carte intégrité -->
      @if (integrity(); as ig) {
        <article class="integrity-card" [class.integrity-card--ok]="ig.isValid" [class.integrity-card--ko]="!ig.isValid">
          <div class="integrity-icon">
            <i class="pi" [class.pi-check-circle]="ig.isValid" [class.pi-exclamation-triangle]="!ig.isValid"></i>
          </div>
          <div class="integrity-body">
            <h3>{{ ig.isValid ? t('integrity.valid.title') : t('integrity.broken.title') }}</h3>
            <p>{{ ig.isValid ? t('integrity.valid.desc') : integrityBrokenDesc(ig) }}</p>
            @if (!ig.isValid && ig.firstBrokenEntryId) {
              <p class="muted">
                <strong>{{ t('integrity.broken.firstId') }} :</strong>
                <code>{{ ig.firstBrokenEntryId }}</code>
              </p>
            }
          </div>
          <p-button
            [label]="t('integrity.refresh')"
            icon="pi pi-refresh"
            [text]="true"
            [loading]="integrityLoading()"
            (onClick)="loadIntegrity()" />
        </article>
      }

      <!-- Filtres -->
      <ft-filter-toolbar>
        <p-calendar
          [(ngModel)]="filterFrom"
          (ngModelChange)="onFilterChange()"
          [placeholder]="t('filters.from')"
          dateFormat="dd/mm/yy"
          [showIcon]="true"
          [showClear]="true"
          styleClass="ft-cal" />
        <p-calendar
          [(ngModel)]="filterTo"
          (ngModelChange)="onFilterChange()"
          [placeholder]="t('filters.to')"
          dateFormat="dd/mm/yy"
          [showIcon]="true"
          [showClear]="true"
          styleClass="ft-cal" />
        <input
          type="text"
          pInputText
          [(ngModel)]="filterAction"
          (ngModelChange)="onFilterChangeDebounced()"
          [placeholder]="t('filters.actionPlaceholder')"
          aria-label="Filtre action" />
        <input
          type="text"
          pInputText
          [(ngModel)]="filterEntityType"
          (ngModelChange)="onFilterChangeDebounced()"
          [placeholder]="t('filters.entityTypePlaceholder')"
          aria-label="Filtre type d'entité" />

        <ng-container ftActions>
          <p-button
            [label]="t('filters.reset')"
            icon="pi pi-filter-slash"
            [text]="true"
            [disabled]="!hasActiveFilters()"
            (onClick)="resetFilters()" />
        </ng-container>
      </ft-filter-toolbar>

      <!-- Table -->
      <p-table
        [value]="page()?.items ?? []"
        [loading]="loading()"
        [lazy]="true"
        [first]="tableFirst()"
        (onLazyLoad)="onLazyLoad($event)"
        [paginator]="(page()?.totalCount ?? 0) > 0"
        [rows]="pageSize()"
        [totalRecords]="page()?.totalCount ?? 0"
        [rowsPerPageOptions]="[25, 50, 100, 200]"
        [showCurrentPageReport]="true"
        [currentPageReportTemplate]="'{first}–{last} sur {totalRecords}'"
        responsiveLayout="scroll"
        styleClass="p-datatable-sm ft-audit-table"
        [tableStyle]="{ 'min-width': '60rem' }">
        <ng-template pTemplate="header">
          <tr>
            <th scope="col">{{ t('col.dateTime') }}</th>
            <th scope="col">{{ t('col.user') }}</th>
            <th scope="col">{{ t('col.action') }}</th>
            <th scope="col">{{ t('col.entity') }}</th>
            <th scope="col">{{ t('col.entityId') }}</th>
            <th scope="col" class="col-actions">{{ t('col.actions') }}</th>
          </tr>
        </ng-template>

        <ng-template pTemplate="body" let-row>
          <tr (click)="openDetail(row)" class="clickable">
            <td>{{ row.createdAt | date: 'dd/MM/yyyy HH:mm:ss' }}</td>
            <td>{{ row.userEmail }}</td>
            <td><ft-badge tone="accent" size="sm">{{ row.action }}</ft-badge></td>
            <td>
              {{ row.entityType }}
              @if (row.entityLabel) {
                <span class="muted"> — {{ row.entityLabel }}</span>
              }
            </td>
            <td><code class="cell-mono">{{ row.entityId ?? '—' }}</code></td>
            <td class="col-actions" (click)="$event.stopPropagation()">
              <p-button
                icon="pi pi-arrow-right"
                [text]="true"
                size="small"
                [pTooltip]="t('col.action.viewDetails')"
                tooltipPosition="left"
                (onClick)="openDetail(row)" />
            </td>
          </tr>
        </ng-template>

        <ng-template pTemplate="emptymessage">
          <tr>
            <td colspan="6">
              <ft-empty-state
                variant="table-empty"
                [title]="t('empty.title')"
                [description]="t('empty.desc')">
                @if (hasActiveFilters()) {
                  <p-button
                    [label]="t('filters.reset')"
                    icon="pi pi-filter-slash"
                    [outlined]="true"
                    (onClick)="resetFilters()" />
                }
              </ft-empty-state>
            </td>
          </tr>
        </ng-template>

        <ng-template pTemplate="loadingbody">
          @for (i of skeletonRows; track $index) {
            <tr>
              <td><ft-skeleton shape="line" width="9rem" /></td>
              <td><ft-skeleton shape="line" width="60%" /></td>
              <td><ft-skeleton shape="line" width="6rem" /></td>
              <td><ft-skeleton shape="line" width="50%" /></td>
              <td><ft-skeleton shape="line" width="6rem" /></td>
              <td class="col-actions"><ft-skeleton shape="circle" width="1.5rem" height="1.5rem" /></td>
            </tr>
          }
        </ng-template>
      </p-table>
    }

    <!-- Detail dialog -->
    <app-audit-detail-dialog
      [(visible)]="detailDialogOpen"
      [data]="detailRow()" />
  `,
  styles: [
    `
      :host {
        display: block;
      }

      .picker-card {
        background: var(--ft-surface);
        border: 1px solid var(--ft-border);
        border-radius: var(--ft-radius);
        padding: var(--gap-sm) var(--gap-md);
        margin-bottom: var(--gap-md);
        display: flex;
        align-items: center;
        gap: var(--gap-sm);
        flex-wrap: wrap;
      }

      .picker-label {
        font-size: 0.78rem;
        text-transform: uppercase;
        letter-spacing: 0.05em;
        color: var(--ft-text-muted);
        font-weight: 600;
      }

      :host ::ng-deep .picker-card .ft-dd {
        min-width: 22rem;
        flex: 1;
      }

      .kpi-row {
        display: grid;
        grid-template-columns: repeat(auto-fit, minmax(15rem, 1fr));
        gap: var(--gap-md);
        margin-bottom: var(--gap-md);
      }

      .integrity-card {
        display: flex;
        align-items: flex-start;
        gap: var(--gap-md);
        padding: var(--gap-md);
        border-radius: var(--ft-radius);
        margin-bottom: var(--gap-md);
        background: var(--ft-surface);
        border: 1px solid var(--ft-border);
      }

      .integrity-card--ok {
        border-color: var(--ft-success-border);
        background: var(--ft-success-surface);
      }

      .integrity-card--ko {
        border-color: var(--ft-danger-border);
        background: var(--ft-danger-surface);
      }

      .integrity-icon {
        width: 2.5rem;
        height: 2.5rem;
        flex-shrink: 0;
        border-radius: var(--ft-radius-pill);
        display: flex;
        align-items: center;
        justify-content: center;
        font-size: 1.4rem;
      }

      .integrity-card--ok .integrity-icon {
        background: rgba(63, 185, 80, 0.18);
        color: var(--ft-success-text);
      }

      .integrity-card--ko .integrity-icon {
        background: rgba(248, 81, 73, 0.18);
        color: var(--ft-danger-text);
      }

      .integrity-body {
        flex: 1;
      }

      .integrity-body h3 {
        margin: 0 0 0.3rem;
        font-size: 1rem;
        color: var(--ft-text);
      }

      .integrity-body p {
        margin: 0;
        font-size: 0.85rem;
        color: var(--ft-text);
      }

      .integrity-body .muted {
        color: var(--ft-text-muted);
        margin-top: 0.4rem;
        font-size: 0.78rem;
      }

      .integrity-body code {
        font-family: ui-monospace, SFMono-Regular, monospace;
        background: var(--ft-surface-2);
        padding: 0.1rem 0.4rem;
        border-radius: var(--ft-radius-sm);
        color: var(--ft-accent);
        font-size: 0.78rem;
      }

      :host ::ng-deep .ft-dd {
        min-width: 9.5rem;
      }

      :host ::ng-deep .ft-cal .p-inputtext {
        min-width: 9rem;
      }

      .clickable {
        cursor: pointer;
      }

      .col-actions {
        text-align: end;
        white-space: nowrap;
        width: 4rem;
      }

      .cell-mono {
        font-family: ui-monospace, SFMono-Regular, monospace;
        font-size: 0.78rem;
        color: var(--ft-text-muted);
      }

      .muted {
        color: var(--ft-text-muted);
      }

      :host ::ng-deep .ft-audit-table.p-datatable .p-datatable-thead > tr > th {
        font-size: 0.72rem;
        text-transform: uppercase;
        letter-spacing: 0.05em;
        color: var(--ft-text-muted);
        border-color: var(--ft-border);
        background: var(--ft-surface-2);
        font-weight: 600;
      }

      :host ::ng-deep .ft-audit-table.p-datatable .p-datatable-tbody > tr > td {
        border-color: var(--ft-border-subtle);
      }

      :host ::ng-deep .ft-audit-table.p-datatable .p-datatable-tbody > tr:hover {
        background: var(--ft-surface-3);
      }
    `
  ]
})
export class PlatformAuditPageComponent implements OnInit {
  private readonly api = inject(PlatformAuditService);
  private readonly tenantsApi = inject(PlatformTenantService);
  private readonly toast = inject(MessageService);

  protected readonly skeletonRows = Array.from({ length: 8 });

  protected t(key: keyof typeof AUDIT_FR): string {
    return AUDIT_FR[key];
  }

  // ----- State -----
  readonly tenants = signal<PlatformTenantListItemDto[]>([]);
  readonly tenantsLoading = signal(false);
  readonly selectedTenantId = signal<string | null>(null);

  readonly page = signal<AuditLogPageDto | null>(null);
  readonly loading = signal(false);
  readonly tableFirst = signal(0);
  readonly pageSize = signal(25);

  readonly integrity = signal<AuditChainVerificationDto | null>(null);
  readonly integrityLoading = signal(false);

  readonly detailRow = signal<AuditLogDetailDto | null>(null);
  detailDialogOpen = false;

  // Filtres
  protected filterFrom: Date | null = null;
  protected filterTo: Date | null = null;
  protected filterAction = '';
  protected filterEntityType = '';

  private debounceHandle: ReturnType<typeof setTimeout> | null = null;

  // ----- Computed -----
  readonly tenantOptions = computed(() =>
    this.tenants().map((t) => ({
      label: `${t.companyName} — ${t.companyEmail}`,
      value: t.tenantId
    }))
  );

  readonly hasActiveFilters = computed(
    () =>
      this.filterFrom !== null ||
      this.filterTo !== null ||
      this.filterAction.trim() !== '' ||
      this.filterEntityType.trim() !== ''
  );

  readonly integrityHint = computed(() => {
    const ig = this.integrity();
    if (!ig) return null;
    return ig.isValid ? 'Chaîne intègre' : 'Anomalie détectée';
  });

  readonly integrityTone = computed<'success' | 'danger' | 'neutral'>(() => {
    const ig = this.integrity();
    if (!ig) return 'neutral';
    return ig.isValid ? 'success' : 'danger';
  });

  ngOnInit(): void {
    this.loadTenants();
  }

  // ----- Tenants picker ---------------------------------------------------
  private loadTenants(): void {
    this.tenantsLoading.set(true);
    this.tenantsApi.list({ page: 1, pageSize: 200, sortBy: 'name', sortDir: 'asc' }).subscribe({
      next: (res) => {
        this.tenantsLoading.set(false);
        if (res.success && res.data) {
          this.tenants.set(res.data.items ?? []);
        }
      },
      error: () => this.tenantsLoading.set(false)
    });
  }

  onTenantChange(value: string | null): void {
    this.selectedTenantId.set(value);
    this.tableFirst.set(0);
    this.page.set(null);
    this.integrity.set(null);
    if (value) {
      this.loadIntegrity();
      this.loadList();
    }
  }

  // ----- Filtres ----------------------------------------------------------
  onFilterChange(): void {
    this.tableFirst.set(0);
    this.loadList();
  }

  onFilterChangeDebounced(): void {
    if (this.debounceHandle) clearTimeout(this.debounceHandle);
    this.debounceHandle = setTimeout(() => this.onFilterChange(), 350);
  }

  resetFilters(): void {
    this.filterFrom = null;
    this.filterTo = null;
    this.filterAction = '';
    this.filterEntityType = '';
    this.onFilterChange();
  }

  reload(): void {
    this.loadIntegrity();
    this.loadList();
  }

  // ----- Lazy load --------------------------------------------------------
  onLazyLoad(event: TableLazyLoadEvent): void {
    this.tableFirst.set(event.first ?? 0);
    this.pageSize.set(event.rows ?? 25);
    this.loadList();
  }

  private currentPage(): number {
    return Math.floor(this.tableFirst() / this.pageSize()) + 1;
  }

  private toIso(date: Date | null): string | undefined {
    return date ? date.toISOString() : undefined;
  }

  private loadList(): void {
    const tenantId = this.selectedTenantId();
    if (!tenantId) return;

    this.loading.set(true);
    this.api
      .list(tenantId, {
        from: this.toIso(this.filterFrom),
        to: this.toIso(this.filterTo),
        action: this.filterAction || undefined,
        entityType: this.filterEntityType || undefined,
        page: this.currentPage(),
        pageSize: this.pageSize()
      })
      .subscribe({
        next: (res) => {
          this.loading.set(false);
          if (res.success && res.data) {
            this.page.set(res.data);
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

  loadIntegrity(): void {
    const tenantId = this.selectedTenantId();
    if (!tenantId) return;
    this.integrityLoading.set(true);
    this.api.integrityReport(tenantId).subscribe({
      next: (res) => {
        this.integrityLoading.set(false);
        if (res.success && res.data) this.integrity.set(res.data);
      },
      error: () => this.integrityLoading.set(false)
    });
  }

  integrityBrokenDesc(ig: AuditChainVerificationDto): string {
    const reason = ig.firstFailureReason ?? '';
    const key = `integrity.broken.desc.${reason}` as keyof typeof AUDIT_FR;
    const dict = AUDIT_FR as Record<string, string>;
    return dict[key] ?? AUDIT_FR['integrity.broken.title'];
  }

  // ----- Détail -----------------------------------------------------------
  openDetail(row: AuditLogEntryDto): void {
    const tenantId = this.selectedTenantId();
    if (!tenantId) return;
    this.api.getById(tenantId, row.id).subscribe({
      next: (res) => {
        if (res.success && res.data) {
          this.detailRow.set(res.data);
          this.detailDialogOpen = true;
        } else {
          this.toastError(res.message);
        }
      },
      error: () => this.toastError()
    });
  }

  // ----- Export -----------------------------------------------------------
  exportFile(format: 'csv' | 'pdf'): void {
    const tenantId = this.selectedTenantId();
    if (!tenantId) return;
    this.api
      .export(tenantId, format, {
        from: this.toIso(this.filterFrom),
        to: this.toIso(this.filterTo),
        action: this.filterAction || undefined,
        entityType: this.filterEntityType || undefined
      })
      .subscribe({
        next: (blob) => {
          const tenant = this.tenants().find((t) => t.tenantId === tenantId);
          const tenantName = tenant?.companyName?.replace(/[^\w-]/g, '_') ?? 'tenant';
          const stamp = new Date().toISOString().slice(0, 10);
          const fileName = `audit-${tenantName}-${stamp}.${format}`;
          downloadBlob(blob, fileName);
          this.toast.add({
            severity: 'success',
            summary: AUDIT_FR['toast.export.success'],
            detail: fileName
          });
        },
        error: () => {
          this.toast.add({
            severity: 'error',
            summary: AUDIT_FR['toast.export.fail'],
            detail: ''
          });
        }
      });
  }

  private toastError(detail?: string | null): void {
    this.toast.add({
      severity: 'error',
      summary: AUDIT_FR['error.title'],
      detail: detail ?? AUDIT_FR['error.detail']
    });
  }
}

function downloadBlob(blob: Blob, fileName: string): void {
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = fileName;
  document.body.appendChild(a);
  a.click();
  document.body.removeChild(a);
  setTimeout(() => URL.revokeObjectURL(url), 1000);
}
