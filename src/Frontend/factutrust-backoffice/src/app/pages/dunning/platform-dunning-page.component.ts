import {
  ChangeDetectionStrategy,
  Component,
  OnInit,
  inject,
  signal
} from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { TableModule } from 'primeng/table';
import { ButtonModule } from 'primeng/button';
import { TabViewModule } from 'primeng/tabview';
import { DropdownModule } from 'primeng/dropdown';
import { DialogModule } from 'primeng/dialog';
import { InputTextModule } from 'primeng/inputtext';
import { InputNumberModule } from 'primeng/inputnumber';
import { TooltipModule } from 'primeng/tooltip';
import { MessageService } from 'primeng/api';

import { PlatformDunningService } from '@core/services/platform-dunning.service';
import {
  DunningOutcomeValue,
  type CreateDunningCampaignRequest,
  type DunningCampaignDto,
  type DunningCampaignsListDto,
  type DunningStateDto,
  type DunningStatesPageDto,
  type UpdateDunningCampaignRequest
} from '@core/models/platform.models';

import { FtPageHeaderComponent } from '@core/ui/page-header/ft-page-header.component';
import { FtKpiCardComponent } from '@core/ui/kpi-card/ft-kpi-card.component';
import { FtFilterToolbarComponent } from '@core/ui/filter-toolbar/ft-filter-toolbar.component';
import { FtBadgeComponent } from '@core/ui/badge/ft-badge.component';
import { FtSkeletonComponent } from '@core/ui/skeleton/ft-skeleton.component';
import { FtEmptyStateComponent } from '@core/ui/empty-state/ft-empty-state.component';
import type { FtTone } from '@core/ui/badge/ft-badge.component';

import { DUNNING_FR } from './dunning.i18n.fr';
import { DunningCampaignFormDialogComponent } from './dunning-campaign-form-dialog.component';

