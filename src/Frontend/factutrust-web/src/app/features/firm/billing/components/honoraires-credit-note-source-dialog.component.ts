import { Component, EventEmitter, Input, Output, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { DialogModule } from 'primeng/dialog';
import { TableModule } from 'primeng/table';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { TagModule } from 'primeng/tag';
import { forkJoin } from 'rxjs';
import { HonorairesInvoiceListItem, HonorairesService } from '../services/honoraires.service';
import { ToastService } from '@core/services/toast.service';
import {
  canCreateHonorairesCreditNote,
  HonorairesDocumentType,
  HonorairesInvoiceStatus
} from '../models/honoraires-invoice-status';

@Component({
  selector: 'app-honoraires-credit-note-source-dialog',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    DialogModule,
    TableModule,
    ButtonModule,
    InputTextModule,
    TagModule
  ],
  template: `
    <p-dialog
      header="Sélectionner la facture source"
      [(visible)]="visible"
      [modal]="true"
      [style]="{ width: '720px' }"
      (onHide)="onHide()">
      <p class="muted dialog-hint">
        Choisissez une facture honoraires validée pour créer un avoir.
      </p>
      <div class="toolbar">
        <input
          pInputText
          [(ngModel)]="search"
          placeholder="Rechercher N° ou client…"
          (keyup.enter)="load()"
          class="search-input" />
        <button pButton type="button" label="Rechercher" class="p-button-outlined" (click)="load()"></button>
      </div>
      <p-table [value]="items()" [loading]="loading()" styleClass="p-datatable-sm">
        <ng-template pTemplate="header">
          <tr>
            <th>N°</th>
            <th>Date</th>
            <th>Client</th>
            <th>Statut</th>
            <th class="text-right">Total TTC</th>
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
            <td>
              <button
                pButton
                type="button"
                label="Créer l'avoir"
                class="p-button-text"
                (click)="select(row)"></button>
            </td>
          </tr>
        </ng-template>
        <ng-template pTemplate="emptymessage">
          <tr>
            <td colspan="6">Aucune facture éligible. Validez une facture honoraires avant de créer un avoir.</td>
          </tr>
        </ng-template>
      </p-table>
      <ng-template pTemplate="footer">
        <button pButton type="button" label="Fermer" class="p-button-text" (click)="close()"></button>
      </ng-template>
    </p-dialog>
  `,
  styles: [`
    .dialog-hint { margin: 0 0 1rem; color: #64748b; }
    .toolbar { display: flex; gap: .75rem; margin-bottom: 1rem; }
    .search-input { flex: 1; }
    .text-right { text-align: right; }
    .muted { color: #64748b; }
  `]
})
export class HonorairesCreditNoteSourceDialogComponent {
  private readonly api = inject(HonorairesService);
  private readonly toast = inject(ToastService);

  @Input() visible = false;
  @Output() visibleChange = new EventEmitter<boolean>();
  @Output() invoiceSelected = new EventEmitter<HonorairesInvoiceListItem>();

  items = signal<HonorairesInvoiceListItem[]>([]);
  loading = signal(false);
  search = '';

  load(): void {
    this.loading.set(true);
    forkJoin({
      invoices: this.api.listInvoices({
        type: HonorairesDocumentType.Invoice,
        search: this.search || undefined,
        page: 1,
        pageSize: 100
      }),
      draftCreditNotes: this.api.listInvoices({
        type: HonorairesDocumentType.CreditNote,
        status: HonorairesInvoiceStatus.Draft,
        page: 1,
        pageSize: 100
      })
    }).subscribe({
      next: ({ invoices, draftCreditNotes }) => {
        const blockedSourceIds = new Set(
          draftCreditNotes.items
            .map(row => row.linkedInvoiceId)
            .filter((id): id is string => !!id)
        );
        const eligible = invoices.items.filter(row =>
          canCreateHonorairesCreditNote(
            row.status,
            row.type === HonorairesDocumentType.CreditNote
          ) && !blockedSourceIds.has(row.id)
        );
        this.items.set(eligible);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.toast.add({ severity: 'error', summary: 'Erreur', detail: 'Impossible de charger les factures' });
      }
    });
  }

  select(row: HonorairesInvoiceListItem): void {
    this.invoiceSelected.emit(row);
    this.close();
  }

  close(): void {
    this.visible = false;
    this.visibleChange.emit(false);
  }

  onHide(): void {
    this.visibleChange.emit(false);
  }
}
