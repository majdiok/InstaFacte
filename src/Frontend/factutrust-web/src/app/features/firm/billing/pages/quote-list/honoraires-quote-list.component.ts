import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterModule } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { TableModule } from 'primeng/table';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { TagModule } from 'primeng/tag';
import { BreadcrumbComponent } from '@shared/components/breadcrumb/breadcrumb.component';
import { HonorairesQuoteListItem, HonorairesService } from '../../services/honoraires.service';
import { ToastService } from '@core/services/toast.service';
import { HonorairesQuoteStatus } from '../../models/honoraires-invoice-status';

@Component({
  selector: 'app-honoraires-quote-list',
  standalone: true,
  imports: [
    CommonModule, RouterModule, FormsModule, TableModule, ButtonModule, InputTextModule, TagModule, BreadcrumbComponent
  ],
  template: `
    <app-breadcrumb [items]="[
      { label: 'Accueil', route: '/firm/dashboard' },
      { label: 'Facturation' },
      { label: 'Devis' }
    ]"></app-breadcrumb>
    <div class="page">
      <div class="page-head">
        <div>
          <h1>Devis honoraires</h1>
          <p>Propositions commerciales pour les dossiers clients.</p>
        </div>
        <button pButton type="button" label="Nouveau devis" icon="pi pi-plus"
          (click)="router.navigate(['/firm/billing/quotes/new'])"></button>
      </div>
      <div class="toolbar">
        <input pInputText [(ngModel)]="search" placeholder="Rechercher…" (keyup.enter)="load()" />
        <button pButton type="button" label="Filtrer" class="p-button-outlined" (click)="load()"></button>
      </div>
      <p-table [value]="items()" [loading]="loading()" styleClass="p-datatable-sm">
        <ng-template pTemplate="header">
          <tr>
            <th>N°</th><th>Date</th><th>Client</th><th>Statut</th><th class="text-right">Total TTC</th><th></th>
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
              <button pButton type="button" icon="pi pi-eye" class="p-button-text"
                (click)="router.navigate(['/firm/billing/quotes', row.id])"></button>
              <button *ngIf="row.status === quoteStatus.Sent" pButton type="button" label="Accepter" class="p-button-text"
                (click)="accept(row)"></button>
              <button *ngIf="row.status === quoteStatus.Accepted" pButton type="button" label="Convertir" class="p-button-text"
                (click)="convert(row)"></button>
            </td>
          </tr>
        </ng-template>
        <ng-template pTemplate="emptymessage">
          <tr><td colspan="6">Aucun devis.</td></tr>
        </ng-template>
      </p-table>
    </div>
  `,
  styles: [`
    .page { padding: 1rem 1.25rem 2rem; }
    .page-head { display: flex; justify-content: space-between; gap: 1rem; margin-bottom: 1.25rem; }
    h1 { margin: 0 0 .25rem; font-size: 1.5rem; color: var(--color-primary-700, #1d4ed8); }
    p { margin: 0; color: #64748b; }
    .toolbar { display: flex; gap: .75rem; margin-bottom: 1rem; }
    .text-right { text-align: right; }
  `]
})
export class HonorairesQuoteListComponent implements OnInit {
  readonly router = inject(Router);
  readonly quoteStatus = HonorairesQuoteStatus;
  private readonly api = inject(HonorairesService);
  private readonly toast = inject(ToastService);
  items = signal<HonorairesQuoteListItem[]>([]);
  loading = signal(false);
  search = '';

  ngOnInit(): void { this.load(); }

  load(): void {
    this.loading.set(true);
    this.api.listQuotes({ search: this.search || undefined, page: 1, pageSize: 50 }).subscribe({
      next: r => { this.items.set(r.items); this.loading.set(false); },
      error: () => { this.loading.set(false); this.toast.add({ severity: 'error', summary: 'Erreur', detail: 'Impossible de charger les devis' }); }
    });
  }

  convert(row: HonorairesQuoteListItem): void {
    this.api.convertQuote(row.id).subscribe({
      next: id => {
        this.toast.add({ severity: 'success', summary: 'OK', detail: 'Devis converti en facture' });
        this.router.navigate(['/firm/billing/invoices', id]);
      },
      error: () => this.toast.add({ severity: 'error', summary: 'Erreur', detail: 'Conversion impossible' })
    });
  }

  accept(row: HonorairesQuoteListItem): void {
    this.api.acceptQuote(row.id).subscribe({
      next: () => {
        this.toast.add({ severity: 'success', summary: 'OK', detail: 'Devis accepté' });
        this.load();
      },
      error: () => this.toast.add({ severity: 'error', summary: 'Erreur', detail: 'Acceptation impossible' })
    });
  }
}
