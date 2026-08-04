import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { PageHeaderComponent } from '@shared/components/page-header/page-header.component';
import { ButtonComponent } from '@shared/components/button/button.component';
import { AccountingFilterBarComponent } from '../shared/accounting-filter-bar.component';
import { AccountingStatusBannerComponent } from '../shared/accounting-status-banner.component';
import { AccountingExportMenuComponent } from '../shared/accounting-export-menu.component';
import { AccountingExportFormat, downloadBlob, exportExtension } from '../shared/accounting-download.util';
import {
  AccountingService,
  FiscalAdjustmentCatalogEntryDto,
  FiscalAdjustmentLineDto,
  FiscalCarryForwardDto,
  FiscalResultDeclarationDto,
  UpsertFiscalResultRequest
} from '../services/accounting.service';

/** Modèle éditable local (copie mutable de la feuille de détermination). */
interface EditableModel {
  taxpayerKind: number;
  accountingResult: number;
  appliedIsRate: number;
  localTurnoverTtc: number;
  minimumTaxRegime: number;
  acomptesPaid: number;
  withholdingSuffered: number;
  priorTaxCredit: number;
  adjustments: FiscalAdjustmentLineDto[];
  carryForwards: FiscalCarryForwardDto[];
}

/**
 * Détermination du résultat fiscal (liasse fiscale tunisienne) : passage du résultat comptable au
 * résultat fiscal (réintégrations / déductions), imputation des reports, puis calcul de l'IS ou de
 * l'IRPP-BIC. Le calcul est fait CÔTÉ SERVEUR à l'enregistrement (aucune duplication du moteur ici).
 */
