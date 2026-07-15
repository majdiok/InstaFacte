import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import {
  WithholdingTaxService,
  TejSubmissionType,
  TejXmlExportLogListItem
} from '@core/services/withholding-tax.service';
import { ErrorHandlerService } from '@core/services/error-handler.service';
import { ConfirmationService } from '@core/services/confirmation.service';

@Component({
  selector: 'app-tej-export',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  template: `
    <div class="page-container">
      <div class="page-header">
        <div>
          <h1>Export TEJ — Déclaration XML</h1>
          <p class="subtitle">Générez le fichier XML pour préparer la déclaration ; la soumission officielle se fait sur le portail TEJ (connexion OIDC).</p>
        </div>
        <a routerLink="/withholding-tax/dashboard" class="btn btn-outline">
          <i class="fa-solid fa-arrow-left"></i> Tableau de bord
        </a>
      </div>

      <!-- Parameters -->
      <div class="card">
        <h3>Paramètres de la déclaration</h3>
        <div class="form-grid">
          <div class="form-group">
            <label for="ex-y">Exercice (année) *</label>
            <select id="ex-y" [(ngModel)]="exerciceYear" (ngModelChange)="onPeriodChange()">
              @for (y of years; track y) {
                <option [ngValue]="y">{{ y }}</option>
              }
            </select>
          </div>
          <div class="form-group">
            <label for="ex-m">Mois *</label>
            <select id="ex-m" [(ngModel)]="month" (ngModelChange)="onPeriodChange()">
              @for (m of months; track m.value) {
                <option [ngValue]="m.value">{{ m.label }}</option>
              }
            </select>
          </div>
          <div class="form-group">
            <label for="ex-t">Type de soumission *</label>
            <select id="ex-t" [(ngModel)]="submissionType">
              <option [ngValue]="TejSubmissionType.Initiale">Initiale</option>
              <option [ngValue]="TejSubmissionType.Corrective">Corrective</option>
            </select>
          </div>
        </div>

        <div class="form-actions">
          <button
            type="button"
            class="btn btn-outline"
            (click)="preview()"
            [disabled]="previewing || exportActionsBlocked"
            [attr.title]="exportActionsBlocked ? exportBlockedTitle : null">
            @if (previewing) { <i class="fa-solid fa-spinner fa-spin"></i> }
            <i class="fa-solid fa-eye"></i> Prévisualiser XML
          </button>
          <button
            type="button"
            class="btn btn-primary"
            (click)="generate()"
            [disabled]="generating || exportActionsBlocked"
            [attr.title]="exportActionsBlocked ? exportBlockedTitle : null">
            @if (generating) { <i class="fa-solid fa-spinner fa-spin"></i> }
            <i class="fa-solid fa-download"></i> Générer et télécharger
          </button>
        </div>
      </div>

      @if (periodHintLoading) {
        <p class="muted period-hint">Vérification des factures fournisseurs éligibles pour cette période…</p>
      }
      @if (!periodHintLoading && periodEligibleCount === 0) {
        <div class="card card-warning" role="status">
          <h3><i class="fa-solid fa-circle-info"></i> Aucune facture fournisseur éligible pour cette période</h3>
          <p class="hint-p">Une facture n’apparaît dans le fichier que si <strong>toutes</strong> les conditions suivantes sont remplies :</p>
          <ul class="hint-list">
            <li>statut <strong>Payée</strong> (soldée) ;</li>
            <li>retenue calculée et enregistrée sur la facture (fournisseur <strong>assujetti à la RS</strong>, type d’opération RS, montant &gt; 0) ;</li>
            <li>date de solde (<strong>Payée le</strong>) dans le <strong>mois et l’année</strong> sélectionnés.</li>
          </ul>
          <p class="hint-p muted-hint">Un montant TTC supérieur au seuil RS7 (achats) ne suffit pas si le fournisseur n’est pas paramétré comme assujetti ou si la facture n’est pas encore soldée.</p>
          <a routerLink="/withholding-tax/dashboard" class="btn btn-outline btn-sm">Tableau de bord retenue à la source</a>
        </div>
      }

      @if (apiError) {
        <div class="card card-error" role="alert">
          <h3><i class="fa-solid fa-circle-xmark"></i> Action impossible</h3>
          <p class="hint-p">{{ apiError }}</p>
        </div>
      }

      <!-- Validation results -->
      @if (validationErrors.length) {
        <div class="card card-error">
          <h3><i class="fa-solid fa-triangle-exclamation"></i> Erreurs de validation</h3>
          <ul>
            @for (err of validationErrors; track $index) {
              <li>{{ err }}</li>
            }
          </ul>
          <p class="help-text">Corrigez ces erreurs avant de soumettre le fichier sur la plateforme TEJ.</p>
        </div>
      }

      @if (validationSuccess) {
        <div class="card card-success">
          <h3><i class="fa-solid fa-check-circle"></i> Validation réussie</h3>
          <p>Le fichier XML est prêt pour contrôle ; vérifiez-le sur le portail avant envoi définitif.</p>
        </div>
      }

      <!-- XML preview -->
      @if (xmlPreview) {
        <div class="card">
          <div class="preview-header">
            <h3>Prévisualisation XML</h3>
            <button type="button" class="btn btn-outline btn-sm" (click)="copyXml()">
              <i class="fa-solid fa-copy"></i> Copier
            </button>
          </div>
          <pre class="xml-preview"><code>{{ xmlPreview }}</code></pre>
        </div>
      }

      <!-- History -->
      <div class="card">
        <h3>Historique des exports</h3>
        @if (historyLoading) {
          <p class="muted">Chargement…</p>
        } @else if (exportHistory.length === 0) {
          <p class="muted">Aucun export enregistré pour ce tenant.</p>
        } @else {
          <div class="table-wrap">
            <table class="data-table">
              <thead>
                <tr>
                  <th>Date</th>
                  <th>Fichier</th>
                  <th>Période</th>
                  <th>Lignes XML</th>
                  <th>Valide</th>
                  <th>Empreinte SHA-256</th>
                </tr>
              </thead>
              <tbody>
                @for (row of exportHistory; track row.id) {
                  <tr>
                    <td>{{ row.createdAt | date:'short' }}</td>
                    <td class="mono">{{ row.fileName }}</td>
                    <td>{{ row.month }}/{{ row.year }}</td>
                    <td>{{ row.certificateCount }}</td>
                    <td>{{ row.isValid ? 'Oui' : 'Non' }}</td>
                    <td class="mono hash">{{ row.sha256Hex }}</td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
        }
      </div>

      <!-- Help section -->
      <div class="card help-card">
        <h3><i class="fa-solid fa-info-circle"></i> Portail TEJ et sécurité</h3>
        <div class="help-grid help-grid-3">
          <div>
            <h4>Format du fichier</h4>
            <ul>
              <li>XML structuré (validation interne + schéma enveloppe embarqué ; complétez avec les XSD officiels si fournis)</li>
              <li>Montants en millimes (entiers)</li>
              <li>Dates au format JJ/MM/AAAA</li>
              <li>Encodage UTF-8</li>
            </ul>
          </div>
          <div>
            <h4>Connexion au portail</h4>
            <p class="help-p">L’authentification se fait via <strong>OpenID Connect</strong> sur le domaine dédié (ex. <code>login-tej.finances.gov.tn</code>). InstaFact ne stocke <strong>aucun mot de passe</strong> du portail TEJ.</p>
            <ol>
              <li>Générez le fichier XML dans InstaFact</li>
              <li>Ouvrez une session sur le portail TEJ avec vos identifiants institutionnels</li>
              <li>Téléversez le fichier et suivez la procédure de déclaration</li>
            </ol>
          </div>
          <div>
            <h4>Dossier fiscal &amp; éligibilité</h4>
            <p class="help-p">Pour vérifier le matricule fiscal ou consulter un dossier, utilisez les services en ligne du ministère des Finances.</p>
            <p class="help-p">
              <a
                href="https://www.finances.gov.tn"
                target="_blank"
                rel="noopener noreferrer"
                class="ext-link">Portail des finances publiques (Tunisie)</a>
            </p>
          </div>
        </div>
      </div>
    </div>
  `,
  styles: [`
    .page-container { padding: 1.5rem; max-width: 960px; }
    .page-header { display: flex; justify-content: space-between; align-items: flex-start; margin-bottom: 1.5rem; flex-wrap: wrap; gap: 1rem; }
    .page-header h1 { font-size: 1.5rem; font-weight: 600; margin: 0; }
    .subtitle { color: var(--text-secondary); margin: 0.25rem 0 0; font-size: 0.875rem; }
    .card { background: var(--bg-card); border: 1px solid var(--border); border-radius: 8px; padding: 1.25rem; margin-bottom: 1.5rem; }
    .card h3 { font-size: 1rem; font-weight: 600; margin: 0 0 1rem; display: flex; align-items: center; gap: 0.5rem; }
    .card-error { border-color: #fca5a5; background: #fef2f2; }
    .card-error h3 { color: #b91c1c; }
    .card-error ul { margin: 0; padding-left: 1.25rem; }
    .card-error li { color: #991b1b; font-size: 0.875rem; margin-bottom: 0.25rem; }
    .card-success { border-color: #86efac; background: #f0fdf4; }
    .card-success h3 { color: #166534; }
    .card-success p { color: #166534; font-size: 0.875rem; margin: 0; }
    .card-warning { border-color: #fcd34d; background: #fffbeb; }
    .card-warning h3 { color: #b45309; font-size: 0.9375rem; }
    .hint-p { font-size: 0.875rem; line-height: 1.5; margin: 0 0 0.75rem; color: var(--text-secondary); }
    .hint-list { margin: 0 0 0.75rem; padding-left: 1.25rem; font-size: 0.875rem; line-height: 1.5; color: var(--text-secondary); }
    .hint-list li { margin-bottom: 0.35rem; }
    .muted-hint { font-size: 0.8125rem; color: var(--text-secondary); opacity: 0.95; }
    .period-hint { margin: -0.5rem 0 1rem; font-size: 0.8125rem; }
    .help-text { font-size: 0.8125rem; color: #b91c1c; margin: 0.5rem 0 0; }
    .form-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(160px, 1fr)); gap: 1rem; margin-bottom: 1rem; }
    .form-group { display: flex; flex-direction: column; gap: 0.25rem; }
    .form-group label { font-size: 0.8125rem; font-weight: 500; color: var(--text-secondary); }
    .form-group select { padding: 0.5rem 0.75rem; border: 1px solid var(--border); border-radius: 6px; font-size: 0.875rem; }
    .form-actions { display: flex; gap: 0.75rem; justify-content: flex-end; flex-wrap: wrap; }
    .preview-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 0.75rem; }
    .preview-header h3 { margin: 0; }
    .xml-preview { background: var(--bg-secondary); padding: 1rem; border-radius: 6px; overflow-x: auto; font-size: 0.8125rem; line-height: 1.5; max-height: 400px; overflow-y: auto; margin: 0; }
    .help-card { background: var(--bg-secondary); }
    .help-grid { display: grid; gap: 1.5rem; }
    .help-grid-3 { grid-template-columns: repeat(auto-fit, minmax(220px, 1fr)); }
    .help-grid h4 { font-size: 0.875rem; font-weight: 600; margin: 0 0 0.5rem; }
    .help-grid ul, .help-grid ol { margin: 0; padding-left: 1.25rem; }
    .help-grid li { font-size: 0.8125rem; margin-bottom: 0.25rem; }
    .help-p { font-size: 0.8125rem; line-height: 1.5; margin: 0 0 0.75rem; color: var(--text-secondary); }
    .ext-link { color: var(--primary); font-weight: 500; }
    .muted { font-size: 0.875rem; color: var(--text-secondary); margin: 0; }
    .table-wrap { overflow-x: auto; }
    .data-table { width: 100%; border-collapse: collapse; font-size: 0.8125rem; }
    .data-table th, .data-table td { padding: 0.5rem 0.75rem; border-bottom: 1px solid var(--border); text-align: left; }
    .data-table th { color: var(--text-secondary); font-weight: 600; }
    .mono { font-family: ui-monospace, monospace; }
    .hash { max-width: 140px; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
    .btn { padding: 0.5rem 1rem; border: 1px solid var(--border); border-radius: 6px; cursor: pointer; font-size: 0.875rem; display: inline-flex; align-items: center; gap: 0.5rem; text-decoration: none; background: transparent; }
    .btn-primary { background: var(--primary); color: white; border-color: var(--primary); }
    .btn-outline { background: transparent; }
    .btn-sm { padding: 0.25rem 0.5rem; font-size: 0.75rem; }
    code { font-size: 0.8em; padding: 0.1rem 0.35rem; border-radius: 4px; background: var(--bg-secondary); }
  `]
})
export class TejExportComponent implements OnInit {
  /** Aligné sur `Error.Validation` côté API (ExportTejXml / preview). */
  private static readonly tejNoEligibleInvoicesApiMessage =
    'Aucune facture fournisseur soldée avec retenue à la source pour cette période';

