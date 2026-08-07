import {
  ChangeDetectionStrategy,
  Component,
  OnInit,
  inject,
  signal
} from '@angular/core';
import { DecimalPipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { InputNumberModule } from 'primeng/inputnumber';
import { CheckboxModule } from 'primeng/checkbox';
import { Textarea } from 'primeng/textarea';
import { TableModule } from 'primeng/table';
import { MessageService } from 'primeng/api';

import { PlatformFiscalSettingsService } from '@core/services/platform-fiscal-settings.service';
import {
  PlatformInvoicesService,
  type PlatformVatPeriodDto
} from '@core/services/platform-invoices.service';
import type {
  PlatformFiscalSettingsDto,
  UpdatePlatformFiscalSettingsRequest
} from '@core/models/platform.models';

import { FtPageHeaderComponent } from '@core/ui/page-header/ft-page-header.component';
import { FtSkeletonComponent } from '@core/ui/skeleton/ft-skeleton.component';

import { INVOICES_FR } from './invoices.i18n.fr';

/**
 * Lot C4 — Page de configuration des paramètres fiscaux singleton plateforme.
 *
 * Modifie les données émetteur (NIF, raison sociale, TVA, timbre, IBAN, mentions
 * légales) qui seront figées dans chaque facture émise.
 */
@Component({
  selector: 'app-platform-fiscal-settings-page',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    DecimalPipe,
    FormsModule,
    RouterLink,
    ButtonModule,
    InputTextModule,
    InputNumberModule,
    CheckboxModule,
    Textarea,
    TableModule,
    FtPageHeaderComponent,
    FtSkeletonComponent
  ],
  template: `
    <ft-page-header [title]="t('fiscal.title')" [subtitle]="t('fiscal.subtitle')">
      <ng-container ftActions>
        <p-button label="Retour aux factures" icon="pi pi-arrow-left" [outlined]="true" severity="secondary" routerLink="/invoices" />
      </ng-container>
    </ft-page-header>

    @if (loading()) {
      <ft-skeleton kind="line" count="6" />
    }
    @if (!loading() && current(); as f) {
      <section class="card">
        <h3>{{ t('fiscal.section.identity') }}</h3>
        <div class="grid">
          <div class="field"><label>{{ t('fiscal.field.companyName') }}</label>
            <input type="text" pInputText [(ngModel)]="form.companyName" maxlength="200" class="w-full" />
          </div>
          <div class="field"><label>{{ t('fiscal.field.nif') }}</label>
            <input type="text" pInputText [(ngModel)]="form.nif" maxlength="40" class="w-full" />
          </div>
          <div class="field"><label>{{ t('fiscal.field.codeTva') }}</label>
            <input type="text" pInputText [(ngModel)]="form.codeTva" maxlength="40" class="w-full" />
          </div>
          <div class="field field--full"><label>{{ t('fiscal.field.address') }}</label>
            <input type="text" pInputText [(ngModel)]="form.address" maxlength="500" class="w-full" />
          </div>
        </div>
      </section>

      <section class="card">
        <h3>{{ t('fiscal.section.contact') }}</h3>
        <div class="grid">
          <div class="field"><label>{{ t('fiscal.field.phone') }}</label>
            <input type="text" pInputText [(ngModel)]="form.phone" maxlength="40" class="w-full" />
          </div>
          <div class="field"><label>{{ t('fiscal.field.email') }}</label>
            <input type="email" pInputText [(ngModel)]="form.email" maxlength="254" class="w-full" />
          </div>
          <div class="field"><label>{{ t('fiscal.field.website') }}</label>
            <input type="text" pInputText [(ngModel)]="form.website" maxlength="254" class="w-full" />
          </div>
          <div class="field"><label>{{ t('fiscal.field.iban') }}</label>
            <input type="text" pInputText [(ngModel)]="form.iban" maxlength="60" class="w-full" />
          </div>
          <div class="field"><label>{{ t('fiscal.field.bankName') }}</label>
            <input type="text" pInputText [(ngModel)]="form.bankName" maxlength="120" class="w-full" />
          </div>
        </div>
      </section>

      <section class="card">
        <h3>{{ t('fiscal.section.tax') }}</h3>
        <div class="grid">
          <div class="field field--full">
            <label class="checkbox">
              <p-checkbox [(ngModel)]="form.applyVat" [binary]="true" />
              <span>{{ t('fiscal.field.applyVat') }}</span>
            </label>
          </div>
          <div class="field"><label>{{ t('fiscal.field.defaultVatRate') }}</label>
            <p-inputNumber [(ngModel)]="form.defaultVatRate" [min]="0" [max]="100" [maxFractionDigits]="2" />
          </div>
          <div class="field"><label>{{ t('fiscal.field.timbreFiscal') }}</label>
            <p-inputNumber [(ngModel)]="form.timbreFiscalAmount" [min]="0" [maxFractionDigits]="3" />
          </div>
          <div class="field field--full">
            <label class="checkbox">
              <p-checkbox [(ngModel)]="form.applyClientWithholding" [binary]="true" />
              <span>{{ t('fiscal.field.applyClientWithholding') }}</span>
            </label>
          </div>
          <div class="field"><label>{{ t('fiscal.field.clientWithholdingRate') }}</label>
            <p-inputNumber [(ngModel)]="form.clientWithholdingRate" [min]="0" [max]="100" [maxFractionDigits]="2" />
          </div>
        </div>
      </section>

      <section class="card">
        <h3>{{ t('fiscal.section.numbering') }}</h3>
        <div class="grid">
          <div class="field"><label>{{ t('fiscal.field.invoicePrefix') }}</label>
            <input type="text" pInputText [(ngModel)]="form.invoiceNumberPrefix" maxlength="10" class="w-full" />
          </div>
          <div class="field"><label>{{ t('fiscal.field.receiptPrefix') }}</label>
            <input type="text" pInputText [(ngModel)]="form.receiptNumberPrefix" maxlength="10" class="w-full" />
          </div>
        </div>
      </section>

      <section class="card">
        <h3>{{ t('fiscal.section.legal') }}</h3>
        <div class="field">
          <label>{{ t('fiscal.field.legalMentions') }}</label>
          <textarea pTextarea rows="4" [(ngModel)]="form.legalMentions" maxlength="2000" class="w-full"></textarea>
        </div>
      </section>

      <div class="footer-actions">
        <p-button label="Annuler" severity="secondary" [text]="true" routerLink="/invoices" />
        <p-button
          [label]="t('fiscal.confirm')"
          icon="pi pi-check"
          severity="primary"
          [disabled]="!canSubmit() || busy()"
          [loading]="busy()"
          (onClick)="submit()" />
      </div>

      <!-- Lot C4 (complément) — Périodes TVA DGI -->
      <section class="card vat-card">
        <header class="vat-header">
          <div>
            <h3>{{ t('vat.title') }}</h3>
            <p class="hint">{{ t('vat.subtitle') }}</p>
          </div>
          <div class="vat-year">
            <label for="vat-year">{{ t('vat.field.year') }}</label>
            <p-inputNumber
              inputId="vat-year"
              [(ngModel)]="vatYear"
              [min]="2020"
              [max]="2099"
              [showButtons]="true"
              (onBlur)="loadVatPeriods()"
              (onInput)="onVatYearChange($event)" />
          </div>
        </header>

        @if (vatLoading()) {
          <ft-skeleton kind="line" count="3" />
        } @else {
          <p-table [value]="vatPeriods()" styleClass="ft-table">
            <ng-template pTemplate="header">
              <tr>
                <th>{{ t('vat.col.month') }}</th>
                <th class="num">{{ t('vat.col.invoices') }}</th>
                <th class="num">{{ t('vat.col.ht') }}</th>
                <th class="num">{{ t('vat.col.vat') }}</th>
                <th class="num">{{ t('vat.col.stamp') }}</th>
                <th class="num">{{ t('vat.col.ttc') }}</th>
                <th class="num">{{ t('vat.col.refunds') }}</th>
              </tr>
            </ng-template>
            <ng-template pTemplate="body" let-row>
              <tr [class.empty-row]="row.invoicesCount === 0 && row.creditNotesCount === 0">
                <td class="strong">{{ row.monthLabel }}</td>
                <td class="num">{{ row.invoicesCount }}</td>
                <td class="num">{{ row.totalHT | number:'1.3-3' }}</td>
                <td class="num">{{ row.totalVat | number:'1.3-3' }}</td>
                <td class="num">{{ row.totalStamp | number:'1.3-3' }}</td>
                <td class="num strong">{{ row.totalTTC | number:'1.3-3' }}</td>
                <td class="num">
                  @if (row.creditNotesCount > 0) {
                    <span class="refund">-{{ row.creditNotesAmountTTC | number:'1.3-3' }} TND</span>
                  } @else {
                    <span class="muted">—</span>
                  }
                </td>
              </tr>
            </ng-template>
          </p-table>
        }
      </section>
    }
  `,
  styles: [
    `
      .card {
        background: var(--ft-surface-2, var(--ft-surface));
        border: 1px solid var(--ft-border);
        border-radius: var(--ft-radius);
        padding: 1.1rem;
        margin-top: 1rem;
      }
      .card h3 {
        font-size: 0.95rem;
        text-transform: uppercase;
        letter-spacing: 0.05em;
        color: var(--ft-text-muted);
        margin: 0 0 0.75rem;
      }
      .grid {
        display: grid;
        grid-template-columns: repeat(3, minmax(0, 1fr));
        gap: var(--gap-md);
      }
      @media (max-width: 980px) { .grid { grid-template-columns: repeat(2, minmax(0, 1fr)); } }
      @media (max-width: 540px) { .grid { grid-template-columns: 1fr; } }

      .field { display: flex; flex-direction: column; gap: 0.35rem; }
      .field--full { grid-column: 1 / -1; }
      .field label { font-size: 0.78rem; text-transform: uppercase; letter-spacing: 0.05em; color: var(--ft-text-muted); font-weight: 600; }
      .checkbox { flex-direction: row; align-items: center; gap: 0.5rem; }

      .footer-actions {
        margin-top: 1.25rem;
        display: flex;
        justify-content: flex-end;
        gap: 0.6rem;
      }

      :host ::ng-deep .w-full { width: 100%; }
      :host ::ng-deep .p-inputnumber { width: 100%; }

      /* Lot C4 (complément) — VAT periods card */
      .vat-card { margin-top: 1.5rem; }
      .vat-header {
        display: flex;
        justify-content: space-between;
        align-items: flex-start;
        gap: 1rem;
        margin-bottom: 1rem;
        flex-wrap: wrap;
      }
      .vat-header h3 {
        margin: 0 0 0.25rem;
        font-size: 1rem;
        color: var(--ft-text);
      }
      .vat-header .hint {
        margin: 0;
        color: var(--ft-text-muted);
        font-size: 0.85rem;
        max-width: 540px;
      }
      .vat-year {
        display: flex;
        flex-direction: column;
        gap: 0.3rem;
        min-width: 10rem;
      }
      .vat-year label {
        font-size: 0.78rem;
        text-transform: uppercase;
        letter-spacing: 0.05em;
        color: var(--ft-text-muted);
        font-weight: 600;
      }
      .num { text-align: right; font-variant-numeric: tabular-nums; }
      .strong { font-weight: 600; color: var(--ft-text); }
      .muted { color: var(--ft-text-subtle); font-style: italic; }
      .refund { color: var(--ft-warning-text, #d29922); font-weight: 500; }
      .empty-row td { color: var(--ft-text-subtle); }
    `
  ]
})
export class PlatformFiscalSettingsPageComponent implements OnInit {
  private readonly api = inject(PlatformFiscalSettingsService);
  private readonly invoicesApi = inject(PlatformInvoicesService);
  private readonly toast = inject(MessageService);
  private readonly router = inject(Router);

