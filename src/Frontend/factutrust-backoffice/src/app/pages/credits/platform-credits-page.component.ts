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
import { TableModule } from 'primeng/table';
import { InputTextModule } from 'primeng/inputtext';
import { ButtonModule } from 'primeng/button';
import { SelectModule } from 'primeng/select';
import { DatePickerModule } from 'primeng/datepicker';
import { InputNumberModule } from 'primeng/inputnumber';
import { DialogModule } from 'primeng/dialog';
import { TooltipModule } from 'primeng/tooltip';
import { MessageService } from 'primeng/api';

import { PlatformTenantCreditsService } from '@core/services/platform-tenant-credits.service';
import { PlatformTenantService } from '@core/services/platform-tenant.service';
import type {
  GrantTenantCreditRequest,
  PlatformTenantListItemDto,
  TenantCreditDto,
  TenantCreditsPageDto
} from '@core/models/platform.models';

import { FtPageHeaderComponent } from '@core/ui/page-header/ft-page-header.component';
import { FtKpiCardComponent } from '@core/ui/kpi-card/ft-kpi-card.component';
import { FtFilterToolbarComponent } from '@core/ui/filter-toolbar/ft-filter-toolbar.component';
import { FtBadgeComponent } from '@core/ui/badge/ft-badge.component';
import { FtSkeletonComponent } from '@core/ui/skeleton/ft-skeleton.component';
import { FtEmptyStateComponent } from '@core/ui/empty-state/ft-empty-state.component';
import { FtConfirmActionComponent } from '@core/ui/confirm-action/ft-confirm-action.component';
import { FtTndCurrencyPipe } from '@core/pipes/ft-tnd-currency.pipe';
import type { FtTone } from '@core/ui/badge/ft-badge.component';

import { CREDITS_FR } from './credits.i18n.fr';

