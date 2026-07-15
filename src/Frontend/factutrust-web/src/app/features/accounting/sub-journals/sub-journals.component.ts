import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { TableModule } from 'primeng/table';
import { PageHeaderComponent } from '@shared/components/page-header/page-header.component';
import { AccountingService, JournalEntryDto } from '../services/accounting.service';
import { AccountingStatusBannerComponent } from '../shared/accounting-status-banner.component';
import { AnalyzeWithAiButtonComponent } from '@features/ai-assistant/components/analyze-with-ai-button/analyze-with-ai-button.component';
import { wrapLegacyAnalyzePayload } from '@features/ai-assistant/utils/ai-screen-payload.factory';
import { ButtonComponent } from '@shared/components/button/button.component';
import {
  firstDayOfMonthLocalYmd,
  parseLocalDateString,
  todayLocalYmd,
  validateDateRange
} from '../shared/accounting-date-utils';
import { AccountingJournalTabsComponent } from '../shared/accounting-journal-tabs.component';
import {
  ACCOUNTING_SUB_JOURNALS,
  AccountingJournalTabChange
} from '../shared/accounting-journal-tabs.model';

type JournalFlatRow = {
  date: string;
  journal: string;
  piece: number;
  label: string;
  account: string;
  debit: number;
  credit: number;
};

