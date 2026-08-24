import { Component, inject } from '@angular/core';
import { CommonModule, CurrencyPipe, DatePipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { toSignal } from '@angular/core/rxjs-interop';
import { PortalService } from './portal.service';

@Component({
  selector: 'app-portal-dashboard',
  standalone: true,
  imports: [CommonModule, RouterLink, CurrencyPipe, DatePipe],
  template: `
    <h1>Bienvenue</h1>
    @if (summary(); as s) {
      <div class="kpis">
        <article><span>Encours</span><strong>{{ s.totalOutstanding | currency:'TND':'symbol':'1.3-3' }}</strong></article>
        <article><span>Impayées</span><strong>{{ s.unpaidCount }}</strong></article>
        <article><span>En retard</span><strong>{{ s.overdueAmount | currency:'TND':'symbol':'1.3-3' }}</strong></article>
      </div>
      <h2>Factures à régler</h2>
      <ul>
        @for (inv of s.upcomingInvoices; track inv.id) {
          <li>
            <a [routerLink]="['/portal/invoices', inv.id]">{{ inv.number }}</a>
            — {{ inv.remainingAmount | currency:'TND':'symbol':'1.3-3' }}
            <span *ngIf="inv.dueDate">échéance {{ inv.dueDate | date:'dd/MM/yyyy' }}</span>
          </li>
        } @empty {
          <li>Aucune facture à régler.</li>
        }
      </ul>
    }
  `,
  styles: [`
    .kpis { display: grid; grid-template-columns: repeat(auto-fit, minmax(180px, 1fr)); gap: 1rem; margin: 1rem 0 2rem; }
    article { background: white; padding: 1rem; border-radius: 10px; border: 1px solid #eadfce; }
    article span { display: block; color: #6b7280; font-size: .85rem; }
    article strong { font-size: 1.25rem; }
  `]
})
export class PortalDashboardComponent {
  readonly summary = toSignal(inject(PortalService).getSummary());
}
