import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { TableModule } from 'primeng/table';
import { PageHeaderComponent } from '@shared/components/page-header/page-header.component';
import { ButtonComponent } from '@shared/components/button/button.component';
import { ToastService } from '@core/services/toast.service';
import { AccountingStatusBannerComponent } from '../shared/accounting-status-banner.component';
import { AccountingFilterBarComponent } from '../shared/accounting-filter-bar.component';
import { AccountingCorrectionBannerComponent } from '../shared/accounting-correction-banner.component';
import { AccountingExportMenuComponent } from '../shared/accounting-export-menu.component';
import { AccountingExportFormat, downloadBlob, exportExtension } from '../shared/accounting-download.util';
import { AccountingService, JournalSearchRowDto } from '../services/accounting.service';
import {
  AutoAssociationResultDto,
  BankLineAssociationDto,
  BankLineAssociationStatus,
  BankReconciliationService,
  BankReconciliationStatementDto,
  BankReconciliationSummaryDto,
  BankStatementDto,
  BankStatementExtractionMethod,
  BankStatementFileFormat,
  BankStatementFilePreviewDto,
  BankStatementLineDto,
  ReconcilePairRequest,
  normalizeExtractionMethod
} from '../services/bank-reconciliation.service';
import { BankAccountDto, BankAccountService } from '@core/services/bank-account.service';
import { formatLocalDate, parseLocalDateString } from '../shared/accounting-date-utils';

interface WizardStepDef {
  index: 1 | 2 | 3 | 4;
  label: string;
  hint: string;
}

