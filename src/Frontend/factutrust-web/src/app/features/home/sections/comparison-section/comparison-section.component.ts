import { Component, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';

interface ComparisonRow {
  criterion: string;
  factutrust: { state: 'check' | 'warn' | 'cross' | 'dash'; label: string };
  excel: { state: 'check' | 'warn' | 'cross' | 'dash'; label: string };
  erp: { state: 'check' | 'warn' | 'cross' | 'dash'; label: string };
}

@Component({
  selector: 'app-comparison-section',
  standalone: true,
  imports: [CommonModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section id="comparaison" class="comparison-section" aria-labelledby="comparison-title">
      <div class="section-container">
        <div class="section-header">
          <span class="eyebrow">COMPARATIF</span>
          <h2 id="comparison-title" class="section-title">InstaFact face aux alternatives</h2>
          <p class="section-description">
            Pourquoi les PME tunisiennes choisissent InstaFact plutôt que les solutions traditionnelles ou les tableurs.
          </p>
        </div>

        <!-- Tableau desktop / tablet -->
        <div class="comparison-table-wrapper" role="region" aria-label="Tableau comparatif">
          <table class="comparison-table">
            <caption class="sr-only">Comparaison InstaFact vs Excel + papier vs ERP traditionnel</caption>
            <thead>
              <tr>
                <th scope="col" class="th-criterion">Critère</th>
                <th scope="col" class="th-product th-factutrust">
                  <span class="header-label">InstaFact</span>
                  <span class="header-tag">Recommandé</span>
                </th>
                <th scope="col" class="th-product">
                  <span class="header-label">Excel + papier</span>
                  <span class="header-tag header-tag-neutral">Manuel</span>
                </th>
                <th scope="col" class="th-product">
                  <span class="header-label">ERP traditionnel</span>
                  <span class="header-tag header-tag-neutral">Solution PME française</span>
                </th>
              </tr>
            </thead>
            <tbody>
              @for (row of rows; track row.criterion) {
                <tr>
                  <th scope="row" class="td-criterion">{{ row.criterion }}</th>
                  <td class="td-cell td-factutrust">
                    <span class="state state-{{ row.factutrust.state }}" aria-hidden="true">{{ stateGlyph(row.factutrust.state) }}</span>
                    <span class="state-label">{{ row.factutrust.label }}</span>
                  </td>
                  <td class="td-cell">
                    <span class="state state-{{ row.excel.state }}" aria-hidden="true">{{ stateGlyph(row.excel.state) }}</span>
                    <span class="state-label">{{ row.excel.label }}</span>
                  </td>
                  <td class="td-cell">
                    <span class="state state-{{ row.erp.state }}" aria-hidden="true">{{ stateGlyph(row.erp.state) }}</span>
                    <span class="state-label">{{ row.erp.label }}</span>
                  </td>
                </tr>
              }
            </tbody>
          </table>
        </div>

        <!-- Cartes empilées mobile -->
        <div class="comparison-mobile-cards">
          <article class="cmp-card cmp-card-factutrust">
            <header class="cmp-card-header">
              <h3>InstaFact</h3>
              <span class="cmp-card-tag">Recommandé</span>
            </header>
            <ul>
              @for (row of rows; track row.criterion) {
                <li>
                  <span class="li-state state-{{ row.factutrust.state }}" aria-hidden="true">{{ stateGlyph(row.factutrust.state) }}</span>
                  <div>
                    <strong>{{ row.criterion }}</strong>
                    <span>{{ row.factutrust.label }}</span>
                  </div>
                </li>
              }
            </ul>
          </article>

          <article class="cmp-card">
            <header class="cmp-card-header">
              <h3>Excel + papier</h3>
              <span class="cmp-card-tag cmp-card-tag-neutral">Manuel</span>
            </header>
            <ul>
              @for (row of rows; track row.criterion) {
                <li>
                  <span class="li-state state-{{ row.excel.state }}" aria-hidden="true">{{ stateGlyph(row.excel.state) }}</span>
                  <div>
                    <strong>{{ row.criterion }}</strong>
                    <span>{{ row.excel.label }}</span>
                  </div>
                </li>
              }
            </ul>
          </article>

          <article class="cmp-card">
            <header class="cmp-card-header">
              <h3>ERP traditionnel</h3>
              <span class="cmp-card-tag cmp-card-tag-neutral">Solution PME française</span>
            </header>
            <ul>
              @for (row of rows; track row.criterion) {
                <li>
                  <span class="li-state state-{{ row.erp.state }}" aria-hidden="true">{{ stateGlyph(row.erp.state) }}</span>
                  <div>
                    <strong>{{ row.criterion }}</strong>
                    <span>{{ row.erp.label }}</span>
                  </div>
                </li>
              }
            </ul>
          </article>
        </div>
      </div>
    </section>
  `,
  styles: [`
    .comparison-section {
      padding: var(--spacing-20) var(--spacing-6);
      background: linear-gradient(180deg, #ffffff 0%, var(--color-neutral-50) 100%);
    }

    .section-container {
      max-width: 1180px;
      margin: 0 auto;
    }

    .section-header {
      text-align: center;
      margin-bottom: var(--spacing-12);
      max-width: 720px;
      margin-left: auto;
      margin-right: auto;
    }

    .eyebrow {
      display: inline-block;
      padding: var(--spacing-2) var(--spacing-4);
      background: var(--color-primary-50);
      color: var(--color-primary-700);
      border-radius: var(--radius-full);
      font-size: var(--font-size-xs);
      font-weight: var(--font-weight-semibold);
      letter-spacing: 0.08em;
      margin-bottom: var(--spacing-4);
    }

    .section-title {
      font-size: clamp(1.875rem, 4vw, 2.5rem);
      font-weight: var(--font-weight-bold);
      color: var(--color-neutral-900);
      margin: 0 0 var(--spacing-4) 0;
    }

    .section-description {
      font-size: var(--font-size-lg);
      color: var(--color-neutral-600);
      margin: 0;
    }

    .sr-only {
      position: absolute;
      width: 1px; height: 1px;
      padding: 0; margin: -1px;
      overflow: hidden;
      clip: rect(0, 0, 0, 0);
      white-space: nowrap;
      border-width: 0;
    }

    /* Tableau */
    .comparison-table-wrapper {
      overflow-x: auto;
      border-radius: var(--radius-2xl);
      box-shadow: 0 12px 30px rgba(15, 23, 42, 0.08);
      background: white;
    }

    .comparison-table {
      width: 100%;
      border-collapse: collapse;
      font-size: 0.95rem;
    }

    .comparison-table thead th {
      padding: var(--spacing-5) var(--spacing-4);
      text-align: left;
      vertical-align: top;
      background: var(--color-neutral-50);
      border-bottom: 1px solid var(--color-neutral-200);
    }

    .comparison-table .th-criterion {
      width: 28%;
      color: var(--color-neutral-500);
      font-weight: var(--font-weight-semibold);
      font-size: 0.85rem;
      text-transform: uppercase;
      letter-spacing: 0.05em;
    }

    .comparison-table .th-product {
      width: 24%;
    }

    .comparison-table .th-factutrust {
      background: linear-gradient(135deg, var(--color-primary-50) 0%, #ffffff 100%);
      border-bottom: 2px solid var(--color-primary-500);
    }

    .header-label {
      display: block;
      font-size: 1.1rem;
      font-weight: var(--font-weight-bold);
      color: var(--color-neutral-900);
    }

    .header-tag {
      display: inline-block;
      margin-top: 0.25rem;
      padding: 0.15rem 0.6rem;
      border-radius: 999px;
      background: var(--color-primary-600);
      color: white;
      font-size: 0.7rem;
      font-weight: 600;
      letter-spacing: 0.04em;
      text-transform: uppercase;
    }

    .header-tag-neutral {
      background: var(--color-neutral-200);
      color: var(--color-neutral-700);
      font-weight: 500;
      text-transform: none;
      letter-spacing: 0;
    }

    .comparison-table tbody tr {
      border-bottom: 1px solid var(--color-neutral-100);
      transition: background 0.2s ease;
    }

    .comparison-table tbody tr:hover {
      background: var(--color-neutral-50);
    }

    .comparison-table tbody tr:last-child {
      border-bottom: none;
    }

    .td-criterion {
      padding: var(--spacing-4);
      text-align: left;
      font-weight: var(--font-weight-semibold);
      color: var(--color-neutral-900);
      font-size: 0.95rem;
    }

    .td-cell {
      padding: var(--spacing-4);
      color: var(--color-neutral-700);
      vertical-align: middle;
    }

    .td-factutrust {
      background: rgba(37, 99, 235, 0.04);
    }

    .state {
      display: inline-block;
      width: 24px;
      height: 24px;
      border-radius: 50%;
      text-align: center;
      line-height: 24px;
      font-size: 0.85rem;
      font-weight: 700;
      margin-right: 0.5rem;
      vertical-align: middle;
    }

    .state-check {
      background: #dcfce7;
      color: #166534;
    }

    .state-warn {
      background: #fef3c7;
      color: #92400e;
    }

    .state-cross {
      background: #fee2e2;
      color: #991b1b;
    }

    .state-dash {
      background: var(--color-neutral-100);
      color: var(--color-neutral-500);
    }

    .state-label {
      font-size: 0.9rem;
      vertical-align: middle;
    }

    /* Mobile cards */
    .comparison-mobile-cards {
      display: none;
      flex-direction: column;
      gap: var(--spacing-5);
    }

    .cmp-card {
      background: white;
      border-radius: var(--radius-xl);
      padding: var(--spacing-5);
      border: 1px solid var(--color-neutral-200);
      box-shadow: var(--shadow-sm);
    }

    .cmp-card-factutrust {
      border-color: var(--color-primary-300);
      box-shadow: 0 8px 20px rgba(37, 99, 235, 0.1);
    }

    .cmp-card-header {
      display: flex;
      align-items: center;
      justify-content: space-between;
      margin-bottom: var(--spacing-4);
      padding-bottom: var(--spacing-3);
      border-bottom: 1px solid var(--color-neutral-100);
    }

    .cmp-card-header h3 {
      margin: 0;
      font-size: 1.1rem;
      font-weight: var(--font-weight-bold);
      color: var(--color-neutral-900);
    }

    .cmp-card-tag {
      padding: 0.15rem 0.6rem;
      border-radius: 999px;
      background: var(--color-primary-600);
      color: white;
      font-size: 0.7rem;
      font-weight: 600;
      text-transform: uppercase;
      letter-spacing: 0.04em;
    }

    .cmp-card-tag-neutral {
      background: var(--color-neutral-200);
      color: var(--color-neutral-700);
      text-transform: none;
      letter-spacing: 0;
      font-weight: 500;
    }

    .cmp-card ul {
      list-style: none;
      padding: 0;
      margin: 0;
      display: flex;
      flex-direction: column;
      gap: var(--spacing-3);
    }

    .cmp-card li {
      display: flex;
      align-items: flex-start;
      gap: var(--spacing-3);
    }

    .cmp-card li > div {
      flex: 1;
      display: flex;
      flex-direction: column;
      gap: 2px;
    }

    .cmp-card li strong {
      font-size: 0.9rem;
      color: var(--color-neutral-900);
      font-weight: var(--font-weight-semibold);
    }

    .cmp-card li span {
      font-size: 0.85rem;
      color: var(--color-neutral-600);
    }

    .li-state {
      display: inline-block;
      width: 22px;
      height: 22px;
      border-radius: 50%;
      text-align: center;
      line-height: 22px;
      font-size: 0.8rem;
      font-weight: 700;
      flex-shrink: 0;
    }

    @media (max-width: 768px) {
      .comparison-section {
        padding: var(--spacing-12) var(--spacing-4);
      }
      .comparison-table-wrapper {
        display: none;
      }
      .comparison-mobile-cards {
        display: flex;
      }
    }
  `]
})
export class ComparisonSectionComponent {
  readonly rows: ComparisonRow[] = [
    {
      criterion: 'Facturation conforme TEJ',
      factutrust: { state: 'check', label: 'Automatique' },
      excel: { state: 'cross', label: 'Manuel, risque erreur' },
      erp: { state: 'warn', label: 'Configuration lourde' }
    },
    {
      criterion: 'IA intégrée native',
      factutrust: { state: 'check', label: 'Inclus tous plans payants' },
      excel: { state: 'cross', label: 'Indisponible' },
      erp: { state: 'cross', label: 'Indisponible' }
    },
    {
      criterion: 'Forecasting calendrier tunisien',
      factutrust: { state: 'check', label: 'Ramadan, Aïd auto' },
      excel: { state: 'cross', label: 'Indisponible' },
      erp: { state: 'cross', label: 'Indisponible' }
    },
    {
      criterion: 'Multi-canal (WhatsApp, Telegram)',
      factutrust: { state: 'check', label: 'Inclus' },
      excel: { state: 'cross', label: 'Indisponible' },
      erp: { state: 'warn', label: 'Modules tiers' }
    },
    {
      criterion: '10 modules dans un seul outil',
      factutrust: { state: 'check', label: 'Tout inclus' },
      excel: { state: 'cross', label: 'Indisponible' },
      erp: { state: 'warn', label: 'Souvent payants à l\'unité' }
    },
    {
      criterion: 'Configuration',
      factutrust: { state: 'check', label: '5 minutes' },
      excel: { state: 'dash', label: '—' },
      erp: { state: 'cross', label: 'Plusieurs jours' }
    },
    {
      criterion: 'Tarif PME tunisienne',
      factutrust: { state: 'check', label: 'À partir de 39 TND/mois' },
      excel: { state: 'warn', label: 'Temps perdu' },
      erp: { state: 'cross', label: 'Coût élevé' }
    }
  ];

  stateGlyph(state: 'check' | 'warn' | 'cross' | 'dash'): string {
    switch (state) {
      case 'check': return '✓';
      case 'warn': return '!';
      case 'cross': return '✗';
      case 'dash': return '—';
    }
  }
}
