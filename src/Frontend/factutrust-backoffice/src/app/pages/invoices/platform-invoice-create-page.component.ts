import {
  ChangeDetectionStrategy,
  Component,
  OnInit,
  computed,
  inject,
  signal
} from '@angular/core';
import { DecimalPipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { InputNumberModule } from 'primeng/inputnumber';
import { CalendarModule } from 'primeng/calendar';
import { DropdownModule } from 'primeng/dropdown';
import { TableModule } from 'primeng/table';
import { MessageService } from 'primeng/api';

import { PlatformInvoicesService } from '@core/services/platform-invoices.service';
import { PlatformTenantService } from '@core/services/platform-tenant.service';
import { PlatformFiscalSettingsService } from '@core/services/platform-fiscal-settings.service';
import {
  PlatformInvoiceBillingTypeValue,
  type CreatePlatformInvoiceLineRequest,
  type CreatePlatformInvoiceRequest
} from '@core/models/platform.models';

import { FtPageHeaderComponent } from '@core/ui/page-header/ft-page-header.component';

import { INVOICES_FR } from './invoices.i18n.fr';

interface DraftLine {
  description: string;
  quantity: number;
  unitPriceHT: number;
  vatRate: number;
}

interface TenantOption {
  label: string;
  value: string;
}

/**
 * Lot C4 — Page création d'une facture plateforme (brouillon).
 *
 * Le brouillon est éditable jusqu'à émission. Aucun numéro fiscal n'est
 * attribué tant que l'utilisateur n'a pas cliqué sur « Émettre » (sur la
 * page détail), ce qui évite les sauts de séquence.
 */
@Component({
  selector: 'app-platform-invoice-create-page',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    DecimalPipe,
    FormsModule,
    RouterLink,
    ButtonModule,
    InputTextModule,
    InputNumberModule,
    CalendarModule,
    DropdownModule,
    TableModule,
    FtPageHeaderComponent
  ],
  template: `
    <ft-page-header [title]="t('create.title')" [subtitle]="t('list.subtitle')">
      <ng-container ftActions>
        <p-button label="Retour" icon="pi pi-arrow-left" [outlined]="true" severity="secondary" routerLink="/invoices" />
      </ng-container>
    </ft-page-header>

    <section class="card">
      <div class="grid">
        <div class="field field--full">
          <label for="ci-tenant">{{ t('create.field.tenant') }}</label>
          <p-dropdown
            inputId="ci-tenant"
            [options]="tenantOptions()"
            [(ngModel)]="tenantId"
            optionLabel="label"
            optionValue="value"
            [filter]="true"
            placeholder="—"
            styleClass="w-full" />
        </div>

        <div class="field">
          <label for="ci-type">{{ t('create.field.billingType') }}</label>
          <p-dropdown
            inputId="ci-type"
            [options]="billingTypeOptions"
            [(ngModel)]="billingType"
            optionLabel="label"
            optionValue="value"
            styleClass="w-full" />
        </div>

        <div class="field">
          <label for="ci-date">{{ t('create.field.invoiceDate') }}</label>
          <p-calendar
            inputId="ci-date"
            [(ngModel)]="invoiceDate"
            dateFormat="dd/mm/yy"
            [showIcon]="true"
            styleClass="w-full" />
        </div>

        <div class="field">
          <label for="ci-due">{{ t('create.field.dueDate') }}</label>
          <p-calendar
            inputId="ci-due"
            [(ngModel)]="dueDate"
            dateFormat="dd/mm/yy"
            [showIcon]="true"
            [showClear]="true"
            styleClass="w-full" />
        </div>

        <div class="field">
          <label for="ci-pf">{{ t('create.field.periodFrom') }}</label>
          <p-calendar
            inputId="ci-pf"
            [(ngModel)]="periodFrom"
            dateFormat="dd/mm/yy"
            [showIcon]="true"
            [showClear]="true"
            styleClass="w-full" />
        </div>

        <div class="field">
          <label for="ci-pt">{{ t('create.field.periodTo') }}</label>
          <p-calendar
            inputId="ci-pt"
            [(ngModel)]="periodTo"
            dateFormat="dd/mm/yy"
            [showIcon]="true"
            [showClear]="true"
            styleClass="w-full" />
        </div>

        <div class="field">
          <label for="ci-disc">{{ t('create.field.discount') }}</label>
          <p-inputNumber
            inputId="ci-disc"
            [(ngModel)]="discountAmount"
            [min]="0"
            [maxFractionDigits]="3" />
        </div>

        <div class="field">
          <label for="ci-cred">{{ t('create.field.credits') }}</label>
          <p-inputNumber
            inputId="ci-cred"
            [(ngModel)]="creditsApplied"
            [min]="0"
            [maxFractionDigits]="3" />
        </div>
      </div>
    </section>

    <section class="card">
      <div class="lines-header">
        <h3>{{ t('create.lines.title') }}</h3>
        <p-button [label]="t('create.lines.add')" icon="pi pi-plus" size="small" [outlined]="true" (onClick)="addLine()" />
      </div>

      <p-table [value]="lines" styleClass="ft-table">
        <ng-template pTemplate="header">
          <tr>
            <th>{{ t('create.lines.description') }}</th>
            <th class="num">{{ t('create.lines.quantity') }}</th>
            <th class="num">{{ t('create.lines.unitPriceHT') }}</th>
            <th class="num">{{ t('create.lines.vatRate') }}</th>
            <th class="num">{{ t('create.lines.lineTotalHT') }}</th>
            <th></th>
          </tr>
        </ng-template>
        <ng-template pTemplate="body" let-line let-i="rowIndex">
          <tr>
            <td><input type="text" pInputText [(ngModel)]="line.description" maxlength="500" class="w-full" /></td>
            <td class="num"><p-inputNumber [(ngModel)]="line.quantity" [min]="0.001" [maxFractionDigits]="3" /></td>
            <td class="num"><p-inputNumber [(ngModel)]="line.unitPriceHT" [min]="0" [maxFractionDigits]="3" /></td>
            <td class="num"><p-inputNumber [(ngModel)]="line.vatRate" [min]="0" [max]="100" [maxFractionDigits]="2" /></td>
            <td class="num">{{ lineTotal(line) | number:'1.3-3' }}</td>
            <td><p-button icon="pi pi-trash" severity="danger" [text]="true" (onClick)="removeLine(i)" /></td>
          </tr>
        </ng-template>
      </p-table>

      <div class="totals">
        <div class="row">
          <span class="label">Total HT (avant remise/crédits)</span>
          <span class="num">{{ subtotalHT() | number:'1.3-3' }} TND</span>
        </div>
      </div>
    </section>

    <div class="footer-actions">
      <p-button label="Annuler" severity="secondary" [text]="true" routerLink="/invoices" />
      <p-button
        [label]="t('create.confirm')"
        icon="pi pi-check"
        severity="primary"
        [disabled]="!canSubmit() || busy()"
        [loading]="busy()"
        (onClick)="submit()" />
    </div>
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
      .grid {
        display: grid;
        grid-template-columns: repeat(4, minmax(0, 1fr));
        gap: var(--gap-md);
      }
      @media (max-width: 980px) { .grid { grid-template-columns: repeat(2, minmax(0, 1fr)); } }
      @media (max-width: 540px) { .grid { grid-template-columns: 1fr; } }
      .field { display: flex; flex-direction: column; gap: 0.35rem; }
      .field--full { grid-column: 1 / -1; }
      .field label { font-size: 0.78rem; text-transform: uppercase; letter-spacing: 0.05em; color: var(--ft-text-muted); font-weight: 600; }

      .lines-header {
        display: flex;
        justify-content: space-between;
        align-items: center;
        margin-bottom: 0.75rem;
      }
      .lines-header h3 { font-size: 0.95rem; text-transform: uppercase; letter-spacing: 0.05em; color: var(--ft-text-muted); margin: 0; }

      .num { text-align: right; font-variant-numeric: tabular-nums; }
      .totals { margin-top: 1rem; display: flex; justify-content: flex-end; }
      .totals .row { display: flex; gap: 1rem; align-items: center; }
      .totals .label { color: var(--ft-text-muted); font-size: 0.85rem; text-transform: uppercase; letter-spacing: 0.04em; }
      .totals .num { min-width: 9rem; text-align: right; font-weight: 500; }

      .footer-actions {
        margin-top: 1.25rem;
        display: flex;
        justify-content: flex-end;
        gap: 0.6rem;
      }

      :host ::ng-deep .w-full { width: 100%; }
      :host ::ng-deep .p-inputnumber { width: 100%; }
    `
  ]
})
export class PlatformInvoiceCreatePageComponent implements OnInit {
  private readonly api = inject(PlatformInvoicesService);
  private readonly tenantsApi = inject(PlatformTenantService);
  private readonly fiscalApi = inject(PlatformFiscalSettingsService);
  private readonly toast = inject(MessageService);
  private readonly router = inject(Router);