@Component({
  selector: 'app-platform-credits-page',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    DatePipe,
    DecimalPipe,
    FormsModule,
    TableModule,
    InputTextModule,
    ButtonModule,
    SelectModule,
    DatePickerModule,
    InputNumberModule,
    DialogModule,
    TooltipModule,
    FtPageHeaderComponent,
    FtKpiCardComponent,
    FtFilterToolbarComponent,
    FtBadgeComponent,
    FtSkeletonComponent,
    FtEmptyStateComponent,
    FtConfirmActionComponent,
    FtTndCurrencyPipe
  ],
  template: `
    <ft-page-header [title]="t('list.title')" [subtitle]="t('list.subtitle')">
      <ng-container ftActions>
        <p-button
          [label]="t('list.actions.refresh')"
          icon="pi pi-refresh"
          [outlined]="true"
          [disabled]="loading()"
          (onClick)="load()" />
        <p-button
          [label]="t('list.actions.grant')"
          icon="pi pi-plus"
          severity="primary"
          (onClick)="openGrant()" />
      </ng-container>
    </ft-page-header>

    <section class="kpi-row" role="region">
      <ft-kpi-card [label]="t('kpi.total')" [value]="page()?.totalCount ?? null" tone="info" icon="pi pi-wallet" [loading]="loading()" />
      <ft-kpi-card [label]="t('kpi.active')" [value]="page()?.activeCount ?? null" tone="success" icon="pi pi-check-circle" [loading]="loading()" />
      <ft-kpi-card [label]="t('kpi.granted')" [value]="page()?.totalGrantedTND ?? null" tone="accent" icon="pi pi-money-bill" hint="TND" [loading]="loading()" />
      <ft-kpi-card [label]="t('kpi.remaining')" [value]="page()?.totalRemainingTND ?? null" tone="warning" icon="pi pi-calculator" hint="TND" [loading]="loading()" />
    </section>

    <ft-filter-toolbar>
      <p-select
        [options]="tenantOptions()"
        [(ngModel)]="filterTenantId"
        (ngModelChange)="onTenantFilterChange()"
        optionLabel="label"
        optionValue="value"
        [placeholder]="t('filter.tenant')"
        [showClear]="true"
        [filter]="true"
        styleClass="ft-dd-large" />
      <p-select
        [options]="activeOptions"
        [(ngModel)]="filterActiveOnly"
        (ngModelChange)="onActiveFilterChange()"
        optionLabel="label"
        optionValue="value"
        [placeholder]="t('filter.activeOnly')"
        styleClass="ft-dd" />
    </ft-filter-toolbar>

    <p-table
      [value]="page()?.items ?? []"
      [loading]="loading()"
      styleClass="p-datatable-sm ft-credits-table"
      [tableStyle]="{ 'min-width': '70rem' }"
      responsiveLayout="scroll">
      <ng-template pTemplate="header">
        <tr>
          <th scope="col">{{ t('col.tenant') }}</th>
          <th scope="col">{{ t('col.amount') }}</th>
          <th scope="col">{{ t('col.consumed') }}</th>
          <th scope="col">{{ t('col.remaining') }}</th>
          <th scope="col">{{ t('col.reason') }}</th>
          <th scope="col">{{ t('col.granted') }}</th>
          <th scope="col">{{ t('col.expires') }}</th>
          <th scope="col">{{ t('col.state') }}</th>
          <th scope="col" class="col-actions">{{ t('col.actions') }}</th>
        </tr>
      </ng-template>

      <ng-template pTemplate="body" let-row>
        <tr>
          <td><strong>{{ row.tenantName }}</strong></td>
          <td><strong>{{ row.amountTND | ftTndCurrency: { fractionDigits: 0 } }}</strong></td>
          <td>{{ row.consumedAmountTND | number: '1.0-3' }} <span class="muted">TND</span></td>
          <td>
            <strong [class.danger]="row.remainingTND === 0">
              {{ row.remainingTND | number: '1.0-3' }}
            </strong>
            <span class="muted">TND</span>
          </td>
          <td class="reason-cell" [pTooltip]="row.reason">{{ row.reason }}</td>
          <td>{{ row.grantedAt | date: 'dd/MM/yy' }}</td>
          <td>
            @if (row.expiresAt) {
              {{ row.expiresAt | date: 'dd/MM/yy' }}
            } @else {
              <span class="muted">∞</span>
            }
          </td>
          <td>
            <ft-badge [tone]="stateTone(row)" [withDot]="true" size="sm">{{ stateLabel(row) }}</ft-badge>
          </td>
          <td class="col-actions">
            @if (row.isActive) {
              <p-button
                icon="pi pi-ban"
                [text]="true"
                severity="danger"
                size="small"
                [pTooltip]="t('action.revoke')"
                tooltipPosition="left"
                [disabled]="busy()"
                (onClick)="askRevoke(row)" />
            }
          </td>
        </tr>
      </ng-template>

      <ng-template pTemplate="emptymessage">
        <tr>
          <td colspan="9">
            <ft-empty-state
              variant="table-empty"
              [title]="t('empty.title')"
              [description]="t('empty.desc')">
              <p-button [label]="t('list.actions.grant')" icon="pi pi-plus" (onClick)="openGrant()" />
            </ft-empty-state>
          </td>
        </tr>
      </ng-template>

      <ng-template pTemplate="loadingbody">
        @for (i of skeletonRows; track $index) {
          <tr>
            <td><ft-skeleton shape="line" width="60%" /></td>
            <td><ft-skeleton shape="line" width="4rem" /></td>
            <td><ft-skeleton shape="line" width="4rem" /></td>
            <td><ft-skeleton shape="line" width="4rem" /></td>
            <td><ft-skeleton shape="line" width="60%" /></td>
            <td><ft-skeleton shape="line" width="4rem" /></td>
            <td><ft-skeleton shape="line" width="4rem" /></td>
            <td><ft-skeleton shape="line" width="4rem" /></td>
            <td class="col-actions"><ft-skeleton shape="circle" width="1.5rem" height="1.5rem" /></td>
          </tr>
        }
      </ng-template>
    </p-table>

    <!-- Grant dialog -->
    <p-dialog
      [(visible)]="grantDialogOpen"
      [modal]="true"
      [closable]="!busy()"
      [draggable]="false"
      [resizable]="false"
      [style]="{ width: '32rem', maxWidth: '95vw' }"
      [header]="t('grant.title')">
      <p class="muted">{{ t('grant.intro') }}</p>

      <div class="field">
        <label for="gr-tenant">{{ t('grant.field.tenant') }}</label>
        <p-select
          inputId="gr-tenant"
          [options]="tenantOptionsNonNull()"
          [(ngModel)]="grantTenantId"
          optionLabel="label"
          optionValue="value"
          [filter]="true"
          [disabled]="busy()"
          styleClass="w-full" />
      </div>

      <div class="field">
        <label for="gr-amount">{{ t('grant.field.amount') }}</label>
        <p-inputNumber
          inputId="gr-amount"
          [(ngModel)]="grantAmount"
          [min]="0.001"
          [maxFractionDigits]="3"
          suffix=" TND"
          [disabled]="busy()"
          styleClass="w-full" />
      </div>

      <div class="field">
        <label for="gr-reason">{{ t('grant.field.reason') }}</label>
        <input
          id="gr-reason"
          type="text"
          pInputText
          [(ngModel)]="grantReason"
          maxlength="500"
          [disabled]="busy()"
          class="w-full" />
      </div>

      <div class="field">
        <label for="gr-expires">{{ t('grant.field.expiresAt') }}</label>
        <p-datepicker
          inputId="gr-expires"
          [(ngModel)]="grantExpiresAt"
          dateFormat="dd/mm/yy"
          [showIcon]="true"
          [showClear]="true"
          [disabled]="busy()"
          styleClass="w-full" />
      </div>

      <ng-template pTemplate="footer">
        <p-button label="Annuler" [text]="true" severity="secondary" [disabled]="busy()" (onClick)="grantDialogOpen = false" />
        <p-button
          [label]="t('grant.confirm')"
          icon="pi pi-plus"
          severity="primary"
          [disabled]="busy() || !canGrantConfirm()"
          [loading]="busy()"
          (onClick)="onGrantConfirmed()" />
      </ng-template>
    </p-dialog>

    <!-- Revoke confirm -->
    <ft-confirm-action
      [(visible)]="revokeDialogOpen"
      variant="destructive"
      [title]="t('revoke.title')"
      [description]="t('revoke.desc')"
      confirmKeyword="REVOQUER"
      [confirmLabel]="t('revoke.confirm')"
      confirmIcon="pi pi-ban"
      [busy]="busy()"
      (confirmed)="onRevokeConfirmed()">
      <div class="field">
        <label for="rv-reason">{{ t('revoke.field.reason') }}</label>
        <input
          id="rv-reason"
          type="text"
          pInputText
          [(ngModel)]="revokeReason"
          maxlength="500"
          [disabled]="busy()"
          class="w-full" />
      </div>
    </ft-confirm-action>
  `,
  styles: [
    `
      :host {
        display: block;
      }

      .kpi-row {
        display: grid;
        grid-template-columns: repeat(auto-fit, minmax(13rem, 1fr));
        gap: var(--gap-md);
        margin-bottom: var(--gap-md);
      }

      .muted {
        color: var(--ft-text-muted);
      }

      .danger {
        color: var(--ft-danger-text);
      }

      .reason-cell {
        max-width: 14rem;
        overflow: hidden;
        text-overflow: ellipsis;
        white-space: nowrap;
      }

      .col-actions {
        text-align: end;
        white-space: nowrap;
        width: 4rem;
      }

      :host ::ng-deep .ft-dd {
        min-width: 11rem;
      }

      :host ::ng-deep .ft-dd-large {
        min-width: 16rem;
      }

      :host ::ng-deep .ft-credits-table.p-datatable .p-datatable-thead > tr > th {
        font-size: 0.72rem;
        text-transform: uppercase;
        letter-spacing: 0.05em;
        color: var(--ft-text-muted);
        border-color: var(--ft-border);
        background: var(--ft-surface-2);
        font-weight: 600;
      }

      .field {
        display: flex;
        flex-direction: column;
        gap: 0.4rem;
        margin-bottom: var(--gap-sm);
      }

      .field label {
        font-size: 0.78rem;
        text-transform: uppercase;
        letter-spacing: 0.05em;
        color: var(--ft-text-muted);
        font-weight: 600;
      }

      :host ::ng-deep .w-full {
        width: 100%;
      }
    `
  ]
})
export class PlatformCreditsPageComponent implements OnInit {
  private readonly api = inject(PlatformTenantCreditsService);
  private readonly tenantsApi = inject(PlatformTenantService);
  private readonly toast = inject(MessageService);