@Component({
  selector: 'app-bank-reconciliation',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    TableModule,
    PageHeaderComponent,
    ButtonComponent,
    AccountingStatusBannerComponent,
    AccountingFilterBarComponent,
    AccountingExportMenuComponent,
    AccountingCorrectionBannerComponent
  ],
  template: `
    <app-page-header
      title="Rapprochement bancaire"
      subtitle="Assistant d'importation de relevé et de rapprochement — Import, association automatique, rapprochement manuel, résultat" />

    <app-accounting-correction-banner />

    <app-accounting-status-banner variant="error" [message]="error() ?? ''" />

    <!-- ── Progression du wizard ─────────────────────────────────────────── -->
    @if (wizardActive()) {
      <div class="card br-card br-stepper-card">
        <ol class="br-stepper" role="list">
          @for (st of steps; track st.index) {
            <li class="br-step"
              [class.br-step-current]="step() === st.index"
              [class.br-step-done]="step() > st.index">
              <span class="br-step-badge">
                @if (step() > st.index) { <i class="pi pi-check"></i> } @else { {{ st.index }} }
              </span>
              <span class="br-step-text">
                <span class="br-step-label">{{ st.label }}</span>
                <span class="br-step-hint">{{ st.hint }}</span>
              </span>
            </li>
          }
        </ol>
        <div class="br-stepper-actions">
          <app-button variant="secondary" icon="pi pi-times" type="button" (click)="exitWizard()" [disabled]="busy() || applying() || autoAssociating()">
            Quitter l'assistant
          </app-button>
        </div>
      </div>
    }

    <!-- ── Liste des relevés (accueil) ───────────────────────────────────── -->
    @if (!wizardActive()) {
      <div class="card br-card">
        <app-accounting-filter-bar ariaLabel="Filtres des relevés bancaires">
          <div accountingFilterFields class="br-toolbar-fields">
            <div class="form-field">
              <label class="field-label" for="br-account">Compte bancaire</label>
              <input id="br-account" class="br-input" [(ngModel)]="accountFilter" placeholder="N° de compte…" [disabled]="loadingList()" />
            </div>
            <div class="form-field">
              <label class="field-label" for="br-from">Du</label>
              <input id="br-from" type="date" class="br-input" [(ngModel)]="fromStr" [disabled]="loadingList()" />
            </div>
            <div class="form-field">
              <label class="field-label" for="br-to">Au</label>
              <input id="br-to" type="date" class="br-input" [(ngModel)]="toStr" [disabled]="loadingList()" />
            </div>
          </div>
          <div accountingFilterActions class="br-toolbar-actions">
            <app-button variant="secondary" icon="pi pi-search" type="button" (click)="loadStatements()" [disabled]="loadingList()">
              Rechercher
            </app-button>
            <app-button variant="primary" icon="pi pi-upload" type="button" (click)="startImport()" [disabled]="loadingList()">
              Importer un relevé
            </app-button>
          </div>
        </app-accounting-filter-bar>

        <p-table [value]="statements()" [loading]="loadingList()" [paginator]="statements().length > 10" [rows]="10"
          styleClass="p-datatable-sm accounting-datatable" [rowHover]="true">
          <ng-template pTemplate="header">
            <tr>
              <th scope="col">Banque</th>
              <th scope="col">Compte</th>
              <th scope="col">Période</th>
              <th scope="col" class="br-amt">Solde initial</th>
              <th scope="col" class="br-amt">Solde final</th>
              <th scope="col">Avancement</th>
              <th scope="col"></th>
            </tr>
          </ng-template>
          <ng-template pTemplate="body" let-s>
            <tr>
              <td data-label="Banque">{{ s.bankName }}</td>
              <td data-label="Compte" class="br-mono">{{ s.accountNumber }}</td>
              <td data-label="Période">{{ s.periodStart | date : 'shortDate' }} → {{ s.periodEnd | date : 'shortDate' }}</td>
              <td data-label="Solde initial" class="br-amt">{{ s.openingBalance | number : '1.3-3' }}</td>
              <td data-label="Solde final" class="br-amt">{{ s.closingBalance | number : '1.3-3' }}</td>
              <td data-label="Avancement">
                <span class="br-badge" [class.br-badge-done]="reconciledCount(s) === s.lines.length" [class.br-badge-partial]="reconciledCount(s) < s.lines.length">
                  {{ reconciledCount(s) }}/{{ s.lines.length }} rapprochée(s)
                </span>
              </td>
              <td>
                <button type="button" class="br-link-btn" (click)="openStatement(s.id)">Ouvrir l'assistant</button>
                <button type="button" class="br-link-btn" (click)="openReconciliationStatement(s.id)">État de rapprochement</button>
              </td>
            </tr>
          </ng-template>
          <ng-template pTemplate="emptymessage">
            <tr>
              <td colspan="7" class="br-empty">
                <p class="br-empty-title">Aucun relevé bancaire</p>
                <p class="br-empty-hint">Importez votre premier relevé (CSV, Excel, OFX, MT940 ou PDF) pour démarrer le rapprochement.</p>
              </td>
            </tr>
          </ng-template>
        </p-table>
      </div>

      <!-- ── État de rapprochement (édition imprimable) ─────────────────── -->
      @if (reconStatement(); as rs) {
        <div class="card br-card br-recon-card">
          <div class="br-detail-head">
            <h2 class="br-section-title">État de rapprochement — {{ rs.bankName }} · {{ rs.accountNumber }}</h2>
            <div class="br-recon-actions">
              <app-accounting-export-menu
                [disabled]="exportingRecon()"
                (exportFormat)="onExportReconciliation($event)" />
              <button type="button" class="br-link-btn" (click)="closeReconciliationStatement()">Fermer</button>
            </div>
          </div>
          <p class="br-help">
            Compte banque {{ rs.chartOfAccountNumber }} · période {{ rs.periodStart | date : 'shortDate' }}
            → {{ rs.periodEnd | date : 'shortDate' }}
          </p>

          <div class="br-recon-balances">
            <div><span class="br-recon-lbl">Solde comptable</span><span class="br-recon-val">{{ rs.accountingBalance | number : '1.3-3' }}</span></div>
            <div><span class="br-recon-lbl">Solde relevé</span><span class="br-recon-val">{{ rs.statementClosingBalance | number : '1.3-3' }}</span></div>
          </div>

          <h3 class="br-recon-sub">Écritures non pointées (chèques émis non débités, remises non créditées)</h3>
          @if (rs.unreconciledBookItems.length === 0) {
            <p class="br-help">Aucune.</p>
          } @else {
            <p-table [value]="rs.unreconciledBookItems" styleClass="p-datatable-sm accounting-datatable" [rowHover]="true">
              <ng-template pTemplate="header">
                <tr><th scope="col">Date</th><th scope="col">Référence</th><th scope="col">Libellé</th><th scope="col" class="br-amt">Débit</th><th scope="col" class="br-amt">Crédit</th></tr>
              </ng-template>
              <ng-template pTemplate="body" let-i>
                <tr>
                  <td data-label="Date">{{ i.date | date : 'shortDate' }}</td>
                  <td data-label="Référence" class="br-mono">{{ i.reference }}</td>
                  <td data-label="Libellé">{{ i.label }}</td>
                  <td data-label="Débit" class="br-amt">{{ i.debit | number : '1.3-3' }}</td>
                  <td data-label="Crédit" class="br-amt">{{ i.credit | number : '1.3-3' }}</td>
                </tr>
              </ng-template>
            </p-table>
          }

          <h3 class="br-recon-sub">Opérations du relevé non comptabilisées (frais, agios)</h3>
          @if (rs.unreconciledStatementItems.length === 0) {
            <p class="br-help">Aucune.</p>
          } @else {
            <p-table [value]="rs.unreconciledStatementItems" styleClass="p-datatable-sm accounting-datatable" [rowHover]="true">
              <ng-template pTemplate="header">
                <tr><th scope="col">Date</th><th scope="col">Référence</th><th scope="col">Libellé</th><th scope="col" class="br-amt">Débit</th><th scope="col" class="br-amt">Crédit</th></tr>
              </ng-template>
              <ng-template pTemplate="body" let-i>
                <tr>
                  <td data-label="Date">{{ i.date | date : 'shortDate' }}</td>
                  <td data-label="Référence" class="br-mono">{{ i.reference }}</td>
                  <td data-label="Libellé">{{ i.label }}</td>
                  <td data-label="Débit" class="br-amt">{{ i.debit | number : '1.3-3' }}</td>
                  <td data-label="Crédit" class="br-amt">{{ i.credit | number : '1.3-3' }}</td>
                </tr>
              </ng-template>
            </p-table>
          }

          <div class="br-recon-balances br-recon-adjusted">
            <div><span class="br-recon-lbl">Solde relevé corrigé</span><span class="br-recon-val">{{ rs.adjustedStatementBalance | number : '1.3-3' }}</span></div>
            <div><span class="br-recon-lbl">Solde comptable corrigé</span><span class="br-recon-val">{{ rs.adjustedAccountingBalance | number : '1.3-3' }}</span></div>
          </div>
          <p class="br-recon-diff" [class.br-recon-ok]="rs.isReconciled" [class.br-recon-ko]="!rs.isReconciled">
            Écart : {{ rs.difference | number : '1.3-3' }}
            <span>{{ rs.isReconciled ? '— rapproché' : '— à justifier' }}</span>
          </p>
        </div>
      } @else if (reconError()) {
        <div class="card br-card">
          <app-accounting-status-banner variant="warning" [message]="reconError() ?? ''" />
        </div>
      }
    }

    <!-- ══ ÉTAPE 1 — Import du relevé ═════════════════════════════════════ -->
    @if (wizardActive() && step() === 1) {
      <div class="card br-card">
        <h2 class="br-section-title">1 · Import du relevé</h2>
        <div class="br-import-fields">
          <div class="form-field">
            <label class="field-label" for="br-bank-acc">Compte bancaire</label>
            <select id="br-bank-acc" class="br-input" [(ngModel)]="selectedBankAccountId" [disabled]="busy() || loadingBankAccounts()">
              <option value="">— Sélectionner —</option>
              @for (ba of bankAccounts(); track ba.id) {
                <option [ngValue]="ba.id">{{ ba.bankName }} — {{ formatRib(ba.rib) }} {{ ba.chartOfAccountNumber ? ('(' + ba.chartOfAccountNumber + ')') : '' }}</option>
              }
            </select>
            @if (preview()?.detectedRib && !preview()?.matchedBankAccountId) {
              <small class="br-help br-rib-hint">
                RIB détecté {{ formatRib(preview()!.detectedRib!) }} — créez un compte bancaire ou sélectionnez-le manuellement.
              </small>
            }
          </div>
          <div class="form-field">
            <label class="field-label" for="br-format">Format</label>
            <select id="br-format" class="br-input" [(ngModel)]="fileFormat" [disabled]="busy()">
              <option [ngValue]="0">CSV (délimité)</option>
              <option [ngValue]="1">Excel (.xlsx)</option>
              <option [ngValue]="4">OFX (Money/banques)</option>
              <option [ngValue]="5">MT940 (SWIFT)</option>
              <option [ngValue]="2">PDF (relevé bancaire)</option>
              <option [ngValue]="3">Image scannée</option>
            </select>
          </div>
          <div class="form-field br-field-file">
            <label class="field-label" for="br-file">Fichier</label>
            <input id="br-file" type="file" class="br-input" (change)="onFileSelected($event)" [disabled]="busy()"
              accept=".csv,.txt,.tsv,.xlsx,.ofx,.qfx,.sta,.mt940,.txt,.pdf,.jpg,.jpeg,.png" />
          </div>
          <div class="br-import-actions">
            <app-button variant="secondary" icon="pi pi-search" type="button"
              (click)="runPreview()" [disabled]="!selectedFile() || busy()">
              {{ previewing() ? 'Analyse…' : "Tester l'importation" }}
            </app-button>
          </div>
        </div>

        <!-- Options d'import (façon Sage) -->
        <h3 class="br-section-sub">Options d'import</h3>
        <div class="br-options">
          <label class="br-check">
            <input type="checkbox" [(ngModel)]="optSkipAlreadyImported" [disabled]="busy()" />
            <span>Ignorer les écritures déjà importées</span>
          </label>
          <label class="br-check">
            <input type="checkbox" [(ngModel)]="optImportBalances" [disabled]="busy()" />
            <span>Importer les soldes</span>
          </label>
          <label class="br-check br-check-disabled" title="Non applicable : les écritures 512↔contrepartie ne créent pas de tiers.">
            <input type="checkbox" disabled />
            <span>Créer les tiers inexistants</span>
          </label>
        </div>

        <!-- Paramètres de comptabilisation -->
        <h3 class="br-section-sub">Paramètres de comptabilisation</h3>
        <div class="br-import-fields">
          <div class="form-field">
            <label class="field-label" for="br-journal">Journal à utiliser</label>
            <input id="br-journal" class="br-input br-input-narrow" [(ngModel)]="defaultJournalCode" placeholder="BQ" [disabled]="busy()" />
          </div>
          <div class="form-field">
            <label class="field-label" for="br-counterparty">Contrepartie par défaut</label>
            <input id="br-counterparty" class="br-input" [(ngModel)]="defaultCounterparty" placeholder="Ex. 6580000" [disabled]="busy()" />
            <small class="br-help">Utilisée pour comptabiliser les lignes non associées (étapes 2 et 3).</small>
          </div>
        </div>

        <p class="br-help">
          Colonnes attendues pour CSV/Excel (1<sup>re</sup> ligne = en-têtes) : <code>date, libelle, [reference]</code> puis
          <code>debit/credit</code> ou une colonne <code>montant</code> signée. OFX/MT940 sont lus automatiquement.
        </p>

        @if (preview(); as p) {
          @if (p.extractionMethod !== undefined && p.extractionMethod !== null && p.extractionMethod !== 0) {
            <p class="br-help br-extract-banner">
              Extraction : {{ extractionLabel(p.extractionMethod) }}
              @if (p.confidenceScore !== undefined && p.confidenceScore !== null) { — confiance {{ p.confidenceScore }} % }
              @if (p.matchedChartOfAccountNumber) { — compte comptable {{ p.matchedChartOfAccountNumber }} }
            </p>
          }
          <div class="br-summary">
            <div class="br-kpi"><span class="br-kpi-label">Lignes valides</span><span class="br-kpi-value br-ok">{{ p.validLines }}</span></div>
            <div class="br-kpi"><span class="br-kpi-label">Anomalies</span><span class="br-kpi-value" [class.br-err]="p.issues.length > 0">{{ p.issues.length }}</span></div>
            <div class="br-kpi"><span class="br-kpi-label">Total débits</span><span class="br-kpi-value">{{ p.totalDebit | number : '1.3-3' }}</span></div>
            <div class="br-kpi"><span class="br-kpi-label">Total crédits</span><span class="br-kpi-value">{{ p.totalCredit | number : '1.3-3' }}</span></div>
          </div>

          @if (p.issues.length > 0) {
            <div class="br-issues">
              <h3 class="br-issues-title">{{ p.issues.length }} anomalie(s)</h3>
              <ul class="br-issues-list">
                @for (issue of p.issues.slice(0, 50); track $index) {
                  <li><span class="br-issue-ref">{{ issue.ref }}</span> {{ issue.message }}</li>
                }
              </ul>
            </div>
          }

          @if (p.canImport) {
            <!-- Aperçu des lignes -->
            <h3 class="br-section-sub">Aperçu des mouvements</h3>
            <p-table [value]="p.lines.slice(0, 100)" styleClass="p-datatable-sm accounting-datatable" [rowHover]="true">
              <ng-template pTemplate="header">
                <tr>
                  <th scope="col">Date op.</th>
                  <th scope="col">Référence</th>
                  <th scope="col">Libellé</th>
                  <th scope="col" class="br-amt">Débit</th>
                  <th scope="col" class="br-amt">Crédit</th>
                </tr>
              </ng-template>
              <ng-template pTemplate="body" let-l>
                <tr>
                  <td>{{ l.transactionDate | date : 'shortDate' }}</td>
                  <td class="br-mono">{{ l.reference }}</td>
                  <td>{{ l.description }}</td>
                  <td class="br-amt">{{ l.isDebit ? (l.amount | number : '1.3-3') : '' }}</td>
                  <td class="br-amt">{{ !l.isDebit ? (l.amount | number : '1.3-3') : '' }}</td>
                </tr>
              </ng-template>
            </p-table>

            <h3 class="br-section-sub">Informations du relevé</h3>
            <div class="br-import-fields">
              <div class="form-field">
                <label class="field-label" for="br-bank">Banque</label>
                <input id="br-bank" class="br-input" [(ngModel)]="meta.bankName" placeholder="Ex. BIAT" [disabled]="busy()" />
              </div>
              <div class="form-field">
                <label class="field-label" for="br-acc">N° de compte</label>
                <input id="br-acc" class="br-input" [(ngModel)]="meta.accountNumber" placeholder="Ex. 08 123 456789" [disabled]="busy()" />
              </div>
              <div class="form-field">
                <label class="field-label" for="br-pstart">Début de période</label>
                <input id="br-pstart" type="date" class="br-input" [(ngModel)]="meta.periodStart" [disabled]="busy()" />
              </div>
              <div class="form-field">
                <label class="field-label" for="br-pend">Fin de période</label>
                <input id="br-pend" type="date" class="br-input" [(ngModel)]="meta.periodEnd" [disabled]="busy()" />
              </div>
              <div class="form-field">
                <label class="field-label" for="br-open">Solde initial</label>
                <input id="br-open" type="text" inputmode="decimal" class="br-input" [(ngModel)]="meta.openingBalance" [disabled]="busy()" />
              </div>
              <div class="form-field">
                <label class="field-label" for="br-close">Solde final</label>
                <input id="br-close" type="text" inputmode="decimal" class="br-input" [(ngModel)]="meta.closingBalance" [disabled]="busy()" />
              </div>
            </div>
            <div class="br-commit-bar">
              <span class="br-commit-hint">{{ p.validLines }} ligne(s) prête(s) à être importée(s).</span>
              <app-button variant="primary" icon="pi pi-arrow-right" type="button"
                (click)="runImport()" [disabled]="!canSubmitImport() || busy()">
                {{ importing() ? 'Import…' : 'Importer et continuer' }}
              </app-button>
            </div>
          } @else {
            <p class="br-commit-hint br-err">Corrigez les anomalies bloquantes avant d'importer.</p>
          }
        }
      </div>
    }

    <!-- ══ ÉTAPE 2 — Association automatique ═════════════════════════════ -->
    @if (wizardActive() && step() === 2 && selected(); as s) {
      <div class="br-two-col">
        <div class="card br-card br-col-main">
          <div class="br-detail-head">
            <h2 class="br-section-title">2 · Association automatique — {{ s.bankName }} · {{ s.accountNumber }}</h2>
            <div class="br-head-actions">
              <app-button variant="secondary" icon="pi pi-refresh" type="button" (click)="runAutoAssociate()" [disabled]="autoAssociating() || applying()">
                Relancer l'association
              </app-button>
              <app-button variant="primary" icon="pi pi-check-circle" type="button" (click)="applySelectedAssociations()"
                [disabled]="applying() || autoAssociating() || selectedAssociatedCount() === 0">
                {{ applying() ? 'Rapprochement…' : 'Rapprocher tout ce qui est associé (' + selectedAssociatedCount() + ')' }}
              </app-button>
            </div>
          </div>
          <p class="br-help">
            Les correspondances certaines sont pré-cochées « Associé » (non enregistrées tant que vous ne cliquez pas sur « Rapprocher »).
            Les lignes à plusieurs candidats restent « À rapprocher », les lignes sans écriture « Non associé ».
          </p>

          <p-table [value]="associations()" [loading]="autoAssociating()" styleClass="p-datatable-sm accounting-datatable" [rowHover]="true">
            <ng-template pTemplate="header">
              <tr>
                <th scope="col" class="br-check-col"></th>
                <th scope="col">Date</th>
                <th scope="col">Libellé</th>
                <th scope="col" class="br-amt">Débit</th>
                <th scope="col" class="br-amt">Crédit</th>
                <th scope="col">Statut</th>
                <th scope="col">Écriture proposée</th>
                <th scope="col">Actions</th>
              </tr>
            </ng-template>
            <ng-template pTemplate="body" let-a>
              <tr>
                <td class="br-check-col">
                  @if (a.status === 2) {
                    <input type="checkbox" [checked]="isChecked(a)" (change)="toggleChecked(a)" [disabled]="applying()" />
                  }
                </td>
                <td>{{ a.transactionDate | date : 'shortDate' }}</td>
                <td>{{ a.description }}</td>
                <td class="br-amt">{{ a.isDebit ? (a.amount | number : '1.3-3') : '' }}</td>
                <td class="br-amt">{{ !a.isDebit ? (a.amount | number : '1.3-3') : '' }}</td>
                <td>
                  <span class="br-badge"
                    [class.br-badge-done]="a.status === 2"
                    [class.br-badge-partial]="a.status === 1"
                    [class.br-badge-none]="a.status === 0">
                    {{ associationStatusLabel(a.status) }}
                  </span>
                </td>
                <td class="br-proposed">
                  @if (a.status === 2) { {{ a.proposedEntryRef }} }
                  @else if (a.status === 1) { <span class="br-muted">{{ a.candidateCount }} candidat(s)</span> }
                  @else { <span class="br-muted">—</span> }
                </td>
                <td data-label="Actions">
                  @if (a.status !== 2) {
                    <button type="button" class="br-link-btn" (click)="reconcileFromStep(a)" [disabled]="applying()">Rapprocher…</button>
                    <button type="button" class="br-link-btn" (click)="openCreateEntry(a.bankStatementLineId, a.description, a.isDebit, a.amount)" [disabled]="applying()">Créer écriture</button>
                  }
                </td>
              </tr>
            </ng-template>
            <ng-template pTemplate="emptymessage">
              <tr><td colspan="8" class="br-empty">Aucune ligne à associer.</td></tr>
            </ng-template>
          </p-table>

          <div class="br-commit-bar">
            <app-button variant="secondary" icon="pi pi-arrow-left" type="button" (click)="goStep(1)" [disabled]="applying() || autoAssociating()">
              Retour
            </app-button>
            <app-button variant="primary" icon="pi pi-arrow-right" type="button" (click)="goToManual()" [disabled]="applying() || autoAssociating()">
              Rapprochement manuel
            </app-button>
          </div>
        </div>

        <!-- Récapitulatif -->
        <aside class="card br-card br-col-side">
          <h3 class="br-section-title">Récapitulatif</h3>
          @if (summary(); as sum) {
            <dl class="br-recap">
              <div class="br-recap-row"><dt>Lignes importées</dt><dd>{{ sum.importedCount }}</dd></div>
              <div class="br-recap-row"><dt>Associées auto</dt><dd class="br-ok">{{ sum.autoMatchedCount }}</dd></div>
              <div class="br-recap-row"><dt>À rapprocher</dt><dd class="br-warn">{{ sum.toReconcileCount }}</dd></div>
              <div class="br-recap-row"><dt>Non associées</dt><dd class="br-err">{{ sum.notAssociatedCount }}</dd></div>
              <div class="br-recap-sep"></div>
              <div class="br-recap-row"><dt>Total crédits</dt><dd>{{ sum.totalCredit | number : '1.3-3' }}</dd></div>
              <div class="br-recap-row"><dt>Total débits</dt><dd>{{ sum.totalDebit | number : '1.3-3' }}</dd></div>
              <div class="br-recap-row"><dt>Solde du relevé</dt><dd class="br-strong">{{ sum.statementBalance | number : '1.3-3' }}</dd></div>
            </dl>
          } @else {
            <p class="br-help">Lancez l'association pour afficher le récapitulatif.</p>
          }
          @if ((s.skippedDuplicateCount ?? 0) > 0) {
            <p class="br-help br-dup-hint">{{ s.skippedDuplicateCount }} ligne(s) déjà importée(s) ont été ignorées.</p>
          }
        </aside>
      </div>
    }

    <!-- ══ ÉTAPE 3 — Rapprochement manuel ═══════════════════════════════ -->
    @if (wizardActive() && step() === 3 && selected(); as s) {
      <div class="card br-card">
        <div class="br-detail-head">
          <h2 class="br-section-title">3 · Rapprochement manuel — {{ s.bankName }} · {{ s.accountNumber }}</h2>
          <span class="br-detail-period">{{ unreconciledLines(s).length }} ligne(s) restante(s)</span>
        </div>
        <p class="br-help">Compte banque {{ reconcileAccountLabel() }}. Choisissez une écriture candidate ou comptabilisez directement la ligne.</p>

        <p-table [value]="s.lines" [loading]="loadingDetail()" styleClass="p-datatable-sm accounting-datatable" [rowHover]="true">
          <ng-template pTemplate="header">
            <tr>
              <th scope="col">Date</th>
              <th scope="col">Référence</th>
              <th scope="col">Libellé</th>
              <th scope="col" class="br-amt">Débit</th>
              <th scope="col" class="br-amt">Crédit</th>
              <th scope="col">Statut</th>
              <th scope="col">Actions</th>
            </tr>
          </ng-template>
          <ng-template pTemplate="body" let-l>
            <tr [class.br-row-reconciled]="l.isReconciled">
              <td>{{ l.transactionDate | date : 'shortDate' }}</td>
              <td class="br-mono">{{ l.reference }}</td>
              <td>{{ l.description }}</td>
              <td class="br-amt">{{ l.isDebit ? (l.amount | number : '1.3-3') : '' }}</td>
              <td class="br-amt">{{ !l.isDebit ? (l.amount | number : '1.3-3') : '' }}</td>
              <td>
                <span class="br-badge" [class.br-badge-done]="l.isReconciled" [class.br-badge-partial]="!l.isReconciled">
                  {{ l.isReconciled ? 'Rapprochée' : 'À rapprocher' }}
                </span>
              </td>
              <td data-label="Actions">
                @if (!l.isReconciled) {
                  <button type="button" class="br-link-btn" (click)="openCandidates(l)" [disabled]="reconciling()">Rechercher</button>
                  <button type="button" class="br-link-btn" (click)="openCreateEntry(l.id, l.description, l.isDebit, l.amount)" [disabled]="reconciling()">Créer écriture</button>
                } @else {
                  <button type="button" class="br-link-btn br-link-danger" (click)="unreconcile(l)" [disabled]="reconciling()">Dé-rapprocher</button>
                }
              </td>
            </tr>
          </ng-template>
          <ng-template pTemplate="emptymessage">
            <tr><td colspan="7" class="br-empty">Aucune ligne dans ce relevé.</td></tr>
          </ng-template>
        </p-table>

        <div class="br-commit-bar">
          <app-button variant="secondary" icon="pi pi-arrow-left" type="button" (click)="goStep(2)" [disabled]="reconciling()">Retour</app-button>
          <app-button variant="primary" icon="pi pi-arrow-right" type="button" (click)="goStep(4)" [disabled]="reconciling()">Voir le résultat</app-button>
        </div>
      </div>

      <!-- Candidats de rapprochement -->
      @if (activeLine(); as line) {
        <div class="card br-card">
          <div class="br-detail-head">
            <h2 class="br-section-title">
              Candidats pour « {{ line.description }} » — {{ line.amount | number : '1.3-3' }}
              {{ line.isDebit ? '(décaissement)' : '(encaissement)' }}
            </h2>
            <button type="button" class="br-link-btn" (click)="closeCandidates()">Fermer</button>
          </div>
          <p class="br-help">Lignes du compte banque {{ reconcileAccountLabel() }} au même montant, à ±10 jours. Les brouillons sont exclus.</p>
          <p-table [value]="candidates()" [loading]="loadingCandidates()" styleClass="p-datatable-sm accounting-datatable" [rowHover]="true">
            <ng-template pTemplate="header">
              <tr>
                <th scope="col">Date</th><th scope="col">Journal</th><th scope="col">N°</th>
                <th scope="col">Compte</th><th scope="col">Libellé</th>
                <th scope="col" class="br-amt">Débit</th><th scope="col" class="br-amt">Crédit</th>
                <th scope="col"></th>
              </tr>
            </ng-template>
            <ng-template pTemplate="body" let-c>
              <tr>
                <td>{{ c.entryDate | date : 'shortDate' }}</td>
                <td>{{ c.journalCode }}</td>
                <td>{{ c.entryNumber }}</td>
                <td class="br-mono">{{ c.accountNumber }}</td>
                <td>{{ c.label }}</td>
                <td class="br-amt">{{ c.debit | number : '1.3-3' }}</td>
                <td class="br-amt">{{ c.credit | number : '1.3-3' }}</td>
                <td><button type="button" class="br-link-btn" (click)="reconcile(line, c)" [disabled]="reconciling()">Choisir</button></td>
              </tr>
            </ng-template>
            <ng-template pTemplate="emptymessage">
              <tr><td colspan="8" class="br-empty">Aucune écriture candidate au même montant sur la fenêtre de dates.</td></tr>
            </ng-template>
          </p-table>
        </div>
      }
    }

    <!-- ══ ÉTAPE 4 — Résultat ════════════════════════════════════════════ -->
    @if (wizardActive() && step() === 4 && selected(); as s) {
      <div class="card br-card">
        <h2 class="br-section-title">4 · Résultat — {{ s.bankName }} · {{ s.accountNumber }}</h2>
        <div class="br-summary">
          <div class="br-kpi"><span class="br-kpi-label">Lignes</span><span class="br-kpi-value">{{ s.lines.length }}</span></div>
          <div class="br-kpi"><span class="br-kpi-label">Rapprochées</span><span class="br-kpi-value br-ok">{{ reconciledCount(s) }}</span></div>
          <div class="br-kpi"><span class="br-kpi-label">Restantes</span><span class="br-kpi-value" [class.br-err]="unreconciledLines(s).length > 0">{{ unreconciledLines(s).length }}</span></div>
        </div>

        @if (unreconciledLines(s).length > 0) {
          <h3 class="br-section-sub">Lignes non rapprochées</h3>
          <ul class="br-result-list">
            @for (l of unreconciledLines(s); track l.id) {
              <li>
                <span class="br-mono">{{ l.transactionDate | date : 'shortDate' }}</span> · {{ l.description }} —
                <strong>{{ l.amount | number : '1.3-3' }}</strong> {{ l.isDebit ? '(débit)' : '(crédit)' }}
              </li>
            }
          </ul>
        } @else {
          <p class="br-help br-ok">Toutes les lignes de ce relevé sont rapprochées. 🎉</p>
        }

        <div class="br-commit-bar">
          <app-button variant="secondary" icon="pi pi-arrow-left" type="button" (click)="goStep(3)">Retour</app-button>
          <app-button variant="primary" icon="pi pi-check" type="button" (click)="finishWizard()">Terminer</app-button>
        </div>
      </div>
    }

    <!-- ── Panneau « Créer une écriture » (étapes 2 et 3) ───────────────── -->
    @if (createLine(); as cl) {
      <div class="card br-card br-create-panel">
        <div class="br-detail-head">
          <h2 class="br-section-title">Comptabiliser « {{ cl.description }} » — {{ cl.amount | number : '1.3-3' }} {{ cl.isDebit ? '(décaissement)' : '(encaissement)' }}</h2>
          <button type="button" class="br-link-btn" (click)="cancelCreateEntry()">Fermer</button>
        </div>
        <div class="br-import-fields">
          <div class="form-field">
            <label class="field-label" for="ce-journal">Journal</label>
            <input id="ce-journal" class="br-input br-input-narrow" [(ngModel)]="createForm.journalCode" placeholder="BQ" [disabled]="creatingEntry()" />
          </div>
          <div class="form-field">
            <label class="field-label" for="ce-account">Compte de contrepartie</label>
            <input id="ce-account" class="br-input" [(ngModel)]="createForm.counterpartyAccount" placeholder="Ex. 6580000" [disabled]="creatingEntry()" />
          </div>
          <div class="form-field br-field-file">
            <label class="field-label" for="ce-label">Libellé (facultatif)</label>
            <input id="ce-label" class="br-input" [(ngModel)]="createForm.label" [disabled]="creatingEntry()" />
          </div>
        </div>
        <p class="br-help">
          Écriture équilibrée compte banque {{ reconcileAccountLabel() }} ↔ contrepartie, puis rapprochement automatique de la ligne
          (selon le paramétrage brouillard).
        </p>
        <div class="br-commit-bar">
          <app-button variant="primary" icon="pi pi-save" type="button" (click)="confirmCreateEntry()"
            [disabled]="creatingEntry() || !createForm.counterpartyAccount.trim() || !createForm.journalCode.trim()">
            {{ creatingEntry() ? 'Comptabilisation…' : "Comptabiliser l'écriture" }}
          </app-button>
        </div>
      </div>
    }
  `,
  styles: `
    .br-card {
      padding: var(--spacing-5);
      border-radius: var(--radius-lg);
      box-shadow: var(--shadow-sm, 0 1px 3px rgba(15, 23, 42, 0.08));
      margin-bottom: var(--spacing-4);
    }
    .br-toolbar-fields { display: flex; flex-wrap: wrap; gap: var(--spacing-3); flex: 1 1 280px; }
    .br-toolbar-actions { display: flex; flex-wrap: wrap; gap: var(--spacing-3); align-items: flex-end; }
    .form-field { display: flex; flex-direction: column; gap: var(--spacing-1); min-width: 0; }
    .field-label { font-size: var(--font-size-sm); font-weight: var(--font-weight-semibold); color: var(--color-text-primary); margin: 0; }
    .br-input {
      padding: var(--spacing-2) var(--spacing-3);
      border: 1px solid var(--color-border-default);
      border-radius: var(--radius-md);
      background: var(--color-background-elevated);
      color: var(--color-text-primary);
      font-size: var(--font-size-sm);
      min-height: 2.5rem;
    }
    .br-input-narrow { max-width: 8rem; }
    .br-import-fields { display: flex; flex-wrap: wrap; align-items: flex-end; gap: var(--spacing-4); }
    .br-field-file { flex: 1 1 260px; }
    .br-import-actions { display: flex; align-items: flex-end; }
    .br-help { margin: var(--spacing-3) 0 0; font-size: var(--font-size-sm); color: var(--color-text-secondary); }
    .br-help code {
      font-family: ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, monospace;
      background: var(--color-background-subtle); padding: 0 0.25rem; border-radius: var(--radius-sm, 4px);
    }
    .br-dup-hint { color: var(--color-warning-700, #a16207); }

    /* Stepper */
    .br-stepper-card { display: flex; align-items: center; justify-content: space-between; gap: var(--spacing-4); flex-wrap: wrap; }
    .br-stepper { list-style: none; margin: 0; padding: 0; display: flex; flex-wrap: wrap; gap: var(--spacing-5); flex: 1 1 auto; }
    .br-step { display: flex; align-items: center; gap: var(--spacing-2); opacity: 0.55; }
    .br-step-current, .br-step-done { opacity: 1; }
    .br-step-badge {
      display: inline-flex; align-items: center; justify-content: center;
      width: 1.75rem; height: 1.75rem; border-radius: 999px;
      background: var(--color-background-subtle); color: var(--color-text-secondary);
      font-weight: var(--font-weight-semibold); font-size: var(--font-size-sm);
      border: 1px solid var(--color-border-default);
    }
    .br-step-current .br-step-badge { background: var(--color-primary-600, #2563eb); color: #fff; border-color: transparent; }
    .br-step-done .br-step-badge { background: var(--color-success-600, #16a34a); color: #fff; border-color: transparent; }
    .br-step-text { display: flex; flex-direction: column; line-height: 1.15; }
    .br-step-label { font-size: var(--font-size-sm); font-weight: var(--font-weight-semibold); }
    .br-step-hint { font-size: var(--font-size-xs); color: var(--color-text-tertiary); }
    .br-stepper-actions { flex: 0 0 auto; }

    .br-two-col { display: grid; grid-template-columns: minmax(0, 1fr) 18rem; gap: var(--spacing-4); align-items: start; }
    @media (max-width: 960px) { .br-two-col { grid-template-columns: 1fr; } }
    .br-col-main, .br-col-side { margin-bottom: 0; }

    .br-section-title { margin: 0 0 var(--spacing-3); font-size: var(--font-size-lg); font-weight: var(--font-weight-semibold); }
    .br-section-sub { margin: var(--spacing-4) 0 var(--spacing-2); font-size: var(--font-size-sm); font-weight: var(--font-weight-semibold); text-transform: uppercase; letter-spacing: 0.04em; color: var(--color-text-tertiary); }
    .br-head-actions { display: flex; flex-wrap: wrap; gap: var(--spacing-2); }
    .br-summary {
      display: flex; flex-wrap: wrap; gap: var(--spacing-5);
      margin-top: var(--spacing-4); padding-bottom: var(--spacing-3);
      border-bottom: 1px solid var(--color-border-subtle);
    }
    .br-kpi { display: flex; flex-direction: column; gap: 2px; }
    .br-kpi-label { font-size: var(--font-size-xs); color: var(--color-text-tertiary); text-transform: uppercase; letter-spacing: 0.04em; }
    .br-kpi-value { font-size: var(--font-size-lg); font-weight: var(--font-weight-semibold); font-variant-numeric: tabular-nums; }
    .br-ok { color: var(--color-success-700, #15803d); }
    .br-warn { color: var(--color-warning-700, #a16207); }
    .br-err { color: var(--color-danger-600, #dc2626); }
    .br-strong { font-weight: var(--font-weight-semibold); }
    .br-muted { color: var(--color-text-tertiary); }

    .br-options { display: flex; flex-wrap: wrap; gap: var(--spacing-4); }
    .br-check { display: inline-flex; align-items: center; gap: var(--spacing-2); font-size: var(--font-size-sm); color: var(--color-text-primary); cursor: pointer; }
    .br-check-disabled { opacity: 0.55; cursor: not-allowed; }

    .br-recap { margin: 0; }
    .br-recap-row { display: flex; justify-content: space-between; gap: var(--spacing-3); padding: var(--spacing-1) 0; font-size: var(--font-size-sm); }
    .br-recap-row dt { color: var(--color-text-secondary); margin: 0; }
    .br-recap-row dd { margin: 0; font-variant-numeric: tabular-nums; font-weight: var(--font-weight-semibold); }
    .br-recap-sep { height: 1px; background: var(--color-border-subtle); margin: var(--spacing-2) 0; }

    .br-issues {
      margin-top: var(--spacing-3); padding: var(--spacing-3) var(--spacing-4);
      border-radius: var(--radius-md);
      background: var(--color-danger-50, #fef2f2);
      border: 1px solid var(--color-danger-200, #fecaca);
    }
    .br-issues-title { margin: 0 0 var(--spacing-2); font-size: var(--font-size-sm); color: var(--color-danger-700, #b91c1c); }
    .br-issues-list { margin: 0; padding-left: var(--spacing-5); font-size: var(--font-size-sm); color: var(--color-text-secondary); }
    .br-issue-ref { font-weight: var(--font-weight-semibold); color: var(--color-text-primary); margin-right: var(--spacing-1); }
    .br-result-list { margin: var(--spacing-2) 0 0; padding-left: var(--spacing-5); font-size: var(--font-size-sm); color: var(--color-text-secondary); }
    .br-result-list li { padding: 2px 0; }

    .br-commit-bar {
      display: flex; align-items: center; justify-content: flex-end; gap: var(--spacing-4);
      margin-top: var(--spacing-4); padding-top: var(--spacing-4);
      border-top: 1px solid var(--color-border-subtle);
    }
    .br-commit-hint { font-size: var(--font-size-sm); color: var(--color-text-secondary); margin-right: auto; }
    .br-amt { text-align: right; font-variant-numeric: tabular-nums; }
    .br-mono { font-family: ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, monospace; }
    .br-check-col { width: 2.5rem; text-align: center; }
    .br-proposed { font-size: var(--font-size-sm); }
    .br-badge {
      display: inline-block; padding: 0.15rem 0.55rem; border-radius: var(--radius-pill, 999px);
      font-size: var(--font-size-xs); font-weight: var(--font-weight-semibold);
    }
    .br-badge-done { background: var(--color-success-100, #dcfce7); color: var(--color-success-700, #15803d); }
    .br-badge-partial { background: var(--color-warning-50, #fffbeb); color: var(--color-warning-700, #a16207); border: 1px solid var(--color-warning-200, #fde68a); }
    .br-badge-none { background: var(--color-danger-50, #fef2f2); color: var(--color-danger-700, #b91c1c); border: 1px solid var(--color-danger-200, #fecaca); }
    .br-link-btn {
      background: none; border: none; padding: 0; margin-right: var(--spacing-3); cursor: pointer;
      color: var(--color-primary-600, #2563eb); font-size: var(--font-size-sm); font-weight: var(--font-weight-semibold);
    }
    .br-link-btn:disabled { opacity: 0.5; cursor: not-allowed; }
    .br-link-danger { color: var(--color-danger-600, #dc2626); }
    .br-row-reconciled { opacity: 0.75; }
    .br-detail-head { display: flex; align-items: baseline; justify-content: space-between; gap: var(--spacing-3); flex-wrap: wrap; }
    .br-detail-period { font-size: var(--font-size-sm); color: var(--color-text-secondary); }
    .br-empty { text-align: center; padding: var(--spacing-6) var(--spacing-4) !important; color: var(--color-text-secondary); }
    .br-empty-title { margin: 0 0 var(--spacing-1); font-weight: var(--font-weight-semibold); color: var(--color-text-primary); }
    .br-empty-hint { margin: 0; font-size: var(--font-size-sm); }
    .br-create-panel { border: 1px solid var(--color-primary-200, #bfdbfe); }
    .br-recon-card { margin-top: var(--spacing-4); }
    .br-recon-actions { display: flex; align-items: center; gap: var(--spacing-3); }
    .br-recon-balances { display: flex; flex-wrap: wrap; gap: var(--spacing-6); margin: var(--spacing-3) 0; }
    .br-recon-balances > div { display: flex; flex-direction: column; }
    .br-recon-lbl { font-size: var(--font-size-sm); color: var(--color-text-secondary); }
    .br-recon-val { font-weight: var(--font-weight-bold); font-variant-numeric: tabular-nums; }
    .br-recon-adjusted { border-top: 1px solid var(--color-border-default); padding-top: var(--spacing-3); margin-top: var(--spacing-4); }
    .br-recon-sub { margin: var(--spacing-4) 0 var(--spacing-2); font-size: var(--font-size-md); font-weight: var(--font-weight-semibold); }
    .br-recon-diff { font-weight: var(--font-weight-bold); font-variant-numeric: tabular-nums; font-size: var(--font-size-lg); }
    .br-recon-diff span { font-size: var(--font-size-sm); font-weight: var(--font-weight-regular); margin-left: var(--spacing-2); }
    .br-recon-ok { color: var(--color-success-600, #16a34a); }
    .br-recon-ko { color: var(--color-danger-600, #dc2626); }
  `
})
export class BankReconciliationComponent implements OnInit {
  private readonly api = inject(BankReconciliationService);
  private readonly accounting = inject(AccountingService);
  private readonly bankAccountsApi = inject(BankAccountService);
  private readonly toast = inject(ToastService);
  private readonly route = inject(ActivatedRoute);