  protected readonly tenantOptions = signal<TenantOption[]>([]);
  protected readonly busy = signal<boolean>(false);

  protected tenantId: string | null = null;
  protected billingType: number = PlatformInvoiceBillingTypeValue.Manual;
  protected invoiceDate: Date = new Date();
  protected dueDate: Date | null = null;
  protected periodFrom: Date | null = null;
  protected periodTo: Date | null = null;
  protected discountAmount = 0;
  protected creditsApplied = 0;

  protected lines: DraftLine[] = [
    { description: '', quantity: 1, unitPriceHT: 0, vatRate: 19 }
  ];

  protected readonly billingTypeOptions = [
    { label: this.t('billing.subscription'), value: PlatformInvoiceBillingTypeValue.Subscription },
    { label: this.t('billing.setupFee'), value: PlatformInvoiceBillingTypeValue.SetupFee },
    { label: this.t('billing.manual'), value: PlatformInvoiceBillingTypeValue.Manual }
  ];

  protected readonly subtotalHT = computed(() => {
    let s = 0;
    for (const l of this.lines) {
      s += this.lineTotal(l);
    }
    return Math.round(s * 1000) / 1000;
  });

  protected readonly canSubmit = computed(() =>
    !!this.tenantId
    && this.lines.length > 0
    && this.lines.every(l => l.description.trim().length > 0 && l.quantity > 0 && l.unitPriceHT >= 0)
  );

