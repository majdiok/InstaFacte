import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { forkJoin, of } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { DatePickerModule } from 'primeng/datepicker';
import { DialogModule } from 'primeng/dialog';
import { BreadcrumbComponent } from '@shared/components/breadcrumb/breadcrumb.component';
import { AuthService } from '@core/services/auth.service';
import { PERMISSIONS } from '@core/config/permission-keys';
import { ToastService } from '@core/services/toast.service';
import {
  HonorairesInvoiceListItem,
  HonorairesPayment,
  HonorairesService
} from '../../services/honoraires.service';
import {
  canRecordHonorairesPayment,
  HonorairesInvoiceStatus
} from '../../models/honoraires-invoice-status';
import { HonorairesRecordPaymentDialogComponent } from '../../components/honoraires-record-payment-dialog/honoraires-record-payment-dialog.component';

@Component({
  selector: 'app-honoraires-payment-list',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    RouterModule,
    TableModule,
    TagModule,
    ButtonModule,
    InputTextModule,
    DatePickerModule,
    DialogModule,
    BreadcrumbComponent,
    HonorairesRecordPaymentDialogComponent
  ],
  template: `
    <app-breadcrumb [items]="[
      { label: 'Accueil', route: '/firm/dashboard' },
      { label: 'Facturation' },
      { label: 'Encaissements' }
    ]"></app-breadcrumb>
    <div class="page">
      <div class="page-head">
        <div>
          <h1>Encaissements honoraires</h1>
          <p>Paiements reçus et retenues à la source.</p>
        </div>
        @if (canCreatePayment) {
          <button
            pButton
            type="button"
            label="Encaisser une facture"
            icon="pi pi-wallet"
            (click)="openInvoicePicker()">
          </button>
        }
      </div>

      <div class="kpis">
        <div class="kpi">
          <span class="kpi-label">Opérations</span>
          <strong>{{ filteredItems().length }}</strong>
        </div>
        <div class="kpi">
          <span class="kpi-label">Total net</span>
          <strong>{{ totalNet() | number:'1.3-3' }} TND</strong>
        </div>
        <div class="kpi">
          <span class="kpi-label">Total RS</span>
          <strong>{{ totalRs() | number:'1.3-3' }} TND</strong>
        </div>
        <div class="kpi kpi--primary">
          <span class="kpi-label">Total appliqué</span>
          <strong>{{ totalApplied() | number:'1.3-3' }} TND</strong>
        </div>
      </div>

      <div class="toolbar">
        <input
          pInputText
          [(ngModel)]="search"
          placeholder="Rechercher client ou N° facture…"
          (keyup.enter)="applyFilters()" />
        <p-datepicker
          [(ngModel)]="dateFrom"
          dateFormat="dd/mm/yy"
          placeholder="Du"
          [showIcon]="true"
          [showClear]="true">
        </p-datepicker>
        <p-datepicker
          [(ngModel)]="dateTo"
          dateFormat="dd/mm/yy"
          placeholder="Au"
          [showIcon]="true"
          [showClear]="true">
        </p-datepicker>
        <button pButton type="button" label="Filtrer" class="p-button-outlined" (click)="applyFilters()"></button>
      </div>

      <p-table [value]="filteredItems()" [loading]="loading()" styleClass="p-datatable-sm">
        <ng-template pTemplate="header">
          <tr>
            <th>Date</th><th>Facture</th><th>Client</th><th>Mode</th>
            <th class="text-right">Net</th><th class="text-right">RS</th><th class="text-right">Appliqué</th>
          </tr>
        </ng-template>
        <ng-template pTemplate="body" let-row>
          <tr>
            <td>{{ row.paymentDate | date: 'dd/MM/yyyy' }}</td>
            <td>
              <a [routerLink]="['/firm/billing/invoices', row.honorairesInvoiceId]">{{ row.invoiceNumber || '—' }}</a>
            </td>
            <td>{{ row.clientName }}</td>
            <td><p-tag [value]="row.methodDisplay"></p-tag></td>
            <td class="text-right">{{ row.amount | number: '1.3-3' }}</td>
            <td class="text-right">{{ row.clientWithholdingAmount | number: '1.3-3' }}</td>
            <td class="text-right">{{ row.appliedAmount | number: '1.3-3' }}</td>
          </tr>
        </ng-template>
        <ng-template pTemplate="emptymessage">
          <tr><td colspan="7">Aucun encaissement.</td></tr>
        </ng-template>
      </p-table>
    </div>

    <p-dialog
      header="Sélectionner une facture à encaisser"
      [(visible)]="showInvoicePicker"
      [modal]="true"
      [style]="{ width: '640px' }">
      <p-table [value]="unpaidInvoices()" [loading]="loadingUnpaid()" styleClass="p-datatable-sm">
        <ng-template pTemplate="header">
          <tr>
            <th>N°</th><th>Client</th><th class="text-right">Reste dû</th><th></th>
          </tr>
        </ng-template>
        <ng-template pTemplate="body" let-row>
          <tr>
            <td>{{ row.number || '—' }}</td>
            <td>{{ row.clientName }}</td>
            <td class="text-right">{{ row.amountDue | number:'1.3-3' }} {{ row.currency }}</td>
            <td>
              <button pButton type="button" label="Encaisser" class="p-button-text" (click)="selectInvoice(row)"></button>
            </td>
          </tr>
        </ng-template>
        <ng-template pTemplate="emptymessage">
          <tr><td colspan="4">Aucune facture en attente d’encaissement.</td></tr>
        </ng-template>
      </p-table>
    </p-dialog>

    <app-honoraires-record-payment-dialog
      [(visible)]="showPaymentDialog"
      [invoiceId]="selectedInvoice?.id ?? null"
      [invoiceNumber]="selectedInvoice?.number || ''"
      [currency]="selectedInvoice?.currency || 'TND'"
      [totalAmount]="selectedInvoice?.totalAmount || 0"
      [amountDue]="selectedInvoice?.amountDue || 0"
      [invoiceWithholdingAmount]="0"
      [paymentsClientWithholdingTotal]="0"
      (paymentRecorded)="onPaymentRecorded()">
    </app-honoraires-record-payment-dialog>
  `,
  styles: [`
    .page { padding: 1rem 1.25rem 2rem; }
    .page-head { display: flex; justify-content: space-between; gap: 1rem; align-items: flex-start; margin-bottom: 1rem; }
    h1 { margin: 0 0 .25rem; font-size: 1.5rem; color: var(--color-primary-700, #1d4ed8); }
    p { margin: 0 0 1rem; color: #64748b; }
    .kpis {
      display: grid;
      grid-template-columns: repeat(4, minmax(0, 1fr));
      gap: 0.75rem;
      margin-bottom: 1rem;
    }
    .kpi {
      padding: 0.85rem 1rem;
      background: #f8fafc;
      border: 1px solid #e2e8f0;
      border-radius: 0.75rem;
      display: flex;
      flex-direction: column;
      gap: 0.25rem;
    }
    .kpi-label { font-size: 0.75rem; color: #64748b; text-transform: uppercase; letter-spacing: 0.02em; }
    .kpi strong { font-size: 1.1rem; color: #0f172a; }
    .kpi--primary { background: #eff6ff; border-color: #bfdbfe; }
    .kpi--primary strong { color: #1d4ed8; }
    .toolbar { display: flex; flex-wrap: wrap; gap: .75rem; margin-bottom: 1rem; align-items: center; }
    .text-right { text-align: right; }
    @media (max-width: 900px) {
      .kpis { grid-template-columns: repeat(2, minmax(0, 1fr)); }
    }
  `]
})
export class HonorairesPaymentListComponent implements OnInit {
  private readonly api = inject(HonorairesService);
  private readonly toast = inject(ToastService);
  private readonly auth = inject(AuthService);

