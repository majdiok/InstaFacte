import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { PageHeaderComponent } from '@shared/components/page-header/page-header.component';
import { ButtonComponent } from '@shared/components/button/button.component';
import { AccountingFilterBarComponent } from '../shared/accounting-filter-bar.component';
import { AccountingStatusBannerComponent } from '../shared/accounting-status-banner.component';
import { AccountingService, IncomeTaxYearParameterDto } from '../services/accounting.service';

/**
 * Paramétrage fiscal par exercice : taux IS, minimum d'impôt, CSS, acomptes, report des déficits et
 * barème IRPP. Indispensable car le droit fiscal tunisien évolue à chaque loi de finances — les
 * valeurs livrées ne sont que des références à valider par l'expert-comptable.
 */
@Component({
  selector: 'app-fiscal-parameters',
  standalone: true,
  imports: [CommonModule, FormsModule, PageHeaderComponent, ButtonComponent, AccountingFilterBarComponent, AccountingStatusBannerComponent],
  template: `
    <app-page-header title="Paramètres fiscaux"
      subtitle="Taux IS, minimum d'impôt, CSS, acomptes et barème IRPP — par exercice" />

    <div class="card accounting-filters-card">
      <app-accounting-filter-bar ariaLabel="Exercice des paramètres fiscaux">
        <div accountingFilterFields>
          <div class="accounting-filter-field">
            <label class="accounting-filter-label" for="fp-year">Exercice</label>
            <input id="fp-year" type="number" [(ngModel)]="fiscalYear" class="accounting-filter-input fp-year-input" min="2000" max="2100" />
          </div>
        </div>
        <div accountingFilterActions>
          <app-button variant="secondary" icon="pi pi-refresh" type="button" (click)="load()" [disabled]="loading()"
            ariaLabel="Charger les paramètres de l'exercice">Charger</app-button>
          <app-button variant="primary" icon="pi pi-save" type="button" (click)="save()" [disabled]="loading() || saving() || !model"
            ariaLabel="Enregistrer les paramètres fiscaux">Enregistrer</app-button>
        </div>
      </app-accounting-filter-bar>
    </div>

    <div class="fp-validate-banner" role="status">
      <i class="pi pi-exclamation-triangle" aria-hidden="true"></i>
      <span>
        <strong>Valeurs indicatives</strong> — à valider selon la loi de finances de l'exercice.
        Le droit fiscal tunisien évolue chaque année : contrôlez les taux avant d'établir la liasse.
      </span>
    </div>

    <app-accounting-status-banner variant="error" [message]="error() ?? ''" [showRetry]="!!error()" retryLabel="Réessayer" (retry)="load()" />

    @if (info(); as msg) {
      <div class="fp-info-banner" role="status"><i class="pi pi-check-circle" aria-hidden="true"></i><span>{{ msg }}</span></div>
    }

    @if (model; as m) {
      @if (!m.isUserModified) {
        <div class="fp-default-banner" role="status">
          <i class="pi pi-info-circle" aria-hidden="true"></i>
          <span>Paramètres issus des valeurs par défaut. Après enregistrement, ils ne seront plus réinitialisés automatiquement.</span>
        </div>
      }

      <div class="fp-grid">
        <div class="card fp-card">
          <h3 class="fp-title">Impôt sur les sociétés</h3>
          <div class="fp-row"><label>Taux de droit commun</label><input type="number" step="0.01" min="0" max="1" [(ngModel)]="m.isStandardRate" /><span class="fp-hint">0.15 = 15 %</span></div>
          <div class="fp-row"><label>Taux réduit</label><input type="number" step="0.01" min="0" max="1" [(ngModel)]="m.isReducedRate" /></div>
          <div class="fp-row"><label>Taux sectoriel majoré</label><input type="number" step="0.01" min="0" max="1" [(ngModel)]="m.isSectorRate" /><span class="fp-hint">banques, assurances, télécoms…</span></div>
        </div>

        <div class="card fp-card">
          <h3 class="fp-title">Minimum d'impôt</h3>
          <div class="fp-row"><label>Taux (droit commun)</label><input type="number" step="0.001" min="0" max="1" [(ngModel)]="m.minTaxRate" /><span class="fp-hint">0.002 = 0,2 % du CA local TTC</span></div>
          <div class="fp-row"><label>Plancher (droit commun)</label><input type="number" step="0.001" min="0" [(ngModel)]="m.minTaxFloorTnd" /><span class="fp-hint">TND</span></div>
          <div class="fp-row"><label>Taux réduit</label><input type="number" step="0.001" min="0" max="1" [(ngModel)]="m.minTaxReducedRate" /><span class="fp-hint">0.001 = 0,1 %</span></div>
          <div class="fp-row"><label>Plancher réduit</label><input type="number" step="0.001" min="0" [(ngModel)]="m.minTaxFloorReducedTnd" /><span class="fp-hint">TND</span></div>
        </div>

        <div class="card fp-card">
          <h3 class="fp-title">Contribution sociale de solidarité</h3>
          <div class="fp-row fp-row--check">
            <label for="fp-css">La CSS s'applique</label>
            <input id="fp-css" type="checkbox" [(ngModel)]="m.cssApplies" />
          </div>
          <div class="fp-row"><label>Taux CSS</label><input type="number" step="0.001" min="0" max="1" [(ngModel)]="m.cssRate" /><span class="fp-hint">0.01 = 1 % de l'assiette imposable</span></div>
          <div class="fp-row"><label>Plancher CSS</label><input type="number" step="0.001" min="0" [(ngModel)]="m.cssFloorTnd" /><span class="fp-hint">TND</span></div>
        </div>

        <div class="card fp-card">
          <h3 class="fp-title">Acomptes, reports et arrondi</h3>
          <div class="fp-row"><label>Taux d'un acompte</label><input type="number" step="0.01" min="0" max="1" [(ngModel)]="m.acompteRate" /><span class="fp-hint">0.30 = 30 % de l'IS N-1</span></div>
          <div class="fp-row"><label>Nombre d'acomptes</label><input type="number" step="1" min="0" [(ngModel)]="m.acompteCount" /></div>
          <div class="fp-row"><label>Report des déficits (années)</label><input type="number" step="1" min="0" [(ngModel)]="m.deficitCarryForwardYears" /><span class="fp-hint">amortissements différés : illimité</span></div>
          <div class="fp-row fp-row--check">
            <label for="fp-round">Arrondir l'assiette au dinar</label>
            <input id="fp-round" type="checkbox" [(ngModel)]="m.roundTaxableToDinar" />
          </div>
        </div>

        <div class="card fp-card fp-card--wide">
          <h3 class="fp-title">Barème IRPP (BIC — régime réel)</h3>
          <p class="fp-hint">Tableau JSON : <code>[{{ '{' }}"lower":0,"rate":0{{ '}' }}, …]</code> — <em>lower</em> = borne inférieure annuelle (TND), <em>rate</em> = taux en %.</p>
          <textarea rows="4" class="fp-json" [(ngModel)]="m.irppBracketsJson" aria-label="Barème IRPP en JSON"></textarea>
        </div>
      </div>
    }
  `,
  styles: `
    @use '../shared/accounting-layout';
    .fp-year-input { max-width:7rem; }
    .fp-validate-banner, .fp-info-banner, .fp-default-banner { display:flex; align-items:flex-start; gap:var(--spacing-2); margin-bottom:var(--spacing-4); padding:var(--spacing-3) var(--spacing-4); border-radius:var(--radius-md); font-size:var(--font-size-sm); }
    .fp-validate-banner { background:var(--color-warning-50,#fffbeb); border:1px solid var(--color-warning-200,#fde68a); color:var(--color-warning-800,#92400e); }
    .fp-info-banner { background:var(--color-success-50,#f0fdf4); border:1px solid var(--color-success-200,#bbf7d0); color:var(--color-success-700,#15803d); }
    .fp-default-banner { background:var(--color-primary-50,#eff6ff); border:1px solid var(--color-primary-200,#bfdbfe); color:var(--color-primary-700,#1d4ed8); }
    .fp-grid { display:grid; grid-template-columns:1fr; gap:var(--spacing-4); }
    @media (min-width:900px){ .fp-grid { grid-template-columns:1fr 1fr; align-items:start; } }
    .fp-card { padding:var(--spacing-4) var(--spacing-5); border-radius:var(--radius-lg); box-shadow:var(--shadow-sm); }
    .fp-card--wide { grid-column:1/-1; }
    .fp-title { font-size:var(--font-size-base); font-weight:var(--font-weight-bold); margin:0 0 var(--spacing-3); }
    .fp-row { display:grid; grid-template-columns:1fr 9rem auto; align-items:center; gap:var(--spacing-2); margin-bottom:var(--spacing-2); }
    .fp-row--check { grid-template-columns:1fr auto; }
    .fp-row label { font-size:var(--font-size-sm); color:var(--color-text-secondary); }
    .fp-row input[type=number] { padding:0.4rem 0.55rem; border:1px solid var(--color-border-default,#cbd5e1); border-radius:var(--radius-md); text-align:right; font-variant-numeric:tabular-nums; }
    .fp-hint { font-size:var(--font-size-xs); color:var(--color-text-tertiary); }
    .fp-json { width:100%; font-family:monospace; font-size:var(--font-size-sm); padding:var(--spacing-2); border:1px solid var(--color-border-default,#cbd5e1); border-radius:var(--radius-md); }
  `
})
export class FiscalParametersComponent implements OnInit {
  private readonly api = inject(AccountingService);

