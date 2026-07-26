import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { TableModule } from 'primeng/table';
import { ButtonComponent } from '@shared/components/button/button.component';
import { AccountingStatusBannerComponent } from '../shared/accounting-status-banner.component';
import {
  AccountingService,
  JournalImportFormat,
  JournalImportPreviewDto
} from '../services/accounting.service';

@Component({
  selector: 'app-accounting-import',
  standalone: true,
  imports: [CommonModule, FormsModule, TableModule, ButtonComponent, AccountingStatusBannerComponent],
  template: `
    <div class="card import-card">
      <div class="import-fields">
        <div class="form-field">
          <label class="field-label" for="imp-format">Format</label>
          <select id="imp-format" class="import-input" [(ngModel)]="format" [disabled]="busy()">
            <option [ngValue]="0">CSV (délimité)</option>
            <option [ngValue]="1">Excel (.xlsx)</option>
            <option [ngValue]="2">FEC (fichier des écritures comptables)</option>
          </select>
        </div>
        <div class="form-field import-field-file">
          <label class="field-label" for="imp-file">Fichier</label>
          <input id="imp-file" type="file" class="import-input" (change)="onFileSelected($event)" [disabled]="busy()"
            accept=".csv,.txt,.xlsx,.tsv" />
        </div>
        <div class="import-actions">
          <app-button variant="secondary" icon="pi pi-search" type="button"
            (click)="runPreview()" [disabled]="!selectedFile() || busy()"
            ariaLabel="Analyser le fichier sans rien enregistrer">
            Aperçu
          </app-button>
        </div>
      </div>

      <p class="import-help">
        Colonnes attendues (CSV/Excel, 1<sup>re</sup> ligne = en-têtes) : <code>journal, numero, date, compte, libelle, debit, credit</code>.
        Les lignes de même <code>journal</code>+<code>numero</code> forment une pièce. Pour une balance d'ouverture,
        utilisez le journal <code>JAN</code>. Les écritures importées arrivent <strong>en brouillard</strong> et devront être validées.
      </p>
    </div>

    <app-accounting-status-banner variant="error" [message]="error() ?? ''" />

    @if (successMessage(); as msg) {
      <div class="import-success" role="status">
        <i class="pi pi-check-circle" aria-hidden="true"></i>
        <span>{{ msg }}</span>
      </div>
    }

    @if (preview(); as p) {
      <div class="card import-card">
        <div class="import-summary">
          <div class="import-kpi"><span class="import-kpi-label">Écritures</span><span class="import-kpi-value">{{ p.totalEntries }}</span></div>
          <div class="import-kpi"><span class="import-kpi-label">Lignes</span><span class="import-kpi-value">{{ p.totalLines }}</span></div>
          <div class="import-kpi"><span class="import-kpi-label">Valides</span><span class="import-kpi-value import-ok">{{ p.validEntries }}</span></div>
          <div class="import-kpi"><span class="import-kpi-label">En erreur</span><span class="import-kpi-value" [class.import-err]="p.entriesWithErrors > 0">{{ p.entriesWithErrors }}</span></div>
          <div class="import-kpi"><span class="import-kpi-label">Total débit</span><span class="import-kpi-value">{{ p.totalDebit | number : '1.3-3' }}</span></div>
          <div class="import-kpi"><span class="import-kpi-label">Total crédit</span><span class="import-kpi-value">{{ p.totalCredit | number : '1.3-3' }}</span></div>
        </div>

        @if (p.issues.length > 0) {
          <div class="import-issues">
            <h3 class="import-issues-title">{{ p.issues.length }} anomalie(s) — à corriger avant l'import</h3>
            <ul class="import-issues-list">
              @for (issue of p.issues.slice(0, 100); track $index) {
                <li><span class="import-issue-ref">{{ issue.ref }}</span> {{ issue.message }}</li>
              }
            </ul>
            @if (p.issues.length > 100) {
              <p class="import-issues-more">… et {{ p.issues.length - 100 }} autre(s).</p>
            }
          </div>
        }

        @if (p.sample.length > 0) {
          <h3 class="import-sample-title">Aperçu ({{ p.sample.length }} première(s) écriture(s))</h3>
          <p-table [value]="p.sample" styleClass="p-datatable-sm accounting-datatable" [rowHover]="true">
            <ng-template pTemplate="header">
              <tr>
                <th scope="col">Pièce</th>
                <th scope="col">Journal</th>
                <th scope="col">Date</th>
                <th scope="col">Libellé</th>
                <th scope="col" class="import-col-amount">Débit</th>
                <th scope="col" class="import-col-amount">Crédit</th>
                <th scope="col">Équilibre</th>
              </tr>
            </ng-template>
            <ng-template pTemplate="body" let-e>
              <tr>
                <td>{{ e.ref }}</td>
                <td>{{ e.journalCode }}</td>
                <td>{{ e.entryDate | date : 'shortDate' }}</td>
                <td>{{ e.label }}</td>
                <td class="import-col-amount">{{ e.totalDebit | number : '1.3-3' }}</td>
                <td class="import-col-amount">{{ e.totalCredit | number : '1.3-3' }}</td>
                <td>
                  <span class="import-badge" [class.import-badge-ok]="e.isBalanced" [class.import-badge-ko]="!e.isBalanced">
                    {{ e.isBalanced ? 'OK' : 'Déséquilibrée' }}
                  </span>
                </td>
              </tr>
            </ng-template>
          </p-table>
        }

        <div class="import-commit-bar">
          @if (p.canCommit) {
            <span class="import-commit-hint">Prêt à importer {{ p.totalEntries }} écriture(s) en brouillard.</span>
          } @else {
            <span class="import-commit-hint import-err">Corrigez les anomalies bloquantes avant d'importer.</span>
          }
          <app-button variant="primary" icon="pi pi-upload" type="button"
            (click)="runCommit()" [disabled]="!p.canCommit || busy()"
            ariaLabel="Importer les écritures en brouillard">
            {{ committing() ? 'Import…' : 'Importer en brouillard' }}
          </app-button>
        </div>
      </div>
    }
  `,
  styles: `
    .import-card {
      padding: var(--spacing-5);
      border-radius: var(--radius-lg);
      box-shadow: var(--shadow-sm, 0 1px 3px rgba(15, 23, 42, 0.08));
      margin-bottom: var(--spacing-4);
    }
    .import-fields {
      display: flex;
      flex-wrap: wrap;
      align-items: flex-end;
      gap: var(--spacing-4);
    }
    .form-field { display: flex; flex-direction: column; gap: var(--spacing-2); min-width: 0; }
    .import-field-file { flex: 1 1 260px; }
    .field-label { font-size: var(--font-size-sm); font-weight: var(--font-weight-semibold); color: var(--color-text-primary); margin: 0; }
    .import-input {
      padding: var(--spacing-2) var(--spacing-3);
      border: 1px solid var(--color-border-default);
      border-radius: var(--radius-md);
      background: var(--color-background-elevated);
      color: var(--color-text-primary);
      font-size: var(--font-size-sm);
      min-height: 2.5rem;
    }
    .import-help {
      margin: var(--spacing-4) 0 0;
      font-size: var(--font-size-sm);
      color: var(--color-text-secondary);
    }
    .import-help code {
      font-family: ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, monospace;
      background: var(--color-background-subtle);
      padding: 0 0.25rem;
      border-radius: var(--radius-sm, 4px);
    }
    .import-success {
      display: flex; align-items: center; gap: var(--spacing-2);
      margin-bottom: var(--spacing-4); padding: var(--spacing-3) var(--spacing-4);
      border-radius: var(--radius-md);
      background: var(--color-success-50, #f0fdf4);
      border: 1px solid var(--color-success-200, #bbf7d0);
      color: var(--color-success-700, #15803d);
      font-weight: var(--font-weight-medium);
    }
    .import-summary {
      display: flex; flex-wrap: wrap; gap: var(--spacing-5);
      padding-bottom: var(--spacing-4); margin-bottom: var(--spacing-4);
      border-bottom: 1px solid var(--color-border-subtle);
    }
    .import-kpi { display: flex; flex-direction: column; gap: 2px; }
    .import-kpi-label { font-size: var(--font-size-xs); color: var(--color-text-tertiary); text-transform: uppercase; letter-spacing: 0.04em; }
    .import-kpi-value { font-size: var(--font-size-lg); font-weight: var(--font-weight-semibold); font-variant-numeric: tabular-nums; }
    .import-ok { color: var(--color-success-700, #15803d); }
    .import-err { color: var(--color-danger-600, #dc2626); }
    .import-issues {
      margin-bottom: var(--spacing-4); padding: var(--spacing-3) var(--spacing-4);
      border-radius: var(--radius-md);
      background: var(--color-danger-50, #fef2f2);
      border: 1px solid var(--color-danger-200, #fecaca);
    }
    .import-issues-title { margin: 0 0 var(--spacing-2); font-size: var(--font-size-sm); color: var(--color-danger-700, #b91c1c); }
    .import-issues-list { margin: 0; padding-left: var(--spacing-5); font-size: var(--font-size-sm); color: var(--color-text-secondary); }
    .import-issue-ref { font-weight: var(--font-weight-semibold); color: var(--color-text-primary); margin-right: var(--spacing-1); }
    .import-issues-more { margin: var(--spacing-2) 0 0; font-size: var(--font-size-xs); color: var(--color-text-tertiary); }
    .import-sample-title { margin: 0 0 var(--spacing-3); font-size: var(--font-size-sm); font-weight: var(--font-weight-semibold); }
    .import-col-amount { text-align: right; font-variant-numeric: tabular-nums; }
    .import-badge { display: inline-block; padding: 0.1rem 0.5rem; border-radius: var(--radius-pill, 999px); font-size: var(--font-size-xs); font-weight: var(--font-weight-semibold); }
    .import-badge-ok { background: var(--color-success-100, #dcfce7); color: var(--color-success-700, #15803d); }
    .import-badge-ko { background: var(--color-danger-100, #fee2e2); color: var(--color-danger-700, #b91c1c); }
    .import-commit-bar {
      display: flex; align-items: center; justify-content: flex-end; gap: var(--spacing-4);
      margin-top: var(--spacing-4); padding-top: var(--spacing-4);
      border-top: 1px solid var(--color-border-subtle);
    }
    .import-commit-hint { font-size: var(--font-size-sm); color: var(--color-text-secondary); }
  `
})
export class ImportComponent {
  private readonly api = inject(AccountingService);