@Component({
  selector: 'app-platform-dunning-page',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    DatePipe,
    FormsModule,
    RouterLink,
    TableModule,
    ButtonModule,
    TabViewModule,
    DropdownModule,
    DialogModule,
    InputTextModule,
    InputNumberModule,
    TooltipModule,
    FtPageHeaderComponent,
    FtKpiCardComponent,
    FtFilterToolbarComponent,
    FtBadgeComponent,
    FtSkeletonComponent,
    FtEmptyStateComponent,
    DunningCampaignFormDialogComponent
  ],
  template: `
    <ft-page-header [title]="t('page.title')" [subtitle]="t('page.subtitle')">
      <ng-container ftActions>
        <p-button
          [label]="t('page.actions.refresh')"
          icon="pi pi-refresh"
          [outlined]="true"
          [disabled]="loading()"
          (onClick)="reloadAll()" />
      </ng-container>
    </ft-page-header>

    <p-tabView styleClass="ft-tab-view">
      <!-- TAB STATES -->
      <p-tabPanel [header]="t('tab.states')" leftIcon="pi pi-clock">
        <section class="kpi-row">
          <ft-kpi-card [label]="t('kpi.active')" [value]="states()?.activeCount ?? null" tone="warning" icon="pi pi-clock" [loading]="loading()" />
          <ft-kpi-card [label]="t('kpi.paid')" [value]="states()?.paidCount ?? null" tone="success" icon="pi pi-check-circle" [loading]="loading()" />
          <ft-kpi-card [label]="t('kpi.suspended')" [value]="states()?.suspendedCount ?? null" tone="danger" icon="pi pi-ban" [loading]="loading()" />
          <ft-kpi-card [label]="t('kpi.giveup')" [value]="states()?.giveUpCount ?? null" tone="neutral" icon="pi pi-times-circle" [loading]="loading()" />
        </section>

        <ft-filter-toolbar>
          <p-dropdown
            [options]="outcomeOptions"
            [(ngModel)]="outcomeFilter"
            optionLabel="label"
            optionValue="value"
            [showClear]="true"
            [placeholder]="t('states.filter.outcomeAll')"
            (onChange)="loadStates()"
            styleClass="filter-dropdown" />
        </ft-filter-toolbar>

        @if (loading()) {
          <ft-skeleton kind="line" count="5" />
        } @else if ((states()?.items?.length ?? 0) === 0) {
          <ft-empty-state variant="table-empty" [title]="t('states.empty.title')" [description]="t('states.empty.desc')" />
        } @else {
          <p-table [value]="states()!.items" styleClass="ft-table">
            <ng-template pTemplate="header">
              <tr>
                <th>{{ t('states.col.tenant') }}</th>
                <th>{{ t('states.col.invoice') }}</th>
                <th>{{ t('states.col.dueDate') }}</th>
                <th>{{ t('states.col.step') }}</th>
                <th>{{ t('states.col.nextAction') }}</th>
                <th class="num">{{ t('states.col.attempts') }}</th>
                <th>{{ t('states.col.outcome') }}</th>
                <th>{{ t('states.col.actions') }}</th>
              </tr>
            </ng-template>
            <ng-template pTemplate="body" let-row>
              <tr>
                <td class="strong">{{ row.tenantName }}</td>
                <td>
                  @if (row.relatedInvoiceId) {
                    <a [routerLink]="['/invoices', row.relatedInvoiceId]" class="link">
                      {{ row.relatedInvoiceNumber ?? '—' }}
                    </a>
                  } @else { <span class="muted">—</span> }
                </td>
                <td>{{ row.dueDate | date:'dd/MM/yyyy' }}</td>
                <td>#{{ row.currentStepIndex + 1 }}</td>
                <td>{{ row.nextActionAt | date:'dd/MM/yyyy' }}</td>
                <td class="num">{{ row.attemptsCount }}</td>
                <td><ft-badge [tone]="outcomeTone(row.outcome)">{{ row.outcomeDisplay }}</ft-badge></td>
                <td>
                  @if (row.outcome === 0) {
                    <p-button
                      [label]="t('states.action.renewNow')"
                      icon="pi pi-sync"
                      size="small"
                      [outlined]="true"
                      severity="success"
                      [loading]="busy()"
                      (onClick)="renewNow(row)" />
                    <p-button
                      [label]="t('states.action.extendGrace')"
                      icon="pi pi-clock"
                      size="small"
                      [outlined]="true"
                      [loading]="busy()"
                      (onClick)="askExtend(row)" />
                  }
                </td>
              </tr>
            </ng-template>
          </p-table>
        }
      </p-tabPanel>

      <!-- TAB CAMPAIGNS -->
      <p-tabPanel [header]="t('tab.campaigns')" leftIcon="pi pi-megaphone">
        <section class="kpi-row">
          <ft-kpi-card [label]="t('campaigns.kpi.total')" [value]="campaigns()?.items?.length ?? null" tone="info" icon="pi pi-list" [loading]="loading()" />
          <ft-kpi-card [label]="t('campaigns.kpi.active')" [value]="campaigns()?.activeCount ?? null" tone="success" icon="pi pi-check-circle" [loading]="loading()" />
        </section>

        <div class="tab-actions">
          <p-button
            [label]="t('campaigns.action.create')"
            icon="pi pi-plus"
            severity="primary"
            size="small"
            [disabled]="loading()"
            (onClick)="openCreateCampaign()" />
        </div>

        @if (loading()) {
          <ft-skeleton kind="line" count="3" />
        } @else if ((campaigns()?.items?.length ?? 0) === 0) {
          <ft-empty-state variant="table-empty" [title]="t('campaigns.empty.title')" [description]="t('campaigns.empty.desc')" />
        } @else {
          <p-table [value]="campaigns()!.items" styleClass="ft-table">
            <ng-template pTemplate="header">
              <tr>
                <th>{{ t('campaigns.col.name') }}</th>
                <th>{{ t('campaigns.col.steps') }}</th>
                <th>{{ t('campaigns.col.state') }}</th>
                <th>{{ t('campaigns.col.actions') }}</th>
              </tr>
            </ng-template>
            <ng-template pTemplate="body" let-row>
              <tr>
                <td>
                  <span class="campaign-name">{{ row.name }}</span>
                  @if (row.description) {
                    <span class="campaign-desc">{{ row.description }}</span>
                  }
                </td>
                <td>
                  <div class="steps-stack">
                    @for (step of row.steps; track step.label) {
                      <span class="step-pill">
                        J+{{ step.daysAfterDueDate }} · {{ step.actionDisplay }}
                      </span>
                    }
                  </div>
                </td>
                <td>
                  <ft-badge [tone]="row.isActive ? 'success' : 'neutral'">
                    {{ row.isActive ? t('campaigns.state.active') : t('campaigns.state.inactive') }}
                  </ft-badge>
                </td>
                <td class="actions-cell">
                  <p-button
                    [label]="t('campaigns.action.edit')"
                    icon="pi pi-pencil"
                    size="small"
                    [outlined]="true"
                    severity="secondary"
                    [loading]="busy()"
                    (onClick)="openEditCampaign(row)" />
                  @if (!row.isActive) {
                    <p-button
                      [label]="t('campaigns.action.activate')"
                      icon="pi pi-play"
                      size="small"
                      severity="primary"
                      [outlined]="true"
                      [loading]="busy()"
                      (onClick)="activateCampaign(row)" />
                  } @else {
                    <p-button
                      [label]="t('campaigns.action.deactivate')"
                      icon="pi pi-pause"
                      size="small"
                      severity="secondary"
                      [outlined]="true"
                      [loading]="busy()"
                      (onClick)="deactivateCampaign(row)" />
                  }
                </td>
              </tr>
            </ng-template>
          </p-table>
        }
      </p-tabPanel>
    </p-tabView>

    <!-- Extend grace dialog -->
    <p-dialog
      [visible]="showExtendDialog()"
      (visibleChange)="showExtendDialog.set($event)"
      [modal]="true"
      [draggable]="false"
      [resizable]="false"
      [closable]="!busy()"
      [style]="{ width: '24rem', maxWidth: '95vw' }"
      [header]="t('extend.title')">
      <div class="field">
        <label>{{ t('extend.field.days') }}</label>
        <p-inputNumber [(ngModel)]="extendDays" [min]="1" [max]="30" />
      </div>
      <ng-template pTemplate="footer">
        <p-button label="Annuler" [text]="true" severity="secondary" [disabled]="busy()" (onClick)="closeExtendDialog()" />
        <p-button [label]="t('extend.confirm')" icon="pi pi-check" severity="primary"
          [disabled]="busy() || !extendDays || extendDays < 1"
          [loading]="busy()" (onClick)="submitExtend()" />
      </ng-template>
    </p-dialog>

    <!-- Lot C6 (complément) — Campaign form dialog -->
    <app-dunning-campaign-form-dialog
      [(visible)]="campaignFormOpen"
      [editing]="campaignFormTarget()"
      [busy]="busy()"
      (confirmed)="onCampaignFormConfirmed($event)"
      (cancelled)="campaignFormOpen = false" />
  `,
  styles: [
    `
      .kpi-row {
        display: grid;
        grid-template-columns: repeat(4, minmax(0, 1fr));
        gap: var(--gap-md);
        margin-bottom: var(--gap-md);
      }
      @media (max-width: 880px) { .kpi-row { grid-template-columns: repeat(2, minmax(0, 1fr)); } }
      @media (max-width: 480px) { .kpi-row { grid-template-columns: 1fr; } }

      .num { text-align: right; font-variant-numeric: tabular-nums; }
      .strong { font-weight: 600; color: var(--ft-text); }
      .muted { color: var(--ft-text-subtle); }
      .link { color: var(--ft-accent); text-decoration: none; font-variant-numeric: tabular-nums; }
      .link:hover { text-decoration: underline; }

      .campaign-name { display: block; font-weight: 600; color: var(--ft-text); }
      .campaign-desc { display: block; color: var(--ft-text-muted); font-size: 0.85rem; margin-top: 0.15rem; }

      /* Lot C6 (complément) — actions row + per-row actions cell */
      .tab-actions {
        display: flex;
        justify-content: flex-end;
        margin-bottom: 0.7rem;
      }
      .actions-cell {
        display: flex;
        gap: 0.4rem;
        flex-wrap: wrap;
      }

      .steps-stack { display: flex; flex-wrap: wrap; gap: 0.4rem; }
      .step-pill {
        display: inline-block;
        padding: 0.18rem 0.55rem;
        border-radius: var(--ft-radius-sm, 6px);
        background: var(--ft-accent-muted, rgba(88, 166, 255, 0.18));
        color: var(--ft-accent, #58a6ff);
        font-size: 0.78rem;
        font-weight: 500;
      }

      .field { display: flex; flex-direction: column; gap: 0.4rem; padding: 0.5rem 0; }
      .field label { font-size: 0.78rem; text-transform: uppercase; letter-spacing: 0.05em; color: var(--ft-text-muted); font-weight: 600; }

      :host ::ng-deep .filter-dropdown { min-width: 14rem; }
      :host ::ng-deep .ft-tab-view .p-tabview-nav {
        background: transparent;
        border-bottom: 1px solid var(--ft-border, #30363d);
      }
      :host ::ng-deep .ft-tab-view .p-tabview-nav li .p-tabview-nav-link {
        background: transparent;
        color: var(--ft-text-muted);
        border-color: transparent;
      }
      :host ::ng-deep .ft-tab-view .p-tabview-nav li.p-highlight .p-tabview-nav-link {
        color: var(--ft-accent);
        border-color: var(--ft-accent);
      }
      :host ::ng-deep .ft-tab-view .p-tabview-panels { background: transparent; padding: 1.1rem 0 0; }
    `
  ]
})
export class PlatformDunningPageComponent implements OnInit {
  private readonly api = inject(PlatformDunningService);
  private readonly toast = inject(MessageService);

