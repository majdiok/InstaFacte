import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { PageHeaderComponent } from '@shared/components/page-header/page-header.component';
import { ButtonComponent } from '@shared/components/button/button.component';
import { AccountingFilterBarComponent } from '../shared/accounting-filter-bar.component';
import { AccountingStatusBannerComponent } from '../shared/accounting-status-banner.component';
import { AccountingService, NctFinancialStatementsDto, NctLineDto } from '../services/accounting.service';
import { FinancialStatementsExportDialogComponent } from './financial-statements-export-dialog.component';

type NctTab = 'bilan' | 'resultat' | 'flux' | 'capitaux' | 'notes';

@Component({
  selector: 'app-nct-statements',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    PageHeaderComponent,
    ButtonComponent,
    AccountingFilterBarComponent,
    AccountingStatusBannerComponent,
    FinancialStatementsExportDialogComponent
  ],
  template: `
    <app-page-header title="États financiers NCT" subtitle="Liasse : bilan, résultat, flux de trésorerie, capitaux propres et notes" />

    <div class="card accounting-filters-card">
      <app-accounting-filter-bar ariaLabel="Exercice de la liasse NCT">
        <div accountingFilterFields>
          <div class="accounting-filter-field">
            <label class="accounting-filter-label" for="nct-year">Exercice</label>
            <input id="nct-year" type="number" [(ngModel)]="fiscalYear" class="accounting-filter-input nct-year-input" />
          </div>
        </div>
        <div accountingFilterActions>
          <app-button variant="secondary" icon="pi pi-refresh" type="button" (click)="load()" [disabled]="loading()"
            ariaLabel="Charger la liasse NCT">Charger</app-button>
          <app-button variant="secondary" icon="pi pi-eye" type="button" (click)="openExportDialog()" [disabled]="loading() || !data()"
            ariaLabel="Aperçu et impression des états financiers">Aperçu / Impression</app-button>
          <app-button variant="secondary" icon="pi pi-file-pdf" type="button" (click)="exportPdf()" [disabled]="loading() || !data()"
            ariaLabel="Exporter la liasse NCT en PDF">Exporter PDF</app-button>
        </div>
      </app-accounting-filter-bar>
    </div>

    <app-financial-statements-export-dialog
      [(visible)]="exportDialogVisible"
      [fiscalYear]="fiscalYear"
      [statements]="data()" />

    <app-accounting-status-banner variant="error" [message]="error() ?? ''" [showRetry]="!!error()" retryLabel="Réessayer" (retry)="load()" />

    @if (data(); as d) {
      @if (!d.nctStatementsEnabled) {
        <div class="nct-info-banner" role="status">
          <i class="pi pi-info-circle" aria-hidden="true"></i>
          <span>La liasse NCT n'est pas activée pour ce dossier. L'aperçu ci-dessous est indicatif.</span>
        </div>
      }

      <div class="nct-tabs" role="tablist">
        <button type="button" role="tab" class="nct-tab" [class.nct-tab--active]="tab() === 'bilan'" (click)="tab.set('bilan')">Bilan</button>
        <button type="button" role="tab" class="nct-tab" [class.nct-tab--active]="tab() === 'resultat'" (click)="tab.set('resultat')">Compte de résultat</button>
        <button type="button" role="tab" class="nct-tab" [class.nct-tab--active]="tab() === 'flux'" (click)="tab.set('flux')">Flux de trésorerie</button>
        <button type="button" role="tab" class="nct-tab" [class.nct-tab--active]="tab() === 'capitaux'" (click)="tab.set('capitaux')">Capitaux propres</button>
        <button type="button" role="tab" class="nct-tab" [class.nct-tab--active]="tab() === 'notes'" (click)="tab.set('notes')">Notes</button>
      </div>

      <div class="card nct-card">
        @switch (tab()) {
          @case ('bilan') {
            <div class="nct-balance-status">
              <span class="nct-badge" [class.nct-badge--ok]="d.balanceSheet.isBalanced" [class.nct-badge--ko]="!d.balanceSheet.isBalanced">
                {{ d.balanceSheet.isBalanced ? 'Bilan équilibré' : 'Écart Actif / Passif' }}
              </span>
            </div>
            <h3 class="nct-section">ACTIF</h3>
            <ng-container [ngTemplateOutlet]="tableTpl" [ngTemplateOutletContext]="{ lines: d.balanceSheet.assets }"></ng-container>
            <h3 class="nct-section">CAPITAUX PROPRES ET PASSIFS</h3>
            <ng-container [ngTemplateOutlet]="tableTpl" [ngTemplateOutletContext]="{ lines: d.balanceSheet.equityAndLiabilities }"></ng-container>
          }
          @case ('resultat') {
            <ng-container [ngTemplateOutlet]="tableTpl" [ngTemplateOutletContext]="{ lines: d.incomeStatement.lines }"></ng-container>
          }
          @case ('flux') {
            <p class="nct-hint">Méthode indirecte. La ligne « Écart de rapprochement » explicite tout résidu par rapport à la variation réelle de trésorerie.</p>
            <ng-container [ngTemplateOutlet]="tableTpl" [ngTemplateOutletContext]="{ lines: d.cashFlow.lines }"></ng-container>
          }
          @case ('capitaux') {
            <ng-container [ngTemplateOutlet]="tableTpl" [ngTemplateOutletContext]="{ lines: d.equityChanges.lines }"></ng-container>
          }
          @case ('notes') {
            @for (note of d.notes; track note.title) {
              <h3 class="nct-section">{{ note.title }}</h3>
              <ng-container [ngTemplateOutlet]="tableTpl" [ngTemplateOutletContext]="{ lines: note.lines }"></ng-container>
            }
          }
        }
      </div>
    }

    <ng-template #tableTpl let-lines="lines">
      <table class="nct-table">
        <thead>
          <tr>
            <th scope="col" class="nct-th-label">Rubrique</th>
            <th scope="col" class="nct-th-amount">Exercice N</th>
            <th scope="col" class="nct-th-amount">Exercice N-1</th>
          </tr>
        </thead>
        <tbody>
          @for (line of lines; track line.code) {
            <tr [class.nct-row--subtotal]="line.isSubtotal">
              <td class="nct-td-label" [style.paddingLeft.rem]="0.75 + line.level * 1.25">{{ line.label }}</td>
              <td class="nct-td-amount">{{ line.amount | number : '1.3-3' }}</td>
              <td class="nct-td-amount nct-td-prev">{{ line.previousAmount | number : '1.3-3' }}</td>
            </tr>
          }
        </tbody>
      </table>
    </ng-template>
  `,
  styles: `
    @use '../shared/accounting-layout';
    .nct-year-input { max-width: 7rem; }
    .nct-info-banner { display:flex; align-items:center; gap:var(--spacing-2); margin-bottom:var(--spacing-4); padding:var(--spacing-3) var(--spacing-4); border-radius:var(--radius-md); background:var(--color-primary-50,#eff6ff); border:1px solid var(--color-primary-200,#bfdbfe); color:var(--color-primary-700,#1d4ed8); font-size:var(--font-size-sm); }
    .nct-tabs { display:flex; flex-wrap:wrap; gap:var(--spacing-2); margin-bottom:var(--spacing-4); }
    .nct-tab { padding:var(--spacing-2) var(--spacing-4); border:1px solid var(--color-border-default); border-radius:var(--radius-pill,999px); background:var(--color-background-elevated); color:var(--color-text-secondary); font-size:var(--font-size-sm); font-weight:var(--font-weight-medium); cursor:pointer; }
    .nct-tab--active { background:var(--color-primary-500,#2563eb); color:#fff; border-color:var(--color-primary-500,#2563eb); }
    .nct-card { padding:var(--spacing-5); border-radius:var(--radius-lg); box-shadow:var(--shadow-sm); max-width:56rem; }
    .nct-balance-status { margin-bottom:var(--spacing-3); }
    .nct-badge { display:inline-block; padding:0.15rem 0.6rem; border-radius:var(--radius-pill,999px); font-size:var(--font-size-xs); font-weight:var(--font-weight-semibold); }
    .nct-badge--ok { background:var(--color-success-100,#dcfce7); color:var(--color-success-700,#15803d); }
    .nct-badge--ko { background:var(--color-danger-100,#fee2e2); color:var(--color-danger-700,#b91c1c); }
    .nct-section { font-size:var(--font-size-sm); font-weight:var(--font-weight-bold); text-transform:uppercase; letter-spacing:0.04em; color:var(--color-text-tertiary); margin:var(--spacing-4) 0 var(--spacing-2); }
    .nct-hint { font-size:var(--font-size-sm); color:var(--color-text-secondary); margin:0 0 var(--spacing-3); }
    .nct-table { width:100%; border-collapse:collapse; margin-bottom:var(--spacing-2); }
    .nct-th-label, .nct-th-amount { font-size:var(--font-size-xs); text-transform:uppercase; letter-spacing:0.04em; color:var(--color-text-tertiary); padding:var(--spacing-2) var(--spacing-3); border-bottom:1px solid var(--color-border-subtle); }
    .nct-th-amount { text-align:right; }
    .nct-td-label { padding:var(--spacing-2) var(--spacing-3); color:var(--color-text-secondary); }
    .nct-td-amount { padding:var(--spacing-2) var(--spacing-3); text-align:right; font-variant-numeric:tabular-nums; font-weight:var(--font-weight-medium); }
    .nct-td-prev { color:var(--color-text-tertiary); }
    .nct-row--subtotal > td { font-weight:var(--font-weight-bold); color:var(--color-text-primary); border-top:1px solid var(--color-border-subtle); background:var(--color-background-subtle); }
  `
})
export class NctStatementsComponent implements OnInit {
  private readonly api = inject(AccountingService);
  fiscalYear = new Date().getFullYear();
  exportDialogVisible = false;
  readonly data = signal<NctFinancialStatementsDto | null>(null);
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);
  readonly tab = signal<NctTab>('bilan');

  ngOnInit(): void {
    this.load();
  }

  openExportDialog(): void {
    if (!this.data()) return;
    this.exportDialogVisible = true;
  }

  load(): void {
    const y = Math.min(2100, Math.max(2000, Math.floor(Number(this.fiscalYear)) || new Date().getFullYear()));
    this.fiscalYear = y;
    this.loading.set(true);
    this.error.set(null);
    this.api.getNctStatements(y).subscribe({
      next: res => {
        this.loading.set(false);
        if (res.success && res.data) this.data.set(res.data);
        else this.error.set(res.error ?? 'Erreur');
      },
      error: () => {
        this.loading.set(false);
        this.error.set('Erreur réseau');
      }
    });
  }

  exportPdf(): void {
    if (!this.data()) return;
    this.api.exportNctStatementsPdf(this.fiscalYear).subscribe({
      next: blob => {
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = `liasse_nct_${this.fiscalYear}.pdf`;
        a.click();
        URL.revokeObjectURL(url);
      },
      error: () => this.error.set("L'export PDF a échoué.")
    });
  }

  // Référence utilisée par le template (évite l'avertissement d'import non utilisé).
  protected trackLine(_: number, line: NctLineDto): string {
    return line.code;
  }
}