  format: JournalImportFormat = JournalImportFormat.Csv;
  readonly selectedFile = signal<File | null>(null);
  readonly preview = signal<JournalImportPreviewDto | null>(null);
  readonly loading = signal(false);
  readonly committing = signal(false);
  readonly error = signal<string | null>(null);
  readonly successMessage = signal<string | null>(null);

  busy(): boolean {
    return this.loading() || this.committing();
  }

  onFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    this.selectedFile.set(input.files && input.files.length > 0 ? input.files[0] : null);
    this.preview.set(null);
    this.successMessage.set(null);
    this.error.set(null);
  }

  runPreview(): void {
    const file = this.selectedFile();
    if (!file || this.busy()) return;
    this.error.set(null);
    this.successMessage.set(null);
    this.loading.set(true);
    this.api.previewJournalImport(file, this.format).subscribe({
      next: res => {
        this.loading.set(false);
        if (!res.success || !res.data) {
          this.error.set(res.error ?? "L'aperçu a échoué.");
          return;
        }
        this.preview.set(res.data);
      },
      error: () => {
        this.loading.set(false);
        this.error.set("Erreur réseau lors de l'aperçu.");
      }
    });
  }

  runCommit(): void {
    const file = this.selectedFile();
    const p = this.preview();
    if (!file || !p?.canCommit || this.busy()) return;
    this.error.set(null);
    this.committing.set(true);
    this.api.commitJournalImport(file, this.format).subscribe({
      next: res => {
        this.committing.set(false);
        if (!res.success || !res.data) {
          this.error.set(res.error ?? "L'import a échoué.");
          return;
        }
        this.successMessage.set(
          `${res.data.importedEntries} écriture(s) et ${res.data.importedLines} ligne(s) importées en brouillard. Validez-les depuis le journal.`
        );
        this.preview.set(null);
        this.selectedFile.set(null);
      },
      error: () => {
        this.committing.set(false);
        this.error.set("Erreur réseau lors de l'import.");
      }
    });
  }
}