@Component({
  selector: 'app-fiscal-result',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    PageHeaderComponent,
    ButtonComponent,
    AccountingFilterBarComponent,
    AccountingStatusBannerComponent,
    AccountingExportMenuComponent
  ],
  template: `
    <app-page-header title="Détermination du résultat fiscal"
      subtitle="Passage du résultat comptable au résultat fiscal, imputation des reports et calcul de l'impôt (IS / IRPP)" />

    <div class="card accounting-filters-card">
      <app-accounting-filter-bar ariaLabel="Exercice et type de contribuable">
        <div accountingFilterFields class="fr-fields">
          <div class="accounting-filter-field">
            <label class="accounting-filter-label" for="fr-year">Exercice</label>
            <input id="fr-year" type="number" [(ngModel)]="fiscalYear" class="accounting-filter-input fr-year-input" min="2000" max="2100" />
          </div>
          <div class="accounting-filter-field">
            <label class="accounting-filter-label" for="fr-kind">Contribuable</label>
            <select id="fr-kind" [(ngModel)]="model.taxpayerKind" class="accounting-filter-input" [disabled]="readonly()">
              <option [ngValue]="0">Société (IS)</option>
              <option [ngValue]="1">Personne physique (IRPP-BIC)</option>
            </select>
          </div>
        </div>
        <div accountingFilterActions>
          <app-button variant="secondary" icon="pi pi-refresh" type="button" (click)="load()" [disabled]="loading()"
            ariaLabel="Charger la feuille de détermination">Charger</app-button>
          <app-button variant="primary" icon="pi pi-save" type="button" (click)="save()"
            [disabled]="loading() || saving() || readonly()"
            ariaLabel="Enregistrer et recalculer">Enregistrer et recalculer</app-button>
          <app-accounting-export-menu label="Exporter la détermination"
            [disabled]="loading() || !data()" (exportFormat)="onExportDetermination($event)" />
          <app-accounting-export-menu label="Exporter la liasse complète"
            [disabled]="loading() || !data()" (exportFormat)="onExportLiasse($event)" />
          <app-button variant="secondary" icon="pi pi-lock" type="button" (click)="finalize()"
            [disabled]="loading() || saving() || readonly()"
            ariaLabel="Finaliser la feuille (cabinet en mode dossier délégué)">Finaliser</app-button>
        </div>
      </app-accounting-filter-bar>
    </div>

    <app-accounting-status-banner variant="error" [message]="error() ?? ''" [showRetry]="!!error()" retryLabel="Réessayer" (retry)="load()" />

    @if (info(); as msg) {
      <div class="fr-info-banner" role="status"><i class="pi pi-check-circle" aria-hidden="true"></i><span>{{ msg }}</span></div>
    }

    @if (data(); as d) {
      @if (!d.fiscalLiasseEnabled) {
        <div class="fr-warn-banner" role="status">
          <i class="pi pi-info-circle" aria-hidden="true"></i>
          <span>La liasse fiscale n'est pas activée pour ce dossier. La détermination ci-dessous reste indicative.</span>
        </div>
      }
      @if (d.isFinalized) {
        <div class="fr-lock-banner" role="status">
          <i class="pi pi-lock" aria-hidden="true"></i>
          <span>Feuille finalisée{{ d.finalizedAt ? ' le ' + (d.finalizedAt | date : 'shortDate') : '' }} — lecture seule.</span>
        </div>
      }
      @if (d.warnings.length) {
        <div class="fr-alert-banner" role="alert">
          <i class="pi pi-exclamation-triangle" aria-hidden="true"></i>
          <div>
            <strong>Contrôles fiscaux</strong>
            <ul class="fr-warn-list">
              @for (w of d.warnings; track w) { <li>{{ w }}</li> }
            </ul>
          </div>
        </div>
      }

      <div class="fr-grid">
        <!-- Colonne saisie -->
        <div class="fr-col">
          <div class="card fr-card">
            <h3 class="fr-card-title">Données de base</h3>
            <div class="fr-input-row">
              <label>Résultat comptable net (après impôt)</label>
              <input type="number" step="0.001" [(ngModel)]="model.accountingResult" [disabled]="readonly()" />
              <span class="fr-hint">l'IS comptabilisé (compte 69) est réintégré ci-dessous</span>
            </div>
            @if (model.taxpayerKind === 0) {
              <div class="fr-input-row">
                <label>Taux IS applicable</label>
                <input type="number" step="0.01" min="0" max="1" [(ngModel)]="model.appliedIsRate" [disabled]="readonly()" />
                <span class="fr-hint">ex. 0.15 = 15 %</span>
              </div>
            }
            <div class="fr-input-row">
              <label>Chiffre d'affaires local TTC</label>
              <input type="number" step="0.001" [(ngModel)]="model.localTurnoverTtc" [disabled]="readonly()" />
              <span class="fr-hint">base du minimum d'impôt</span>
            </div>
            @if (suggestedTurnover() > 0) {
              <div class="fr-suggest-row">
                <span class="fr-hint">CA comptable calculé : {{ suggestedTurnover() | number : '1.3-3' }}</span>
                <app-button variant="ghost" size="sm" icon="pi pi-arrow-down-left" type="button"
                  (click)="applySuggestedTurnover()" [disabled]="readonly()"
                  ariaLabel="Reprendre le chiffre d'affaires comptable">Reprendre</app-button>
              </div>
            }
            <div class="fr-input-row">
              <label>Régime de minimum d'impôt</label>
              <select [(ngModel)]="model.minimumTaxRegime" [disabled]="readonly()" aria-label="Régime de minimum d'impôt">
                <option [ngValue]="0">Droit commun</option>
                <option [ngValue]="1">Réduit</option>
                <option [ngValue]="2">Exonéré</option>
              </select>
              <span class="fr-hint">exonéré : société nouvellement créée, ZDR, totalement exportatrice…</span>
            </div>
            <div class="fr-input-row">
              <label>Acomptes provisionnels payés</label>
              <input type="number" step="0.001" [(ngModel)]="model.acomptesPaid" [disabled]="readonly()" />
            </div>
            <div class="fr-input-row">
              <label>Retenues à la source subies</label>
              <input type="number" step="0.001" [(ngModel)]="model.withholdingSuffered" [disabled]="readonly()" />
            </div>
            <div class="fr-input-row">
              <label>Crédit d'impôt antérieur</label>
              <input type="number" step="0.001" [(ngModel)]="model.priorTaxCredit" [disabled]="readonly()" />
            </div>
          </div>

          <div class="card fr-card">
            <div class="fr-card-head">
              <h3 class="fr-card-title">Réintégrations <span class="fr-sum">+{{ reintegrationsTotal() | number : '1.3-3' }}</span></h3>
              <div class="fr-add">
                <select #reCat class="fr-cat-select" [disabled]="readonly()" aria-label="Ajouter une réintégration du catalogue">
                  <option value="">Ajouter du catalogue…</option>
                  @for (c of catalogReintegrations(); track c.code) { <option [value]="c.code">{{ c.label }}</option> }
                </select>
                <app-button variant="ghost" size="sm" icon="pi pi-plus" type="button" (click)="addFromCatalog(reCat.value, 0); reCat.value=''"
                  [disabled]="readonly()" ariaLabel="Ajouter la réintégration sélectionnée">Catalogue</app-button>
                <app-button variant="ghost" size="sm" icon="pi pi-plus" type="button" (click)="addLine(0)" [disabled]="readonly()"
                  ariaLabel="Ajouter une ligne libre de réintégration">Ligne libre</app-button>
              </div>
            </div>
            <ng-container [ngTemplateOutlet]="linesTpl" [ngTemplateOutletContext]="{ kind: 0 }"></ng-container>
          </div>

          <div class="card fr-card">
            <div class="fr-card-head">
              <h3 class="fr-card-title">Déductions <span class="fr-sum fr-sum--neg">−{{ deductionsTotal() | number : '1.3-3' }}</span></h3>
              <div class="fr-add">
                <select #deCat class="fr-cat-select" [disabled]="readonly()" aria-label="Ajouter une déduction du catalogue">
                  <option value="">Ajouter du catalogue…</option>
                  @for (c of catalogDeductions(); track c.code) { <option [value]="c.code">{{ c.label }}</option> }
                </select>
                <app-button variant="ghost" size="sm" icon="pi pi-plus" type="button" (click)="addFromCatalog(deCat.value, 1); deCat.value=''"
                  [disabled]="readonly()" ariaLabel="Ajouter la déduction sélectionnée">Catalogue</app-button>
                <app-button variant="ghost" size="sm" icon="pi pi-plus" type="button" (click)="addLine(1)" [disabled]="readonly()"
                  ariaLabel="Ajouter une ligne libre de déduction">Ligne libre</app-button>
              </div>
            </div>
            <ng-container [ngTemplateOutlet]="linesTpl" [ngTemplateOutletContext]="{ kind: 1 }"></ng-container>
          </div>

          <div class="card fr-card">
            <div class="fr-card-head">
              <h3 class="fr-card-title">Déficits & amortissements différés reportables</h3>
              <div class="fr-add">
                <app-button variant="ghost" size="sm" icon="pi pi-plus" type="button" (click)="addCarryForward(0)" [disabled]="readonly()"
                  ariaLabel="Ajouter un déficit reportable">Déficit</app-button>
                <app-button variant="ghost" size="sm" icon="pi pi-plus" type="button" (click)="addCarryForward(1)" [disabled]="readonly()"
                  ariaLabel="Ajouter un amortissement différé">Amort. différé</app-button>
              </div>
            </div>
            @if (model.carryForwards.length === 0) {
              <p class="fr-empty">Aucun report imputable.</p>
            } @else {
              <table class="fr-table">
                <thead>
                  <tr><th>Type</th><th>Origine</th><th class="fr-num">Montant initial</th><th class="fr-num">Imputé cette année</th><th>Expire</th><th></th></tr>
                </thead>
                <tbody>
                  @for (cf of model.carryForwards; track $index) {
                    <tr>
                      <td>
                        <select [(ngModel)]="cf.kind" [disabled]="readonly()" aria-label="Type de report">
                          <option [ngValue]="0">Déficit</option><option [ngValue]="1">Amort. différé</option>
                        </select>
                      </td>
                      <td><input type="number" class="fr-yr" [(ngModel)]="cf.originYear" [disabled]="readonly()" aria-label="Année d'origine" /></td>
                      <td class="fr-num"><input type="number" step="0.001" [(ngModel)]="cf.initialAmount" [disabled]="readonly()" aria-label="Montant initial" /></td>
                      <td class="fr-num"><input type="number" step="0.001" [(ngModel)]="cf.imputedThisYear" [disabled]="readonly()" aria-label="Imputé cette année" /></td>
                      <td><input type="number" class="fr-yr" [ngModel]="cf.expiryYear" (ngModelChange)="cf.expiryYear = $event" [disabled]="readonly()" aria-label="Année d'expiration" /></td>
                      <td><app-button variant="ghost" size="sm" icon="pi pi-trash" type="button" (click)="removeCarryForward($index)" [disabled]="readonly()" ariaLabel="Supprimer ce report" /></td>
                    </tr>
                  }
                </tbody>
              </table>
            }
          </div>
        </div>

        <!-- Colonne calcul -->
        <div class="fr-col">
          <div class="card fr-card fr-compute">
            <h3 class="fr-card-title">Calcul de l'impôt</h3>
            <p class="fr-compute-note">Calcul serveur, mis à jour à chaque « Enregistrer et recalculer ».</p>
            <table class="fr-compute-table">
              <tbody>
                <tr><td>Résultat comptable net (après impôt)</td><td class="fr-num">{{ d.computation.accountingResult | number : '1.3-3' }}</td></tr>
                <tr><td>+ Réintégrations</td><td class="fr-num">{{ d.computation.totalReintegrations | number : '1.3-3' }}</td></tr>
                <tr><td>− Déductions</td><td class="fr-num">{{ d.computation.totalDeductions | number : '1.3-3' }}</td></tr>
                <tr class="fr-sub"><td>= Résultat fiscal avant reports</td><td class="fr-num">{{ d.computation.resultBeforeCarryForward | number : '1.3-3' }}</td></tr>
                <tr><td>− Déficits imputés</td><td class="fr-num">{{ d.computation.deficitsImputed | number : '1.3-3' }}</td></tr>
                <tr><td>− Amortissements différés imputés</td><td class="fr-num">{{ d.computation.deferredDepreciationImputed | number : '1.3-3' }}</td></tr>
                <tr class="fr-sub"><td>= Résultat fiscal imposable</td><td class="fr-num">{{ d.computation.taxableResult | number : '1.3-3' }}</td></tr>
                @if (d.computation.deficitGeneratedThisYear > 0) {
                  <tr class="fr-deficit"><td>Déficit généré (reportable)</td><td class="fr-num">{{ d.computation.deficitGeneratedThisYear | number : '1.3-3' }}</td></tr>
                }
                <tr><td>Impôt sur le résultat{{ d.computation.taxpayerKind === 0 ? ' (IS ' + (d.computation.appliedIsRate * 100 | number : '1.0-2') + ' %)' : ' (barème IRPP)' }}</td><td class="fr-num">{{ d.computation.taxOnResult | number : '1.3-3' }}</td></tr>
                <tr><td>Minimum d'impôt</td><td class="fr-num">{{ d.computation.minimumTax | number : '1.3-3' }}</td></tr>
                <tr class="fr-sub"><td>= Impôt dû (max)</td><td class="fr-num">{{ d.computation.taxDue | number : '1.3-3' }}</td></tr>
                <tr><td>+ Contribution sociale de solidarité (CSS)</td><td class="fr-num">{{ d.computation.css | number : '1.3-3' }}</td></tr>
                <tr class="fr-sub"><td>= Total impôt dû</td><td class="fr-num">{{ d.computation.totalTaxDue | number : '1.3-3' }}</td></tr>
                <tr><td>− Acomptes provisionnels</td><td class="fr-num">{{ d.computation.acomptesPaid | number : '1.3-3' }}</td></tr>
                <tr><td>− Retenues à la source subies</td><td class="fr-num">{{ d.computation.withholdingSuffered | number : '1.3-3' }}</td></tr>
                <tr><td>− Crédit d'impôt antérieur</td><td class="fr-num">{{ d.computation.priorTaxCredit | number : '1.3-3' }}</td></tr>
                <tr class="fr-total"><td>NET À PAYER</td><td class="fr-num">{{ d.computation.netToPay | number : '1.3-3' }}</td></tr>
                @if (d.computation.creditToCarry > 0) {
                  <tr class="fr-credit"><td>Crédit d'impôt à reporter</td><td class="fr-num">{{ d.computation.creditToCarry | number : '1.3-3' }}</td></tr>
                }
              </tbody>
            </table>
          </div>
        </div>
      </div>
    }

    <ng-template #linesTpl let-kind="kind">
      @if (linesFor(kind).length === 0) {
        <p class="fr-empty">Aucune ligne.</p>
      } @else {
        <table class="fr-table">
          <thead><tr><th>Libellé</th><th class="fr-num">Montant</th><th></th></tr></thead>
          <tbody>
            @for (line of linesFor(kind); track $index) {
              <tr>
                <td>
                  <input type="text" [(ngModel)]="line.label" [disabled]="readonly()" aria-label="Libellé de la ligne" />
                  @if (line.isAutoSuggested) { <span class="fr-auto" title="Suggestion automatique">auto</span> }
                </td>
                <td class="fr-num"><input type="number" step="0.001" [(ngModel)]="line.amount" [disabled]="readonly()" aria-label="Montant" /></td>
                <td><app-button variant="ghost" size="sm" icon="pi pi-trash" type="button" (click)="removeLine(line)" [disabled]="readonly()" ariaLabel="Supprimer la ligne" /></td>
              </tr>
            }
          </tbody>
        </table>
      }
    </ng-template>
  `,
  styles: `
    @use '../shared/accounting-layout';
    .fr-fields { display:flex; flex-wrap:wrap; align-items:flex-end; gap:var(--spacing-4); }
    .fr-year-input { max-width:7rem; }
    .fr-info-banner, .fr-warn-banner, .fr-lock-banner { display:flex; align-items:center; gap:var(--spacing-2); margin-bottom:var(--spacing-4); padding:var(--spacing-3) var(--spacing-4); border-radius:var(--radius-md); font-size:var(--font-size-sm); }
    .fr-info-banner { background:var(--color-success-50,#f0fdf4); border:1px solid var(--color-success-200,#bbf7d0); color:var(--color-success-700,#15803d); }
    .fr-warn-banner { background:var(--color-primary-50,#eff6ff); border:1px solid var(--color-primary-200,#bfdbfe); color:var(--color-primary-700,#1d4ed8); }
    .fr-lock-banner { background:var(--color-background-subtle,#f1f5f9); border:1px solid var(--color-border-default,#cbd5e1); color:var(--color-text-secondary,#475569); }
    .fr-grid { display:grid; grid-template-columns:1fr; gap:var(--spacing-4); }
    @media (min-width:1100px){ .fr-grid { grid-template-columns:1.4fr 1fr; align-items:start; } }
    .fr-col { display:flex; flex-direction:column; gap:var(--spacing-4); }
    .fr-card { padding:var(--spacing-4) var(--spacing-5); border-radius:var(--radius-lg); box-shadow:var(--shadow-sm); }
    .fr-card-head { display:flex; flex-wrap:wrap; align-items:center; justify-content:space-between; gap:var(--spacing-2); }
    .fr-card-title { font-size:var(--font-size-base); font-weight:var(--font-weight-bold); margin:0 0 var(--spacing-3); }
    .fr-sum { font-size:var(--font-size-sm); color:var(--color-success-700,#15803d); font-variant-numeric:tabular-nums; }
    .fr-sum--neg { color:var(--color-danger-600,#dc2626); }
    .fr-add { display:flex; align-items:center; gap:var(--spacing-2); flex-wrap:wrap; }
    .fr-cat-select { max-width:16rem; padding:0.35rem 0.5rem; border:1px solid var(--color-border-default,#cbd5e1); border-radius:var(--radius-md); font-size:var(--font-size-sm); }
    .fr-input-row { display:grid; grid-template-columns:1fr 10rem auto; align-items:center; gap:var(--spacing-2); margin-bottom:var(--spacing-2); }
    .fr-input-row label { font-size:var(--font-size-sm); color:var(--color-text-secondary); }
    .fr-input-row input { padding:0.4rem 0.55rem; border:1px solid var(--color-border-default,#cbd5e1); border-radius:var(--radius-md); text-align:right; font-variant-numeric:tabular-nums; }
    .fr-input-row select { padding:0.4rem 0.55rem; border:1px solid var(--color-border-default,#cbd5e1); border-radius:var(--radius-md); }
    .fr-hint { font-size:var(--font-size-xs); color:var(--color-text-tertiary); }
    .fr-suggest-row { display:flex; align-items:center; justify-content:flex-end; gap:var(--spacing-2); margin:-0.25rem 0 var(--spacing-2); }
    .fr-alert-banner { display:flex; align-items:flex-start; gap:var(--spacing-2); margin-bottom:var(--spacing-4); padding:var(--spacing-3) var(--spacing-4); border-radius:var(--radius-md); font-size:var(--font-size-sm); background:var(--color-warning-50,#fffbeb); border:1px solid var(--color-warning-200,#fde68a); color:var(--color-warning-800,#92400e); }
    .fr-warn-list { margin:0.25rem 0 0; padding-left:1.1rem; }
    .fr-warn-list li { margin:0.15rem 0; }
    .fr-empty { font-size:var(--font-size-sm); color:var(--color-text-tertiary); margin:var(--spacing-2) 0 0; }
    .fr-table { width:100%; border-collapse:collapse; margin-top:var(--spacing-2); }
    .fr-table th { font-size:var(--font-size-xs); text-transform:uppercase; letter-spacing:0.04em; color:var(--color-text-tertiary); text-align:left; padding:var(--spacing-1) var(--spacing-2); border-bottom:1px solid var(--color-border-subtle); }
    .fr-table td { padding:var(--spacing-1) var(--spacing-2); vertical-align:middle; }
    .fr-table input, .fr-table select { width:100%; padding:0.3rem 0.45rem; border:1px solid var(--color-border-default,#cbd5e1); border-radius:var(--radius-sm,4px); }
    .fr-num { text-align:right; font-variant-numeric:tabular-nums; }
    .fr-table .fr-num input { text-align:right; }
    .fr-yr { max-width:6rem; }
    .fr-auto { margin-left:0.4rem; font-size:var(--font-size-xs); color:var(--color-primary-600,#2563eb); border:1px solid var(--color-primary-200,#bfdbfe); border-radius:var(--radius-pill,999px); padding:0 0.4rem; }
    .fr-compute { position:sticky; top:var(--spacing-4); }
    .fr-compute-note { font-size:var(--font-size-xs); color:var(--color-text-tertiary); margin:0 0 var(--spacing-3); }
    .fr-compute-table { width:100%; border-collapse:collapse; }
    .fr-compute-table td { padding:var(--spacing-2) var(--spacing-2); font-size:var(--font-size-sm); border-bottom:1px solid var(--color-border-subtle); }
    .fr-compute-table td.fr-num { font-variant-numeric:tabular-nums; font-weight:var(--font-weight-medium); white-space:nowrap; }
    .fr-compute-table .fr-sub td { font-weight:var(--font-weight-bold); background:var(--color-background-subtle); }
    .fr-compute-table .fr-total td { font-weight:var(--font-weight-bold); font-size:var(--font-size-base); color:var(--color-primary-700,#1d4ed8); border-top:2px solid var(--color-border-default); }
    .fr-compute-table .fr-credit td, .fr-compute-table .fr-deficit td { color:var(--color-warning-700,#b45309); }
  `
})
export class FiscalResultComponent implements OnInit {
  private readonly api = inject(AccountingService);

