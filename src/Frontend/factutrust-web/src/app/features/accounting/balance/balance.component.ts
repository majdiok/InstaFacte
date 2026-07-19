import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { TableModule } from 'primeng/table';
import { TabViewModule } from 'primeng/tabview';
import { PageHeaderComponent } from '@shared/components/page-header/page-header.component';
import { AccountingService, BalanceRowDto } from '../services/accounting.service';
import { AccountingStatusBannerComponent } from '../shared/accounting-status-banner.component';
import { AccountingFilterBarComponent } from '../shared/accounting-filter-bar.component';
import { AnalyzeWithAiButtonComponent } from '@features/ai-assistant/components/analyze-with-ai-button/analyze-with-ai-button.component';
import { wrapLegacyAnalyzePayload } from '@features/ai-assistant/utils/ai-screen-payload.factory';
import { ButtonComponent } from '@shared/components/button/button.component';
import { TooltipModule } from 'primeng/tooltip';
import {
  firstDayOfMonthLocalYmd,
  parseLocalDateString,
  todayLocalYmd,
  validateDateRange
} from '../shared/accounting-date-utils';

@Component({
  selector: 'app-accounting-balance',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    RouterModule,
    TableModule,
    TabViewModule,
    PageHeaderComponent,
    AccountingStatusBannerComponent,
    AccountingFilterBarComponent,
    AnalyzeWithAiButtonComponent,
    ButtonComponent,
    TooltipModule
  ],
  template: `
    <app-page-header title="Balance" subtitle="Balance générale — 8 colonnes avec soldes d'ouverture" />
    <div class="card balance-filters-card accounting-filters-card">
      <app-accounting-filter-bar ariaLabel="Période et filtre de classe">
        <div accountingFilterFields class="balance-toolbar-fields">
          <div class="accounting-filter-field">
            <label class="accounting-filter-label" for="bal-from">Du</label>
            <input id="bal-from" type="date" [(ngModel)]="fromStr" class="accounting-filter-input" />
          </div>
          <div class="accounting-filter-field">
            <label class="accounting-filter-label" for="bal-to">Au</label>
            <input id="bal-to" type="date" [(ngModel)]="toStr" class="accounting-filter-input" />
          </div>
          <div class="accounting-filter-field">
            <label class="accounting-filter-label" for="bal-class">Classe</label>
            <select id="bal-class" [(ngModel)]="filterClass" class="accounting-filter-input">
              <option [ngValue]="0">Toutes</option>
              <option *ngFor="let c of [1,2,3,4,5,6,7]" [ngValue]="c">{{ c }}</option>
            </select>
          </div>
        </div>
        <div accountingFilterActions>
          <app-button
            variant="secondary"
            icon="pi pi-refresh"
            iconPos="left"
            type="button"
            (click)="load()"
            [disabled]="loading()"
            ariaLabel="Actualiser la balance pour la période sélectionnée">
            Actualiser
          </app-button>
          <app-analyze-with-ai-button
            screenId="accounting-balance"
            density="toolbar"
            [payloadBuilder]="buildBalanceAnalyzePayload"
            [disabled]="loading()" />
        </div>
      </app-accounting-filter-bar>
    </div>
    <app-accounting-status-banner
      variant="error"
      [message]="error() ?? ''"
      [showRetry]="!!error()"
      retryLabel="Réessayer"
      (retry)="load()" />
    <p-tabView>
      <p-tabPanel header="Générale">
        <p-table [value]="filteredRows()" [paginator]="true" [rows]="25" [rowsPerPageOptions]="[25, 50, 100]"
          [loading]="loading()" [rowHover]="true" [scrollable]="true" scrollHeight="flex"
          styleClass="p-datatable-sm accounting-datatable balance-table"
          [showCurrentPageReport]="true" currentPageReportTemplate="Affichage de {first} à {last} sur {totalRecords} comptes">
          <ng-template pTemplate="header">
            <tr>
              <th>Compte</th>
              <th>Libellé</th>
              <th class="text-right" pTooltip="Ouverture débit" tooltipPosition="top">Ouv. D</th>
              <th class="text-right" pTooltip="Ouverture crédit" tooltipPosition="top">Ouv. C</th>
              <th class="text-right" pTooltip="Mouvement débit" tooltipPosition="top">Mouv. D</th>
              <th class="text-right" pTooltip="Mouvement crédit" tooltipPosition="top">Mouv. C</th>
              <th class="text-right" pTooltip="Clôture débit" tooltipPosition="top">Clôture D</th>
              <th class="text-right" pTooltip="Clôture crédit" tooltipPosition="top">Clôture C</th>
            </tr>
          </ng-template>
          <ng-template pTemplate="body" let-r>
            <tr>
              <td data-label="Compte">
                <a
                  class="account-link"
                  [routerLink]="['/accounting/ledger']"
                  [queryParams]="{ account: r.accountNumber, from: fromStr, to: toStr }"
                  title="Voir le grand livre de ce compte">
                  {{ r.accountNumber }}
                </a>
              </td>
              <td data-label="Libellé">{{ r.label }}</td>
              <td class="text-right" data-label="Ouv. D" style="font-variant-numeric:tabular-nums">{{ r.openingDebit | number : '1.3-3' }}</td>
              <td class="text-right" data-label="Ouv. C" style="font-variant-numeric:tabular-nums">{{ r.openingCredit | number : '1.3-3' }}</td>
              <td class="text-right" data-label="Mouv. D" style="font-variant-numeric:tabular-nums">{{ r.movementDebit | number : '1.3-3' }}</td>
              <td class="text-right" data-label="Mouv. C" style="font-variant-numeric:tabular-nums">{{ r.movementCredit | number : '1.3-3' }}</td>
              <td class="text-right" data-label="Clôture D" style="font-variant-numeric:tabular-nums">{{ r.closingDebit | number : '1.3-3' }}</td>
              <td class="text-right" data-label="Clôture C" style="font-variant-numeric:tabular-nums">{{ r.closingCredit | number : '1.3-3' }}</td>
            </tr>
          </ng-template>
          <ng-template pTemplate="footer">
            <tr class="acc-totals-row">
              <td colspan="2">Totaux</td>
              <td class="text-right" style="font-variant-numeric:tabular-nums">{{ totals().openingDebit | number : '1.3-3' }}</td>
              <td class="text-right" style="font-variant-numeric:tabular-nums">{{ totals().openingCredit | number : '1.3-3' }}</td>
              <td class="text-right" style="font-variant-numeric:tabular-nums">{{ totals().movementDebit | number : '1.3-3' }}</td>
              <td class="text-right" style="font-variant-numeric:tabular-nums">{{ totals().movementCredit | number : '1.3-3' }}</td>
              <td class="text-right" style="font-variant-numeric:tabular-nums">{{ totals().closingDebit | number : '1.3-3' }}</td>
              <td class="text-right" style="font-variant-numeric:tabular-nums">{{ totals().closingCredit | number : '1.3-3' }}</td>
            </tr>
          </ng-template>
          <ng-template pTemplate="emptymessage">
            <tr><td colspan="8" style="text-align:center;padding:2rem">Aucun mouvement sur la période.</td></tr>
          </ng-template>
        </p-table>
      </p-tabPanel>
    </p-tabView>
  `,
  styles: `
    @use '../shared/accounting-layout';
    .balance-toolbar-fields { display:flex; flex-wrap:wrap; align-items:flex-end; gap:var(--spacing-4); }
    .text-right { text-align:right; }
    .acc-totals-row td { font-weight:var(--font-weight-bold); background:var(--color-background-subtle); border-top:2px solid var(--color-border-default); }
    .account-link { font-family:monospace; font-weight:500; color:var(--color-primary-500); cursor:pointer; text-decoration:none; }
    .account-link:hover { text-decoration:underline; color:var(--color-primary-700); }
    :host ::ng-deep .balance-table.p-datatable .p-datatable-table { table-layout:fixed; min-width:56rem; }
  `
})
export class BalanceComponent implements OnInit {
  private readonly api = inject(AccountingService);
  fromStr = '';
  toStr = '';
  filterClass = 0;
  readonly rows = signal<BalanceRowDto[]>([]);
  readonly error = signal<string | null>(null);
  readonly loading = signal(false);

