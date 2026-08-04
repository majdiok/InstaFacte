import { Component, EventEmitter, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { TableModule } from 'primeng/table';
import { ButtonModule } from 'primeng/button';
import { TagModule } from 'primeng/tag';
import { HonorairesPayment } from '../../services/honoraires.service';

@Component({
  selector: 'app-honoraires-payments-section',
  standalone: true,
  imports: [CommonModule, TableModule, ButtonModule, TagModule],
  template: `
    <section class="payments-section" *ngIf="showSection">
      <div class="payments-section__header">
        <div>
          <h3>Encaissements</h3>
          <p class="muted">Historique des paiements et retenues à la source</p>
        </div>
        @if (canRecordPayment) {
          <button
            pButton
            type="button"
            label="Encaisser"
            icon="pi pi-wallet"
            class="p-button-sm"
            (click)="recordPaymentClick.emit()">
          </button>
        }
      </div>

      <div class="payments-summary" *ngIf="currency">
        <div class="payments-summary__item">
          <span>Payé</span>
          <strong>{{ amountPaid | number:'1.3-3' }} {{ currency }}</strong>
        </div>
        <div class="payments-summary__item payments-summary__item--due">
          <span>Reste dû</span>
          <strong>{{ amountDue | number:'1.3-3' }} {{ currency }}</strong>
        </div>
      </div>

      <p-table [value]="payments" styleClass="p-datatable-sm payments-table">
        <ng-template pTemplate="header">
          <tr>
            <th>Date</th>
            <th>Mode</th>
            <th class="text-right">Net</th>
            <th class="text-right">RS</th>
            <th class="text-right">Appliqué</th>
            <th>Référence</th>
          </tr>
        </ng-template>
        <ng-template pTemplate="body" let-row>
          <tr>
            <td>{{ row.paymentDate | date:'dd/MM/yyyy' }}</td>
            <td><p-tag [value]="row.methodDisplay"></p-tag></td>
            <td class="text-right">{{ row.amount | number:'1.3-3' }}</td>
            <td class="text-right">{{ row.clientWithholdingAmount | number:'1.3-3' }}</td>
            <td class="text-right">{{ row.appliedAmount | number:'1.3-3' }}</td>
            <td>{{ row.reference || '—' }}</td>
          </tr>
        </ng-template>
        <ng-template pTemplate="emptymessage">
          <tr><td colspan="6">Aucun encaissement enregistré.</td></tr>
        </ng-template>
      </p-table>
    </section>
  `,
  styles: [`
    .payments-section {
      margin-top: 1.25rem;
      padding: 1rem 1.25rem;
      background: #fff;
      border: 1px solid #e2e8f0;
      border-radius: 0.75rem;
    }
    .payments-section__header {
      display: flex;
      justify-content: space-between;
      align-items: flex-start;
      gap: 1rem;
      margin-bottom: 0.75rem;
    }
    h3 { margin: 0 0 0.15rem; font-size: 1rem; color: #1e293b; }
    .muted { margin: 0; color: #64748b; font-size: 0.8125rem; }
    .payments-summary {
      display: flex;
      gap: 1.5rem;
      margin-bottom: 0.75rem;
      padding: 0.75rem 1rem;
      background: #f8fafc;
      border-radius: 0.5rem;
    }
    .payments-summary__item {
      display: flex;
      flex-direction: column;
      gap: 0.15rem;
      font-size: 0.8125rem;
      color: #64748b;
    }
    .payments-summary__item strong { color: #0f172a; font-size: 0.9375rem; }
    .payments-summary__item--due strong { color: #2563eb; }
    .text-right { text-align: right; }
  `]
})
export class HonorairesPaymentsSectionComponent {
  @Input() payments: HonorairesPayment[] = [];
  @Input() amountPaid = 0;
  @Input() amountDue = 0;
  @Input() currency = 'TND';
  @Input() canRecordPayment = false;
  /** Hide entirely for quotes / drafts without payment context. */
  @Input() showSection = true;

  @Output() recordPaymentClick = new EventEmitter<void>();
}