  unmatchedOnly = false;

  readonly steps: WizardStepDef[] = [
    { index: 1, label: 'Import', hint: 'Charger le relevé' },
    { index: 2, label: 'Association auto', hint: 'Propositions + récap' },
    { index: 3, label: 'Rapprochement manuel', hint: 'Lignes restantes' },
    { index: 4, label: 'Résultat', hint: 'Synthèse' }
  ];

  // Wizard
  readonly wizardActive = signal(false);
  readonly step = signal<1 | 2 | 3 | 4>(1);

  readonly bankAccounts = signal<BankAccountDto[]>([]);
  readonly loadingBankAccounts = signal(false);
  selectedBankAccountId = '';
  private lastImportMethod = 0;
  private lastSourceFileName: string | null = null;

  // Options + paramètres d'import
  optSkipAlreadyImported = false;
  optImportBalances = true;
  defaultJournalCode = 'BQ';
  defaultCounterparty = '';

  // Liste
  accountFilter = '';
  fromStr = '';
  toStr = '';
  readonly statements = signal<BankStatementDto[]>([]);
  readonly loadingList = signal(false);

  // Détail
  readonly selected = signal<BankStatementDto | null>(null);
  readonly loadingDetail = signal(false);

  // État de rapprochement (édition imprimable, hors wizard)
  readonly reconStatement = signal<BankReconciliationStatementDto | null>(null);
  readonly reconError = signal<string | null>(null);
  readonly exportingRecon = signal(false);
  private reconStatementId: string | null = null;