  protected readonly states = signal<DunningStatesPageDto | null>(null);
  protected readonly campaigns = signal<DunningCampaignsListDto | null>(null);
  protected readonly loading = signal<boolean>(false);
  protected readonly busy = signal<boolean>(false);

  protected outcomeFilter: string | null = null;

  protected readonly showExtendDialog = signal<boolean>(false);
  protected extendDays = 7;
  private extendingSubscriptionId: string | null = null;

  // Lot C6 (complément) — Campaign form dialog state
  protected campaignFormOpen = false;
  protected readonly campaignFormTarget = signal<DunningCampaignDto | null>(null);

  protected readonly outcomeOptions = [
    { label: this.t('states.filter.outcomeAll'), value: null },
    { label: this.t('states.filter.outcomeActive'), value: 'Active' },
    { label: this.t('states.filter.outcomePaid'), value: 'Paid' },
    { label: this.t('states.filter.outcomeSuspended'), value: 'Suspended' },
    { label: this.t('states.filter.outcomeGiveup'), value: 'GiveUp' }
  ];

  protected t(key: keyof typeof DUNNING_FR): string {
    return DUNNING_FR[key];
  }

  ngOnInit(): void {
    this.reloadAll();
  }

  reloadAll(): void {
    this.loadStates();
    this.loadCampaigns();
  }

