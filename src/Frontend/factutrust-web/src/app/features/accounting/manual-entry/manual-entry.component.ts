import { Component, DestroyRef, HostListener, OnInit, computed, effect, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { TableModule } from 'primeng/table';
import { AutoCompleteModule } from 'primeng/autocomplete';
import { PageHeaderComponent } from '@shared/components/page-header/page-header.component';
import {
  AccountingService,
  AccountingPeriodDto,
  ChartOfAccountDto,
  CreateJournalEntryTemplateRequest,
  CreateManualJournalEntryRequest,
  JournalEntryTemplateDto,
  ManualJournalLineRequest
} from '../services/accounting.service';
import { ToastService } from '@core/services/toast.service';
import { ErrorHandlerService } from '@core/services/error-handler.service';
import { ClientService } from '@core/services/client.service';
import { SupplierService } from '@core/services/supplier.service';
import { catchError, forkJoin, map, of } from 'rxjs';
import { formatLocalDate } from '../shared/accounting-date-utils';
import { AnalyzeWithAiButtonComponent } from '@features/ai-assistant/components/analyze-with-ai-button/analyze-with-ai-button.component';
import { wrapLegacyAnalyzePayload } from '@features/ai-assistant/utils/ai-screen-payload.factory';
import { BalanceIndicatorComponent } from './components/balance-indicator.component';
import { PeriodBadgeComponent } from './components/period-badge.component';
import { TemplatePickerModalComponent } from './components/template-picker-modal.component';
import { SaveTemplateModalComponent, SaveTemplatePayload } from './components/save-template-modal.component';
import { DraftBannerComponent } from './components/draft-banner.component';
import { ManualEntryDraft, ManualEntryDraftService } from './services/manual-entry-draft.service';

interface AccountSuggestion {
  number: string;
  label: string;
  display: string;
}

/** Suggestion de tiers pour la comptabilité auxiliaire (kind : 1 = client, 2 = fournisseur). */
interface ThirdPartySuggestion {
  id: string;
  kind: number;
  name: string;
  display: string;
}

interface EntryLine {
  accountNumber: string;
  lineLabel: string;
  debit: number | null;
  credit: number | null;
  /** Tiers optionnel de la ligne — objet sélectionné dans l'autocomplete, sinon null. */
  thirdParty: ThirdPartySuggestion | null;
}

@Component({
  selector: 'app-manual-entry',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    TableModule,
    AutoCompleteModule,
    PageHeaderComponent,
    AnalyzeWithAiButtonComponent,
    BalanceIndicatorComponent,
    PeriodBadgeComponent,
    TemplatePickerModalComponent,
    SaveTemplateModalComponent,
    DraftBannerComponent
  ],
  template: `
    <app-page-header title="Saisie d'écriture manuelle" subtitle="Écriture de journal multi-lignes avec équilibrage automatique">
      <div class="me-header-actions">
        <button type="button" class="btn btn-outline-secondary btn-sm"
                (click)="openTemplatePicker()"
                [disabled]="loading()"
                title="Charger un modèle d'écriture sauvegardé">
          📋 Charger un modèle
        </button>
        <button type="button" class="btn btn-outline-secondary btn-sm"
                (click)="openSaveTemplate()"
                [disabled]="loading() || !canSaveAsTemplate()"
                title="Sauvegarder l'écriture actuelle comme modèle réutilisable">
          💾 Sauvegarder comme modèle
        </button>
        <app-analyze-with-ai-button
          screenId="accounting-manual-entry"
          [payloadBuilder]="buildManualEntryAnalyzePayload"
          [disabled]="loading()" />
      </div>
    </app-page-header>

    <app-draft-banner
      [visible]="pendingDraft() !== null"
      [savedAt]="pendingDraft()?.savedAt ?? null"
      (restore)="restoreDraft()"
      (discard)="discardDraft()" />

    <div class="card me-card">
      <div class="me-header-fields">
        <div class="form-field">
          <label class="field-label" for="me-journal">Journal</label>
          <select id="me-journal" class="me-input"
                  [ngModel]="journalCode()" (ngModelChange)="journalCode.set($event)">
            @for (opt of journalOptions(); track opt.code) {
              <option [value]="opt.code">{{ opt.code }} — {{ opt.label }}</option>
            }
          </select>
        </div>
        <div class="form-field">
          <label class="field-label" for="me-date">Date</label>
          <input id="me-date" type="date" class="me-input"
                 [ngModel]="entryDate()" (ngModelChange)="entryDate.set($event)"
                 [attr.aria-describedby]="periodsLoaded() ? 'me-period-badge' : null" />
          @if (periodsLoaded()) {
            <span id="me-period-badge" class="me-period-badge-wrapper">
              <app-period-badge [date]="entryDate()" [periods]="periods()" />
            </span>
          }
        </div>
        <div class="form-field" style="flex:1 1 200px">
          <label class="field-label" for="me-label">Libellé</label>
          <input id="me-label" type="text" class="me-input" placeholder="Libellé de l'écriture"
                 [ngModel]="entryLabel()" (ngModelChange)="entryLabel.set($event)" />
        </div>
        <div class="form-field">
          <label class="field-label" for="me-piece-ref">N° pièce <span class="me-optional">(facultatif)</span></label>
          <input id="me-piece-ref" type="text" class="me-input" maxlength="50"
                 placeholder="Réf. pièce externe"
                 [ngModel]="pieceRef()" (ngModelChange)="pieceRef.set($event)" />
        </div>
        <div class="form-field">
          <label class="field-label" for="me-piece-date">Date pièce <span class="me-optional">(facultatif)</span></label>
          <input id="me-piece-date" type="date" class="me-input"
                 [ngModel]="pieceDate()" (ngModelChange)="pieceDate.set($event)" />
        </div>
      </div>

      <h3 class="me-section-title">Lignes</h3>
      <p-table [value]="lines()" styleClass="p-datatable-sm">
        <ng-template pTemplate="header">
          <tr>
            <th style="width:2.5rem" class="text-center">#</th>
            <th>Compte</th>
            <th>Libellé ligne</th>
            <th>Tiers <span class="me-optional">(facultatif)</span></th>
            <th class="text-right">Débit</th>
            <th class="text-right">Crédit</th>
            <th style="width:6rem" class="text-center">Actions</th>
          </tr>
        </ng-template>
        <ng-template pTemplate="body" let-line let-i="rowIndex">
          <tr>
            <td class="text-center me-line-number">{{ i + 1 }}</td>
            <td class="me-cell-account"
                [class.me-cell-unknown]="getLineStatus(line) === 'unknown'"
                [class.me-cell-inactive]="getLineStatus(line) === 'inactive'"
                [attr.title]="getLineStatusMessage(line) || null">
              <div class="me-account-wrapper">
                <p-autoComplete
                  [(ngModel)]="line.accountNumber"
                  [suggestions]="accountSuggestions"
                  (completeMethod)="filterAccounts($event)"
                  (onSelect)="onAccountSelect($event, i)"
                  [field]="'display'"
                  [minLength]="1"
                  [forceSelection]="false"
                  placeholder="N° compte"
                  [inputStyle]="{'width':'12rem'}"
                />
                @if (getAccountClass(line); as cls) {
                  <span class="me-class-badge"
                        [class]="'me-class-' + cls"
                        [attr.title]="'Classe comptable ' + cls"
                        aria-hidden="true">{{ cls }}</span>
                }
                @if (getLineStatus(line) === 'unknown') {
                  <span class="me-line-warning" aria-label="Compte inconnu" title="Compte inconnu dans le plan comptable">⚠</span>
                } @else if (getLineStatus(line) === 'inactive') {
                  <span class="me-line-warning me-line-inactive" aria-label="Compte désactivé" title="Compte désactivé">⊘</span>
                }
              </div>
            </td>
            <td><input type="text" [(ngModel)]="line.lineLabel" class="me-line-input" placeholder="Libellé" /></td>
            <td>
              <p-autoComplete
                [(ngModel)]="line.thirdParty"
                [suggestions]="thirdPartySuggestions"
                (completeMethod)="filterThirdParties($event)"
                [field]="'display'"
                [minLength]="2"
                [forceSelection]="true"
                [showClear]="true"
                placeholder="Client ou fournisseur"
                [inputStyle]="{'width':'13rem'}"
              />
            </td>
            <td><input type="number" [(ngModel)]="line.debit" class="me-line-input text-right" min="0" step="0.001" placeholder="0,000" (ngModelChange)="onDebitChange(i)" /></td>
            <td><input type="number" [(ngModel)]="line.credit" class="me-line-input text-right" min="0" step="0.001" placeholder="0,000" (ngModelChange)="onCreditChange(i)" /></td>
            <td class="me-row-actions">
              <button type="button" class="btn btn-sm btn-outline-secondary me-row-action-btn"
                      (click)="duplicateLine(i)"
                      title="Dupliquer cette ligne (compte et libellé, sans montants)"
                      aria-label="Dupliquer cette ligne">⎘</button>
              <button type="button" class="btn btn-sm btn-outline-danger me-row-action-btn"
                      (click)="removeLine(i)"
                      [disabled]="lines().length <= 2"
                      [attr.title]="lines().length <= 2 ? 'Minimum 2 lignes requises' : 'Supprimer cette ligne'"
                      aria-label="Supprimer cette ligne">✕</button>
            </td>
          </tr>
        </ng-template>
        <ng-template pTemplate="footer">
          <tr style="font-weight:700">
            <td colspan="4">Totaux</td>
            <td class="text-right">{{ totals().debit | number : '1.3-3' }}</td>
            <td class="text-right">{{ totals().credit | number : '1.3-3' }}</td>
            <td></td>
          </tr>
        </ng-template>
      </p-table>

      <div class="me-line-actions">
        <button type="button" class="btn btn-outline-secondary btn-sm" (click)="addLine()">+ Ajouter une ligne</button>
      </div>

      <app-balance-indicator
        [totalDebit]="totals().debit"
        [totalCredit]="totals().credit"
        [canAutoBalance]="canAutoBalance()"
        (autoBalance)="autoBalance()" />

      @if (error()) {
        <p class="text-danger me-error" role="alert">{{ error() }}</p>
      }
      @if (successMsg()) {
        <p class="text-success me-error" role="status">{{ successMsg() }}</p>
      }

      <div class="me-actions">
        <button type="button" class="btn btn-primary"
                (click)="submit('navigate')"
                [disabled]="!canSubmit()">
          {{ loading() ? 'Enregistrement…' : "Enregistrer l'écriture" }}
        </button>
        <button type="button" class="btn btn-outline-primary"
                (click)="submit('reset')"
                [disabled]="!canSubmit()"
                title="Enregistrer puis commencer une nouvelle écriture vide">
          Enregistrer & nouveau
        </button>
        <button type="button" class="btn btn-outline-secondary"
                (click)="resetWithConfirm()"
                [disabled]="loading() || !hasUserInput()"
                title="Vider le formulaire">
          Réinitialiser
        </button>
        @if (periodClosed()) {
          <span class="me-period-warning" role="alert">
            La période comptable de cette date est clôturée — modifiez la date pour enregistrer.
          </span>
        }
      </div>

      <details class="me-shortcuts" aria-label="Liste des raccourcis clavier">
        <summary>⌨ Raccourcis clavier</summary>
        <ul class="me-shortcuts-list">
          <li><kbd>Ctrl</kbd>+<kbd>S</kbd> — Enregistrer l'écriture</li>
          <li><kbd>Ctrl</kbd>+<kbd>Shift</kbd>+<kbd>S</kbd> — Enregistrer & nouveau</li>
          <li><kbd>Alt</kbd>+<kbd>L</kbd> — Ajouter une ligne</li>
          <li><kbd>Alt</kbd>+<kbd>B</kbd> — Équilibrer automatiquement</li>
          <li><kbd>Alt</kbd>+<kbd>M</kbd> — Charger un modèle</li>
          <li><kbd>Alt</kbd>+<kbd>N</kbd> — Réinitialiser le formulaire</li>
          <li><kbd>Esc</kbd> — Fermer la modale ouverte</li>
        </ul>
      </details>
    </div>

    <app-template-picker-modal
      [visible]="showTemplatePicker()"
      (close)="closeTemplatePicker()"
      (templateSelected)="onTemplateSelected($event)" />

    <app-save-template-modal
      [visible]="showSaveTemplate()"
      [defaultName]="entryLabel()"
      [journalCode]="journalCode()"
      [saving]="savingTemplate()"
      (close)="closeSaveTemplate()"
      (save)="onSaveAsTemplate($event)" />
  `,
  styles: `
    .me-card { padding:var(--spacing-5); border-radius:var(--radius-lg); box-shadow:var(--shadow-sm); }
    .me-header-fields { display:flex; flex-wrap:wrap; gap:var(--spacing-4); align-items:flex-end; margin-bottom:var(--spacing-4); }
    .form-field { display:flex; flex-direction:column; gap:var(--spacing-2); }
    .field-label { font-size:var(--font-size-sm); font-weight:var(--font-weight-semibold); color:var(--color-text-primary); }
    .me-optional { font-weight:var(--font-weight-normal); font-size:var(--font-size-xs); color:var(--color-text-tertiary); }
    .me-input { padding:var(--spacing-2) var(--spacing-3); border:1px solid var(--color-border-default); border-radius:var(--radius-md); background:var(--color-background-elevated); font-size:var(--font-size-sm); min-height:2.5rem; }
    .me-input:focus { outline:none; border-color:var(--color-primary-500); box-shadow:0 0 0 3px var(--color-primary-200); }
    .me-period-badge-wrapper { display:inline-flex; }
    .me-section-title { font-size:var(--font-size-md); font-weight:var(--font-weight-semibold); margin:var(--spacing-4) 0 var(--spacing-2); }
    .me-line-input { width:100%; padding:var(--spacing-1) var(--spacing-2); border:1px solid var(--color-border-default); border-radius:var(--radius-sm); font-size:var(--font-size-sm); }
    .me-line-input:focus { outline:none; border-color:var(--color-primary-500); }
    .text-right { text-align:right; }
    .text-center { text-align:center; }
    .me-line-actions { margin:var(--spacing-3) 0; }
    .me-row-actions { display:flex; gap:var(--spacing-1); justify-content:center; }
    .me-row-action-btn { min-width:2rem; padding:var(--spacing-1) var(--spacing-2); line-height:1; }
    .me-error { margin:var(--spacing-3) 0; }
    .me-actions { display:flex; flex-wrap:wrap; gap:var(--spacing-3); align-items:center; margin-top:var(--spacing-4); padding-top:var(--spacing-4); border-top:1px solid var(--color-border-subtle); }
    .me-period-warning { color:var(--color-error-700,#b91c1c); font-size:var(--font-size-sm); font-weight:var(--font-weight-medium); }
    .text-success { color:var(--color-success-600); }
    .me-header-actions { display:flex; flex-wrap:wrap; gap:var(--spacing-2); align-items:center; }
    .me-line-number { color:var(--color-text-tertiary); font-weight:var(--font-weight-semibold); font-variant-numeric:tabular-nums; }
    .me-cell-account { transition: background 150ms; }
    .me-cell-unknown { background:var(--color-error-50,#fef2f2); }
    .me-cell-inactive { background:var(--color-warning-50,#fffbeb); }
    .me-account-wrapper { display:flex; align-items:center; gap:var(--spacing-2); flex-wrap:wrap; }
    .me-line-warning { font-size:1rem; color:var(--color-error-600,#dc2626); cursor:help; line-height:1; }
    .me-line-inactive { color:var(--color-warning-600,#d97706); }
    .me-class-badge { display:inline-flex; align-items:center; justify-content:center; width:1.25rem; height:1.25rem; border-radius:var(--radius-sm); font-size:0.7rem; font-weight:var(--font-weight-bold); color:#fff; font-family:monospace; }
    .me-class-1 { background:#7c3aed; } /* Capitaux propres — violet */
    .me-class-2 { background:#0891b2; } /* Immobilisations — cyan */
    .me-class-3 { background:#65a30d; } /* Stocks — vert lime */
    .me-class-4 { background:#d97706; } /* Tiers — orange */
    .me-class-5 { background:#0284c7; } /* Financiers — bleu */
    .me-class-6 { background:#dc2626; } /* Charges — rouge */
    .me-class-7 { background:#16a34a; } /* Produits — vert */
    .me-class-8 { background:#64748b; } /* Spéciaux — gris */
    .me-class-9 { background:#475569; } /* Analytique — gris foncé */
    .me-shortcuts { margin-top:var(--spacing-4); padding-top:var(--spacing-3); border-top:1px dashed var(--color-border-subtle); font-size:var(--font-size-xs); color:var(--color-text-tertiary); }
    .me-shortcuts summary { cursor:pointer; font-weight:var(--font-weight-semibold); padding:var(--spacing-1) 0; user-select:none; }
    .me-shortcuts summary:hover { color:var(--color-text-secondary); }
    .me-shortcuts-list { display:grid; grid-template-columns:repeat(auto-fill,minmax(220px,1fr)); gap:var(--spacing-1) var(--spacing-4); list-style:none; padding:var(--spacing-2) 0 0; margin:0; }
    .me-shortcuts-list li { padding:var(--spacing-1) 0; }
    .me-shortcuts kbd { display:inline-block; padding:0 var(--spacing-1); margin:0 0.125rem; font-family:monospace; font-size:0.75em; background:var(--color-background-subtle,#f1f5f9); border:1px solid var(--color-border-default,#cbd5e1); border-radius:var(--radius-sm); box-shadow:0 1px 0 var(--color-border-default,#cbd5e1); }
  `
})
export class ManualEntryComponent implements OnInit {
  private readonly api = inject(AccountingService);
  private readonly router = inject(Router);
  private readonly toast = inject(ToastService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly draftStore = inject(ManualEntryDraftService);
  private readonly clientService = inject(ClientService);
  private readonly supplierService = inject(SupplierService);
  private readonly errorHandler = inject(ErrorHandlerService);

  readonly journalCode = signal('JOD');
  readonly entryDate = signal(formatLocalDate(new Date()));
  readonly entryLabel = signal('');
  /** Référence de la pièce externe justificative (facultative). */
  readonly pieceRef = signal('');
  /** Date de la pièce externe (facultative, yyyy-MM-dd). */
  readonly pieceDate = signal('');
  /** Options de journal : catalogue si disponible, sinon liste standard figée (repli). */
  readonly journalOptions = signal<{ code: string; label: string }[]>([
    { code: 'JOD', label: 'Opérations diverses' },
    { code: 'JV', label: 'Ventes' },
    { code: 'JA', label: 'Achats' },
    { code: 'JC', label: 'Caisse' },
    { code: 'JB', label: 'Banque' },
    { code: 'JIM', label: 'Immobilisations' },
    { code: 'JAN', label: 'À-Nouveaux' }
  ]);
  accountSuggestions: AccountSuggestion[] = [];
  private allAccounts: ChartOfAccountDto[] = [];
  thirdPartySuggestions: ThirdPartySuggestion[] = [];

  readonly lines = signal<EntryLine[]>([
    { accountNumber: '', lineLabel: '', debit: null, credit: null, thirdParty: null },
    { accountNumber: '', lineLabel: '', debit: null, credit: null, thirdParty: null }
  ]);
  readonly error = signal<string | null>(null);
  readonly successMsg = signal<string | null>(null);
  readonly loading = signal(false);
  readonly periods = signal<AccountingPeriodDto[]>([]);
  readonly periodsLoaded = signal(false);
  readonly showTemplatePicker = signal(false);
  readonly showSaveTemplate = signal(false);
  readonly savingTemplate = signal(false);
  /** Brouillon en attente de décision utilisateur (Restaurer / Ignorer). */
  readonly pendingDraft = signal<ManualEntryDraft | null>(null);
  /** True une fois que l'utilisateur a décidé du brouillon — débloque l'auto-save. */
  private readonly draftInitialized = signal(false);

  readonly buildManualEntryAnalyzePayload = (): unknown =>
    wrapLegacyAnalyzePayload(
      'accounting-manual-entry',
      {
        screen: 'accounting-manual-entry',
        entry: {
          journalCode: this.journalCode() || null,
          entryDate: this.entryDate() || null,
          label: this.entryLabel() || null,
          isBalanced: this.isBalanced(),
          totalDebit: this.totals().debit,
          totalCredit: this.totals().credit
        },
        lines: this.lines().map(l => ({
          accountNumber: l.accountNumber || null,
          label: l.lineLabel || null,
          debit: l.debit,
          credit: l.credit
        }))
      } as Record<string, unknown>,
      { rowsKey: 'lines' }
    );

  readonly totals = computed(() => {
    let debit = 0, credit = 0;
    for (const l of this.lines()) {
      debit += Number(l.debit) || 0;
      credit += Number(l.credit) || 0;
    }
    return { debit, credit };
  });

  readonly isBalanced = computed(() => {
    const t = this.totals();
    // Aligné sur la validation backend (JournalEntry.cs): Math.Round à 3 décimales (millimes TND)
    return Math.round(t.debit * 1000) === Math.round(t.credit * 1000) && t.debit > 0;
  });

  readonly periodClosed = computed(() => {
    if (!this.periodsLoaded()) return false;
    const d = this.entryDate();
    if (!d) return false;
    const found = this.periods().find(p => {
      const start = (p.startDate ?? '').substring(0, 10);
      const end = (p.endDate ?? '').substring(0, 10);
      return d >= start && d <= end;
    });
    return found ? found.isClosed : false;
  });

  readonly canAutoBalance = computed(() => {
    if (this.isBalanced()) return false;
    const t = this.totals();
    return t.debit > 0 || t.credit > 0;
  });

  readonly hasUserInput = computed(() => {
    if (this.entryLabel().trim().length > 0) return true;
    return this.lines().some(l =>
      l.accountNumber.trim().length > 0 ||
      l.lineLabel.trim().length > 0 ||
      (l.debit ?? 0) > 0 ||
      (l.credit ?? 0) > 0
    );
  });

  readonly canSubmit = computed(() =>
    this.isBalanced() && !this.loading() && this.lines().length >= 2 && !this.periodClosed()
  );

  readonly canSaveAsTemplate = computed(() => {
    // Au moins 2 lignes avec un compte renseigné
    const linesWithAccount = this.lines().filter(l => l.accountNumber.trim().length > 0);
    return linesWithAccount.length >= 2;
  });

  /** Type de statut visuel d'une ligne par rapport à son compte. */
  getLineStatus(line: EntryLine): 'empty' | 'valid' | 'unknown' | 'inactive' {
    const acc = line.accountNumber.trim();
    if (!acc) return 'empty';
    const account = this.allAccounts.find(a => a.accountNumber === acc);
    if (!account) return 'unknown';
    if (!account.isActive) return 'inactive';
    return 'valid';
  }

  getLineStatusMessage(line: EntryLine): string {
    switch (this.getLineStatus(line)) {
      case 'unknown': return 'Compte inconnu dans le plan comptable';
      case 'inactive': return 'Compte désactivé — l\'enregistrement sera refusé';
      default: return '';
    }
  }

  /** Numéro de classe comptable (1-9) ou null si le compte est inconnu/vide. */
  getAccountClass(line: EntryLine): number | null {
    const acc = line.accountNumber.trim();
    if (!acc) return null;
    const account = this.allAccounts.find(a => a.accountNumber === acc);
    return account ? account.accountClass : null;
  }

  constructor() {
    // Auto-save brouillon — debouncing simple via timer.
    // L'effect ne déclenche pas la sauvegarde tant que l'utilisateur n'a pas
    // décidé du brouillon existant (Restaurer / Ignorer), pour éviter d'écraser
    // un brouillon en cours de chargement.
    let saveTimer: ReturnType<typeof setTimeout> | null = null;
    effect(() => {
      const jc = this.journalCode();
      const dt = this.entryDate();
      const lb = this.entryLabel();
      const ls = this.lines();
      if (!this.draftInitialized()) return;
      if (saveTimer) clearTimeout(saveTimer);
      saveTimer = setTimeout(() => {
        const hasInput = this.hasUserInput();
        if (!hasInput) {
          this.draftStore.clear();
          return;
        }
        this.draftStore.save({
          journalCode: jc,
          entryDate: dt,
          entryLabel: lb,
          lines: ls.map(l => ({
            accountNumber: l.accountNumber,
            lineLabel: l.lineLabel,
            debit: l.debit,
            credit: l.credit
          }))
        });
      }, 800);
    });
  }

  ngOnInit(): void {
    this.api.getChartOfAccounts()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: res => { if (res.success && res.data) this.allAccounts = res.data; }
      });