  fiscalYear = new Date().getFullYear() - 1;
  readonly data = signal<FiscalResultDeclarationDto | null>(null);
  readonly catalog = signal<FiscalAdjustmentCatalogEntryDto[]>([]);
  readonly loading = signal(false);
  readonly saving = signal(false);
  readonly error = signal<string | null>(null);
  readonly info = signal<string | null>(null);
  /**
   * CA local TTC calculé depuis la comptabilité. Conservé côté client : l'enregistrement ne le
   * recalcule pas (il l'est au chargement), la valeur reste donc disponible après sauvegarde.
   */
  readonly suggestedTurnover = signal(0);

  model: EditableModel = this.blankModel();

  readonly readonly = computed(() => this.data()?.isFinalized ?? false);
  readonly catalogReintegrations = computed(() => this.catalog().filter(c => c.kind === 0));
  readonly catalogDeductions = computed(() => this.catalog().filter(c => c.kind === 1));

  ngOnInit(): void {
    this.api.getFiscalAdjustmentCatalog().subscribe({
      next: res => { if (res.success && res.data) this.catalog.set(res.data); }
    });
    this.load();
  }

  /** Lignes de réintégration (kind=0) ou déduction (kind=1) du modèle courant. */
  linesFor(kind: number): FiscalAdjustmentLineDto[] {
    return this.model.adjustments.filter(a => a.kind === kind);
  }
  reintegrationsTotal(): number {
    return this.linesFor(0).reduce((s, l) => s + (Number(l.amount) || 0), 0);
  }
  deductionsTotal(): number {
    return this.linesFor(1).reduce((s, l) => s + (Number(l.amount) || 0), 0);
  }