  protected t(key: keyof typeof INVOICES_FR): string {
    return INVOICES_FR[key];
  }

  ngOnInit(): void {
    this.loadTenants();
    this.loadDefaultVatRate();
  }

  protected lineTotal(line: DraftLine): number {
    return Math.round(line.quantity * line.unitPriceHT * 1000) / 1000;
  }

  addLine(): void {
    this.lines = [...this.lines, { description: '', quantity: 1, unitPriceHT: 0, vatRate: 19 }];
  }

  removeLine(index: number): void {
    if (this.lines.length === 1) return;
    this.lines = this.lines.filter((_, i) => i !== index);
  }

  private loadTenants(): void {
    this.tenantsApi.list({ pageSize: 200 }).subscribe({
      next: (res) => {
        if (res.success && res.data) {
          this.tenantOptions.set(
            res.data.items.map(t => ({ label: t.companyName, value: t.tenantId }))
          );
        }
      },
      error: () => {
        // silencieux
      }
    });
  }

  private loadDefaultVatRate(): void {
    this.fiscalApi.get().subscribe({
      next: (res) => {
        if (res.success && res.data) {
          const rate = res.data.defaultVatRate;
          this.lines = this.lines.map(l => ({ ...l, vatRate: l.vatRate || rate }));
        }
      },
      error: () => {
        // silencieux
      }
    });
  }

  submit(): void {
    if (!this.canSubmit() || !this.tenantId) return;
    const request: CreatePlatformInvoiceRequest = {
      tenantId: this.tenantId,
      billingType: this.billingType as 0 | 1 | 2 | 3,
      invoiceDate: this.invoiceDate?.toISOString(),
      dueDate: this.dueDate?.toISOString(),
      periodFrom: this.periodFrom?.toISOString(),
      periodTo: this.periodTo?.toISOString(),
      discountAmount: this.discountAmount,
      creditsApplied: this.creditsApplied,
      lines: this.lines.map<CreatePlatformInvoiceLineRequest>(l => ({
        description: l.description.trim(),
        quantity: l.quantity,
        unitPriceHT: l.unitPriceHT,
        vatRate: l.vatRate
      }))
    };

    this.busy.set(true);
    this.api.create(request).subscribe({
      next: (res) => {
        if (res.success && res.data) {
          this.toast.add({ severity: 'success', summary: this.t('toast.create.success') });
          this.router.navigate(['/invoices', res.data.id]);
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