  loadStates(): void {
    this.loading.set(true);
    this.api.listStates(this.outcomeFilter, null, 1, 100).subscribe({
      next: (res) => {
        if (res.success && res.data) this.states.set(res.data);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.toast.add({ severity: 'error', summary: this.t('toast.error.title'), detail: this.t('toast.error.detail') });
      }
    });
  }

  loadCampaigns(): void {
    this.api.listCampaigns().subscribe({
      next: (res) => {
        if (res.success && res.data) this.campaigns.set(res.data);
      },
      error: () => {
        // silencieux
      }
    });
  }

  protected outcomeTone(outcome: number): FtTone {
    switch (outcome) {
      case DunningOutcomeValue.Active: return 'warning';
      case DunningOutcomeValue.Paid: return 'success';
      case DunningOutcomeValue.Suspended: return 'danger';
      case DunningOutcomeValue.GiveUp: return 'neutral';
      default: return 'neutral';
    }
  }

  // ─── State actions ────────────────────────────────────────────────────────

  renewNow(row: DunningStateDto): void {
    this.busy.set(true);
    this.api.renewNow(row.subscriptionId).subscribe({
      next: (res) => {
        if (res.success) {
          this.toast.add({ severity: 'success', summary: this.t('toast.renew.success') });
          this.loadStates();
        } else {
          this.toast.add({ severity: 'error', summary: this.t('toast.error.title'), detail: res.message ?? '' });
        }
        this.busy.set(false);
      },
      error: () => {
        this.busy.set(false);
        this.toast.add({ severity: 'error', summary: this.t('toast.error.title'), detail: this.t('toast.error.detail') });
      }
    });
  }