  protected readonly current = signal<PlatformFiscalSettingsDto | null>(null);
  protected readonly loading = signal<boolean>(false);
  protected readonly busy = signal<boolean>(false);

  // Lot C4 (complément) — VAT periods
  protected vatYear = new Date().getFullYear();
  protected readonly vatPeriods = signal<PlatformVatPeriodDto[]>([]);
  protected readonly vatLoading = signal<boolean>(false);

  protected form: UpdatePlatformFiscalSettingsRequest = {
    nif: '',
    codeTva: '',
    companyName: '',
    address: '',
    phone: '',
    email: '',
    website: '',
    iban: '',
    bankName: '',
    applyVat: true,
    defaultVatRate: 19,
    timbreFiscalAmount: 1,
    applyClientWithholding: false,
    clientWithholdingRate: 1.5,
    invoiceNumberPrefix: 'FT',
    receiptNumberPrefix: 'FT-RC',
    legalMentions: ''
  };

  protected t(key: keyof typeof INVOICES_FR): string {
    return INVOICES_FR[key];
  }

  ngOnInit(): void {
    this.load();
    this.loadVatPeriods();
  }

  /** Lot C4 (complément) — Charge l'agrégation TVA mensuelle pour l'année courante. */
  loadVatPeriods(): void {
    const year = Number(this.vatYear);
    if (!year || year < 2020) return;
    this.vatLoading.set(true);
    this.invoicesApi.vatPeriods(year).subscribe({
      next: (res) => {
        if (res.success && res.data) this.vatPeriods.set(res.data);
        this.vatLoading.set(false);
      },
      error: () => {
        this.vatLoading.set(false);
      }
    });
  }