  items = signal<HonorairesPayment[]>([]);
  filteredItems = signal<HonorairesPayment[]>([]);
  unpaidInvoices = signal<HonorairesInvoiceListItem[]>([]);
  loading = signal(false);
  loadingUnpaid = signal(false);

  search = '';
  dateFrom: Date | null = null;
  dateTo: Date | null = null;
  showInvoicePicker = false;
  showPaymentDialog = false;
  selectedInvoice: HonorairesInvoiceListItem | null = null;

  readonly totalNet = computed(() =>
    this.filteredItems().reduce((s, r) => s + (r.amount || 0), 0)
  );
  readonly totalRs = computed(() =>
    this.filteredItems().reduce((s, r) => s + (r.clientWithholdingAmount || 0), 0)
  );
  readonly totalApplied = computed(() =>
    this.filteredItems().reduce((s, r) => s + (r.appliedAmount || 0), 0)
  );

  get canCreatePayment(): boolean {
    return this.auth.hasPermission(PERMISSIONS.honorairesPayments.create);
  }

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.api.listPayments().subscribe({
      next: rows => {
        this.items.set(rows);
        this.applyFilters();
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.toast.add({
          severity: 'error',
          summary: 'Erreur',
          detail: 'Impossible de charger les encaissements'
        });
      }
    });
  }

  applyFilters(): void {
    const q = this.search.trim().toLowerCase();
    const from = this.dateFrom ? startOfDay(this.dateFrom) : null;
    const to = this.dateTo ? endOfDay(this.dateTo) : null;

    this.filteredItems.set(
      this.items().filter(row => {
        if (q) {
          const hay = `${row.clientName || ''} ${row.invoiceNumber || ''}`.toLowerCase();
          if (!hay.includes(q)) return false;
        }
        const d = new Date(row.paymentDate);
        if (from && d < from) return false;
        if (to && d > to) return false;
        return true;
      })
    );
  }

  openInvoicePicker(): void {
    this.showInvoicePicker = true;
    this.loadingUnpaid.set(true);
    forkJoin({
      validated: this.api
        .listInvoices({
          type: 0,
          status: HonorairesInvoiceStatus.Validated,
          page: 1,
          pageSize: 100
        })
        .pipe(catchError(() => of({ items: [], totalCount: 0, page: 1, pageSize: 100 }))),
      partial: this.api
        .listInvoices({
          type: 0,
          status: HonorairesInvoiceStatus.PartiallyPaid,
          page: 1,
          pageSize: 100
        })
        .pipe(catchError(() => of({ items: [], totalCount: 0, page: 1, pageSize: 100 })))
    }).subscribe({
      next: ({ validated, partial }) => {
        const rows = [...validated.items, ...partial.items]
          .filter(r => canRecordHonorairesPayment(r.status) && r.amountDue > 0)
          .sort((a, b) => (b.amountDue || 0) - (a.amountDue || 0));
        this.unpaidInvoices.set(rows);
        this.loadingUnpaid.set(false);
      },
      error: () => {
        this.loadingUnpaid.set(false);
        this.toast.add({
          severity: 'error',
          summary: 'Erreur',
          detail: 'Impossible de charger les factures'
        });
      }
    });
  }

  selectInvoice(row: HonorairesInvoiceListItem): void {
    this.selectedInvoice = row;
    this.showInvoicePicker = false;
    this.showPaymentDialog = true;
  }

  onPaymentRecorded(): void {
    this.selectedInvoice = null;
    this.load();
  }
}

function startOfDay(d: Date): Date {
  const x = new Date(d);
  x.setHours(0, 0, 0, 0);
  return x;
}

function endOfDay(d: Date): Date {
  const x = new Date(d);
  x.setHours(23, 59, 59, 999);
  return x;
}
