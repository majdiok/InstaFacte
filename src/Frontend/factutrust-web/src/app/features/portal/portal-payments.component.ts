import { Component, inject } from '@angular/core';
import { CommonModule, CurrencyPipe, DatePipe } from '@angular/common';
import { toSignal } from '@angular/core/rxjs-interop';
import { PortalService } from './portal.service';

@Component({
  selector: 'app-portal-payments',
  standalone: true,
  imports: [CommonModule, CurrencyPipe, DatePipe],
  template: `
    <h1>Paiements</h1>
    <table>
      <thead><tr><th>Date</th><th>Facture</th><th>Montant</th><th>Mode</th></tr></thead>
      <tbody>
        @for (p of payments(); track p.id) {
          <tr>
            <td>{{ p.paymentDate | date:'dd/MM/yyyy' }}</td>
            <td>{{ p.invoiceNumber }}</td>
            <td>{{ p.amount | currency:'TND':'symbol':'1.3-3' }}</td>
            <td>{{ p.methodDisplay }}</td>
          </tr>
        } @empty {
          <tr><td colspan="4">Aucun paiement.</td></tr>
        }
      </tbody>
    </table>
  `,
  styles: [`table { width: 100%; background: white; border-collapse: collapse; } th, td { padding: .6rem; border-bottom: 1px solid #eadfce; }`]
})
export class PortalPaymentsComponent {
  readonly payments = toSignal(inject(PortalService).getPayments(), { initialValue: [] });
}
