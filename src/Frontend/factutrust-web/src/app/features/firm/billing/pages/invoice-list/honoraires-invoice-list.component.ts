import { Component, OnInit, ViewChild, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterModule, ActivatedRoute } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { TableModule } from 'primeng/table';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { TagModule } from 'primeng/tag';
import { BreadcrumbComponent } from '@shared/components/breadcrumb/breadcrumb.component';
import { AuthService } from '@core/services/auth.service';
import { PERMISSIONS } from '@core/config/permission-keys';
import { ToastService } from '@core/services/toast.service';
import { formatLocalDate } from '@core/utils/date.util';
import {
  HonorairesInvoiceListItem,
  HonorairesService
} from '../../services/honoraires.service';
import {
  canCreateHonorairesCreditNote,
  canRecordHonorairesPayment,
  HonorairesDocumentType
} from '../../models/honoraires-invoice-status';
import { HonorairesRecordPaymentDialogComponent } from '../../components/honoraires-record-payment-dialog/honoraires-record-payment-dialog.component';
import { HonorairesCreditNoteSourceDialogComponent } from '../../components/honoraires-credit-note-source-dialog.component';

@Component({
  selector: 'app-honoraires-invoice-list',
  standalone: true,
  imports: [
    CommonModule,
    RouterModule,
    FormsModule,
    TableModule,
    ButtonModule,
    InputTextModule,
    TagModule,
    BreadcrumbComponent,
    HonorairesRecordPaymentDialogComponent,
    HonorairesCreditNoteSourceDialogComponent
  ],
  template: `
    <app-breadcrumb [items]="[
      { label: 'Accueil', route: '/firm/dashboard' },
      { label: 'Facturation' },
      { label: isCreditNotes ? 'Avoirs' : 'Factures' }
    ]"></app-breadcrumb>

    <div class="page">
      <div class="page-head">
        <div>
          <h1>{{ isCreditNotes ? 'Avoirs honoraires' : 'Factures honoraires' }}</h1>
          <p>Facturation des missions et services du cabinet.</p>
        </div>
        <button
          *ngIf="!isCreditNotes"
          pButton
          type="button"
          label="Nouvelle facture"
          icon="pi pi-plus"
          (click)="router.navigate(['/firm/billing/invoices/new'])"></button>
        <button
          *ngIf="isCreditNotes && canCreateCreditNotes"
          pButton
          type="button"
          label="Nouvel avoir"
          icon="pi pi-plus"
          (click)="openNewCreditNote()"></button>
      </div>

      <div class="toolbar">
        <input pInputText [(ngModel)]="search" placeholder="Rechercher N° ou client…" (keyup.enter)="load()" />
        <button pButton type="button" label="Filtrer" class="p-button-outlined" (click)="load()"></button>
      </div>

      <p-table [value]="items()" [loading]="loading()" styleClass="p-datatable-sm">
        <ng-template pTemplate="header">
          <tr>
            <th>N°</th>
            <th>Date</th>
            <th>Client</th>
            <th>Statut</th>
            <th class="text-right">Total TTC</th>
            <th class="text-right">Reste dû</th>
            <th></th>
          </tr>
        </ng-template>
        <ng-template pTemplate="body" let-row>
          <tr>
            <td>{{ row.number || 'Provisoire' }}</td>
            <td>{{ row.issueDate | date: 'dd/MM/yyyy' }}</td>
            <td>{{ row.clientName }}</td>
            <td><p-tag [value]="row.statusDisplay"></p-tag></td>
            <td class="text-right">{{ row.totalAmount | number: '1.3-3' }} {{ row.currency }}</td>
            <td class="text-right">{{ row.amountDue | number: '1.3-3' }} {{ row.currency }}</td>
            <td class="row-actions">
              <button pButton type="button" icon="pi pi-eye" class="p-button-text"
                (click)="router.navigate(['/firm/billing/invoices', row.id])"></button>
              <button
                *ngIf="canEncaisser(row)"
                pButton
                type="button"
                icon="pi pi-wallet"
                class="p-button-text"
                label="Encaisser"
                (click)="openPayment(row)">
              </button>
              <button
                *ngIf="!isCreditNotes && canCreateCreditNote(row)"
                pButton
                type="button"
                label="Avoir"
                class="p-button-text"
                (click)="createCreditNote(row)">
              </button>
            </td>
          </tr>
        </ng-template>
        <ng-template pTemplate="emptymessage">
          <tr><td colspan="7">Aucun document.</td></tr>
        </ng-template>
      </p-table>
    </div>

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

    <app-honoraires-credit-note-source-dialog
      [(visible)]="showSourceDialog"
      (invoiceSelected)="createCreditNote($event)">
    </app-honoraires-credit-note-source-dialog>
  `,
  styles: [`
    .page { padding: 1rem 1.25rem 2rem; }
    .page-head { display: flex; justify-content: space-between; gap: 1rem; align-items: flex-start; margin-bottom: 1.25rem; }
    h1 { margin: 0 0 .25rem; font-size: 1.5rem; color: var(--color-primary-700, #1d4ed8); }
    p { margin: 0; color: #64748b; }
    .toolbar { display: flex; gap: .75rem; margin-bottom: 1rem; }
    .text-right { text-align: right; }
    .row-actions { display: flex; flex-wrap: wrap; gap: 0.15rem; justify-content: flex-end; }
  `]
})
export class HonorairesInvoiceListComponent implements OnInit {
  readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly api = inject(HonorairesService);
  private readonly toast = inject(ToastService);
  private readonly auth = inject(AuthService);