@Component({
  selector: 'app-sub-journals',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    TableModule,
    PageHeaderComponent,
    AccountingStatusBannerComponent,
    AnalyzeWithAiButtonComponent,
    ButtonComponent,
    AccountingJournalTabsComponent
  ],
  template: `
    <app-page-header title="Journaux auxiliaires" subtitle="Consultation par journal" />

    <div class="card sub-journals-filters-card">
      <div class="sub-journals-toolbar">
        <div class="form-field sub-journals-field-date">
          <label class="field-label" for="sj-from">Du</label>
          <input
            id="sj-from"
            type="date"
            [(ngModel)]="fromStr"
            (ngModelChange)="onDatesChange()"
            class="sub-journals-date-input"
            [disabled]="loading()" />
        </div>
        <div class="form-field sub-journals-field-date">
          <label class="field-label" for="sj-to">Au</label>
          <input
            id="sj-to"
            type="date"
            [(ngModel)]="toStr"
            (ngModelChange)="onDatesChange()"
            class="sub-journals-date-input"
            [disabled]="loading()" />
        </div>
        <div class="sub-journals-toolbar-actions">
          <app-analyze-with-ai-button
            screenId="accounting-sub-journals"
            [payloadBuilder]="buildSubJournalsAnalyzePayload"
            [disabled]="loading()" />
        </div>
      </div>
    </div>

    <app-accounting-journal-tabs
      [tabs]="tabs"
      [activeIndex]="activeTabIndex"
      (tabChange)="onJournalTabChange($event)" />

    <div
      class="sub-journals-panel"
      role="tabpanel"
      [attr.id]="'sub-journal-panel-' + activeJournalCode"
      [attr.aria-labelledby]="'accounting-journal-tab-' + activeJournalCode">
      <div class="accounting-kpi-row" aria-label="Totaux du journal">
        <div class="accounting-kpi-item">
          <span class="accounting-kpi-label">Total débit</span>
          <span class="accounting-kpi-value">{{ tabTotals().debit | number : '1.3-3' }}</span>
        </div>
        <div class="accounting-kpi-item">
          <span class="accounting-kpi-label">Total crédit</span>
          <span class="accounting-kpi-value">{{ tabTotals().credit | number : '1.3-3' }}</span>
        </div>
      </div>

      <app-accounting-status-banner
        variant="error"
        [message]="error() ?? ''"
        [showRetry]="!!error()"
        retryLabel="Réessayer"
        (retry)="loadJournal(activeJournalCode)" />

      <p-table
        [value]="flatRows()"
        [paginator]="true"
        [rows]="20"
        [rowsPerPageOptions]="[20, 50, 100]"
        [loading]="loading()"
        [rowHover]="true"
        [showCurrentPageReport]="true"
        currentPageReportTemplate="Affichage de {first} à {last} sur {totalRecords} lignes"
        styleClass="p-datatable-sm sub-journals-table accounting-datatable">
        <ng-template pTemplate="header">
          <tr>
            <th scope="col">Date</th>
            <th scope="col">Journal</th>
            <th scope="col" class="sub-journals-col-narrow">N°</th>
            <th scope="col">Libellé</th>
            <th scope="col" class="sub-journals-col-account">Compte</th>
            <th scope="col" class="sub-journals-col-amount">Débit</th>
            <th scope="col" class="sub-journals-col-amount">Crédit</th>
          </tr>
        </ng-template>
        <ng-template pTemplate="body" let-r>
          <tr>
            <td data-label="Date">{{ r.date | date : 'shortDate' }}</td>
            <td data-label="Journal">{{ r.journal }}</td>
            <td class="sub-journals-col-narrow sub-journals-cell-num" data-label="N°">{{ r.piece }}</td>
            <td data-label="Libellé">{{ r.label }}</td>
            <td class="sub-journals-col-account" data-label="Compte">
              <span class="sub-journals-account-code">{{ r.account }}</span>
            </td>
            <td class="sub-journals-col-amount" data-label="Débit">{{ r.debit | number : '1.3-3' }}</td>
            <td class="sub-journals-col-amount" data-label="Crédit">{{ r.credit | number : '1.3-3' }}</td>
          </tr>
        </ng-template>
        <ng-template pTemplate="emptymessage">
          <tr>
            <td colspan="7" class="sub-journals-empty">
              <p class="sub-journals-empty-title">Aucune écriture</p>
              <p class="sub-journals-empty-hint">
                Aucune écriture trouvée pour le journal {{ activeJournalCode }} sur la période sélectionnée.
              </p>
            </td>
          </tr>
        </ng-template>
      </p-table>
    </div>
  `,
  styles: `
    @import '../shared/accounting-layout';

    .sub-journals-filters-card {
      padding: var(--spacing-5);
      border-radius: var(--radius-lg);
      box-shadow: var(--shadow-sm, 0 1px 3px rgba(15, 23, 42, 0.08));
      margin-bottom: var(--spacing-4);
    }
    .sub-journals-toolbar {
      display: flex;
      flex-wrap: wrap;
      align-items: flex-end;
      gap: var(--spacing-4);
    }
    .form-field {
      display: flex;
      flex-direction: column;
      gap: var(--spacing-2);
      min-width: 0;
    }
    .sub-journals-field-date { flex: 0 1 auto; }
    .sub-journals-toolbar-actions { display: flex; align-items: flex-end; padding-bottom: 2px; }
    .field-label {
      font-size: var(--font-size-sm);
      font-weight: var(--font-weight-semibold);
      color: var(--color-text-primary);
      margin: 0;
    }
    .sub-journals-date-input {
      padding: var(--spacing-2) var(--spacing-3);
      border: 1px solid var(--color-border-default);
      border-radius: var(--radius-md);
      background: var(--color-background-elevated);
      color: var(--color-text-primary);
      font-size: var(--font-size-sm);
      min-height: 2.5rem;
    }
    .sub-journals-date-input:focus {
      outline: none;
      border-color: var(--color-primary-500);
      box-shadow: 0 0 0 3px var(--color-primary-200);
    }
    .sub-journals-date-input:disabled {
      opacity: 0.65;
      cursor: not-allowed;
    }
    .sub-journals-panel {
      padding-top: var(--spacing-2);
    }
    .sub-journals-col-narrow { width: 1%; white-space: nowrap; }
    .sub-journals-col-account { width: 1%; white-space: nowrap; }
    .sub-journals-account-code {
      font-family: ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, monospace;
      font-variant-numeric: tabular-nums;
      font-weight: var(--font-weight-medium);
    }
    .sub-journals-col-amount {
      text-align: right;
      font-variant-numeric: tabular-nums;
    }
    .sub-journals-cell-num {
      font-variant-numeric: tabular-nums;
      text-align: right;
    }
    .sub-journals-empty {
      text-align: center;
      padding: var(--spacing-8) var(--spacing-4) !important;
      border: none !important;
    }
    .sub-journals-empty-title {
      margin: 0 0 var(--spacing-2);
      font-weight: var(--font-weight-semibold);
      color: var(--color-text-primary);
    }
    .sub-journals-empty-hint {
      margin: 0;
      font-size: var(--font-size-sm);
      color: var(--color-text-secondary);
    }
    :host ::ng-deep .sub-journals-table .p-datatable-thead > tr > th {
      text-transform: uppercase;
      font-size: var(--font-size-xs);
      letter-spacing: 0.04em;
      color: var(--color-text-tertiary);
      background: var(--color-background-subtle);
    }
  `
})
export class SubJournalsComponent implements OnInit {
  private readonly api = inject(AccountingService);