  // Import fichier
  fileFormat: BankStatementFileFormat = BankStatementFileFormat.Csv;
  readonly selectedFile = signal<File | null>(null);
  readonly preview = signal<BankStatementFilePreviewDto | null>(null);
  readonly previewing = signal(false);
  readonly importing = signal(false);
  meta = this.emptyMeta();

  // Association automatique
  readonly associations = signal<BankLineAssociationDto[]>([]);
  readonly summary = signal<BankReconciliationSummaryDto | null>(null);
  readonly autoAssociating = signal(false);
  readonly applying = signal(false);
  private readonly checked = signal<Set<string>>(new Set());

  // Rapprochement manuel
  readonly activeLine = signal<BankStatementLineDto | null>(null);
  readonly candidates = signal<JournalSearchRowDto[]>([]);
  readonly loadingCandidates = signal(false);
  readonly reconciling = signal(false);

  // Création d'écriture
  readonly createLine = signal<{ id: string; description: string; isDebit: boolean; amount: number } | null>(null);
  createForm = { journalCode: 'BQ', counterpartyAccount: '', label: '' };
  readonly creatingEntry = signal(false);

  readonly error = signal<string | null>(null);

  readonly canSubmitImport = computed(() => {
    const p = this.preview();
    return !!p?.canImport
      && this.meta.bankName.trim().length > 0
      && this.meta.accountNumber.trim().length > 0
      && this.meta.periodStart.length > 0
      && this.meta.periodEnd.length > 0;
  });

