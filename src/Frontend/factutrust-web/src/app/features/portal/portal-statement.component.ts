import { Component, inject } from '@angular/core';
import { CommonModule, CurrencyPipe, DatePipe } from '@angular/common';
import { toSignal } from '@angular/core/rxjs-interop';
import { PortalService } from './portal.service';

@Component({
  selector: 'app-portal-statement',
  standalone: true,
  imports: [CommonModule, CurrencyPipe, DatePipe],
  template: `
    <h1>Relevé</h1>
    @if (statement(); as s) {
      <p>Solde d'ouverture {{ s.openingBalance | currency:'TND':'symbol':'1.3-3' }} · clôture {{ s.closingBalance | currency:'TND':'symbol':'1.3-3' }}</p>
      <table>
        <thead><tr><th>Date</th><th>Libellé</th><th>Débit</th><th>Crédit</th><th>Solde</th></tr></thead>
        <tbody>
          @for (line of s.lines; track $index) {
            <tr>
              <td>{{ line.date | date:'dd/MM/yyyy' }}</td>
              <td>{{ line.label }}</td>
              <td>{{ line.debit | currency:'TND':'symbol':'1.3-3' }}</td>
              <td>{{ line.credit | currency:'TND':'symbol':'1.3-3' }}</td>
              <td>{{ line.balance | currency:'TND':'symbol':'1.3-3' }}</td>
            </tr>
          }
        </tbody>
      </table>
    }
  `,
  styles: [`table { width: 100%; background: white; border-collapse: collapse; } th, td { padding: .6rem; border-bottom: 1px solid #eadfce; }`]
})
export class PortalStatementComponent {
  readonly statement = toSignal(inject(PortalService).getStatement());
}
