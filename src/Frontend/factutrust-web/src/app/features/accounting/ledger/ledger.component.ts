import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { finalize } from 'rxjs/operators';
import { TableModule } from 'primeng/table';
import { ToolbarModule } from 'primeng/toolbar';
import { CardModule } from 'primeng/card';
import { ProgressSpinnerModule } from 'primeng/progressspinner';
import { AutoCompleteModule } from 'primeng/autocomplete';
import { PageHeaderComponent } from '@shared/components/page-header/page-header.component';
import { EmptyStateComponent } from '@shared/components/empty-state/empty-state.component';
import { AccountingService, LedgerRowDto, ChartOfAccountDto } from '../services/accounting.service';
import { AccountingStatusBannerComponent } from '../shared/accounting-status-banner.component';
import { AccountingToolbarActionsComponent } from '../shared/accounting-toolbar-actions.component';
import { ButtonComponent } from '@shared/components/button/button.component';
import { AnalyzeWithAiButtonComponent } from '@features/ai-assistant/components/analyze-with-ai-button/analyze-with-ai-button.component';
import {
  buildScreenAnalysisPayloadV2,
  detectBasicAnomalies,
  sampleRowsSmart
} from '@features/ai-assistant/utils/ai-screen-payload.factory';
import {
  firstDayOfYearLocalYmd,
  parseLocalDateString,
  todayLocalYmd,
  validateDateRange
} from '../shared/accounting-date-utils';
import { AccountingExportMenuComponent } from '../shared/accounting-export-menu.component';
import { AccountingExportFormat, downloadBlob, exportExtension } from '../shared/accounting-download.util';

@Component({
  selector: 'app-accounting-ledger',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    TableModule,
    ToolbarModule,
    ButtonComponent,
    AccountingToolbarActionsComponent,
    CardModule,
    ProgressSpinnerModule,
    AutoCompleteModule,
    PageHeaderComponent,
    EmptyStateComponent,
    AccountingStatusBannerComponent,
    AnalyzeWithAiButtonComponent,
    AccountingExportMenuComponent
  ],
  templateUrl: './ledger.component.html',
  styleUrl: './ledger.component.scss'
})
export class LedgerComponent implements OnInit {
  private readonly api = inject(AccountingService);
  private readonly route = inject(ActivatedRoute);
  account = '4111';
  fromStr = '';
  toStr = '';
  accountSuggestions: string[] = [];
  private allAccounts: ChartOfAccountDto[] = [];
  readonly rows = signal<LedgerRowDto[]>([]);
  readonly error = signal<string | null>(null);
  readonly loading = signal(true);
  readonly exporting = signal(false);

  readonly showEmpty = computed(
    () => this.rows().length === 0 && !this.loading() && !this.error()
  );

  readonly totals = computed(() => {
    let debit = 0, credit = 0;
    for (const r of this.rows()) {
      debit += r.debit;
      credit += r.credit;
    }
    const lastRow = this.rows().at(-1);
    return { debit, credit, balance: lastRow?.runningBalance ?? 0 };
  });

  constructor() {
    this.fromStr = firstDayOfYearLocalYmd();
    this.toStr = todayLocalYmd();
  }

  ngOnInit(): void {
    // Accept query parameters from balance drill-down
    const params = this.route.snapshot.queryParams;
    if (params['account']) this.account = params['account'];
    if (params['from']) this.fromStr = params['from'];
    if (params['to']) this.toStr = params['to'];

    this.api.getChartOfAccounts().subscribe({
      next: res => {
        if (res.success && res.data) this.allAccounts = res.data;
      }
    });
    this.load();
  }

  readonly buildLedgerAnalyzePayload = (): unknown => {
    const allRows = this.rows();
    const totals = this.totals();
    const { rows, sampling } = sampleRowsSmart(
      allRows,
      200,
      'anomaly-first',
      r => Math.max(r.debit, r.credit)
    );
    const imbalance = Math.abs(totals.debit - totals.credit);
    const avgAmount =
      allRows.length > 0
        ? allRows.reduce((s, r) => s + Math.max(r.debit, r.credit), 0) / allRows.length
        : 0;
    const threshold = Math.max(avgAmount * 3, 1000);

    return buildScreenAnalysisPayloadV2({
      screenId: 'accounting-ledger',
      filters: {
        account: this.account || null,
        from: this.fromStr || null,
        to: this.toStr || null
      },
      summary: {
        totalRows: allRows.length,
        totalDebit: totals.debit,
        totalCredit: totals.credit,
        finalBalance: totals.balance,
        debitCreditGap: imbalance
      },
      highlights: [
        ...(imbalance > 0.001
          ? [{ type: 'warning' as const, label: 'Écart débit/crédit', value: imbalance, context: 'Vérifier l\'équilibre' }]
          : []),
        ...detectBasicAnomalies(
          rows.map(r => ({ label: r.label, debit: r.debit, credit: r.credit })),
          threshold
        )
      ],
      rows: rows.map(r => ({
        entryDate: r.entryDate,
        journalCode: r.journalCode,
        pieceNumber: r.pieceNumber,
        label: r.label,
        debit: r.debit,
        credit: r.credit,
        runningBalance: r.runningBalance
      })),
      sampling,
      dataQuality: {
        hasData: allRows.length > 0,
        isPartial: sampling.truncated,
        warnings: sampling.truncated ? [`Échantillon de ${sampling.included} lignes sur ${sampling.totalAvailable}`] : []
      }
    });
  };

  filterAccounts(event: { query: string }): void {
    const q = event.query.toLowerCase();
    this.accountSuggestions = this.allAccounts
      .filter(a => a.accountNumber.toLowerCase().includes(q) || a.label.toLowerCase().includes(q))
      .map(a => a.accountNumber)
      .slice(0, 20);
  }

  load(): void {
    if (!this.account.trim()) {
      this.loading.set(false);
      this.rows.set([]);
      this.error.set('Saisissez un numéro de compte pour afficher le grand livre.');
      return;
    }
    const from = parseLocalDateString(this.fromStr);
    const to = parseLocalDateString(this.toStr);
    const vr = validateDateRange(from, to);
    if (!vr.valid) {
      this.loading.set(false);
      this.error.set(vr.message ?? 'Période invalide.');
      return;
    }
    this.error.set(null);
    this.loading.set(true);
    this.api
      .getLedger(this.account.trim(), from, to)
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: res => {
          if (res.success && res.data) {
            this.rows.set(res.data);
            this.error.set(null);
          } else {
            this.error.set(res.error ?? 'Erreur');
          }
        },
        error: () => this.error.set('Erreur réseau')
      });
  }

  onExport(format: AccountingExportFormat): void {
    const account = this.account.trim();
    if (!account) {
      this.error.set('Saisissez un numéro de compte avant d\'exporter.');
      return;
    }
    const from = parseLocalDateString(this.fromStr);
    const to = parseLocalDateString(this.toStr);
    const vr = validateDateRange(from, to);
    if (!vr.valid) {
      this.error.set(vr.message ?? 'Période invalide.');
      return;
    }
    this.exporting.set(true);
    this.api.exportLedger(account, from, to, format).subscribe({
      next: blob => {
        this.exporting.set(false);
        downloadBlob(blob, `grand_livre_${account}_${this.fromStr}_${this.toStr}.${exportExtension(format)}`);
      },
      error: () => {
        this.exporting.set(false);
        this.error.set("Erreur lors de l'export.");
      }
    });
  }
}