  private service = inject(WithholdingTaxService);
  private errorHandler = inject(ErrorHandlerService);
  private confirmation = inject(ConfirmationService);

  readonly TejSubmissionType = TejSubmissionType;

  exerciceYear = new Date().getFullYear();
  month = new Date().getMonth() + 1;
  submissionType = TejSubmissionType.Initiale;

  generating = false;
  previewing = false;
  xmlPreview = '';
  validationErrors: string[] = [];
  validationSuccess = false;
  apiError = '';

  /** null = chargement ou indisponible ; 0 = aucune FF éligible pour la période */
  periodEligibleCount: number | null = null;
  periodHintLoading = false;

  exportHistory: TejXmlExportLogListItem[] = [];
  historyLoading = true;

  years = Array.from({ length: 7 }, (_, i) => new Date().getFullYear() - i);
  months = [
    { value: 1, label: 'Janvier' }, { value: 2, label: 'Février' }, { value: 3, label: 'Mars' },
    { value: 4, label: 'Avril' }, { value: 5, label: 'Mai' }, { value: 6, label: 'Juin' },
    { value: 7, label: 'Juillet' }, { value: 8, label: 'Août' }, { value: 9, label: 'Septembre' },
    { value: 10, label: 'Octobre' }, { value: 11, label: 'Novembre' }, { value: 12, label: 'Décembre' }
  ];