  fiscalYear = new Date().getFullYear() - 1;
  model: IncomeTaxYearParameterDto | null = null;
  readonly loading = signal(false);
  readonly saving = signal(false);
  readonly error = signal<string | null>(null);
  readonly info = signal<string | null>(null);

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    const y = Math.min(2100, Math.max(2000, Math.floor(Number(this.fiscalYear)) || new Date().getFullYear()));
    this.fiscalYear = y;
    this.loading.set(true);
    this.error.set(null);
    this.info.set(null);
    this.api.getIncomeTaxParameters(y).subscribe({
      next: res => {
        this.loading.set(false);
        if (res.success && res.data) this.model = { ...res.data };
        else this.error.set(res.error ?? 'Erreur');
      },
      error: () => {
        this.loading.set(false);
        this.error.set('Erreur réseau');
      }
    });
  }

  save(): void {
    if (!this.model) return;
    this.saving.set(true);
    this.error.set(null);
    this.info.set(null);
    this.api.updateIncomeTaxParameters(this.fiscalYear, this.model).subscribe({
      next: res => {
        this.saving.set(false);
        if (res.success && res.data) {
          this.model = { ...res.data };
          this.info.set('Paramètres enregistrés — ils ne seront plus réinitialisés automatiquement.');
        } else {
          this.error.set(res.error ?? "L'enregistrement a échoué.");
        }
      },
      error: () => {
        this.saving.set(false);
        this.error.set("L'enregistrement a échoué.");
      }
    });
  }
}
