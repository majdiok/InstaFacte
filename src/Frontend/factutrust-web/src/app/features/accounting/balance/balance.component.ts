import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { TableModule } from 'primeng/table';
import { TabViewModule } from 'primeng/tabview';
import { PageHeaderComponent } from '@shared/components/page-header/page-header.component';
import {
  AccountingService,
  BalanceRowDto,
  DetailedBalanceDto,
  PeriodicBalanceDto,
  PeriodicBalanceRowDto
} from '../services/accounting.service';
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
import { AccountingExportMenuComponent } from '../shared/accounting-export-menu.component';
import { AccountingExportFormat, downloadBlob, exportExtension } from '../shared/accounting-download.util';

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
    TooltipModule,
    AccountingExportMenuComponent
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
          @if (activeTabIndex !== 2) {
            <div class="accounting-filter-field">
              <label class="accounting-filter-label" for="bal-acc-from">Du compte</label>
              <input id="bal-acc-from" type="text" class="accounting-filter-input" [(ngModel)]="accountFrom" placeholder="Début" />
            </div>
            <div class="accounting-filter-field">
              <label class="accounting-filter-label" for="bal-acc-to">Au compte</label>
              <input id="bal-acc-to" type="text" class="accounting-filter-input" [(ngModel)]="accountTo" placeholder="Fin" />
            </div>
          }
          @if (activeTabIndex === 0) {
            <div class="accounting-filter-field">
              <label class="accounting-filter-label" for="bal-scope">Comptes</label>
              <select id="bal-scope" [(ngModel)]="accountScope" class="accounting-filter-input">
                <option value="all">Tous</option>
                <option value="moved">Mouvementés</option>
                <option value="unsettled">Non soldés</option>
              </select>
            </div>
            <div class="accounting-filter-field">
              <label class="accounting-filter-label" for="bal-level">Regroupement</label>
              <select id="bal-level" [(ngModel)]="groupLevel" class="accounting-filter-input">
                <option [ngValue]="0">Détail par compte</option>
                @for (l of [1, 2, 3, 4, 5]; track l) {
                  <option [ngValue]="l">Racine à {{ l }} chiffre{{ l > 1 ? 's' : '' }}</option>
                }
              </select>
            </div>
          }
          @if (activeTabIndex === 2) {
            <div class="accounting-filter-field">
              <label class="accounting-filter-label" for="bal-year">Exercice</label>
              <input id="bal-year" type="number" class="accounting-filter-input" [(ngModel)]="fiscalYear" min="2000" max="2100" />
            </div>
          }
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
          <app-accounting-export-menu
            [disabled]="loading() || exporting() || !hasResults()"
            (exportFormat)="onExport($event)" />
        </div>
      </app-accounting-filter-bar>
    </div>
    <app-accounting-status-banner
      variant="error"
      [message]="error() ?? ''"
      [showRetry]="!!error()"
      retryLabel="Réessayer"
      (retry)="load()" />
    <p-tabView [(activeIndex)]="activeTabIndex" (activeIndexChange)="onTabChange()">
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

      <!-- ══ Balance détaillée : chaque solde suivi de ses mouvements ══════ -->
      <p-tabPanel header="Détaillée">
        @if (detailed(); as d) {
          @for (acc of d.accounts; track acc.balance.accountNumber) {
            <section class="bal-detail-block">
              <h3 class="bal-detail-title">
                <span class="bal-code">{{ acc.balance.accountNumber }}</span> {{ acc.balance.label }}
                <span class="bal-detail-solde">
                  Mouv. D {{ acc.balance.movementDebit | number : '1.3-3' }} ·
                  Mouv. C {{ acc.balance.movementCredit | number : '1.3-3' }} ·
                  Clôture {{ acc.balance.closingDebit - acc.balance.closingCredit | number : '1.3-3' }}
                </span>
              </h3>
              <p-table [value]="acc.rows" [rowHover]="true" styleClass="p-datatable-sm accounting-datatable">
                <ng-template pTemplate="header">
                  <tr>
                    <th scope="col">Date</th>
                    <th scope="col">Journal</th>
                    <th scope="col">Pièce</th>
                    <th scope="col">Libellé</th>
                    <th scope="col" class="text-right">Débit</th>
                    <th scope="col" class="text-right">Crédit</th>
                    <th scope="col" class="text-right">Solde</th>
                  </tr>
                </ng-template>
                <ng-template pTemplate="body" let-r>
                  <tr>
                    <td data-label="Date">{{ r.entryDate | date : 'shortDate' }}</td>
                    <td data-label="Journal">{{ r.journalCode }}</td>
                    <td data-label="Pièce">{{ r.pieceNumber }}</td>
                    <td data-label="Libellé">{{ r.label }}</td>
                    <td class="text-right" data-label="Débit">{{ r.debit | number : '1.3-3' }}</td>
                    <td class="text-right" data-label="Crédit">{{ r.credit | number : '1.3-3' }}</td>
                    <td class="text-right" data-label="Solde">{{ r.runningBalance | number : '1.3-3' }}</td>
                  </tr>
                </ng-template>
                <ng-template pTemplate="emptymessage">
                  <tr><td colspan="7" class="bal-empty">Aucun mouvement — solde reporté seul.</td></tr>
                </ng-template>
              </p-table>
            </section>
          }
          @if (d.accounts.length === 0) {
            <p class="bal-empty">Aucun mouvement sur la période.</p>
          } @else {
            <p class="bal-grand-total">
              Total mouvements — débit {{ d.totalMovementDebit | number : '1.3-3' }} ·
              crédit {{ d.totalMovementCredit | number : '1.3-3' }}
            </p>
          }
        }
      </p-tabPanel>

      <!-- ══ Balance par période : 12 colonnes mensuelles ══════════════════ -->
      <p-tabPanel header="Par période">
        @if (periodic(); as p) {
          <div class="bal-scroll">
            <table class="bal-matrix">
              <thead>
                <tr>
                  <th scope="col" class="bal-sticky">Compte</th>
                  <th scope="col" class="text-right">Ouverture</th>
                  @for (m of monthLabels; track m) {
                    <th scope="col" class="text-right">{{ m }}</th>
                  }
                  <th scope="col" class="text-right bal-total-col">Clôture</th>
                </tr>
              </thead>
              <tbody>
                @for (row of p.rows; track row.accountNumber) {
                  <tr>
                    <td class="bal-sticky">
                      <span class="bal-code">{{ row.accountNumber }}</span>
                      <span class="bal-label">{{ row.label }}</span>
                    </td>
                    <td class="text-right">{{ row.opening | number : '1.3-3' }}</td>
                    @for (m of monthIndexes; track m) {
                      <td class="text-right">
                        {{ netOf(row, m) ? (netOf(row, m) | number : '1.3-3') : '' }}
                      </td>
                    }
                    <td class="text-right bal-total-col">{{ row.closing | number : '1.3-3' }}</td>
                  </tr>
                }
                @if (p.rows.length === 0) {
                  <tr><td colspan="15" class="bal-empty">Aucun mouvement sur l'exercice.</td></tr>
                }
              </tbody>
            </table>
          </div>
          <p class="bal-grand-total">
            Total mouvements — débit {{ p.totalDebit | number : '1.3-3' }} ·
            crédit {{ p.totalCredit | number : '1.3-3' }}
          </p>
        }
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

    /* ── Balance détaillée ─────────────────────────────────────────────── */
    .bal-detail-block { margin-bottom:var(--spacing-6); }
    .bal-detail-title {
      margin:0 0 var(--spacing-2);
      font-size:var(--font-size-md);
      font-weight:var(--font-weight-semibold);
      display:flex; flex-wrap:wrap; align-items:baseline; gap:var(--spacing-3);
    }
    .bal-detail-solde {
      font-size:var(--font-size-sm);
      font-weight:var(--font-weight-regular);
      color:var(--color-text-secondary);
      font-variant-numeric:tabular-nums;
    }
    .bal-code { font-family:ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, monospace; font-weight:500; }
    .bal-label { margin-left:var(--spacing-2); color:var(--color-text-secondary); }
    .bal-empty { text-align:center; padding:var(--spacing-6); color:var(--color-text-secondary); }
    .bal-grand-total {
      margin:var(--spacing-4) 0 0;
      text-align:right;
      font-weight:var(--font-weight-bold);
      font-variant-numeric:tabular-nums;
    }

    /* ── Balance par période : large par nature, défile dans son conteneur ── */
    .bal-scroll { overflow-x:auto; max-width:100%; }
    .bal-matrix { border-collapse:collapse; width:max-content; min-width:100%; font-size:var(--font-size-sm); }
    .bal-matrix th, .bal-matrix td {
      padding:var(--spacing-2) var(--spacing-3);
      border-bottom:1px solid var(--color-border-subtle);
      white-space:nowrap;
      font-variant-numeric:tabular-nums;
    }
    .bal-matrix thead th {
      background:var(--color-background-subtle);
      color:var(--color-text-tertiary);
      text-transform:uppercase;
      font-size:var(--font-size-xs);
      letter-spacing:0.04em;
    }
    .bal-sticky { position:sticky; left:0; z-index:1; background:var(--color-background-elevated); text-align:left; }
    .bal-matrix thead .bal-sticky { background:var(--color-background-subtle); }
    .bal-total-col { background:var(--color-background-subtle); }
  `
})
export class BalanceComponent implements OnInit {
  private readonly api = inject(AccountingService);
  fromStr = '';
  toStr = '';
  filterClass = 0;

  /** Onglet actif : 0 = générale (état d'ouverture historique), 1 = détaillée, 2 = par période. */
  activeTabIndex = 0;
  accountFrom = '';
  accountTo = '';
  accountScope: 'all' | 'moved' | 'unsettled' = 'all';
  groupLevel = 0;
  fiscalYear = new Date().getFullYear();

  readonly monthLabels = ['Jan', 'Fév', 'Mar', 'Avr', 'Mai', 'Juin', 'Juil', 'Août', 'Sep', 'Oct', 'Nov', 'Déc'];
  readonly monthIndexes = [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11];

  readonly rows = signal<BalanceRowDto[]>([]);
  readonly detailed = signal<DetailedBalanceDto | null>(null);
  readonly periodic = signal<PeriodicBalanceDto | null>(null);
  readonly error = signal<string | null>(null);
  readonly loading = signal(false);
  readonly exporting = signal(false);

  /**
   * Balance générale filtrée puis, si demandé, regroupée par racine. Les filtres s'appliquent
   * côté écran : l'appel serveur et sa réponse restent ceux d'avant.
   */
  readonly filteredRows = computed(() => {
    const fc = this.filterClass;
    let data = this.rows();
    if (fc) data = data.filter(r => r.accountNumber.startsWith(String(fc)));

    const from = this.accountFrom.trim();
    const to = this.accountTo.trim();
    if (from) data = data.filter(r => r.accountNumber >= from);
    if (to) data = data.filter(r => r.accountNumber <= to);

    if (this.accountScope === 'moved') {
      data = data.filter(r => r.movementDebit !== 0 || r.movementCredit !== 0);
    } else if (this.accountScope === 'unsettled') {
      data = data.filter(r => r.closingDebit - r.closingCredit !== 0);
    }

    return this.groupLevel > 0 ? BalanceComponent.groupByRoot(data, this.groupLevel) : data;
  });

  /** Vrai quand l'onglet actif a produit des données exportables. */
  readonly hasResults = computed(() => {
    switch (this.activeTabIndex) {
      case 1:
        return (this.detailed()?.accounts.length ?? 0) > 0;
      case 2:
        return (this.periodic()?.rows.length ?? 0) > 0;
      default:
        return this.rows().length > 0;
    }
  });

  /** Agrège les lignes par racine à N chiffres — les soldes se recomposent en net. */
  private static groupByRoot(rows: BalanceRowDto[], level: number): BalanceRowDto[] {
    const byRoot = new Map<string, BalanceRowDto>();
    for (const r of rows) {
      const root = r.accountNumber.length <= level ? r.accountNumber : r.accountNumber.slice(0, level);
      const acc = byRoot.get(root);
      if (!acc) {
        byRoot.set(root, { ...r, accountNumber: root, label: `Racine ${root}` });
        continue;
      }
      const openingNet =
        acc.openingDebit - acc.openingCredit + (r.openingDebit - r.openingCredit);
      const closingNet =
        acc.closingDebit - acc.closingCredit + (r.closingDebit - r.closingCredit);
      byRoot.set(root, {
        accountNumber: root,
        label: `Racine ${root}`,
        openingDebit: openingNet > 0 ? openingNet : 0,
        openingCredit: openingNet < 0 ? -openingNet : 0,
        movementDebit: acc.movementDebit + r.movementDebit,
        movementCredit: acc.movementCredit + r.movementCredit,
        closingDebit: closingNet > 0 ? closingNet : 0,
        closingCredit: closingNet < 0 ? -closingNet : 0
      });
    }
    return [...byRoot.values()].sort((a, b) => a.accountNumber.localeCompare(b.accountNumber));
  }

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

  /** Mouvement net d'un mois (débit − crédit) — la matrice n'affiche qu'une valeur par cellule. */
  netOf(row: PeriodicBalanceRowDto, month: number): number {
    return row.monthlyDebit[month] - row.monthlyCredit[month];
  }

  onTabChange(): void {
    this.error.set(null);
    this.load();
  }

  load(): void {
    if (this.activeTabIndex === 1) {
      this.loadDetailed();
      return;
    }
    if (this.activeTabIndex === 2) {
      this.loadPeriodic();
      return;
    }

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

  private loadDetailed(): void {
    const range = this.resolvePeriod();
    if (!range) return;

    this.error.set(null);
    this.loading.set(true);
    this.api
      .getDetailedBalance(range.from, range.to, this.accountFrom.trim() || undefined, this.accountTo.trim() || undefined)
      .subscribe({
        next: res => {
          this.loading.set(false);
          if (res.success && res.data) this.detailed.set(res.data);
          else this.error.set(res.error ?? 'Erreur');
        },
        error: () => {
          this.loading.set(false);
          this.error.set('Erreur réseau');
        }
      });
  }

  private loadPeriodic(): void {
    this.error.set(null);
    this.loading.set(true);
    this.api.getPeriodicBalance(this.fiscalYear).subscribe({
      next: res => {
        this.loading.set(false);
        if (res.success && res.data) this.periodic.set(res.data);
        else this.error.set(res.error ?? 'Erreur');
      },
      error: () => {
        this.loading.set(false);
        this.error.set('Erreur réseau');
      }
    });
  }

  /** Période validée, ou null après publication du message d'erreur. */
  private resolvePeriod(): { from: Date; to: Date } | null {
    const from = parseLocalDateString(this.fromStr);
    const to = parseLocalDateString(this.toStr);
    const vr = validateDateRange(from, to);
    if (!vr.valid) {
      this.error.set(vr.message ?? 'Période invalide.');
      return null;
    }
    return { from, to };
  }

  onExport(format: AccountingExportFormat): void {
    if (this.activeTabIndex === 1) {
      this.exportDetailed(format);
      return;
    }
    if (this.activeTabIndex === 2) {
      this.exportPeriodic(format);
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
    this.api.exportBalance(from, to, format).subscribe({
      next: blob => {
        this.exporting.set(false);
        downloadBlob(blob, `balance_${this.fromStr}_${this.toStr}.${exportExtension(format)}`);
      },
      error: () => {
        this.exporting.set(false);
        this.error.set("Erreur lors de l'export.");
      }
    });
  }

  private exportDetailed(format: AccountingExportFormat): void {
    const range = this.resolvePeriod();
    if (!range) return;

    this.exporting.set(true);
    this.api
      .exportDetailedBalance(
        range.from,
        range.to,
        this.accountFrom.trim() || undefined,
        this.accountTo.trim() || undefined,
        format
      )
      .subscribe({
        next: blob => {
          this.exporting.set(false);
          downloadBlob(blob, `balance_detaillee_${this.fromStr}_${this.toStr}.${exportExtension(format)}`);
        },
        error: () => {
          this.exporting.set(false);
          this.error.set("Erreur lors de l'export.");
        }
      });
  }

  private exportPeriodic(format: AccountingExportFormat): void {
    this.exporting.set(true);
    this.api.exportPeriodicBalance(this.fiscalYear, format).subscribe({
      next: blob => {
        this.exporting.set(false);
        downloadBlob(blob, `balance_par_periode_${this.fiscalYear}.${exportExtension(format)}`);
      },
      error: () => {
        this.exporting.set(false);
        this.error.set("Erreur lors de l'export.");
      }
    });
  }
}