  @ViewChild(HonorairesCreditNoteSourceDialogComponent)
  private sourceDialog?: HonorairesCreditNoteSourceDialogComponent;

  items = signal<HonorairesInvoiceListItem[]>([]);
  loading = signal(false);
  search = '';
  isCreditNotes = false;
  showPaymentDialog = false;
  showSourceDialog = false;
  selectedInvoice: HonorairesInvoiceListItem | null = null;

  get canCreateCreditNotes(): boolean {
    return this.auth.hasPermission(PERMISSIONS.honorairesInvoices.create);
  }

  ngOnInit(): void {
    this.isCreditNotes = this.route.snapshot.data['documentType'] === 1
      || this.router.url.includes('credit-notes');
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.api.listInvoices({
      type: this.isCreditNotes ? HonorairesDocumentType.CreditNote : HonorairesDocumentType.Invoice,
      search: this.search || undefined,
      page: 1,
      pageSize: 50
    }).subscribe({
      next: r => {
        this.items.set(r.items);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.toast.add({ severity: 'error', summary: 'Erreur', detail: 'Impossible de charger les documents honoraires' });
      }
    });
  }

  canEncaisser(row: HonorairesInvoiceListItem): boolean {
    if (this.isCreditNotes) return false;
    if (!this.auth.hasPermission(PERMISSIONS.honorairesPayments.create)) return false;
    return canRecordHonorairesPayment(row.status) && row.amountDue > 0;
  }

  canCreateCreditNote(row: HonorairesInvoiceListItem): boolean {
    if (!this.canCreateCreditNotes) return false;
    return canCreateHonorairesCreditNote(
      row.status,
      row.type === HonorairesDocumentType.CreditNote
    );
  }

  openNewCreditNote(): void {
    this.showSourceDialog = true;
    setTimeout(() => this.sourceDialog?.load(), 0);
  }

  openPayment(row: HonorairesInvoiceListItem): void {
    this.selectedInvoice = row;
    this.showPaymentDialog = true;
  }

  onPaymentRecorded(): void {
    this.selectedInvoice = null;
    this.load();
  }

  createCreditNote(row: HonorairesInvoiceListItem): void {
    this.api.createCreditNote({
      linkedInvoiceId: row.id,
      issueDate: formatLocalDate(new Date())
    }).subscribe({
      next: id => {
        this.toast.add({ severity: 'success', summary: 'OK', detail: 'Avoir créé (brouillon)' });
        this.router.navigate(['/firm/billing/invoices', id]);
      },
      error: err => {
        const detail = err?.error?.message || err?.error?.errors?.[0] || 'Création de l’avoir impossible';
        this.toast.add({ severity: 'error', summary: 'Erreur', detail });
      }
    });
  }
}