  readonly tabs = ACCOUNTING_SUB_JOURNALS;
  fromStr = '';
  toStr = '';
  activeTabIndex = 0;

  readonly error = signal<string | null>(null);
  readonly loading = signal(false);
  readonly flatRows = signal<JournalFlatRow[]>([]);

  readonly tabTotals = computed(() => {
    let debit = 0;
    let credit = 0;
    for (const r of this.flatRows()) {
      debit += Number(r.debit);
      credit += Number(r.credit);
    }
    return { debit, credit };
  });

  get activeJournalCode(): string {
    return this.tabs[this.activeTabIndex]?.code ?? 'JV';
  }

  readonly buildSubJournalsAnalyzePayload = (): unknown =>
    wrapLegacyAnalyzePayload(
      'accounting-sub-journals',
      {
        screen: 'accounting-sub-journals',
        filters: {
          from: this.fromStr || null,
          to: this.toStr || null,
          activeJournal: this.tabs[this.activeTabIndex]?.code ?? null,
          activeJournalLabel: this.tabs[this.activeTabIndex]?.label ?? null
        },
        summary: {
          totalRows: this.flatRows().length,
          totalDebit: this.tabTotals().debit,
          totalCredit: this.tabTotals().credit
        },
        rows: this.flatRows().slice(0, 200).map(r => ({
          date: r.date,
          journal: r.journal,
          piece: r.piece,
          label: r.label,
          account: r.account,
          debit: r.debit,
          credit: r.credit
        }))
      } as Record<string, unknown>
    );

  ngOnInit(): void {
    this.fromStr = firstDayOfMonthLocalYmd();
    this.toStr = todayLocalYmd();
    this.loadJournal(this.tabs[0].code);
  }

  onJournalTabChange(event: AccountingJournalTabChange): void {
    if (event.index === this.activeTabIndex) {
      return;
    }
    this.activeTabIndex = event.index;
    this.loadJournal(event.code);
  }

  onDatesChange(): void {
    this.loadJournal(this.tabs[this.activeTabIndex].code);
  }

  loadJournal(journalCode: string): void {
    this.error.set(null);
    const from = parseLocalDateString(this.fromStr);
    const to = parseLocalDateString(this.toStr);
    const vr = validateDateRange(from, to);
    if (!vr.valid) {
      this.error.set(vr.message ?? 'Période invalide.');
      return;
    }
    this.loading.set(true);

    this.api.getJournal(journalCode, from, to).subscribe({
      next: res => {
        this.loading.set(false);
        if (!res.success || !res.data) {
          this.error.set(res.error ?? 'Erreur');
          return;
        }
        this.flatRows.set(this.flatten(res.data));
      },
      error: () => {
        this.loading.set(false);
        this.error.set('Erreur réseau');
      }
    });
  }

  private flatten(entries: JournalEntryDto[]): JournalFlatRow[] {
    const flat: JournalFlatRow[] = [];
    for (const e of entries) {
      for (const l of e.lines) {
        flat.push({
          date: e.entryDate,
          journal: e.journalCode,
          piece: e.entryNumber,
          label: l.label?.trim() ? l.label : e.label,
          account: l.accountNumber,
          debit: l.debit,
          credit: l.credit
        });
      }
    }
    return flat;
  }
}