  load(): void {
    const y = Math.min(2100, Math.max(2000, Math.floor(Number(this.fiscalYear)) || new Date().getFullYear()));
    this.fiscalYear = y;
    this.loading.set(true);
    this.error.set(null);
    this.info.set(null);
    this.api.getFiscalResult(y).subscribe({
      next: res => {
        this.loading.set(false);
        if (res.success && res.data) {
          this.data.set(res.data);
          this.model = this.toModel(res.data);
          this.suggestedTurnover.set(res.data.suggestedLocalTurnoverTtc ?? 0);
        } else {
          this.error.set(res.error ?? 'Erreur');
        }
      },
      error: () => {
        this.loading.set(false);
        this.error.set('Erreur réseau');
      }
    });
  }

  save(): void {
    if (this.readonly()) return;
    this.saving.set(true);
    this.error.set(null);
    this.info.set(null);
    const request: UpsertFiscalResultRequest = {
      taxpayerKind: this.model.taxpayerKind,
      accountingResult: Number(this.model.accountingResult) || 0,
      appliedIsRate: Number(this.model.appliedIsRate) || 0,
      localTurnoverTtc: Number(this.model.localTurnoverTtc) || 0,
      minimumTaxRegime: Number(this.model.minimumTaxRegime) || 0,
      acomptesPaid: Number(this.model.acomptesPaid) || 0,
      withholdingSuffered: Number(this.model.withholdingSuffered) || 0,
      priorTaxCredit: Number(this.model.priorTaxCredit) || 0,
      adjustments: this.model.adjustments.map(a => ({
        catalogCode: a.catalogCode ?? null,
        kind: a.kind,
        label: a.label,
        amount: Number(a.amount) || 0,
        isAutoSuggested: a.isAutoSuggested
      })),
      carryForwards: this.model.carryForwards.map(c => ({
        kind: c.kind,
        originYear: Number(c.originYear) || 0,
        initialAmount: Number(c.initialAmount) || 0,
        imputedThisYear: Number(c.imputedThisYear) || 0,
        expiryYear: c.expiryYear ?? null
      }))
    };
    this.api.upsertFiscalResult(this.fiscalYear, request).subscribe({
      next: res => {
        this.saving.set(false);
        if (res.success && res.data) {
          this.data.set(res.data);
          this.model = this.toModel(res.data);
          this.info.set('Feuille enregistrée et recalculée.');
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

  finalize(): void {
    if (this.readonly()) return;
    if (!confirm('Finaliser la feuille de détermination ? Elle deviendra non modifiable.')) return;
    this.saving.set(true);
    this.error.set(null);
    this.api.finalizeFiscalResult(this.fiscalYear).subscribe({
      next: res => {
        this.saving.set(false);
        if (res.success) {
          this.info.set('Feuille finalisée.');
          this.load();
        } else {
          this.error.set(res.error ?? 'La finalisation a échoué.');
        }
      },
      error: () => {
        this.saving.set(false);
        this.error.set('La finalisation a échoué (réservée au cabinet en mode dossier délégué).');
      }
    });
  }

  addLine(kind: number): void {
    this.model.adjustments.push({ catalogCode: null, kind, label: '', amount: 0, isAutoSuggested: false });
  }
  addFromCatalog(code: string, kind: number): void {
    if (!code) return;
    const entry = this.catalog().find(c => c.code === code);
    if (!entry) return;
    this.model.adjustments.push({ catalogCode: entry.code, kind, label: entry.label, amount: 0, isAutoSuggested: false });
  }
  removeLine(line: FiscalAdjustmentLineDto): void {
    const i = this.model.adjustments.indexOf(line);
    if (i >= 0) this.model.adjustments.splice(i, 1);
  }
  addCarryForward(kind: number): void {
    this.model.carryForwards.push({ kind, originYear: this.fiscalYear - 1, initialAmount: 0, imputedThisYear: 0, expiryYear: null });
  }
  removeCarryForward(index: number): void {
    this.model.carryForwards.splice(index, 1);
  }

  onExportDetermination(format: AccountingExportFormat): void {
    this.api.exportFiscalResult(this.fiscalYear, format).subscribe({
      next: blob => downloadBlob(blob, `determination_fiscale_${this.fiscalYear}.${exportExtension(format)}`),
      error: () => this.error.set("L'export a échoué.")
    });
  }
  onExportLiasse(format: AccountingExportFormat): void {
    this.api.exportConsolidatedLiasse(this.fiscalYear, format).subscribe({
      next: blob => downloadBlob(blob, `liasse_fiscale_${this.fiscalYear}.${exportExtension(format)}`),
      error: () => this.error.set("L'export a échoué.")
    });
  }

  /** Reprend le CA local TTC calculé depuis la comptabilité comme base du minimum d'impôt. */
  applySuggestedTurnover(): void {
    if (this.readonly()) return;
    this.model.localTurnoverTtc = this.suggestedTurnover();
  }

  private toModel(d: FiscalResultDeclarationDto): EditableModel {
    return {
      taxpayerKind: d.taxpayerKind,
      accountingResult: d.accountingResult,
      appliedIsRate: d.appliedIsRate,
      localTurnoverTtc: d.localTurnoverTtc,
      minimumTaxRegime: d.minimumTaxRegime ?? 0,
      acomptesPaid: d.acomptesPaid,
      withholdingSuffered: d.withholdingSuffered,
      priorTaxCredit: d.priorTaxCredit,
      adjustments: d.adjustments.map(a => ({ ...a })),
      carryForwards: d.carryForwards.map(c => ({ ...c }))
    };
  }
  private blankModel(): EditableModel {
    return {
      taxpayerKind: 0, accountingResult: 0, appliedIsRate: 0.15, localTurnoverTtc: 0, minimumTaxRegime: 0,
      acomptesPaid: 0, withholdingSuffered: 0, priorTaxCredit: 0, adjustments: [], carryForwards: []
    };
  }
}