  askExtend(row: DunningStateDto): void {
    this.extendingSubscriptionId = row.subscriptionId;
    this.extendDays = 7;
    this.showExtendDialog.set(true);
  }

  closeExtendDialog(): void {
    this.showExtendDialog.set(false);
    this.extendingSubscriptionId = null;
  }

  submitExtend(): void {
    if (!this.extendingSubscriptionId || !this.extendDays) return;
    this.busy.set(true);
    this.api.extendGrace(this.extendingSubscriptionId, { days: this.extendDays }).subscribe({
      next: (res) => {
        if (res.success) {
          this.toast.add({ severity: 'success', summary: this.t('toast.extend.success') });
          this.closeExtendDialog();
          this.loadStates();
        } else {
          this.toast.add({ severity: 'error', summary: this.t('toast.error.title'), detail: res.message ?? '' });
        }
        this.busy.set(false);
      },
      error: () => {
        this.busy.set(false);
        this.toast.add({ severity: 'error', summary: this.t('toast.error.title'), detail: this.t('toast.error.detail') });
      }
    });
  }

  // ─── Campaign actions ─────────────────────────────────────────────────────

  openCreateCampaign(): void {
    this.campaignFormTarget.set(null);
    this.campaignFormOpen = true;
  }

  openEditCampaign(row: DunningCampaignDto): void {
    this.campaignFormTarget.set(row);
    this.campaignFormOpen = true;
  }

  onCampaignFormConfirmed(payload: {
    isEdit: boolean;
    id?: string;
    create?: CreateDunningCampaignRequest;
    update?: UpdateDunningCampaignRequest;
  }): void {
    if (payload.isEdit && payload.id && payload.update) {
      this.busy.set(true);
      this.api.updateCampaign(payload.id, payload.update).subscribe({
        next: (res) => {
          this.busy.set(false);
          if (res.success) {
            this.toast.add({ severity: 'success', summary: this.t('toast.update.success') });
            this.campaignFormOpen = false;
            this.loadCampaigns();
          } else {
            this.toast.add({ severity: 'error', summary: this.t('toast.error.title'), detail: res.message ?? '' });
          }
        },
        error: () => {
          this.busy.set(false);
          this.toast.add({ severity: 'error', summary: this.t('toast.error.title'), detail: this.t('toast.error.detail') });
        }
      });
    } else if (!payload.isEdit && payload.create) {
      this.busy.set(true);
      this.api.createCampaign(payload.create).subscribe({
        next: (res) => {
          this.busy.set(false);
          if (res.success) {
            this.toast.add({ severity: 'success', summary: this.t('toast.create.success') });
            this.campaignFormOpen = false;
            this.loadCampaigns();
          } else {
            this.toast.add({ severity: 'error', summary: this.t('toast.error.title'), detail: res.message ?? '' });
          }
        },
        error: () => {
          this.busy.set(false);
          this.toast.add({ severity: 'error', summary: this.t('toast.error.title'), detail: this.t('toast.error.detail') });
        }
      });
    }
  }

  activateCampaign(row: DunningCampaignDto): void {
    this.busy.set(true);
    this.api.activate(row.id).subscribe({
      next: (res) => {
        if (res.success) {
          this.toast.add({ severity: 'success', summary: this.t('toast.activate.success') });
          this.loadCampaigns();
        } else {
          this.toast.add({ severity: 'error', summary: this.t('toast.error.title'), detail: res.message ?? '' });
        }
        this.busy.set(false);
      },
      error: () => {
        this.busy.set(false);
        this.toast.add({ severity: 'error', summary: this.t('toast.error.title'), detail: this.t('toast.error.detail') });
      }
    });
  }

  deactivateCampaign(row: DunningCampaignDto): void {
    this.busy.set(true);
    this.api.deactivate(row.id).subscribe({
      next: (res) => {
        if (res.success) {
          this.toast.add({ severity: 'success', summary: this.t('toast.deactivate.success') });
          this.loadCampaigns();
        } else {
          this.toast.add({ severity: 'error', summary: this.t('toast.error.title'), detail: res.message ?? '' });
        }
        this.busy.set(false);
      },
      error: () => {
        this.busy.set(false);
        this.toast.add({ severity: 'error', summary: this.t('toast.error.title'), detail: this.t('toast.error.detail') });
      }
    });
  }
}
