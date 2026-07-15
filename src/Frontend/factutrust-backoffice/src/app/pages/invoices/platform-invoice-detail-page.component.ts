import {
  ChangeDetectionStrategy,
  Component,
  OnInit,
  inject,
  signal
} from '@angular/core';
import { DatePipe, DecimalPipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { TableModule } from 'primeng/table';
import { DialogModule } from 'primeng/dialog';
import { InputTextModule } from 'primeng/inputtext';
import { InputNumberModule } from 'primeng/inputnumber';
import { CalendarModule } from 'primeng/calendar';
import { DropdownModule } from 'primeng/dropdown';
import { TooltipModule } from 'primeng/tooltip';
import { CheckboxModule } from 'primeng/checkbox';
import { MessageService } from 'primeng/api';

import {
  PlatformInvoicesService,
  type IssueCreditNoteRequest
} from '@core/services/platform-invoices.service';
import { PlatformPaymentProvidersService } from '@core/services/platform-payment-providers.service';
import {
  PlatformPaymentMethodValue,
  type PlatformInvoiceDetailDto,
  type CreatePlatformReceiptRequest,
  type CancelPlatformInvoiceRequest,
  type WireTransferInstructionsDto
} from '@core/models/platform.models';

import { FtPageHeaderComponent } from '@core/ui/page-header/ft-page-header.component';
import { FtBadgeComponent } from '@core/ui/badge/ft-badge.component';
import { FtSkeletonComponent } from '@core/ui/skeleton/ft-skeleton.component';
import { FtConfirmActionComponent } from '@core/ui/confirm-action/ft-confirm-action.component';
import type { FtTone } from '@core/ui/badge/ft-badge.component';

import { INVOICES_FR } from './invoices.i18n.fr';

/**
 * Lot C4 — Page détail d'une facture plateforme.
 *
 * Affiche : entête, lignes, totaux, reçus.
 * Actions : Émettre (si Draft), Annuler (avec génération avoir), Voir PDF, Enregistrer paiement.
 */
@Component({
  selector: 'app-platform-invoice-detail-page',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    DatePipe,
    DecimalPipe,
    FormsModule,
    RouterLink,
    ButtonModule,
    TableModule,
    DialogModule,
    InputTextModule,
    InputNumberModule,
    CalendarModule,
    DropdownModule,
    TooltipModule,
    CheckboxModule,
    FtPageHeaderComponent,
    FtBadgeComponent,
    FtSkeletonComponent,
    FtConfirmActionComponent
  ],
  template: `
    <ft-page-header
      [title]="detail()?.number ?? 'Brouillon'"
      [subtitle]="detail()?.tenantName ?? ''">
      <ng-container ftActions>
        <p-button
          icon="pi pi-arrow-left"
          label="Retour"
          [outlined]="true"
          severity="secondary"
          routerLink="/invoices" />
        @if (detail(); as d) {
          <p-button
            icon="pi pi-file-pdf"
            [label]="t('action.viewPdf')"
            [outlined]="true"
            (onClick)="openPdf()" />
          @if (d.status === 0) {
            <p-button
              icon="pi pi-send"
              [label]="t('action.issue')"
              severity="primary"
              [loading]="busy()"
              (onClick)="issue()" />
          }
          @if (d.status !== 0 && d.status !== 5 && d.status !== 6) {
            <p-button
              icon="pi pi-times"
              [label]="t('action.cancel')"
              severity="danger"
              [outlined]="true"
              (onClick)="askCancel()" />
            <p-button
              icon="pi pi-wallet"
              [label]="t('action.addReceipt')"
              severity="success"
              (onClick)="openReceiptDialog()" />
            @if (d.remainingAmount > 0 && d.status !== 2) {
              <p-button
                icon="pi pi-credit-card"
                [label]="t('action.pay')"
                severity="primary"
                (onClick)="openPayDialog()" />
            }
          }
        }
      </ng-container>
    </ft-page-header>

    @if (loading()) {
      <ft-skeleton kind="line" count="6" />
    }
    @if (!loading() && error()) {
      <p class="error">{{ error() }}</p>
    }
    @if (!loading() && !error() && detail(); as d) {

      <!-- Synthèse -->
      <section class="grid">
        <div class="card">
          <div class="row">
            <span class="label">État</span>
            <ft-badge [tone]="statusTone(d.status)">{{ d.statusDisplay }}</ft-badge>
          </div>
          <div class="row"><span class="label">Type</span><span>{{ d.billingTypeDisplay }}</span></div>
          <div class="row"><span class="label">Date facture</span><span>{{ d.invoiceDate | date:'dd/MM/yyyy' }}</span></div>
          @if (d.dueDate) {
            <div class="row"><span class="label">Échéance</span><span>{{ d.dueDate | date:'dd/MM/yyyy' }}</span></div>
          }
          @if (d.periodFrom && d.periodTo) {
            <div class="row">
              <span class="label">Période</span>
              <span>{{ d.periodFrom | date:'dd/MM/yyyy' }} — {{ d.periodTo | date:'dd/MM/yyyy' }}</span>
            </div>
          }
          @if (d.relatedInvoiceNumber) {
            <div class="row">
              <span class="label">{{ t('detail.related') }}</span>
              <a [routerLink]="['/invoices', d.relatedInvoiceId]" class="link">{{ d.relatedInvoiceNumber }}</a>
            </div>
          }
          @if (d.cancelledReason) {
            <div class="row col">
              <span class="label">{{ t('detail.cancelledReason') }}</span>
              <span class="reason">{{ d.cancelledReason }}</span>
            </div>
          }
        </div>

        <div class="card totals">
          <div class="row"><span class="label">{{ t('detail.totals.subtotalHT') }}</span><span class="num">{{ d.subtotalHT | number:'1.3-3' }} TND</span></div>
          @if (d.discountAmount > 0) {
            <div class="row"><span class="label">{{ t('detail.totals.discount') }}</span><span class="num">-{{ d.discountAmount | number:'1.3-3' }} TND</span></div>
          }
          <div class="row"><span class="label">{{ t('detail.totals.vat') }}</span><span class="num">{{ d.vatAmount | number:'1.3-3' }} TND</span></div>
          @if (d.stampDuty > 0) {
            <div class="row"><span class="label">{{ t('detail.totals.stamp') }}</span><span class="num">{{ d.stampDuty | number:'1.3-3' }} TND</span></div>
          }
          @if (d.creditsApplied > 0) {
            <div class="row"><span class="label">{{ t('detail.totals.credits') }}</span><span class="num">-{{ d.creditsApplied | number:'1.3-3' }} TND</span></div>
          }
          <div class="row total"><span class="label">{{ t('detail.totals.totalTtc') }}</span><span class="num strong">{{ d.totalTTC | number:'1.3-3' }} TND</span></div>
          @if (d.totalReceived > 0) {
            <div class="row"><span class="label">{{ t('detail.totals.received') }}</span><span class="num">{{ d.totalReceived | number:'1.3-3' }} TND</span></div>
            <div class="row"><span class="label">{{ t('detail.totals.remaining') }}</span><span class="num remaining">{{ d.remainingAmount | number:'1.3-3' }} TND</span></div>
          }
        </div>
      </section>

      <!-- Lignes -->
      <h3 class="section-title">{{ t('detail.section.lines') }}</h3>
      <p-table [value]="d.lines" styleClass="ft-table">
        <ng-template pTemplate="header">
          <tr>
            <th>{{ t('create.lines.description') }}</th>
            <th class="num">{{ t('create.lines.quantity') }}</th>
            <th class="num">{{ t('create.lines.unitPriceHT') }}</th>
            <th class="num">{{ t('create.lines.vatRate') }}</th>
            <th class="num">{{ t('create.lines.lineTotalHT') }}</th>
          </tr>
        </ng-template>
        <ng-template pTemplate="body" let-line>
          <tr>
            <td>{{ line.description }}</td>
            <td class="num">{{ line.quantity | number:'1.0-3' }}</td>
            <td class="num">{{ line.unitPriceHT | number:'1.3-3' }}</td>
            <td class="num">{{ line.vatRate | number:'1.0-2' }}%</td>
            <td class="num">{{ line.lineTotalHT | number:'1.3-3' }}</td>
          </tr>
        </ng-template>
      </p-table>

      <!-- Reçus -->
      <h3 class="section-title">{{ t('detail.section.receipts') }}</h3>
      @if (d.receipts.length === 0) {
        <p class="empty">{{ t('detail.empty.receipts') }}</p>
      } @else {
        <p-table [value]="d.receipts" styleClass="ft-table">
          <ng-template pTemplate="header">
            <tr>
              <th>N° reçu</th>
              <th>Date</th>
              <th>Mode</th>
              <th>Référence</th>
              <th>État</th>
              <th class="num">Montant</th>
              <th></th>
            </tr>
          </ng-template>
          <ng-template pTemplate="body" let-r>
            <tr>
              <td class="strong">{{ r.receiptNumber }}</td>
              <td>{{ r.paymentDate | date:'dd/MM/yyyy' }}</td>
              <td>{{ r.methodDisplay }}</td>
              <td>{{ r.reference ?? '—' }}</td>
              <td><ft-badge [tone]="receiptTone(r.status)">{{ r.statusDisplay }}</ft-badge></td>
              <td class="num">{{ r.amountTND | number:'1.3-3' }} TND</td>
              <td class="cell-action">
                @if (r.status === 1) {
                  <p-button
                    icon="pi pi-file-pdf"
                    [text]="true"
                    size="small"
                    pTooltip="{{ t('action.receiptPdf') }}"
                    tooltipPosition="left"
                    (onClick)="openReceiptPdf(r.id)" />
                }
              </td>
            </tr>
          </ng-template>
        </p-table>
      }

      <!-- Lot C4 (complément) — Bouton avoir partiel -->
      @if (d.status !== 0 && d.status !== 5 && d.status !== 6) {
        <div class="extra-actions">
          <p-button
            [label]="t('action.creditNote')"
            icon="pi pi-replay"
            severity="secondary"
            [outlined]="true"
            size="small"
            (onClick)="openCreditNote()" />
        </div>
      }
    }

    <!-- Sous-lot C5.5 — Dialog Payer (admin checkout) -->
    <p-dialog
      [visible]="showPayDialog()"
      (visibleChange)="showPayDialog.set($event)"
      [modal]="true"
      [draggable]="false"
      [resizable]="false"
      [closable]="!busy()"
      [style]="{ width: '34rem', maxWidth: '95vw' }"
      [header]="t('pay.title')">

      @if (!payRedirectUrl() && !payWireInstructions()) {
        <p class="dialog-intro">{{ t('pay.intro') }}</p>
        <div class="provider-radio-grid">
          <button
            type="button"
            class="provider-card"
            [class.selected]="payProvider === 'konnect'"
            (click)="payProvider = 'konnect'"
            [disabled]="busy()">
            <i class="pi pi-credit-card" aria-hidden="true"></i>
            <span class="title">Konnect</span>
            <span class="hint">Carte bancaire (TND)</span>
          </button>
          <button
            type="button"
            class="provider-card"
            [class.selected]="payProvider === 'paymee'"
            (click)="payProvider = 'paymee'"
            [disabled]="busy()">
            <i class="pi pi-credit-card" aria-hidden="true"></i>
            <span class="title">Paymee</span>
            <span class="hint">Carte bancaire (TND)</span>
          </button>
          <button
            type="button"
            class="provider-card"
            [class.selected]="payProvider === 'wire'"
            (click)="payProvider = 'wire'"
            [disabled]="busy()">
            <i class="pi pi-building" aria-hidden="true"></i>
            <span class="title">Virement</span>
            <span class="hint">IBAN InstaFact</span>
          </button>
        </div>

        <ng-template pTemplate="footer">
          <p-button label="Annuler" [text]="true" severity="secondary" [disabled]="busy()" (onClick)="closePayDialog()" />
          <p-button
            [label]="t('pay.confirm')"
            icon="pi pi-arrow-right"
            severity="primary"
            [disabled]="busy() || !payProvider"
            [loading]="busy()"
            (onClick)="submitPay()" />
        </ng-template>
      }
      @if (payRedirectUrl(); as url) {
        <p class="dialog-intro">{{ t('pay.redirect.intro') }}</p>
        <a [href]="url" target="_blank" rel="noopener" class="redirect-link">
          <i class="pi pi-external-link" aria-hidden="true"></i>
          {{ url }}
        </a>
        <ng-template pTemplate="footer">
          <p-button [label]="t('pay.close')" severity="secondary" (onClick)="closePayDialog()" />
        </ng-template>
      }
      @if (payWireInstructions(); as w) {
        <p class="dialog-intro">{{ t('pay.wire.intro') }}</p>
        <dl class="wire-grid">
          <dt>Bénéficiaire</dt><dd>{{ w.companyName }}</dd>
          @if (w.bankName) { <dt>Banque</dt><dd>{{ w.bankName }}</dd> }
          <dt>IBAN</dt><dd class="mono">{{ w.iban }}</dd>
          <dt>Référence à indiquer</dt><dd class="mono">{{ w.reference }}</dd>
          <dt>Montant</dt><dd class="strong">{{ w.amountTND | number:'1.3-3' }} TND</dd>
        </dl>
        <p class="hint">{{ t('pay.wire.followup') }}</p>
        <ng-template pTemplate="footer">
          <p-button [label]="t('pay.close')" severity="secondary" (onClick)="closePayDialog()" />
        </ng-template>
      }
    </p-dialog>

    <!-- Dialog enregistrer un paiement -->
    <p-dialog
      [visible]="showReceiptDialog()"
      (visibleChange)="showReceiptDialog.set($event)"
      [modal]="true"
      [draggable]="false"
      [resizable]="false"
      [style]="{ width: '34rem', maxWidth: '95vw' }"
      [header]="t('receipt.title')">

      <div class="dialog-grid">
        <div class="field">
          <label for="rc-amount">{{ t('receipt.field.amount') }}</label>
          <p-inputNumber
            inputId="rc-amount"
            [(ngModel)]="receiptAmount"
            [min]="0.001"
            [maxFractionDigits]="3"
            [disabled]="busy()" />
        </div>
        <div class="field">
          <label for="rc-method">{{ t('receipt.field.method') }}</label>
          <p-dropdown
            inputId="rc-method"
            [options]="methodOptions"
            [(ngModel)]="receiptMethod"
            optionLabel="label"
            optionValue="value"
            [disabled]="busy()"
            styleClass="w-full" />
        </div>
        <div class="field">
          <label for="rc-date">{{ t('receipt.field.paymentDate') }}</label>
          <p-calendar
            inputId="rc-date"
            [(ngModel)]="receiptDate"
            dateFormat="dd/mm/yy"
            [showIcon]="true"
            [disabled]="busy()"
            styleClass="w-full" />
        </div>
        <div class="field">
          <label for="rc-ref">{{ t('receipt.field.reference') }}</label>
          <input id="rc-ref" type="text" pInputText [(ngModel)]="receiptReference" maxlength="120" [disabled]="busy()" class="w-full" />
        </div>
        <div class="field">
          <label for="rc-tx">{{ t('receipt.field.providerTxId') }}</label>
          <input id="rc-tx" type="text" pInputText [(ngModel)]="receiptProviderTxId" maxlength="120" [disabled]="busy()" class="w-full" />
        </div>
        <div class="field field--full">
          <label class="checkbox">
            <p-checkbox [(ngModel)]="receiptAutoConfirm" [binary]="true" [disabled]="busy()" />
            <span>{{ t('receipt.field.autoConfirm') }}</span>
          </label>
        </div>
      </div>

      <ng-template pTemplate="footer">
        <p-button label="Annuler" [text]="true" severity="secondary" [disabled]="busy()" (onClick)="closeReceiptDialog()" />
        <p-button [label]="t('receipt.confirm')" icon="pi pi-check" severity="primary"
          [disabled]="busy() || !receiptAmount || receiptAmount <= 0"
          [loading]="busy()" (onClick)="submitReceipt()" />
      </ng-template>
    </p-dialog>

    <!-- Dialog annuler -->
    <ft-confirm-action
      [visible]="showCancelDialog()"
      (visibleChange)="showCancelDialog.set($event)"
      variant="destructive"
      [title]="t('cancel.title')"
      [description]="t('cancel.desc')"
      [confirmLabel]="t('cancel.confirm')"
      confirmKeyword="ANNULER"
      [busy]="busy()"
      (confirmed)="onCancelConfirmed($event)">
      <div class="field">
        <label for="cancel-reason">{{ t('cancel.field.reason') }}</label>
        <input id="cancel-reason" type="text" pInputText [(ngModel)]="cancelReason" maxlength="500" class="w-full" />
      </div>
    </ft-confirm-action>

    <!-- Lot C4 (complément) — Dialog avoir partiel -->
    <p-dialog
      [visible]="showCreditNoteDialog()"
      (visibleChange)="showCreditNoteDialog.set($event)"
      [modal]="true"
      [draggable]="false"
      [resizable]="false"
      [closable]="!busy()"
      [style]="{ width: '32rem', maxWidth: '95vw' }"
      [header]="t('creditNote.title')">
      <p class="dialog-intro">{{ t('creditNote.desc') }}</p>
      <div class="dialog-grid">
        <div class="field field--full">
          <label for="cn-amount">{{ t('creditNote.field.amount') }}</label>
          <p-inputNumber
            inputId="cn-amount"
            [(ngModel)]="creditNoteAmount"
            [min]="0"
            [maxFractionDigits]="3"
            [disabled]="busy()"
            placeholder="(complet par défaut)" />
        </div>
        <div class="field field--full">
          <label for="cn-reason">{{ t('creditNote.field.reason') }}</label>
          <input id="cn-reason" type="text" pInputText [(ngModel)]="creditNoteReason" [disabled]="busy()" maxlength="500" class="w-full" />
        </div>
      </div>
      <ng-template pTemplate="footer">
        <p-button label="Annuler" [text]="true" severity="secondary" [disabled]="busy()" (onClick)="closeCreditNote()" />
        <p-button
          [label]="t('creditNote.confirm')"
          icon="pi pi-check"
          severity="primary"
          [disabled]="busy() || !canConfirmCreditNote()"
          [loading]="busy()"
          (onClick)="submitCreditNote()" />
      </ng-template>
    </p-dialog>
  `,
  styles: [
    `
      .grid {
        display: grid;
        grid-template-columns: 1fr 1fr;
        gap: var(--gap-md);
        margin-top: 1rem;
      }
      @media (max-width: 980px) { .grid { grid-template-columns: 1fr; } }

      .card {
        background: var(--ft-surface-2, var(--ft-surface));
        border: 1px solid var(--ft-border);
        border-radius: var(--ft-radius);
        padding: 1rem 1.1rem;
      }

      .row {
        display: flex;
        justify-content: space-between;
        align-items: center;
        padding: 0.4rem 0;
        border-bottom: 1px dashed var(--ft-border-subtle);
        gap: 1rem;
      }
      .row:last-child { border-bottom: none; }
      .row.col { flex-direction: column; align-items: flex-start; }
      .row.total {
        margin-top: 0.6rem;
        padding-top: 0.7rem;
        border-top: 1px solid var(--ft-border);
        border-bottom: none;
        font-size: 1.05rem;
      }
      .label { color: var(--ft-text-muted); font-size: 0.85rem; text-transform: uppercase; letter-spacing: 0.04em; }
      .num { font-variant-numeric: tabular-nums; font-weight: 500; }
      .num.strong { font-weight: 700; color: var(--ft-text); }
      .num.remaining { color: var(--ft-warning-text); font-weight: 600; }
      .strong { font-weight: 600; }
      .reason { color: var(--ft-warning-text); font-size: 0.9rem; }
      .link { color: var(--ft-accent); text-decoration: none; }
      .link:hover { text-decoration: underline; }

      .section-title { margin-top: 1.5rem; font-size: 0.95rem; text-transform: uppercase; letter-spacing: 0.05em; color: var(--ft-text-muted); }
      .empty { color: var(--ft-text-subtle); font-style: italic; padding: 0.6rem 0; }
      .error { color: var(--ft-danger-text); }

      .dialog-grid {
        display: grid;
        grid-template-columns: 1fr 1fr;
        gap: var(--gap-md);
      }
      .field { display: flex; flex-direction: column; gap: 0.35rem; }
      .field--full { grid-column: 1 / -1; }
      .field label { font-size: 0.78rem; text-transform: uppercase; letter-spacing: 0.05em; color: var(--ft-text-muted); font-weight: 600; }
      .checkbox { flex-direction: row; align-items: center; gap: 0.5rem; }
      :host ::ng-deep .w-full { width: 100%; }

      /* Lot C4 (complément) — extra-actions sous la table des reçus */
      .extra-actions {
        display: flex;
        gap: 0.5rem;
        margin-top: 0.6rem;
        justify-content: flex-end;
      }
      .cell-action { width: 3rem; text-align: right; }
      .dialog-grid {
        display: grid;
        grid-template-columns: 1fr;
        gap: var(--gap-md, 1rem);
      }

      /* Sous-lot C5.5 — Pay dialog */
      .dialog-intro {
        margin: 0 0 0.85rem;
        color: var(--ft-text-muted);
        font-size: 0.92rem;
        line-height: 1.45;
      }
      .provider-radio-grid {
        display: grid;
        grid-template-columns: repeat(3, 1fr);
        gap: var(--gap-md, 1rem);
        margin: 0.4rem 0;
      }
      @media (max-width: 540px) { .provider-radio-grid { grid-template-columns: 1fr; } }
      .provider-card {
        display: flex;
        flex-direction: column;
        align-items: flex-start;
        gap: 0.3rem;
        padding: 0.95rem 0.9rem;
        border: 1.5px solid var(--ft-border, #30363d);
        border-radius: var(--ft-radius, 8px);
        background: var(--ft-surface-2, #0d1117);
        color: var(--ft-text);
        text-align: left;
        cursor: pointer;
        transition: border-color var(--duration-fast, 120ms) var(--easing-standard, ease),
          background var(--duration-fast, 120ms) var(--easing-standard, ease);
      }
      .provider-card:hover:not(:disabled) {
        border-color: var(--ft-accent-border, rgba(88, 166, 255, 0.4));
        background: var(--ft-surface-3, #1c232c);
      }
      .provider-card.selected {
        border-color: var(--ft-accent, #58a6ff);
        background: var(--ft-accent-surface, rgba(88, 166, 255, 0.10));
      }
      .provider-card:disabled { opacity: 0.5; cursor: not-allowed; }
      .provider-card i { font-size: 1.3rem; color: var(--ft-accent, #58a6ff); }
      .provider-card .title { font-weight: 600; font-size: 1rem; }
      .provider-card .hint { font-size: 0.78rem; color: var(--ft-text-muted); }

      .redirect-link {
        display: inline-flex;
        align-items: center;
        gap: 0.45rem;
        padding: 0.55rem 0.7rem;
        border-radius: var(--ft-radius, 8px);
        background: var(--ft-accent-surface, rgba(88, 166, 255, 0.10));
        border: 1px solid var(--ft-accent-border, rgba(88, 166, 255, 0.4));
        color: var(--ft-accent, #58a6ff);
        text-decoration: none;
        word-break: break-all;
        font-size: 0.88rem;
      }
      .redirect-link:hover { text-decoration: underline; }

      .wire-grid {
        display: grid;
        grid-template-columns: max-content 1fr;
        gap: 0.5rem 1rem;
        margin: 0.6rem 0;
      }
      .wire-grid dt {
        color: var(--ft-text-muted);
        font-size: 0.85rem;
        margin: 0;
      }
      .wire-grid dd { margin: 0; color: var(--ft-text); font-size: 0.95rem; }
      .wire-grid dd.mono { font-family: var(--font-mono, ui-monospace); font-size: 0.92rem; }
      .wire-grid dd.strong { font-weight: 700; font-size: 1.05rem; }
    `
  ]
})
export class PlatformInvoiceDetailPageComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly api = inject(PlatformInvoicesService);
  private readonly paymentApi = inject(PlatformPaymentProvidersService);
  private readonly toast = inject(MessageService);

  protected readonly detail = signal<PlatformInvoiceDetailDto | null>(null);
  protected readonly loading = signal<boolean>(false);
  protected readonly busy = signal<boolean>(false);
  protected readonly error = signal<string | null>(null);

  protected readonly showReceiptDialog = signal<boolean>(false);
  protected readonly showCancelDialog = signal<boolean>(false);

  // Lot C4 (complément) — Credit note (avoir partiel) state
  protected readonly showCreditNoteDialog = signal<boolean>(false);
  protected creditNoteAmount: number | null = null;
  protected creditNoteReason = '';

  // Sous-lot C5.5 — Pay dialog state
  protected readonly showPayDialog = signal<boolean>(false);
  protected readonly payRedirectUrl = signal<string | null>(null);
  protected readonly payWireInstructions = signal<WireTransferInstructionsDto | null>(null);
  protected payProvider: 'konnect' | 'paymee' | 'wire' | null = null;

  protected receiptAmount: number | null = null;
  protected receiptMethod: number = PlatformPaymentMethodValue.BankTransfer;
  protected receiptDate: Date = new Date();
  protected receiptReference = '';
  protected receiptProviderTxId = '';
  protected receiptAutoConfirm = true;

  protected cancelReason = '';

  protected readonly methodOptions = [
    { label: this.t('method.bankTransfer'), value: PlatformPaymentMethodValue.BankTransfer },
    { label: this.t('method.cardKonnect'), value: PlatformPaymentMethodValue.CardKonnect },
    { label: this.t('method.cardPaymee'), value: PlatformPaymentMethodValue.CardPaymee },
    { label: this.t('method.cash'), value: PlatformPaymentMethodValue.Cash },
    { label: this.t('method.manual'), value: PlatformPaymentMethodValue.Manual }
  ];

  protected t(key: keyof typeof INVOICES_FR): string {
    return INVOICES_FR[key];
  }

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (!id) {
      this.router.navigate(['/invoices']);
      return;
    }
    this.load(id);
  }

  private load(id: string): void {
    this.loading.set(true);
    this.error.set(null);
    this.api.get(id).subscribe({
      next: (res) => {
        if (res.success && res.data) {
          this.detail.set(res.data);
          this.receiptAmount = res.data.remainingAmount > 0 ? res.data.remainingAmount : null;
        } else {
          this.error.set(res.message ?? 'Facture introuvable');
        }
        this.loading.set(false);
      },
      error: () => {
        this.error.set(this.t('toast.error.detail'));
        this.loading.set(false);
      }
    });
  }

  protected statusTone(s: number): FtTone {
    switch (s) {
      case 0: return 'neutral';
      case 1: return 'accent';
      case 2: return 'success';
      case 3: return 'warning';
      case 4: return 'danger';
      default: return 'neutral';
    }
  }

  protected receiptTone(s: number): FtTone {
    switch (s) {
      case 0: return 'warning';
      case 1: return 'success';
      case 2: return 'neutral';
      default: return 'neutral';
    }
  }

  issue(): void {
    const d = this.detail();
    if (!d) return;
    this.busy.set(true);
    this.api.issue(d.id).subscribe({
      next: (res) => {
        if (res.success && res.data) {
          this.detail.set(res.data);
          this.toast.add({ severity: 'success', summary: this.t('toast.issue.success') });
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

  openPdf(): void {
    const d = this.detail();
    if (!d) return;
    this.api.pdf(d.id).subscribe({
      next: (blob) => {
        const url = URL.createObjectURL(blob);
        window.open(url, '_blank');
      },
      error: () => {
        this.toast.add({ severity: 'error', summary: this.t('toast.error.title'), detail: this.t('toast.error.detail') });
      }
    });
  }

  // ─── Lot C4 (complément) — Receipt PDF + Credit note ─────────────────────

  openReceiptPdf(receiptId: string): void {
    this.api.receiptPdf(receiptId).subscribe({
      next: (blob) => {
        const url = URL.createObjectURL(blob);
        window.open(url, '_blank');
      },
      error: () => {
        this.toast.add({ severity: 'error', summary: this.t('toast.error.title'), detail: this.t('toast.error.detail') });
      }
    });
  }

  openCreditNote(): void {
    this.creditNoteAmount = null;
    this.creditNoteReason = '';
    this.showCreditNoteDialog.set(true);
  }

  closeCreditNote(): void {
    this.showCreditNoteDialog.set(false);
  }

  canConfirmCreditNote(): boolean {
    return (this.creditNoteReason?.trim().length ?? 0) >= 3;
  }

  submitCreditNote(): void {
    const d = this.detail();
    if (!d || !this.canConfirmCreditNote() || this.busy()) return;
    const request: IssueCreditNoteRequest = {
      reason: this.creditNoteReason.trim(),
      amountTtcTND: this.creditNoteAmount && this.creditNoteAmount > 0 ? this.creditNoteAmount : null
    };
    this.busy.set(true);
    this.api.creditNote(d.id, request).subscribe({
      next: (res) => {
        if (res.success && res.data) {
          this.toast.add({ severity: 'success', summary: this.t('toast.creditNote.success'), detail: res.data.number ?? undefined });
          this.closeCreditNote();
          // Recharge la facture d'origine pour rafraîchir les liens
          this.load(d.id);
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

  // ─── Receipts ───────────────────────────────────────────────────────────────

  openReceiptDialog(): void {
    const d = this.detail();
    if (!d) return;
    this.receiptAmount = d.remainingAmount > 0 ? d.remainingAmount : null;
    this.receiptMethod = PlatformPaymentMethodValue.BankTransfer;
    this.receiptDate = new Date();
    this.receiptReference = '';
    this.receiptProviderTxId = '';
    this.receiptAutoConfirm = true;
    this.showReceiptDialog.set(true);
  }

  closeReceiptDialog(): void {
    this.showReceiptDialog.set(false);
  }

  submitReceipt(): void {
    const d = this.detail();
    if (!d || !this.receiptAmount || this.receiptAmount <= 0) return;
    const request: CreatePlatformReceiptRequest = {
      amountTND: this.receiptAmount,
      method: this.receiptMethod as 0 | 1 | 2 | 3 | 4,
      paymentDate: this.receiptDate?.toISOString(),
      reference: this.receiptReference?.trim() || undefined,
      providerTxId: this.receiptProviderTxId?.trim() || undefined,
      autoConfirm: this.receiptAutoConfirm
    };
    this.busy.set(true);
    this.api.addReceipt(d.id, request).subscribe({
      next: (res) => {
        if (res.success) {
          this.toast.add({ severity: 'success', summary: this.t('toast.receipt.success') });
          this.closeReceiptDialog();
          this.load(d.id);
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

  // ─── Pay (Sous-lot C5.5) ───────────────────────────────────────────────────

  openPayDialog(): void {
    this.payProvider = null;
    this.payRedirectUrl.set(null);
    this.payWireInstructions.set(null);
    this.showPayDialog.set(true);
  }

  closePayDialog(): void {
    this.showPayDialog.set(false);
    this.payProvider = null;
    this.payRedirectUrl.set(null);
    this.payWireInstructions.set(null);
    // Recharge la facture en cas de wire (un reçu peut avoir été ajouté en parallèle)
    const d = this.detail();
    if (d) this.load(d.id);
  }

  submitPay(): void {
    const d = this.detail();
    if (!d || !this.payProvider || this.busy()) return;

    this.busy.set(true);
    const returnUrl = `${window.location.origin}/invoices/${d.id}`;
    this.paymentApi.initiateAdminCheckout(d.id, {
      invoiceId: d.id,
      providerCode: this.payProvider,
      returnUrl
    }).subscribe({
      next: (res) => {
        if (res.success && res.data) {
          if (res.data.redirectUrl) {
            this.payRedirectUrl.set(res.data.redirectUrl);
            // Tente d'ouvrir l'onglet automatiquement (l'admin peut bloquer le popup)
            try { window.open(res.data.redirectUrl, '_blank', 'noopener'); } catch { /* ignore */ }
          } else if (res.data.wireInstructions) {
            this.payWireInstructions.set(res.data.wireInstructions);
          } else {
            this.toast.add({ severity: 'warn', summary: this.t('toast.error.title'), detail: 'Réponse provider incomplète.' });
          }
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

  // ─── Cancel ────────────────────────────────────────────────────────────────

  askCancel(): void {
    this.cancelReason = '';
    this.showCancelDialog.set(true);
  }

  onCancelConfirmed(_: void): void {
    const d = this.detail();
    if (!d) return;
    if (!this.cancelReason || this.cancelReason.trim().length < 3) {
      this.toast.add({ severity: 'warn', summary: 'Motif requis', detail: 'Saisissez un motif (3 caractères min).' });
      return;
    }
    const request: CancelPlatformInvoiceRequest = { reason: this.cancelReason.trim() };
    this.busy.set(true);
    this.api.cancel(d.id, request).subscribe({
      next: (res) => {
        if (res.success && res.data) {
          this.detail.set(res.data);
          this.showCancelDialog.set(false);
          this.toast.add({ severity: 'success', summary: this.t('toast.cancel.success') });
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
