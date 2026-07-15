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
import { DialogModule } from 'primeng/dialog';
import { InputTextModule } from 'primeng/inputtext';
import { InputTextareaModule } from 'primeng/inputtextarea';
import { CheckboxModule } from 'primeng/checkbox';
import { TooltipModule } from 'primeng/tooltip';
import { MessageService } from 'primeng/api';

import { PlatformPaymentProvidersService } from '@core/services/platform-payment-providers.service';
import type {
  PaymentProviderConfigDto,
  PaymentProviderConfigsListDto,
  UpdatePaymentProviderConfigRequest
} from '@core/models/platform.models';

import { FtPageHeaderComponent } from '@core/ui/page-header/ft-page-header.component';
import { FtKpiCardComponent } from '@core/ui/kpi-card/ft-kpi-card.component';
import { FtBadgeComponent } from '@core/ui/badge/ft-badge.component';
import { FtSkeletonComponent } from '@core/ui/skeleton/ft-skeleton.component';
import { FtEmptyStateComponent } from '@core/ui/empty-state/ft-empty-state.component';
import type { FtTone } from '@core/ui/badge/ft-badge.component';

import { PAYMENTS_FR } from './payments.i18n.fr';

/**
 * Lot C5 — Page admin de configuration des providers de paiement.
 *
 * Affiche les 3 providers (Konnect / Paymee / Wire) avec leur état (activé,
 * mode test/prod, présence des secrets) et permet d'éditer la config dans un dialog.
 */
@Component({
  selector: 'app-platform-payment-providers-page',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    DatePipe,
    FormsModule,
    RouterLink,
    TableModule,
    ButtonModule,
    DialogModule,
    InputTextModule,
    InputTextareaModule,
    CheckboxModule,
    TooltipModule,
    FtPageHeaderComponent,
    FtKpiCardComponent,
    FtBadgeComponent,
    FtSkeletonComponent,
    FtEmptyStateComponent
  ],
  template: `
    <ft-page-header [title]="t('list.title')" [subtitle]="t('list.subtitle')">
      <ng-container ftActions>
        <p-button
          [label]="t('list.actions.viewIntents')"
          icon="pi pi-list"
          [outlined]="true"
          severity="secondary"
          routerLink="/payments/intents" />
        <p-button
          [label]="t('list.actions.refresh')"
          icon="pi pi-refresh"
          [outlined]="true"
          [disabled]="loading()"
          (onClick)="load()" />
      </ng-container>
    </ft-page-header>

    <section class="kpi-row">
      <ft-kpi-card
        [label]="t('kpi.total')"
        [value]="page()?.items?.length ?? null"
        tone="info" icon="pi pi-credit-card" [loading]="loading()" />
      <ft-kpi-card
        [label]="t('kpi.enabled')"
        [value]="page()?.enabledCount ?? null"
        tone="success" icon="pi pi-check-circle" [loading]="loading()" />
      <ft-kpi-card
        [label]="t('kpi.testMode')"
        [value]="testModeCount()"
        tone="warning" icon="pi pi-cog" [loading]="loading()" />
    </section>

    @if (loading()) {
      <ft-skeleton kind="line" count="4" />
    } @else if ((page()?.items?.length ?? 0) === 0) {
      <ft-empty-state variant="table-empty" [title]="t('empty.title')" [description]="t('empty.desc')" />
    } @else {
      <p-table [value]="page()!.items" styleClass="ft-table">
        <ng-template pTemplate="header">
          <tr>
            <th>{{ t('col.provider') }}</th>
            <th>{{ t('col.status') }}</th>
            <th>{{ t('col.testMode') }}</th>
            <th>{{ t('col.secrets') }}</th>
            <th>{{ t('col.webhook') }}</th>
            <th>{{ t('col.updatedAt') }}</th>
            <th>{{ t('col.actions') }}</th>
          </tr>
        </ng-template>
        <ng-template pTemplate="body" let-row>
          <tr>
            <td>
              <span class="provider-code">{{ row.providerCode }}</span>
              <span class="provider-name">{{ row.displayName }}</span>
            </td>
            <td>
              <ft-badge [tone]="row.isEnabled ? 'success' : 'neutral'">
                {{ row.isEnabled ? t('state.enabled') : t('state.disabled') }}
              </ft-badge>
            </td>
            <td>
              <ft-badge [tone]="row.isTestMode ? 'warning' : 'accent'">
                {{ row.isTestMode ? t('state.test') : t('state.live') }}
              </ft-badge>
            </td>
            <td>
              <ft-badge [tone]="row.hasSecrets ? 'success' : 'danger'">
                {{ row.hasSecrets ? t('secrets.set') : t('secrets.missing') }}
              </ft-badge>
            </td>
            <td>
              <ft-badge [tone]="row.hasWebhookSecret ? 'success' : 'warning'">
                {{ row.hasWebhookSecret ? t('secrets.set') : t('secrets.missing') }}
              </ft-badge>
            </td>
            <td>
              {{ row.updatedAt ? (row.updatedAt | date:'dd/MM/yyyy HH:mm') : '—' }}
            </td>
            <td>
              <p-button
                icon="pi pi-cog"
                [label]="t('action.configure')"
                size="small"
                [outlined]="true"
                (onClick)="openEdit(row)" />
            </td>
          </tr>
        </ng-template>
      </p-table>
    }

    <!-- Edit dialog -->
    <p-dialog
      [visible]="showEdit()"
      (visibleChange)="showEdit.set($event)"
      [modal]="true"
      [draggable]="false"
      [resizable]="false"
      [closable]="!busy()"
      [style]="{ width: '38rem', maxWidth: '95vw' }"
      [header]="editTitle()">
      @if (editing(); as e) {
        <div class="grid">
          <div class="field field--full">
            <label>{{ t('edit.field.displayName') }}</label>
            <input type="text" pInputText [(ngModel)]="form.displayName" maxlength="120" class="w-full" />
          </div>
          <div class="field field--full">
            <label class="checkbox">
              <p-checkbox [(ngModel)]="form.isEnabled" [binary]="true" />
              <span>{{ t('edit.field.isEnabled') }}</span>
            </label>
          </div>
          <div class="field field--full">
            <label class="checkbox">
              <p-checkbox [(ngModel)]="form.isTestMode" [binary]="true" />
              <span>{{ t('edit.field.isTestMode') }}</span>
            </label>
          </div>
          <div class="field field--full">
            <label>{{ t('edit.field.allowedReturnDomain') }}</label>
            <input type="text" pInputText [(ngModel)]="form.allowedReturnDomain" maxlength="254" placeholder="ex: app.factutrust.tn" class="w-full" />
          </div>
          @if (e.providerCode !== 'wire') {
            <div class="field field--full">
              <label>{{ t('edit.field.secretsJson') }}</label>
              <textarea pInputTextarea rows="4" [(ngModel)]="form.secretsJson" placeholder='{ "apiKey": "..." }' class="w-full mono"></textarea>
              <small class="hint">{{ t('edit.field.secretsJson.hint') }}</small>
            </div>
            <div class="field field--full">
              <label>{{ t('edit.field.webhookSecret') }}</label>
              <input type="password" pInputText [(ngModel)]="form.webhookSecret" maxlength="500" autocomplete="new-password" class="w-full" />
              <small class="hint">{{ t('edit.field.webhookSecret.hint') }}</small>
            </div>
          }
        </div>
      }

      <ng-template pTemplate="footer">
        <p-button label="Annuler" [text]="true" severity="secondary" [disabled]="busy()" (onClick)="closeEdit()" />
        <p-button [label]="t('edit.confirm')" icon="pi pi-check" severity="primary"
          [disabled]="busy() || !canSubmit()" [loading]="busy()" (onClick)="submitEdit()" />
      </ng-template>
    </p-dialog>
  `,
  styles: [
    `
      .kpi-row {
        display: grid;
        grid-template-columns: repeat(3, minmax(0, 1fr));
        gap: var(--gap-md);
        margin-bottom: var(--gap-lg);
      }
      @media (max-width: 720px) { .kpi-row { grid-template-columns: 1fr; } }

      .provider-code {
        display: block;
        font-family: var(--font-mono, ui-monospace);
        font-size: 0.78rem;
        text-transform: uppercase;
        color: var(--ft-text-muted);
      }
      .provider-name { font-weight: 500; color: var(--ft-text); }

      .grid { display: grid; grid-template-columns: 1fr; gap: var(--gap-md); }
      .field { display: flex; flex-direction: column; gap: 0.35rem; }
      .field--full { grid-column: 1 / -1; }
      .field label { font-size: 0.78rem; text-transform: uppercase; letter-spacing: 0.05em; color: var(--ft-text-muted); font-weight: 600; }
      .checkbox { flex-direction: row; align-items: center; gap: 0.5rem; }
      .hint { color: var(--ft-text-subtle); font-size: 0.72rem; }
      .mono { font-family: var(--font-mono, ui-monospace); }
      :host ::ng-deep .w-full { width: 100%; }
    `
  ]
})
export class PlatformPaymentProvidersPageComponent implements OnInit {
  private readonly api = inject(PlatformPaymentProvidersService);
  private readonly toast = inject(MessageService);