  onVatYearChange(event: { value: number | string | null }): void {
    // L'InputNumber émet une valeur valide ; on attend le blur pour recharger.
    const raw = event?.value;
    if (typeof raw === 'number') this.vatYear = raw;
    else if (typeof raw === 'string') {
      const parsed = Number.parseInt(raw, 10);
      if (!Number.isNaN(parsed)) this.vatYear = parsed;
    }
  }

  private load(): void {
    this.loading.set(true);
    this.api.get().subscribe({
      next: (res) => {
        if (res.success && res.data) {
          this.current.set(res.data);
          this.populateForm(res.data);
        }
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.toast.add({ severity: 'error', summary: this.t('toast.error.title'), detail: this.t('toast.error.detail') });
      }
    });
  }

  private populateForm(d: PlatformFiscalSettingsDto): void {
    this.form = {
      nif: d.nif,
      codeTva: d.codeTva,
      companyName: d.companyName,
      address: d.address,
      phone: d.phone,
      email: d.email,
      website: d.website,
      iban: d.iban,
      bankName: d.bankName,
      applyVat: d.applyVat,
      defaultVatRate: d.defaultVatRate,
      timbreFiscalAmount: d.timbreFiscalAmount,
      applyClientWithholding: d.applyClientWithholding,
      clientWithholdingRate: d.clientWithholdingRate,
      invoiceNumberPrefix: d.invoiceNumberPrefix,
      receiptNumberPrefix: d.receiptNumberPrefix,
      legalMentions: d.legalMentions
    };
  }

  protected canSubmit(): boolean {
    return !!this.form.nif?.trim()
      && !!this.form.companyName?.trim()
      && !!this.form.address?.trim()
      && !!this.form.invoiceNumberPrefix?.trim()
      && !!this.form.receiptNumberPrefix?.trim();
  }

  submit(): void {
    if (!this.canSubmit()) return;
    this.busy.set(true);
    this.api.update(this.form).subscribe({
      next: (res) => {
        if (res.success && res.data) {
          this.current.set(res.data);
          this.populateForm(res.data);
          this.toast.add({ severity: 'success', summary: 'Paramètres mis à jour' });
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
