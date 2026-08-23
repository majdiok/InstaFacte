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
import { TooltipModule } from 'primeng/tooltip';
import { MessageService } from 'primeng/api';

import { PlatformCouponsService } from '@core/services/platform-coupons.service';
import {
  CouponType,
  type CouponDto,
  type CouponsPageDto,
  type CreateCouponRequest,
  type UpdateCouponRequest
} from '@core/models/platform.models';

import { FtPageHeaderComponent } from '@core/ui/page-header/ft-page-header.component';
import { FtKpiCardComponent } from '@core/ui/kpi-card/ft-kpi-card.component';
import { FtFilterToolbarComponent } from '@core/ui/filter-toolbar/ft-filter-toolbar.component';
import { FtBadgeComponent } from '@core/ui/badge/ft-badge.component';
import { FtSkeletonComponent } from '@core/ui/skeleton/ft-skeleton.component';
import { FtEmptyStateComponent } from '@core/ui/empty-state/ft-empty-state.component';
import type { FtTone } from '@core/ui/badge/ft-badge.component';

import { COUPONS_FR } from './coupons.i18n.fr';
import { CouponFormDialogComponent } from './coupon-form-dialog.component';

@Component({
  selector: 'app-platform-coupons-page',
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
    TooltipModule,
    FtPageHeaderComponent,
    FtKpiCardComponent,
    FtFilterToolbarComponent,
    FtBadgeComponent,
    FtSkeletonComponent,
    FtEmptyStateComponent,
    CouponFormDialogComponent
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
          [label]="t('list.actions.create')"
          icon="pi pi-plus"
          severity="primary"
          (onClick)="openCreate()" />
      </ng-container>
    </ft-page-header>

    <section class="kpi-row kpi-row--compact" role="region">
      <ft-kpi-card [label]="t('kpi.total')" [value]="page()?.totalCount ?? null" tone="info" icon="pi pi-tag" [loading]="loading()" />
      <ft-kpi-card [label]="t('kpi.active')" [value]="page()?.activeCount ?? null" tone="success" icon="pi pi-check-circle" [loading]="loading()" />
      <ft-kpi-card [label]="t('kpi.redeemable')" [value]="page()?.redeemableCount ?? null" tone="accent" icon="pi pi-bolt" [loading]="loading()" />
      <ft-kpi-card [label]="t('kpi.redemptions')" [value]="page()?.totalRedemptions ?? null" tone="neutral" icon="pi pi-shopping-cart" [loading]="loading()" />
    </section>

    <ft-filter-toolbar>
      <input
        type="text"
        pInputText
        [(ngModel)]="search"
        (ngModelChange)="onSearchChange()"
        [placeholder]="t('filter.search')" />
      <p-select
        [options]="activeOptions"
        [ngModel]="filterActive()"
        (ngModelChange)="onFilterActiveChange($event)"
        optionLabel="label"
        optionValue="value"
        [placeholder]="t('filter.activeOnly')"
        styleClass="ft-dd" />
    </ft-filter-toolbar>

    <p-table
      [value]="page()?.items ?? []"
      [loading]="loading()"
      styleClass="p-datatable-sm ft-table"
      [tableStyle]="{ 'min-width': '60rem' }"
      responsiveLayout="scroll">
      <ng-template pTemplate="header">
        <tr>
          <th scope="col">{{ t('col.code') }}</th>
          <th scope="col">{{ t('col.type') }}</th>
          <th scope="col">{{ t('col.value') }}</th>
          <th scope="col">{{ t('col.validity') }}</th>
          <th scope="col">{{ t('col.usage') }}</th>
          <th scope="col">{{ t('col.state') }}</th>
          <th scope="col" class="col-actions">{{ t('col.actions') }}</th>
        </tr>
      </ng-template>

      <ng-template pTemplate="body" let-row>
        <tr>
          <td><code class="cell-code">{{ row.code }}</code></td>
          <td>
            <ft-badge [tone]="row.type === 0 ? 'info' : 'accent'" size="sm">{{ row.typeDisplay }}</ft-badge>
          </td>
          <td>
            @if (row.type === 0) {
              <strong>{{ row.value | number: '1.0-2' }}%</strong>
            } @else {
              <strong>{{ row.value | number: '1.0-3' }} TND</strong>
            }
          </td>
          <td>
            <span class="muted">{{ row.validFrom | date: 'dd/MM/yy' }}</span>
            →
            <span>{{ row.validTo | date: 'dd/MM/yy' }}</span>
          </td>
          <td>
            <span [class.danger]="row.maxRedemptions !== null && row.redeemedCount >= row.maxRedemptions">
              {{ row.redeemedCount }}
              @if (row.maxRedemptions !== null) {
                <span class="muted">/ {{ row.maxRedemptions }}</span>
              } @else {
                <span class="muted">/ ∞</span>
              }
            </span>
          </td>
          <td>
            <ft-badge [tone]="stateTone(row)" [withDot]="true" size="sm">{{ stateLabel(row) }}</ft-badge>
            @if (row.appliesToPlanCode) {
              <small class="muted block">→ {{ row.appliesToPlanCode }}</small>
            }
          </td>
          <td class="col-actions">
            <p-button
              icon="pi pi-pencil"
              [text]="true"
              size="small"
              [pTooltip]="t('action.edit')"
              tooltipPosition="left"
              [disabled]="busy()"
              (onClick)="openEdit(row)" />
            @if (row.isActive) {
              <p-button
                icon="pi pi-pause"
                [text]="true"
                severity="warn"
                size="small"
                [pTooltip]="t('action.deactivate')"
                tooltipPosition="left"
                [disabled]="busy()"
                (onClick)="deactivate(row)" />
            } @else {
              <p-button
                icon="pi pi-play"
                [text]="true"
                severity="success"
                size="small"
                [pTooltip]="t('action.reactivate')"
                tooltipPosition="left"
                [disabled]="busy()"
                (onClick)="reactivate(row)" />
            }
          </td>
        </tr>
      </ng-template>

      <ng-template pTemplate="emptymessage">
        <tr>
          <td colspan="7">
            <ft-empty-state
              variant="table-empty"
              [title]="t('empty.title')"
              [description]="t('empty.desc')">
              <p-button
                [label]="t('list.actions.create')"
                icon="pi pi-plus"
                (onClick)="openCreate()" />
            </ft-empty-state>
          </td>
        </tr>
      </ng-template>

      <ng-template pTemplate="loadingbody">
        @for (i of skeletonRows; track $index) {
          <tr>
            <td><ft-skeleton shape="line" width="6rem" /></td>
            <td><ft-skeleton shape="line" width="4rem" /></td>
            <td><ft-skeleton shape="line" width="4rem" /></td>
            <td><ft-skeleton shape="line" width="8rem" /></td>
            <td><ft-skeleton shape="line" width="3rem" /></td>
            <td><ft-skeleton shape="line" width="5rem" /></td>
            <td class="col-actions"><ft-skeleton shape="circle" width="1.5rem" height="1.5rem" /></td>
          </tr>
        }
      </ng-template>
    </p-table>

    <app-coupon-form-dialog
      [(visible)]="formDialogOpen"
      [editing]="formEditing()"
      [busy]="busy()"
      (confirmed)="onFormConfirmed($event)" />
  `,
  styles: [
    `
      :host {
        display: block;
      }

      .cell-code {
        font-family: ui-monospace, SFMono-Regular, monospace;
        font-size: 0.85rem;
        background: var(--ft-surface-2);
        padding: 0.1rem 0.4rem;
        border-radius: var(--ft-radius-sm);
        color: var(--ft-accent);
        font-weight: 600;
      }

      .muted {
        color: var(--ft-text-muted);
      }

      .danger {
        color: var(--ft-danger-text);
        font-weight: 600;
      }

      .block {
        display: block;
      }
    `
  ]
})
export class PlatformCouponsPageComponent implements OnInit {
  private readonly api = inject(PlatformCouponsService);
  private readonly toast = inject(MessageService);

  protected readonly skeletonRows = Array.from({ length: 5 });
  protected t(key: keyof typeof COUPONS_FR): string {
    return COUPONS_FR[key];
  }

  readonly page = signal<CouponsPageDto | null>(null);
  readonly loading = signal(false);
  readonly busy = signal(false);
  readonly filterActive = signal<boolean | null>(null);
  protected search = '';
  private searchDebounce: ReturnType<typeof setTimeout> | null = null;

  formDialogOpen = false;
  readonly formEditing = signal<CouponDto | null>(null);

  protected readonly activeOptions = [
    { label: COUPONS_FR['filter.all'], value: null },
    { label: COUPONS_FR['filter.activeOnlyTrue'], value: true },
    { label: COUPONS_FR['filter.activeOnlyFalse'], value: false }
  ];

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.api.list(this.filterActive(), this.search, 1, 100).subscribe({
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

  onSearchChange(): void {
    if (this.searchDebounce) clearTimeout(this.searchDebounce);
    this.searchDebounce = setTimeout(() => this.load(), 350);
  }

  onFilterActiveChange(value: boolean | null): void {
    this.filterActive.set(value);
    this.load();
  }

  openCreate(): void {
    this.formEditing.set(null);
    this.formDialogOpen = true;
  }

  openEdit(coupon: CouponDto): void {
    this.formEditing.set(coupon);
    this.formDialogOpen = true;
  }

  onFormConfirmed(payload: { isEdit: boolean; id?: string; create?: CreateCouponRequest; update?: UpdateCouponRequest }): void {
    this.busy.set(true);
    const obs = payload.isEdit && payload.id
      ? this.api.update(payload.id, payload.update!)
      : this.api.create(payload.create!);
    obs.subscribe({
      next: (res) => {
        this.busy.set(false);
        if (res.success) {
          this.formDialogOpen = false;
          this.toast.add({
            severity: 'success',
            summary: payload.isEdit ? COUPONS_FR['toast.update.success'] : COUPONS_FR['toast.create.success'],
            detail: ''
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

  deactivate(coupon: CouponDto): void {
    this.busy.set(true);
    this.api.deactivate(coupon.id).subscribe({
      next: (res) => {
        this.busy.set(false);
        if (res.success) {
          this.toast.add({ severity: 'success', summary: COUPONS_FR['toast.deactivate.success'], detail: coupon.code });
          this.load();
        } else this.toastError(res.message);
      },
      error: () => { this.busy.set(false); this.toastError(); }
    });
  }

  reactivate(coupon: CouponDto): void {
    this.busy.set(true);
    this.api.reactivate(coupon.id).subscribe({
      next: (res) => {
        this.busy.set(false);
        if (res.success) {
          this.toast.add({ severity: 'success', summary: COUPONS_FR['toast.reactivate.success'], detail: coupon.code });
          this.load();
        } else this.toastError(res.message);
      },
      error: () => { this.busy.set(false); this.toastError(); }
    });
  }

  stateTone(c: CouponDto): FtTone {
    if (!c.isActive) return 'neutral';
    if (c.isRedeemable) return 'success';
    return 'warning';
  }

  stateLabel(c: CouponDto): string {
    if (!c.isActive) return COUPONS_FR['state.deactivated'];
    if (c.isRedeemable) return COUPONS_FR['state.redeemable'];
    const now = new Date();
    if (new Date(c.validFrom) > now) return COUPONS_FR['state.notYetValid'];
    if (new Date(c.validTo) < now) return COUPONS_FR['state.expired'];
    if (c.maxRedemptions !== null && c.redeemedCount >= c.maxRedemptions) return COUPONS_FR['state.exhausted'];
    return '—';
  }

  private toastError(detail?: string | null): void {
    this.toast.add({
      severity: 'error',
      summary: COUPONS_FR['toast.error.title'],
      detail: detail ?? COUPONS_FR['toast.error.detail']
    });
  }
}
