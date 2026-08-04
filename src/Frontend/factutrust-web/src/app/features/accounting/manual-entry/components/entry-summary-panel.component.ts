import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { EntryFormStore } from '../services/entry-form.store';
import { formatPeriodLabel } from '../models/guided-scenarios.catalog';

@Component({
  selector: 'app-entry-summary-panel',
  standalone: true,
  imports: [CommonModule],
  template: `
    <aside class="summary-panel card" aria-labelledby="summary-title">
      <h3 id="summary-title" class="summary-panel__title">Récapitulatif</h3>
      <dl class="summary-panel__list">
        <div class="summary-row">
          <dt>Journal</dt>
          <dd>{{ store.journalCode() }}</dd>
        </div>
        <div class="summary-row">
          <dt>Date</dt>
          <dd>{{ store.entryDate() | date:'dd/MM/yyyy' }}</dd>
        </div>
        @if (store.selectedPeriod(); as period) {
          <div class="summary-row">
            <dt>Période</dt>
            <dd>{{ periodLabel(period) }}</dd>
          </div>
        }
        @if (store.pieceRef()) {
          <div class="summary-row">
            <dt>Référence</dt>
            <dd>{{ store.pieceRef() }}</dd>
          </div>
        }
        @if (store.dominantThirdParty(); as tp) {
          <div class="summary-row">
            <dt>Tiers</dt>
            <dd>{{ tp.name }}</dd>
          </div>
        }
        <div class="summary-row">
          <dt>Total débit</dt>
          <dd class="mono">{{ store.totals().debit | number:'1.3-3' }} TND</dd>
        </div>
        <div class="summary-row">
          <dt>Total crédit</dt>
          <dd class="mono">{{ store.totals().credit | number:'1.3-3' }} TND</dd>
        </div>
        <div class="summary-row summary-row--highlight">
          <dt>Solde</dt>
          <dd class="mono" [class.balanced]="store.isBalanced()" [class.unbalanced]="!store.isBalanced()">
            {{ store.balance() | number:'1.3-3' }} TND
          </dd>
        </div>
      </dl>
      @if (store.hasNonPersistedAssistFields()) {
        <p class="summary-panel__warn" role="note">
          Certaines colonnes d'aide (pièce, échéance) ne seront pas enregistrées.
        </p>
      }
    </aside>
  `,
  styles: `
    .summary-panel { padding:var(--spacing-4); position:sticky; top:var(--spacing-4); }
    .summary-panel__title { font-size:var(--font-size-sm); font-weight:var(--font-weight-semibold); margin:0 0 var(--spacing-3); text-transform:uppercase; letter-spacing:0.04em; color:var(--color-text-secondary); }
    .summary-panel__list { margin:0; }
    .summary-row { display:flex; justify-content:space-between; gap:var(--spacing-2); padding:var(--spacing-2) 0; border-bottom:1px solid var(--color-border-subtle); font-size:var(--font-size-sm); }
    .summary-row dt { color:var(--color-text-secondary); margin:0; }
    .summary-row dd { margin:0; font-weight:var(--font-weight-medium); text-align:right; }
    .summary-row--highlight { border-bottom:none; padding-top:var(--spacing-3); }
    .mono { font-variant-numeric:tabular-nums; }
    .balanced { color:var(--color-success-600); }
    .unbalanced { color:var(--color-error-600); }
    .summary-panel__warn { font-size:var(--font-size-xs); color:var(--color-warning-700); margin:var(--spacing-3) 0 0; padding:var(--spacing-2); background:var(--color-warning-50); border-radius:var(--radius-sm); }
  `
})
export class EntrySummaryPanelComponent {
  readonly store = inject(EntryFormStore);

  periodLabel(p: { fiscalYear: number; month: number }): string {
    return formatPeriodLabel(p.fiscalYear, p.month);
  }
}
