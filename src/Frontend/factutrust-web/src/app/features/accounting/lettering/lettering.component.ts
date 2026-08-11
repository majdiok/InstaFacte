import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { finalize } from 'rxjs/operators';
import { TableModule } from 'primeng/table';
import { ButtonModule } from 'primeng/button';
import { ToolbarModule } from 'primeng/toolbar';
import { CardModule } from 'primeng/card';
import { ProgressSpinnerModule } from 'primeng/progressspinner';
import { AutoCompleteModule } from 'primeng/autocomplete';
import { CheckboxModule } from 'primeng/checkbox';
import { TagModule } from 'primeng/tag';
import { PageHeaderComponent } from '@shared/components/page-header/page-header.component';
import { EmptyStateComponent } from '@shared/components/empty-state/empty-state.component';
import {
  AccountingService,
  JournalEntryDto,
  JournalEntryLineDto,
  ChartOfAccountDto
} from '../services/accounting.service';
import {
  firstDayOfYearLocalYmd,
  parseLocalDateString,
  todayLocalYmd,
  validateDateRange
} from '../shared/accounting-date-utils';
import { AnalyzeWithAiButtonComponent } from '@features/ai-assistant/components/analyze-with-ai-button/analyze-with-ai-button.component';
import { wrapLegacyAnalyzePayload } from '@features/ai-assistant/utils/ai-screen-payload.factory';
import { AccountingToolbarActionsComponent } from '../shared/accounting-toolbar-actions.component';
import { ButtonComponent } from '@shared/components/button/button.component';
import { AccountingCorrectionBannerComponent } from '../shared/accounting-correction-banner.component';

interface LetteringLine {
  lineId: string;
  entryDate: string;
  journalCode: string;
  entryNumber: number;
  accountNumber: string;
  label: string;
  debit: number;
  credit: number;
  letteringCode?: string | null;
}

@Component({
  selector: 'app-lettering',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    TableModule,
    ButtonModule,
    ToolbarModule,
    CardModule,
    ProgressSpinnerModule,
    AutoCompleteModule,
    CheckboxModule,
    TagModule,
    PageHeaderComponent,
    EmptyStateComponent,
    AnalyzeWithAiButtonComponent,
    AccountingToolbarActionsComponent,
    ButtonComponent,
    AccountingCorrectionBannerComponent
  ],
  templateUrl: './lettering.component.html',
  styleUrl: './lettering.component.scss'
})
export class LetteringComponent implements OnInit {
  private readonly api = inject(AccountingService);
  private readonly route = inject(ActivatedRoute);

  account = '';
  fromStr = '';
  toStr = '';
  accountHint = '';
  unletteredOnly = false;
  accountSuggestions: string[] = [];
  private allAccounts: ChartOfAccountDto[] = [];

  readonly lines = signal<LetteringLine[]>([]);
  readonly selectedLines = signal<LetteringLine[]>([]);
  readonly loading = signal(false);
  readonly lettering = signal(false);
  readonly error = signal<string | null>(null);
  readonly successMessage = signal<string | null>(null);
  /** Code du groupe en cours de délettrage (désactive son bouton). */
  readonly unlettering = signal<string | null>(null);
  /** Lettrage partiel demandé explicitement (sélection non équilibrée). */
  allowPartial = false;

  readonly showEmpty = computed(
    () => this.lines().length === 0 && !this.loading() && !this.error()
  );

  readonly selectionSummary = computed(() => {
    const sel = this.selectedLines();
    let debit = 0;
    let credit = 0;
    for (const l of sel) {
      debit += l.debit;
      credit += l.credit;
    }
    const gap = Math.round((debit - credit) * 1000) / 1000;
    return { count: sel.length, debit, credit, gap, balanced: sel.length >= 2 && gap === 0 };
  });

  constructor() {
    this.fromStr = firstDayOfYearLocalYmd();
    this.toStr = todayLocalYmd();
  }

  ngOnInit(): void {
    const qp = this.route.snapshot.queryParamMap;
    const accountParam = qp.get('account');
    const fromParam = qp.get('from');
    const toParam = qp.get('to');
    if (accountParam) this.account = accountParam;
    if (fromParam) this.fromStr = fromParam;
    if (toParam) this.toStr = toParam;
    this.unletteredOnly = qp.get('unletteredOnly') === '1';
    this.accountHint = qp.get('accountHint') ?? '';

    this.api.getChartOfAccounts().subscribe({
      next: res => {
        if (res.success && res.data) this.allAccounts = res.data;
      }
    });

    if (qp.get('autoLoad') === '1' && this.account.trim()) {
      this.load();
    }
  }