  readonly selectedAssociatedCount = computed(() =>
    this.associations().filter(a => a.status === BankLineAssociationStatus.Associated && this.checked().has(a.bankStatementLineId)).length);

  ngOnInit(): void {
    const qp = this.route.snapshot.queryParamMap;
    const fy = qp.get('fiscalYear');
    if (fy) {
      this.fromStr = `${fy}-01-01`;
      this.toStr = `${fy}-12-31`;
    }
    this.unmatchedOnly = qp.get('unmatchedOnly') === '1';
    this.loadStatements();
    this.loadBankAccounts();
  }

  loadBankAccounts(): void {
    this.loadingBankAccounts.set(true);
    this.bankAccountsApi.list().subscribe({
      next: res => {
        this.loadingBankAccounts.set(false);
        if (res.success && res.data) this.bankAccounts.set(res.data);
      },
      error: () => this.loadingBankAccounts.set(false)
    });
  }

  formatRib(rib: string): string {
    const d = rib.replace(/\D/g, '');
    if (d.length !== 20) return rib;
    return `${d.slice(0, 2)} ${d.slice(2, 5)} ${d.slice(5, 18)} ${d.slice(18, 20)}`;
  }

  extractionLabel(method: BankStatementExtractionMethod): string {
    switch (method) {
      case BankStatementExtractionMethod.TextParser: return 'Parser texte BIAT/TN';
      case BankStatementExtractionMethod.OcrTextParser: return 'OCR + parser texte';
      case BankStatementExtractionMethod.OcrLlm: return 'OCR + IA';
      default: return 'Inconnue';
    }
  }