  ngOnInit(): void {
    this.loadHistory();
    this.refreshPeriodHint();
  }

  onPeriodChange(): void {
    this.apiError = '';
    this.refreshPeriodHint();
  }

  /** Bloque preview/generate tant que le hint charge ou qu’il n’y a aucune FF éligible (null = indisponible, ne pas bloquer). */
  get exportActionsBlocked(): boolean {
    return this.periodHintLoading || this.periodEligibleCount === 0;
  }

  get exportBlockedTitle(): string {
    if (this.periodHintLoading) {
      return 'Vérification des factures éligibles pour cette période en cours…';
    }
    if (this.periodEligibleCount === 0) {
      return 'Aucune facture fournisseur soldée avec retenue pour ce mois. La date de solde doit correspondre à la période choisie.';
    }
    return '';
  }

  private refreshPeriodHint(): void {
    this.periodHintLoading = true;
    this.service.getTejEligibleInvoiceCount(this.exerciceYear, this.month).subscribe({
      next: (res) => {
        this.periodEligibleCount = res.count ?? 0;
        this.periodHintLoading = false;
      },
      error: () => {
        this.periodEligibleCount = null;
        this.periodHintLoading = false;
      }
    });
  }

  private extractHttpMessage(err: unknown): string {
    if (err instanceof HttpErrorResponse) {
      return this.errorHandler.extractErrorMessage(err);
    }
    return 'Une erreur est survenue.';
  }

