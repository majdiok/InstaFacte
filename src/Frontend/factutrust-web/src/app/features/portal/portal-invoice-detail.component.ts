import { Component, inject } from '@angular/core';
import { CommonModule, CurrencyPipe, DatePipe } from '@angular/common';
import { ActivatedRoute } from '@angular/router';
import { switchMap } from 'rxjs';
import { toSignal } from '@angular/core/rxjs-interop';
import { PortalService } from './portal.service';

@Component({
  selector: 'app-portal-invoice-detail',
  standalone: true,
  imports: [CommonModule, CurrencyPipe, DatePipe],
  template: `
    @if (invoice(); as inv) {
      <h1>{{ inv.number }}</h1>
      <p>{{ inv.statusDisplay }} — {{ inv.issueDate | date:'dd/MM/yyyy' }}</p>
      <p>Total {{ inv.totalAmount | currency:'TND':'symbol':'1.3-3' }} · Reste {{ inv.remainingAmount | currency:'TND':'symbol':'1.3-3' }}</p>
      <button type="button" (click)="download()">Télécharger le PDF</button>
      <table>
        <tr *ngFor="let line of inv.lines"><td>{{ line.description }}</td><td>{{ line.lineTotal | currency:'TND':'symbol':'1.3-3' }}</td></tr>
      </table>
    }
  `
})
export class PortalInvoiceDetailComponent {
  private readonly portal = inject(PortalService);
  private readonly route = inject(ActivatedRoute);
  readonly invoice = toSignal(
    this.route.paramMap.pipe(switchMap(p => this.portal.getInvoice(p.get('id')!)))
  );

  download(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (!id) return;
    this.portal.downloadInvoicePdf(id).subscribe(blob => {
      const url = URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = `facture-${id}.pdf`;
      a.click();
      URL.revokeObjectURL(url);
    });
  }
}