  protected readonly page = signal<PaymentProviderConfigsListDto | null>(null);
  protected readonly loading = signal<boolean>(false);
  protected readonly busy = signal<boolean>(false);
  protected readonly showEdit = signal<boolean>(false);
  protected readonly editing = signal<PaymentProviderConfigDto | null>(null);

  protected form: UpdatePaymentProviderConfigRequest = {
    displayName: '',
    isEnabled: false,
    isTestMode: true,
    secretsJson: '',
    webhookSecret: '',
    allowedReturnDomain: ''
  };

  protected t(key: keyof typeof PAYMENTS_FR): string {
    return PAYMENTS_FR[key];
  }

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.api.list().subscribe({
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

  protected testModeCount(): number {
    return this.page()?.items.filter((i) => i.isEnabled && i.isTestMode).length ?? 0;
  }

  protected editTitle(): string {
    const e = this.editing();
    return e ? this.t('edit.title').replace('{{provider}}', e.displayName) : '';
  }

  openEdit(row: PaymentProviderConfigDto): void {
    this.editing.set(row);
    this.form = {
      displayName: row.displayName,
      isEnabled: row.isEnabled,
      isTestMode: row.isTestMode,
      secretsJson: '',
      webhookSecret: '',
      allowedReturnDomain: row.allowedReturnDomain ?? ''
    };
    this.showEdit.set(true);
  }

  closeEdit(): void {
    this.showEdit.set(false);
    this.editing.set(null);
  }

  protected canSubmit(): boolean {
    return !!this.form.displayName?.trim();
  }

  submitEdit(): void {
    const e = this.editing();
    if (!e || !this.canSubmit()) return;
    this.busy.set(true);
    this.api.update(e.providerCode, this.form).subscribe({
      next: (res) => {
        if (res.success) {
          this.toast.add({ severity: 'success', summary: this.t('toast.update.success') });
          this.closeEdit();
          this.load();
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