  private isTejNoEligibleInvoicesError(message: string): boolean {
    const t = message.trim().replace(/\.\s*$/, '');
    return t === TejExportComponent.tejNoEligibleInvoicesApiMessage;
  }

  private showNoEligibleInvoicesModal(): void {
    this.confirmation.alert({
      header: 'Aucune facture éligible pour cette période',
      message:
        'L’export TEJ inclut uniquement les factures fournisseurs payées avec retenue enregistrée (fournisseur assujetti, montant et type RS), pour la date de solde (mois/année) sélectionnés.\n\n' +
        'Vérifiez le paramétrage fournisseur, le statut Payée et que la date de solde tombe dans la période.',
      icon: 'pi pi-exclamation-triangle',
      size: 'md',
      scrollable: true
    });
  }

  private handleTejExportHttpError(message: string): void {
    if (this.isTejNoEligibleInvoicesError(message)) {
      this.showNoEligibleInvoicesModal();
    } else {
      this.apiError = message;
    }
  }

  loadHistory(): void {
    this.historyLoading = true;
    this.service.getTejExportHistory(50).subscribe({
      next: (res) => {
        this.exportHistory = res.items ?? [];
        this.historyLoading = false;
      },
      error: () => {
        this.exportHistory = [];
        this.historyLoading = false;
      }
    });
  }