  protected readonly skeletonRows = Array.from({ length: 5 });
  protected t(key: keyof typeof CREDITS_FR): string {
    return CREDITS_FR[key];
  }

  readonly page = signal<TenantCreditsPageDto | null>(null);
  readonly tenants = signal<PlatformTenantListItemDto[]>([]);
  readonly loading = signal(false);
  readonly busy = signal(false);

  protected filterTenantId: string | null = null;
  protected filterActiveOnly: boolean | null = true;

  // Grant dialog
  grantDialogOpen = false;
  protected grantTenantId: string | null = null;
  protected grantAmount = 50;
  protected grantReason = '';
  protected grantExpiresAt: Date | null = null;

  // Revoke dialog
  revokeDialogOpen = false;
  protected revokeReason = '';
  readonly revokeTarget = signal<TenantCreditDto | null>(null);

  protected readonly activeOptions = [
    { label: CREDITS_FR['filter.allStates'], value: null },
    { label: CREDITS_FR['filter.activeOnlyTrue'], value: true }
  ];

  readonly tenantOptions = computed(() => {
    const list = this.tenants().map((t) => ({
      label: `${t.companyName}`,
      value: t.tenantId
    }));
    return [{ label: CREDITS_FR['filter.all'], value: null }, ...list];
  });