  readonly filteredRows = computed(() => {
    const fc = this.filterClass;
    const data = this.rows();
    if (!fc) return data;
    return data.filter(r => r.accountNumber.startsWith(String(fc)));
  });

  readonly totals = computed(() => {
    let openingDebit = 0, openingCredit = 0, movementDebit = 0, movementCredit = 0, closingDebit = 0, closingCredit = 0;
    for (const r of this.filteredRows()) {
      openingDebit += r.openingDebit;
      openingCredit += r.openingCredit;
      movementDebit += r.movementDebit;
      movementCredit += r.movementCredit;
      closingDebit += r.closingDebit;
      closingCredit += r.closingCredit;
    }
    return { openingDebit, openingCredit, movementDebit, movementCredit, closingDebit, closingCredit };
  });

  readonly buildBalanceAnalyzePayload = (): unknown =>
    wrapLegacyAnalyzePayload(
      'accounting-balance',
      {
        screen: 'accounting-balance',
        filters: {
          from: this.fromStr || null,
          to: this.toStr || null,
          filterClass: this.filterClass || null
        },
        summary: this.totals(),
        rows: this.filteredRows().slice(0, 200).map(r => ({
          accountNumber: r.accountNumber,
          label: r.label,
          openingDebit: r.openingDebit,
          openingCredit: r.openingCredit,
          movementDebit: r.movementDebit,
          movementCredit: r.movementCredit,
          closingDebit: r.closingDebit,
          closingCredit: r.closingCredit
        }))
      } as Record<string, unknown>
    );

  ngOnInit(): void {
    this.fromStr = firstDayOfMonthLocalYmd();
    this.toStr = todayLocalYmd();
    this.load();
  }

  load(): void {
    this.error.set(null);
    const from = parseLocalDateString(this.fromStr);
    const to = parseLocalDateString(this.toStr);
    const vr = validateDateRange(from, to);
    if (!vr.valid) {
      this.error.set(vr.message ?? 'Période invalide.');
      return;
    }
    this.loading.set(true);
    this.api.getBalance(from, to).subscribe({
      next: res => {
        this.loading.set(false);
        if (res.success && res.data) this.rows.set(res.data);
        else this.error.set(res.error ?? 'Erreur');
      },
      error: () => {
        this.loading.set(false);
        this.error.set('Erreur réseau');
      }
    });
  }
}