  associationStatusLabel(status: BankLineAssociationStatus): string {
    switch (status) {
      case BankLineAssociationStatus.Associated: return 'Associé';
      case BankLineAssociationStatus.ToReconcile: return 'À rapprocher';
      default: return 'Non associé';
    }
  }

  reconcileAccountLabel(): string {
    const s = this.selected();
    return s?.chartOfAccountNumber?.trim() || '53…';
  }

  busy(): boolean {
    return this.previewing() || this.importing();
  }

  reconciledCount(s: BankStatementDto): number {
    return s.lines.filter(l => l.isReconciled).length;
  }

  unreconciledLines(s: BankStatementDto): BankStatementLineDto[] {
    return s.lines.filter(l => !l.isReconciled);
  }

  private emptyMeta() {
    return { bankName: '', accountNumber: '', periodStart: '', periodEnd: '', openingBalance: 0, closingBalance: 0 };
  }

  loadStatements(): void {
    this.loadingList.set(true);
    this.error.set(null);
    const from = this.fromStr ? parseLocalDateString(this.fromStr) : null;
    const to = this.toStr ? parseLocalDateString(this.toStr) : null;
    this.api.getStatements(this.accountFilter || null, from, to).subscribe({
      next: res => {
        this.loadingList.set(false);
        if (res.success && res.data) {
          let list = res.data;
          if (this.unmatchedOnly) {
            list = list.filter(s => s.lines.some(l => !l.isReconciled));
          }
          this.statements.set(list);
        } else this.error.set(res.error ?? 'Erreur de chargement des relevés.');
      },
      error: () => {
        this.loadingList.set(false);
        this.error.set('Erreur réseau lors du chargement des relevés.');
      }
    });
  }