  readonly buildLetteringAnalyzePayload = (): unknown =>
    wrapLegacyAnalyzePayload(
      'accounting-lettering',
      {
        screen: 'accounting-lettering',
        filters: {
          account: this.account || null,
          from: this.fromStr || null,
          to: this.toStr || null
        },
        summary: {
          totalLines: this.lines().length,
          selectedCount: this.selectedLines().length,
          selectionBalanced: this.selectionSummary().balanced
        },
        lines: this.lines().slice(0, 200).map(l => ({
          entryDate: l.entryDate,
          label: l.label,
          debit: l.debit,
          credit: l.credit,
          letteringCode: l.letteringCode ?? null
        }))
      } as Record<string, unknown>,
      { rowsKey: 'lines' }
    );

  filterAccounts(event: { query: string }): void {
    const q = event.query.toLowerCase();
    this.accountSuggestions = this.allAccounts
      .filter(a => a.accountNumber.toLowerCase().includes(q) || a.label.toLowerCase().includes(q))
      .map(a => a.accountNumber)
      .slice(0, 20);
  }

  load(): void {
    const acct = this.account.trim();
    if (!acct) {
      this.error.set('Veuillez saisir un numéro de compte.');
      return;
    }
    this.error.set(null);
    this.successMessage.set(null);
    const from = parseLocalDateString(this.fromStr);
    const to = parseLocalDateString(this.toStr);
    const vr = validateDateRange(from, to);
    if (!vr.valid) {
      this.error.set(vr.message ?? 'Période invalide.');
      return;
    }
    this.loading.set(true);
    this.selectedLines.set([]);

    this.api
      .getJournal(undefined, from, to)
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: res => {
          if (res.success && res.data) {
            let result = this.extractLinesForAccount(res.data, acct);
            if (this.unletteredOnly) {
              result = result.filter(l => !l.letteringCode);
            }
            this.lines.set(result);
          } else {
            this.error.set(res.error ?? 'Erreur lors du chargement des écritures.');
          }
        },
        error: () => this.error.set('Erreur réseau. Vérifiez votre connexion.')
      });
  }

  onSelectionChange(selected: LetteringLine[]): void {
    this.selectedLines.set(selected);
    this.successMessage.set(null);
  }

  /** Lettrage possible : ≥ 2 lignes, équilibrées OU partiel explicitement coché. */
  canLetter(): boolean {
    const s = this.selectionSummary();
    return s.count >= 2 && (s.balanced || this.allowPartial);
  }

  letterSelected(): void {
    if (!this.canLetter()) return;
    const ids = this.selectedLines().map(l => l.lineId);
    const partial = !this.selectionSummary().balanced && this.allowPartial;

    this.lettering.set(true);
    this.error.set(null);
    this.successMessage.set(null);

    this.api
      .letterEntries(ids, partial)
      .pipe(finalize(() => this.lettering.set(false)))
      .subscribe({
        next: res => {
          if (res.success) {
            this.successMessage.set(partial
              ? `Lettrage partiel effectué sur ${ids.length} lignes (code « P »).`
              : `Lettrage effectué avec succès sur ${ids.length} lignes.`);
            this.selectedLines.set([]);
            this.allowPartial = false;
            this.load();
          } else {
            this.error.set(res.error ?? 'Erreur lors du lettrage.');
          }
        },
        error: () => this.error.set('Erreur réseau lors du lettrage.')
      });
  }

  /** Délettrage d'un groupe : libère toutes ses lignes (un partiel se complète ainsi). */
  unletter(code: string): void {
    if (!code) return;
    if (!window.confirm(`Délettrer le groupe ${code} ? Toutes ses lignes redeviendront lettrables.`)) return;

    this.unlettering.set(code);
    this.error.set(null);
    this.successMessage.set(null);

    this.api
      .unletterEntries(code)
      .pipe(finalize(() => this.unlettering.set(null)))
      .subscribe({
        next: res => {
          if (res.success) {
            this.successMessage.set(`Groupe ${code} délettré.`);
            this.selectedLines.set([]);
            this.load();
          } else {
            this.error.set(res.error ?? 'Erreur lors du délettrage.');
          }
        },
        error: () => this.error.set('Erreur réseau lors du délettrage.')
      });
  }

  isLettered(line: LetteringLine): boolean {
    return !!line.letteringCode;
  }

  /** Code de lettrage partiel (préfixe « P ») vs définitif (« L »). */
  isPartialCode(code: string | null | undefined): boolean {
    return !!code && code.startsWith('P');
  }

  private extractLinesForAccount(entries: JournalEntryDto[], accountNumber: string): LetteringLine[] {
    const result: LetteringLine[] = [];
    for (const entry of entries) {
      for (const line of entry.lines) {
        if (line.accountNumber === accountNumber) {
          result.push({
            lineId: line.id,
            entryDate: entry.entryDate,
            journalCode: entry.journalCode,
            entryNumber: entry.entryNumber,
            accountNumber: line.accountNumber,
            label: line.label || entry.label,
            debit: line.debit,
            credit: line.credit,
            letteringCode: line.letteringCode ?? null
          });
        }
      }
    }
    result.sort((a, b) => a.entryDate.localeCompare(b.entryDate));
    return result;
  }
}