    // Charge le catalogue de journaux (actifs) ; conserve la liste figée en repli si vide/échec.
    this.api.getJournals()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: res => {
          if (res.success && res.data && res.data.length > 0) {
            this.journalOptions.set(res.data.map(j => ({ code: j.code, label: j.label })));
          }
        }
      });

    this.api.getPeriods()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: res => {
          if (res.success && res.data) this.periods.set(res.data);
          this.periodsLoaded.set(true);
        },
        error: () => this.periodsLoaded.set(true)
      });

    // Brouillon en attente : on attend la décision de l'utilisateur avant d'activer l'auto-save.
    const draft = this.draftStore.load();
    if (draft && this.draftStore.isMeaningful(draft)) {
      this.pendingDraft.set(draft);
    } else {
      this.draftStore.clear();
      this.draftInitialized.set(true);
    }
  }

  filterAccounts(event: { query: string }): void {
    const q = event.query.toLowerCase();
    this.accountSuggestions = this.allAccounts
      .filter(a => a.isActive && (a.accountNumber.toLowerCase().includes(q) || a.label.toLowerCase().includes(q)))
      .map(a => ({ number: a.accountNumber, label: a.label, display: `${a.accountNumber} — ${a.label}` }))
      .slice(0, 20);
  }

  onAccountSelect(event: { value: AccountSuggestion }, index: number): void {
    const l = this.lines();
    l[index].accountNumber = event.value.number;
    this.lines.set([...l]);
  }

  /** Recherche fusionnée clients + fournisseurs actifs pour la colonne « Tiers ». */
  filterThirdParties(event: { query: string }): void {
    const search = event.query.trim();
    if (search.length < 2) {
      this.thirdPartySuggestions = [];
      return;
    }
    forkJoin({
      clients: this.clientService.getClients({ search, isActive: true, page: 1, pageSize: 10, skipGlobalErrorUi: true })
        .pipe(catchError(() => of(null))),
      suppliers: this.supplierService.getSuppliers({ search, isActive: true, page: 1, pageSize: 10 })
        .pipe(catchError(() => of(null)))
    })
      .pipe(
        takeUntilDestroyed(this.destroyRef),
        map(({ clients, suppliers }) => {
          const out: ThirdPartySuggestion[] = [];
          for (const c of clients?.data?.items ?? []) {
            out.push({ id: c.id, kind: 1, name: c.name, display: `Client — ${c.name}` });
          }
          for (const s of suppliers?.data?.items ?? []) {
            out.push({ id: s.id, kind: 2, name: s.name, display: `Fournisseur — ${s.name}` });
          }
          return out;
        })
      )
      .subscribe(suggestions => (this.thirdPartySuggestions = suggestions));
  }

  addLine(): void {
    this.lines.update(l => [...l, { accountNumber: '', lineLabel: '', debit: null, credit: null, thirdParty: null }]);
  }

  removeLine(index: number): void {
    this.lines.update(l => l.filter((_, i) => i !== index));
  }

  duplicateLine(index: number): void {
    const lines = [...this.lines()];
    const source = lines[index];
    if (!source) return;
    const dup: EntryLine = {
      accountNumber: source.accountNumber,
      lineLabel: source.lineLabel,
      debit: null,
      credit: null,
      thirdParty: source.thirdParty
    };
    lines.splice(index + 1, 0, dup);
    this.lines.set(lines);
  }

  autoBalance(): void {
    const t = this.totals();
    const gap = Math.round((t.debit - t.credit) * 1000) / 1000;
    if (gap === 0) return;
    const absGap = Math.abs(gap);
    const lines = [...this.lines()];

    let targetIndex = -1;
    for (let i = lines.length - 1; i >= 0; i--) {
      const l = lines[i];
      if ((l.debit ?? 0) === 0 && (l.credit ?? 0) === 0) {
        targetIndex = i;
        break;
      }
    }

    if (targetIndex >= 0) {
      lines[targetIndex] = gap > 0
        ? { ...lines[targetIndex], credit: absGap, debit: null }
        : { ...lines[targetIndex], debit: absGap, credit: null };
    } else {
      lines.push(gap > 0
        ? { accountNumber: '', lineLabel: '', debit: null, credit: absGap, thirdParty: null }
        : { accountNumber: '', lineLabel: '', debit: absGap, credit: null, thirdParty: null });
    }
    this.lines.set(lines);
  }

  resetWithConfirm(): void {
    if (!this.hasUserInput()) {
      this.resetForm();
      return;
    }
    if (window.confirm('Voulez-vous vraiment vider le formulaire ? Toutes les saisies en cours seront perdues.')) {
      this.resetForm();
    }
  }

  private navigateToJournal(journalCode: string, date: string): void {
    this.router.navigate(['/accounting/journal'], {
      queryParams: {
        journalCode,
        from: date,
        to: date
      }
    });
  }

  // ---------------------------------------------------------------------------
  // Draft restoration (localStorage)
  // ---------------------------------------------------------------------------

  restoreDraft(): void {
    const draft = this.pendingDraft();
    if (!draft) return;
    this.journalCode.set(draft.journalCode || 'JOD');
    this.entryDate.set(draft.entryDate || formatLocalDate(new Date()));
    this.entryLabel.set(draft.entryLabel || '');
    const lines = draft.lines.map(l => ({
      accountNumber: l.accountNumber,
      lineLabel: l.lineLabel,
      debit: l.debit,
      credit: l.credit,
      thirdParty: null
    } satisfies EntryLine));
    while (lines.length < 2) {
      lines.push({ accountNumber: '', lineLabel: '', debit: null, credit: null, thirdParty: null });
    }
    this.lines.set(lines);
    this.pendingDraft.set(null);
    this.draftInitialized.set(true);
    this.toast.add({
      severity: 'info',
      summary: 'Brouillon restauré',
      detail: 'Votre saisie a été restaurée. Vérifiez les montants avant d\'enregistrer.',
      life: 4000
    });
  }

  discardDraft(): void {
    this.draftStore.clear();
    this.pendingDraft.set(null);
    this.draftInitialized.set(true);
  }

  // ---------------------------------------------------------------------------
  // Keyboard shortcuts
  // ---------------------------------------------------------------------------

  @HostListener('document:keydown', ['$event'])
  onKeydown(event: KeyboardEvent): void {
    // Esc ferme les modales ouvertes en priorité
    if (event.key === 'Escape') {
      if (this.showTemplatePicker()) {
        this.closeTemplatePicker();
        event.preventDefault();
        return;
      }
      if (this.showSaveTemplate()) {
        this.closeSaveTemplate();
        event.preventDefault();
        return;
      }
      return;
    }

    // Désactiver les raccourcis quand une modale est ouverte
    if (this.showTemplatePicker() || this.showSaveTemplate()) return;

    // Ctrl/Cmd + S → Enregistrer (avec ou sans Shift)
    if ((event.ctrlKey || event.metaKey) && (event.key === 's' || event.key === 'S')) {
      event.preventDefault();
      if (!this.canSubmit()) return;
      if (event.shiftKey) {
        this.submit('reset');
      } else {
        this.submit('navigate');
      }
      return;
    }

    // Alt + … : actions rapides
    if (event.altKey && !event.ctrlKey && !event.metaKey) {
      const key = event.key.toLowerCase();
      switch (key) {
        case 'l':
          event.preventDefault();
          this.addLine();
          break;
        case 'b':
          event.preventDefault();
          if (this.canAutoBalance()) this.autoBalance();
          break;
        case 'm':
          event.preventDefault();
          this.openTemplatePicker();
          break;
        case 'n':
          event.preventDefault();
          this.resetWithConfirm();
          break;
      }
    }
  }

  // ---------------------------------------------------------------------------
  // Templates: open/close modales, apply, save
  // ---------------------------------------------------------------------------

  openTemplatePicker(): void {
    this.showTemplatePicker.set(true);
  }

  closeTemplatePicker(): void {
    this.showTemplatePicker.set(false);
  }

  onTemplateSelected(template: JournalEntryTemplateDto): void {
    if (this.hasUserInput() && !window.confirm(
      `Charger le modèle « ${template.name} » va remplacer la saisie en cours. Voulez-vous continuer ?`
    )) {
      return;
    }
    this.applyTemplate(template);
  }

  private applyTemplate(template: JournalEntryTemplateDto): void {
    this.journalCode.set(template.journalCode || 'JOD');
    if (template.labelTemplate) {
      this.entryLabel.set(template.labelTemplate);
    }
    const lines = template.lines
      .slice()
      .sort((a, b) => a.lineNumber - b.lineNumber)
      .map(l => ({
        accountNumber: l.accountNumber,
        lineLabel: l.lineLabelTemplate ?? '',
        debit: l.fixedDebit ?? null,
        credit: l.fixedCredit ?? null,
        thirdParty: null
      } satisfies EntryLine));
    // Garantir au moins 2 lignes pour respecter la validation métier
    while (lines.length < 2) {
      lines.push({ accountNumber: '', lineLabel: '', debit: null, credit: null, thirdParty: null });
    }
    this.lines.set(lines);
    this.error.set(null);
    this.successMsg.set(null);
    this.toast.add({
      severity: 'info',
      summary: 'Modèle chargé',
      detail: `Le modèle « ${template.name} » a été chargé. Adaptez les montants au besoin.`,
      life: 4000
    });
  }

  openSaveTemplate(): void {
    if (!this.canSaveAsTemplate()) {
      this.toast.add({
        severity: 'warn',
        summary: 'Sauvegarde impossible',
        detail: 'Veuillez saisir au moins 2 lignes avec un numéro de compte avant de sauvegarder comme modèle.',
        life: 5000
      });
      return;
    }
    this.showSaveTemplate.set(true);
  }

  closeSaveTemplate(): void {
    this.showSaveTemplate.set(false);
  }

  onSaveAsTemplate(payload: SaveTemplatePayload): void {
    const linesWithAccount = this.lines().filter(l => l.accountNumber.trim().length > 0);
    if (linesWithAccount.length < 2) {
      this.toast.add({
        severity: 'error',
        summary: 'Sauvegarde impossible',
        detail: 'Au moins 2 lignes avec un numéro de compte sont requises.',
        life: 6000
      });
      return;
    }

    const request: CreateJournalEntryTemplateRequest = {
      name: payload.name,
      description: payload.description || null,
      journalCode: payload.journalCode,
      labelTemplate: payload.labelTemplate || null,
      lines: linesWithAccount.map((l, idx) => ({
        lineNumber: idx + 1,
        accountNumber: l.accountNumber.trim(),
        lineLabelTemplate: l.lineLabel.trim() || null,
        fixedDebit: payload.keepAmounts ? (l.debit ?? null) : null,
        fixedCredit: payload.keepAmounts ? (l.credit ?? null) : null
      }))
    };

    this.savingTemplate.set(true);
    this.api.createJournalTemplate(request)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: res => {
          this.savingTemplate.set(false);
          if (res.success) {
            this.closeSaveTemplate();
            this.toast.add({
              severity: 'success',
              summary: 'Modèle sauvegardé',
              detail: `Le modèle « ${payload.name} » est désormais disponible dans « Charger un modèle ».`,
              life: 5000
            });
          } else {
            this.toast.add({
              severity: 'error',
              summary: 'Échec de la sauvegarde',
              detail: res.error ?? 'Erreur lors de la sauvegarde du modèle.',
              life: 6000
            });
          }
        },
        error: () => {
          this.savingTemplate.set(false);
          this.toast.add({
            severity: 'error',
            summary: 'Erreur réseau',
            detail: 'Impossible de sauvegarder le modèle.',
            life: 6000
          });
        }
      });
  }

  onDebitChange(index: number): void {
    const l = this.lines();
    if ((l[index].debit ?? 0) > 0) {
      l[index].credit = null;
      this.lines.set([...l]);
    }
  }

  onCreditChange(index: number): void {
    const l = this.lines();
    if ((l[index].credit ?? 0) > 0) {
      l[index].debit = null;
      this.lines.set([...l]);
    }
  }

  submit(afterSuccess: 'reset' | 'navigate' = 'navigate'): void {
    this.error.set(null);
    this.successMsg.set(null);

    const label = this.entryLabel().trim();
    if (!label) {
      this.error.set("Le libellé de l'écriture est obligatoire.");
      return;
    }

    const linesWithAmount = this.lines().filter(l => {
      const d = Number(l.debit) || 0;
      const c = Number(l.credit) || 0;
      return l.accountNumber.trim() && (d > 0 || c > 0);
    });
    if (linesWithAmount.length < 2) {
      this.error.set('Au moins deux lignes avec un compte renseigné et un montant au débit ou au crédit sont requises.');
      return;
    }

    const invalidLine = linesWithAmount.find(l => {
      const d = Number(l.debit) || 0;
      const c = Number(l.credit) || 0;
      return d > 0 && c > 0;
    });
    if (invalidLine) {
      this.error.set("Chaque ligne ne doit avoir qu'un montant au débit ou au crédit, pas les deux.");
      return;
    }

    const journalCode = this.journalCode();
    const entryDate = this.entryDate();
    const request: CreateManualJournalEntryRequest = {
      journalCode,
      entryDate,
      label,
      pieceRef: this.pieceRef().trim() || null,
      pieceDate: this.pieceDate() || null,
      lines: linesWithAmount.map(l => {
        // forceSelection garantit un objet sélectionné ; toute saisie libre est ramenée à null.
        const tp = l.thirdParty && typeof l.thirdParty === 'object' ? l.thirdParty : null;
        return {
          accountNumber: l.accountNumber.trim(),
          lineLabel: l.lineLabel || label,
          debit: Number(l.debit) || 0,
          credit: Number(l.credit) || 0,
          thirdPartyId: tp?.id ?? null,
          thirdPartyKind: tp?.kind ?? null
        };
      })
    };

    this.loading.set(true);
    this.api.createManualJournalEntry(request)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: res => {
          this.loading.set(false);
          if (res.success) {
            this.successMsg.set('Écriture enregistrée avec succès.');
            this.toast.add({
              severity: 'success',
              summary: 'Écriture enregistrée',
              detail: `L'écriture du journal ${journalCode} a été enregistrée.${afterSuccess === 'reset' ? ' Le formulaire est prêt pour une nouvelle saisie.' : ' Redirection vers le journal…'}`,
              life: 5000
            });
            this.draftStore.clear();
            this.resetForm();
            if (afterSuccess === 'navigate') {
              this.navigateToJournal(journalCode, entryDate);
            }
          } else {
            this.error.set(res.error ?? "Erreur lors de l'enregistrement.");
          }
        },
        error: err => {
          this.loading.set(false);
          this.error.set(this.errorHandler.extractErrorMessage(err));
        }
      });
  }

  private resetForm(): void {
    this.entryLabel.set('');
    this.pieceRef.set('');
    this.pieceDate.set('');
    this.lines.set([
      { accountNumber: '', lineLabel: '', debit: null, credit: null, thirdParty: null },
      { accountNumber: '', lineLabel: '', debit: null, credit: null, thirdParty: null }
    ]);
    this.error.set(null);
    this.successMsg.set(null);
  }
}