  // ── Navigation wizard ────────────────────────────────────────────────

  goStep(n: 1 | 2 | 3 | 4): void {
    this.closeCandidates();
    this.cancelCreateEntry();
    this.step.set(n);
  }

  startImport(): void {
    this.resetImport();
    this.selected.set(null);
    this.associations.set([]);
    this.summary.set(null);
    this.wizardActive.set(true);
    this.step.set(1);
  }

  exitWizard(): void {
    this.wizardActive.set(false);
    this.resetImport();
    this.selected.set(null);
    this.associations.set([]);
    this.summary.set(null);
    this.closeCandidates();
    this.cancelCreateEntry();
    this.loadStatements();
  }

  finishWizard(): void {
    this.toast.add({ severity: 'success', summary: 'Rapprochement terminé', life: 3000 });
    this.exitWizard();
  }

  openStatement(id: string): void {
    this.loadingDetail.set(true);
    this.error.set(null);
    this.closeCandidates();
    this.api.getStatement(id).subscribe({
      next: res => {
        this.loadingDetail.set(false);
        if (res.success && res.data) {
          this.selected.set(res.data);
          this.wizardActive.set(true);
          this.step.set(2);
          this.runAutoAssociate();
        } else {
          this.error.set(res.error ?? 'Relevé introuvable.');
        }
      },
      error: () => {
        this.loadingDetail.set(false);
        this.error.set('Erreur réseau lors du chargement du relevé.');
      }
    });
  }

  /** Ouvre l'état de rapprochement d'un relevé (hors wizard, en lecture seule). */
  openReconciliationStatement(id: string): void {
    this.reconStatementId = id;
    this.reconError.set(null);
    this.reconStatement.set(null);
    this.api.getReconciliationStatement(id).subscribe({
      next: res => {
        if (res.success && res.data) this.reconStatement.set(res.data);
        else this.reconError.set(res.error ?? "Impossible d'établir l'état de rapprochement.");
      },
      error: () => this.reconError.set("Erreur réseau lors du chargement de l'état de rapprochement.")
    });
  }

  closeReconciliationStatement(): void {
    this.reconStatement.set(null);
    this.reconError.set(null);
    this.reconStatementId = null;
  }

  onExportReconciliation(format: AccountingExportFormat): void {
    const id = this.reconStatementId;
    if (!id) return;
    this.exportingRecon.set(true);
    this.api.exportReconciliationStatement(id, format).subscribe({
      next: blob => {
        this.exportingRecon.set(false);
        downloadBlob(blob, `etat_rapprochement_${id}.${exportExtension(format)}`);
      },
      error: () => {
        this.exportingRecon.set(false);
        this.toast.add({ severity: 'error', summary: "Erreur lors de l'export de l'état de rapprochement.", life: 4000 });
      }
    });
  }

  private refreshSelected(then?: () => void): void {
    const current = this.selected();
    if (!current) return;
    this.loadingDetail.set(true);
    this.api.getStatement(current.id).subscribe({
      next: res => {
        this.loadingDetail.set(false);
        if (res.success && res.data) this.selected.set(res.data);
        then?.();
      },
      error: () => {
        this.loadingDetail.set(false);
        then?.();
      }
    });
  }

  // ── Import fichier ─────────────────────────────────────────────────────

  onFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files && input.files.length > 0 ? input.files[0] : null;
    this.selectedFile.set(file);
    this.preview.set(null);
    this.error.set(null);
    if (file) {
      const ext = file.name.split('.').pop()?.toLowerCase() ?? '';
      if (ext === 'pdf') this.fileFormat = BankStatementFileFormat.Pdf;
      else if (['jpg', 'jpeg', 'png'].includes(ext)) this.fileFormat = BankStatementFileFormat.Image;
      else if (ext === 'xlsx') this.fileFormat = BankStatementFileFormat.Excel;
      else if (['ofx', 'qfx'].includes(ext)) this.fileFormat = BankStatementFileFormat.Ofx;
      else if (['sta', 'mt940'].includes(ext)) this.fileFormat = BankStatementFileFormat.Mt940;
      else this.fileFormat = BankStatementFileFormat.Csv;
    }
  }

  runPreview(): void {
    const file = this.selectedFile();
    if (!file || this.busy()) return;
    this.error.set(null);
    this.previewing.set(true);
    this.api.previewStatementFile(file, this.fileFormat).subscribe({
      next: res => {
        this.previewing.set(false);
        if (!res.success || !res.data) {
          this.error.set(res.error ?? "L'aperçu du relevé a échoué.");
          return;
        }
        const data: BankStatementFilePreviewDto = {
          ...res.data,
          extractionMethod: normalizeExtractionMethod(res.data.extractionMethod)
        };
        this.preview.set(data);
        if (data.periodStart) this.meta.periodStart = data.periodStart.substring(0, 10);
        if (data.periodEnd) this.meta.periodEnd = data.periodEnd.substring(0, 10);
        if (this.optImportBalances) {
          if (data.suggestedOpeningBalance != null) this.meta.openingBalance = data.suggestedOpeningBalance;
          if (data.suggestedClosingBalance != null) this.meta.closingBalance = data.suggestedClosingBalance;
        }
        if (data.suggestedBankName) this.meta.bankName = data.suggestedBankName;
        if (data.detectedRib) this.meta.accountNumber = this.formatRib(data.detectedRib);
        if (data.matchedBankAccountId) this.selectedBankAccountId = data.matchedBankAccountId;
        this.lastSourceFileName = this.selectedFile()?.name ?? null;
        this.lastImportMethod = this.mapImportMethod(data.extractionMethod, this.fileFormat);
      },
      error: () => {
        this.previewing.set(false);
        this.error.set("Erreur réseau lors de l'aperçu du relevé.");
      }
    });
  }

  runImport(): void {
    const p = this.preview();
    if (!p?.canImport || !this.canSubmitImport() || this.busy()) return;
    this.error.set(null);
    this.importing.set(true);
    this.api.importStatement({
      bankName: this.meta.bankName.trim(),
      accountNumber: this.meta.accountNumber.trim(),
      statementDate: this.meta.periodEnd,
      periodStart: this.meta.periodStart,
      periodEnd: this.meta.periodEnd,
      openingBalance: Number(this.meta.openingBalance) || 0,
      closingBalance: Number(this.meta.closingBalance) || 0,
      bankAccountId: this.selectedBankAccountId || p.matchedBankAccountId || null,
      chartOfAccountNumber: this.resolveChartAccountNumber(p),
      sourceFileName: this.lastSourceFileName,
      importMethod: this.lastImportMethod,
      skipAlreadyImported: this.optSkipAlreadyImported,
      lines: p.lines
    }).subscribe({
      next: res => {
        this.importing.set(false);
        if (!res.success || !res.data) {
          this.error.set(res.error ?? "L'import du relevé a échoué.");
          return;
        }
        const skipped = res.data.skippedDuplicateCount ?? 0;
        this.toast.add({
          severity: 'success',
          summary: 'Relevé importé',
          detail: skipped > 0
            ? `${res.data.lines.length} ligne(s) — ${skipped} déjà importée(s) ignorée(s)`
            : `${res.data.lines.length} ligne(s) — ${res.data.bankName}`,
          life: 4000
        });
        this.selected.set(res.data);
        this.resetImport();
        this.step.set(2);
        this.runAutoAssociate();
      },
      error: () => {
        this.importing.set(false);
        this.error.set("Erreur réseau lors de l'import du relevé.");
      }
    });
  }

  private resetImport(): void {
    this.selectedFile.set(null);
    this.preview.set(null);
    this.meta = this.emptyMeta();
    this.lastImportMethod = 0;
    this.lastSourceFileName = null;
  }

  private resolveChartAccountNumber(p: BankStatementFilePreviewDto): string | null {
    if (p.matchedChartOfAccountNumber) return p.matchedChartOfAccountNumber;
    const id = this.selectedBankAccountId || p.matchedBankAccountId;
    if (!id) return null;
    const ba = this.bankAccounts().find(b => b.id === id);
    return ba?.chartOfAccountNumber ?? null;
  }

  private mapImportMethod(extraction: BankStatementExtractionMethod | undefined | null, format: BankStatementFileFormat): number {
    if (extraction === BankStatementExtractionMethod.TextParser) return 3;
    if (extraction === BankStatementExtractionMethod.OcrTextParser) return 4;
    if (extraction === BankStatementExtractionMethod.OcrLlm) return 5;
    if (format === BankStatementFileFormat.Csv) return 1;
    if (format === BankStatementFileFormat.Excel) return 2;
    if (format === BankStatementFileFormat.Pdf) return 3;
    if (format === BankStatementFileFormat.Image) return 6;
    return 0;
  }

  // ── Association automatique ─────────────────────────────────────────────

  runAutoAssociate(): void {
    const s = this.selected();
    if (!s || this.autoAssociating()) return;
    this.autoAssociating.set(true);
    this.error.set(null);
    this.api.autoAssociate(s.id).subscribe({
      next: (res: { success: boolean; data?: AutoAssociationResultDto; error?: string }) => {
        this.autoAssociating.set(false);
        if (!res.success || !res.data) {
          this.error.set(res.error ?? "L'association automatique a échoué.");
          return;
        }
        this.associations.set(res.data.associations);
        this.summary.set(res.data.summary);
        // Pré-cocher toutes les correspondances certaines (« Associé »).
        const preChecked = new Set<string>(
          res.data.associations
            .filter(a => a.status === BankLineAssociationStatus.Associated && a.proposedJournalEntryLineId)
            .map(a => a.bankStatementLineId));
        this.checked.set(preChecked);
      },
      error: () => {
        this.autoAssociating.set(false);
        this.error.set("Erreur réseau lors de l'association automatique.");
      }
    });
  }

  isChecked(a: BankLineAssociationDto): boolean {
    return this.checked().has(a.bankStatementLineId);
  }

  toggleChecked(a: BankLineAssociationDto): void {
    const next = new Set(this.checked());
    if (next.has(a.bankStatementLineId)) next.delete(a.bankStatementLineId);
    else next.add(a.bankStatementLineId);
    this.checked.set(next);
  }

  applySelectedAssociations(): void {
    const s = this.selected();
    if (!s || this.applying()) return;
    const pairs: ReconcilePairRequest[] = this.associations()
      .filter(a => a.status === BankLineAssociationStatus.Associated
        && a.proposedJournalEntryLineId
        && this.checked().has(a.bankStatementLineId))
      .map(a => ({ bankStatementLineId: a.bankStatementLineId, journalEntryLineId: a.proposedJournalEntryLineId! }));
    if (pairs.length === 0) return;
    this.applying.set(true);
    this.error.set(null);
    this.api.applyAssociations(s.id, pairs).subscribe({
      next: res => {
        this.applying.set(false);
        if (!res.success || !res.data) {
          this.error.set(res.error ?? 'Le rapprochement en lot a échoué.');
          return;
        }
        const r = res.data;
        this.toast.add({
          severity: r.failures.length > 0 ? 'warn' : 'success',
          summary: `${r.appliedCount} rapprochement(s) appliqué(s)`,
          detail: r.failures.length > 0 ? `${r.failures.length} échec(s)` : undefined,
          life: 4000
        });
        this.refreshSelected(() => this.runAutoAssociate());
      },
      error: () => {
        this.applying.set(false);
        this.error.set('Erreur réseau lors du rapprochement en lot.');
      }
    });
  }

  /** Depuis l'étape 2 : bascule vers le rapprochement manuel sur une ligne précise. */
  reconcileFromStep(a: BankLineAssociationDto): void {
    const s = this.selected();
    if (!s) return;
    const line = s.lines.find(l => l.id === a.bankStatementLineId);
    this.step.set(3);
    if (line) this.openCandidates(line);
  }

  goToManual(): void {
    this.goStep(3);
  }

  // ── Rapprochement ligne à ligne ────────────────────────────────────────

  openCandidates(line: BankStatementLineDto): void {
    this.activeLine.set(line);
    this.candidates.set([]);
    this.loadingCandidates.set(true);

    const opDate = new Date(line.transactionDate);
    const from = new Date(opDate);
    from.setDate(from.getDate() - 10);
    const to = new Date(opDate);
    to.setDate(to.getDate() + 10);

    this.accounting.searchJournalEntries({
      account: this.reconcileAccountLabel(),
      from: formatLocalDate(from),
      to: formatLocalDate(to),
      minAmount: line.amount,
      maxAmount: line.amount,
      take: 50
    }).subscribe({
      next: res => {
        this.loadingCandidates.set(false);
        if (!res.success || !res.data) {
          this.error.set(res.error ?? 'La recherche de candidats a échoué.');
          return;
        }
        // Encaissement (crédit banque relevé) ⇒ débit du compte 53x ; décaissement ⇒ crédit. Brouillons exclus.
        const wantDebit = !line.isDebit;
        this.candidates.set(res.data.filter(c => !c.isDraft && (wantDebit ? c.debit > 0 : c.credit > 0)));
      },
      error: () => {
        this.loadingCandidates.set(false);
        this.error.set('Erreur réseau lors de la recherche de candidats.');
      }
    });
  }

  closeCandidates(): void {
    this.activeLine.set(null);
    this.candidates.set([]);
  }

  reconcile(line: BankStatementLineDto, candidate: JournalSearchRowDto): void {
    if (this.reconciling()) return;
    this.reconciling.set(true);
    this.error.set(null);
    this.api.reconcileLine({ bankStatementLineId: line.id, journalEntryLineId: candidate.lineId }).subscribe({
      next: res => {
        this.reconciling.set(false);
        if (!res.success) {
          this.error.set(res.error ?? 'Le rapprochement a échoué.');
          return;
        }
        this.toast.add({ severity: 'success', summary: 'Ligne rapprochée', detail: line.description, life: 3000 });
        this.closeCandidates();
        this.refreshSelected();
      },
      error: () => {
        this.reconciling.set(false);
        this.error.set('Erreur réseau lors du rapprochement.');
      }
    });
  }

  unreconcile(line: BankStatementLineDto): void {
    if (this.reconciling()) return;
    this.reconciling.set(true);
    this.error.set(null);
    this.api.unreconcileLine(line.id).subscribe({
      next: res => {
        this.reconciling.set(false);
        if (!res.success) {
          this.error.set(res.error ?? 'Le dé-rapprochement a échoué.');
          return;
        }
        this.toast.add({ severity: 'success', summary: 'Rapprochement annulé', detail: line.description, life: 3000 });
        this.refreshSelected();
      },
      error: () => {
        this.reconciling.set(false);
        this.error.set('Erreur réseau lors du dé-rapprochement.');
      }
    });
  }

  // ── Création d'écriture depuis une ligne non rapprochée ─────────────────

  openCreateEntry(lineId: string, description: string, isDebit: boolean, amount: number): void {
    this.createForm = {
      journalCode: this.defaultJournalCode?.trim() || 'BQ',
      counterpartyAccount: this.defaultCounterparty?.trim() || '',
      label: description
    };
    this.createLine.set({ id: lineId, description, isDebit, amount });
  }

  cancelCreateEntry(): void {
    this.createLine.set(null);
  }

  confirmCreateEntry(): void {
    const s = this.selected();
    const cl = this.createLine();
    if (!s || !cl || this.creatingEntry()) return;
    const journal = this.createForm.journalCode.trim();
    const counterparty = this.createForm.counterpartyAccount.trim();
    if (!journal || !counterparty) return;
    this.creatingEntry.set(true);
    this.error.set(null);
    this.api.createEntryForLine(s.id, cl.id, {
      journalCode: journal,
      counterpartyAccount: counterparty,
      label: this.createForm.label?.trim() || null
    }).subscribe({
      next: res => {
        this.creatingEntry.set(false);
        if (!res.success) {
          this.error.set(res.error ?? "La comptabilisation de la ligne a échoué.");
          return;
        }
        this.toast.add({ severity: 'success', summary: 'Écriture comptabilisée', detail: cl.description, life: 3000 });
        this.cancelCreateEntry();
        this.refreshSelected(() => {
          if (this.step() === 2) this.runAutoAssociate();
        });
      },
      error: () => {
        this.creatingEntry.set(false);
        this.error.set('Erreur réseau lors de la comptabilisation.');
      }
    });
  }
}
