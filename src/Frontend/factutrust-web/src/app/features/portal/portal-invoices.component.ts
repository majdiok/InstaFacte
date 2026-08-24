import { Component, inject, signal } from '@angular/core';
import { CommonModule, CurrencyPipe, DatePipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { PortalInvoiceListItem, PortalService } from './portal.service';

@Component({
  selector: 'app-portal-invoices',
  standalone: true,
  imports: [CommonModule, RouterLink, FormsModule, CurrencyPipe, DatePipe],
  template: `
    <h1>Factures</h1>
    <div class="filters">
      <select [(ngModel)]="status" (ngModelChange)="reload()">
        <option value="">Tous les statuts</option>
        <option value="Validated">Validée</option>
        <option value="Signed">Signée</option>
        <option value="PartiallyPaid">Partiellement payée</option>
        <option value="Paid">Payée</option>
        <option value="Overdue">En retard</option>
      </select>
      <label><input type="checkbox" [(ngModel)]="unpaidOnly" (ngModelChange)="reload()" /> Impayées uniquement</label>
    </div>
    <table>
      <thead>
        <tr><th>N°</th><th>Date</th><th>Échéance</th><th>Statut</th><th>Reste</th></tr>
      </thead>
      <tbody>
        @for (inv of items(); track inv.id) {
          <tr>
            <td><a [routerLink]="['/portal/invoices', inv.id]">{{ inv.number }}</a></td>
            <td>{{ inv.issueDate | date:'dd/MM/yyyy' }}</td>
            <td>{{ inv.dueDate | date:'dd/MM/yyyy' }}</td>
            <td>{{ inv.statusDisplay }}</td>
            <td>{{ inv.remainingAmount | currency:'TND':'symbol':'1.3-3' }}</td>
          </tr>
        }
      </tbody>
    </table>
  `,
  styles: [`
    table { width: 100%; background: white; border-collapse: collapse; }
    th, td { padding: .6rem .75rem; border-bottom: 1px solid #eadfce; text-align: left; }
    .filters { display: flex; gap: 1rem; margin-bottom: 1rem; align-items: center; }
  `]
})
export class PortalInvoicesComponent {
  private readonly portal = inject(PortalService);
  readonly items = signal<PortalInvoiceListItem[]>([]);
  status = '';
  unpaidOnly = false;

  constructor() {
    this.reload();
  }

  reload(): void {
    this.portal
      .getInvoices({
        status: this.status || undefined,
        unpaidOnly: this.unpaidOnly || undefined,
        page: 1,
        pageSize: 50
      })
      .subscribe(res => this.items.set(res.items ?? []));
  }
}