  preview(): void {
    if (this.periodHintLoading) return;
    if (this.periodEligibleCount === 0) {
      this.showNoEligibleInvoicesModal();
      return;
    }

    this.previewing = true;
    this.xmlPreview = '';
    this.validationErrors = [];
    this.validationSuccess = false;
    this.apiError = '';

    this.service
      .previewXml(
        {
          exerciceYear: this.exerciceYear,
          month: this.month,
          submissionType: this.submissionType
        },
        { skipGlobalErrorUi: true }
      )
      .subscribe({
        next: (result) => {
          this.xmlPreview = result.xmlContent;
          if (result.validationErrors?.length) {
            this.validationErrors = result.validationErrors;
          } else {
            this.validationSuccess = true;
          }
          this.previewing = false;
        },
        error: (err) => {
          this.handleTejExportHttpError(this.extractHttpMessage(err));
          this.previewing = false;
        }
      });
  }

  generate(): void {
    if (this.periodHintLoading) return;
    if (this.periodEligibleCount === 0) {
      this.showNoEligibleInvoicesModal();
      return;
    }

    this.generating = true;
    this.validationErrors = [];
    this.validationSuccess = false;
    this.apiError = '';

    this.service
      .generateXml(
        {
          exerciceYear: this.exerciceYear,
          month: this.month,
          submissionType: this.submissionType
        },
        { skipGlobalErrorUi: true }
      )
      .subscribe({
        next: (blob) => {
          const url = URL.createObjectURL(blob);
          const a = document.createElement('a');
          a.href = url;
          a.download = `DeclarationsRS_${this.exerciceYear}_${String(this.month).padStart(2, '0')}.xml`;
          a.click();
          URL.revokeObjectURL(url);
          this.generating = false;
          this.loadHistory();
        },
        error: (err) => {
          this.handleTejExportHttpError(this.extractHttpMessage(err));
          this.generating = false;
        }
      });
  }

  copyXml(): void {
    void navigator.clipboard.writeText(this.xmlPreview);
  }
}