  readonly tenantOptionsNonNull = computed(() =>
    this.tenants().map((t) => ({ label: t.companyName, value: t.tenantId }))
  );

  readonly canGrantConfirm = computed(
    () => !!this.grantTenantId && this.grantAmount > 0 && this.grantReason.trim().length >= 3
  );

  ngOnInit(): void {
    this.loadTenants();
    this.load();
  }

  private loadTenants(): void {
    this.tenantsApi.list({ page: 1, pageSize: 200 }).subscribe({
      next: (res) => {
        if (res.success && res.data) this.tenants.set(res.data.items);
      }
    });
  }

  load(): void {
    this.loading.set(true);
    const obs = this.filterTenantId
      ? this.api.listByTenant(this.filterTenantId, 1, 100)
      : this.api.listAll(this.filterActiveOnly, 1, 100);
    obs.subscribe({
      next: (res) => {
        this.loading.set(false);
        if (res.success && res.data) this.page.set(res.data);
      },
      error: () => {
        this.loading.set(false);
        this.toastError();
      }
    });
  }

  onTenantFilterChange(): void {
    this.load();
  }

  onActiveFilterChange(): void {
    this.load();
  }

  // ----- Grant -----
  openGrant(): void {
    this.grantTenantId = null;
    this.grantAmount = 50;
    this.grantReason = '';
    this.grantExpiresAt = null;
    this.grantDialogOpen = true;
  }

  onGrantConfirmed(): void {
    if (!this.canGrantConfirm() || !this.grantTenantId) return;
    this.busy.set(true);
    const request: GrantTenantCreditRequest = {
      amountTND: this.grantAmount,
      reason: this.grantReason.trim(),
      expiresAt: this.grantExpiresAt ? this.grantExpiresAt.toISOString() : undefined
    };
    this.api.grant(this.grantTenantId, request).subscribe({
      next: (res) => {
        this.busy.set(false);
        if (res.success) {
          this.grantDialogOpen = false;
          this.toast.add({
            severity: 'success',
            summary: CREDITS_FR['toast.grant.success'],
            detail: `${this.grantAmount} TND`
          });
          this.load();
        } else {
          this.toastError(res.message);
        }
      },
      error: (err) => {
        this.busy.set(false);
        this.toastError(err?.error?.message);
      }
    });
  }

  // ----- Revoke -----
  askRevoke(credit: TenantCreditDto): void {
    this.revokeTarget.set(credit);
    this.revokeReason = '';
    this.revokeDialogOpen = true;
  }

  onRevokeConfirmed(): void {
    const target = this.revokeTarget();
    if (!target || this.revokeReason.trim().length < 3) {
      this.toast.add({ severity: 'warn', summary: 'Motif requis', detail: 'Saisissez un motif (≥ 3 caractères).' });
      return;
    }
    this.busy.set(true);
    this.api.revoke(target.id, { reason: this.revokeReason.trim() }).subscribe({
      next: (res) => {
        this.busy.set(false);
        this.revokeDialogOpen = false;
        if (res.success) {
          this.toast.add({
            severity: 'success',
            summary: CREDITS_FR['toast.revoke.success'],
            detail: target.tenantName
          });
          this.load();
        } else {
          this.toastError(res.message);
        }
      },
      error: () => {
        this.busy.set(false);
        this.toastError();
      }
    });
  }

  // ----- Helpers -----
  stateTone(c: TenantCreditDto): FtTone {
    if (c.revokedAt) return 'danger';
    if (c.expiresAt && new Date(c.expiresAt) < new Date()) return 'neutral';
    if (c.remainingTND === 0) return 'neutral';
    return 'success';
  }

  stateLabel(c: TenantCreditDto): string {
    if (c.revokedAt) return CREDITS_FR['state.revoked'];
    if (c.expiresAt && new Date(c.expiresAt) < new Date()) return CREDITS_FR['state.expired'];
    if (c.remainingTND === 0) return CREDITS_FR['state.consumed'];
    return CREDITS_FR['state.active'];
  }

  private toastError(detail?: string | null): void {
    this.toast.add({
      severity: 'error',
      summary: CREDITS_FR['toast.error.title'],
      detail: detail ?? CREDITS_FR['toast.error.detail']
    });
  }
}
