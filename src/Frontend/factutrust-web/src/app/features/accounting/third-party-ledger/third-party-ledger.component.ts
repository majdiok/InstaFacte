import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterModule } from '@angular/router';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { PageHeaderComponent } from '@shared/components/page-header/page-header.component';
import { AccountingService, ThirdPartyLedgerDto } from '../services/accounting.service';
import { AccountingStatusBannerComponent } from '../shared/accounting-status-banner.component';
import { AccountingFilterBarComponent } from '../shared/accounting-filter-bar.component';
import { ButtonComponent } from '@shared/components/button/button.component';
import {
  firstDayOfYearLocalYmd,
  parseLocalDateString,
  todayLocalYmd,
  validateDateRange
} from '../shared/accounting-date-utils';

/**
 * Grand livre d'un tiers (client ou fournisseur) : solde d'ouverture + mouvements
 * avec solde progressif, réf. pièce et lettrage. Accessible par drill-down depuis
 * la balance auxiliaire (queryParams : thirdPartyId, kind, from, to, name).
 */
@Component({
  selector: 'app-third-party-ledger',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    RouterModule,
    TableModule,
    TagModule,
    PageHeaderComponent,
    AccountingStatusBannerComponent,
    AccountingFilterBarComponent,
    ButtonComponent
  ],
  template: `
    <app-page-header
      [title]="'Grand livre tiers' + (ledger()?.thirdPartyName ? ' — ' + ledger()!.thirdPartyName : '')"
      subtitle="Mouvements d'un tiers avec solde progressif" />
    <div class="card accounting-filters-card">
      <app-accounting-filter-bar ariaLabel="Période du grand livre tiers">
        <div accountingFilterFields class="tpl-toolbar-fields">
          <div class="accounting-filter-field">
            <label class="accounting-filter-label" for="tpl-from">Du</label>
            <input id="tpl-from" type="date" [(ngModel)]="fromStr" class="accounting-filter-input" />
          </div>
          <div class="accounting-filter-field">
            <label class="accounting-filter-label" for="tpl-to">Au</label>
            <input id="tpl-to" type="date" [(ngModel)]="toStr" class="accounting-filter-input" />
          </div>
        </div>
        <div accountingFilterActions>
          <app-button
            variant="secondary"
            icon="pi pi-arrow-left"
            iconPos="left"
            type="button"
            [routerLink]="['/accounting/auxiliary-balance']"
            ariaLabel="Retour à la balance auxiliaire">
            Balance auxiliaire
          </app-button>
          <app-button
            variant="secondary"
            icon="pi pi-refresh"
            iconPos="left"
            type="button"
            (click)="load()"
            [disabled]="loading() || !thirdPartyId"
            ariaLabel="Actualiser le grand livre du tiers">
            Actualiser
          </app-button>
          <app-button
            variant="secondary"
            icon="pi pi-download"
            iconPos="left"
            type="button"
            (click)="exportCsv()"
            [disabled]="loading() || exporting() || !thirdPartyId"
            ariaLabel="Exporter le grand livre du tiers en CSV">
            Export CSV
          </app-button>
        </div>
      </app-accounting-filter-bar>
    </div>
    <app-accounting-status-banner
      variant="error"
      [message]="error() ?? ''"
      [showRetry]="!!error()"
      retryLabel="Réessayer"
      (retry)="load()" />
    @if (ledger(); as l) {
      <div class="tpl-opening" role="note">
        <span class="tpl-opening-label">Solde d'ouverture au {{ fromStr }} :</span>
        <span class="tpl-opening-value" [class.tpl-negative]="l.openingBalance < 0">
          {{ l.openingBalance | number : '1.3-3' }}
        </span>
      </div>
    }
    <p-table [value]="ledger()?.rows ?? []" [paginator]="true" [rows]="50" [rowsPerPageOptions]="[25, 50, 100]"
      [loading]="loading()" [rowHover]="true" [scrollable]="true" scrollHeight="flex"
      styleClass="p-datatable-sm accounting-datatable tpl-table"
      [showCurrentPageReport]="true" currentPageReportTemplate="Affichage de {first} à {last} sur {totalRecords} mouvements">
      <ng-template pTemplate="header">
        <tr>
          <th>Date</th>
          <th>Journal</th>
          <th>N°</th>
          <th>Pièce</th>
          <th>Compte</th>
          <th>Libellé</th>
          <th class="text-right">Débit</th>
          <th class="text-right">Crédit</th>
          <th class="text-right">Solde</th>
          <th>Lettrage</th>
        </tr>
      </ng-template>
      <ng-template pTemplate="body" let-r>
        <tr>
          <td data-label="Date">{{ r.entryDate | date : 'shortDate' }}</td>
          <td data-label="Journal">{{ r.journalCode }}</td>
          <td data-label="N°" class="tpl-num">{{ r.pieceNumber }}</td>
          <td data-label="Pièce" class="tpl-num">{{ r.pieceRef }}</td>
          <td data-label="Compte" class="tpl-num">{{ r.accountNumber }}</td>
          <td data-label="Libellé">{{ r.label }}</td>
          <td class="text-right tpl-num" data-label="Débit">{{ r.debit | number : '1.3-3' }}</td>
          <td class="text-right tpl-num" data-label="Crédit">{{ r.credit | number : '1.3-3' }}</td>
          <td class="text-right tpl-num" data-label="Solde" [class.tpl-negative]="r.runningBalance < 0">
            {{ r.runningBalance | number : '1.3-3' }}
          </td>
          <td data-label="Lettrage">
            @if (r.letteringCode) {
              <p-tag [value]="r.letteringCode" [severity]="r.letteringCode.startsWith('P') ? 'warn' : 'info'" [rounded]="true" />
            }
          </td>
        </tr>
      </ng-template>
      <ng-template pTemplate="emptymessage">
        <tr><td colspan="10" style="text-align:center;padding:2rem">
          @if (thirdPartyId) { Aucun mouvement sur la période. } @else { Sélectionnez un tiers depuis la balance auxiliaire. }
        </td></tr>
      </ng-template>
    </p-table>
  `,
  styles: `
    @import '../shared/accounting-layout';
    .tpl-toolbar-fields { display:flex; flex-wrap:wrap; align-items:flex-end; gap:var(--spacing-4); }
    .text-right { text-align:right; }
    .tpl-num { font-variant-numeric:tabular-nums; }
    .tpl-negative { color:var(--color-danger-600, #dc2626); }
    .tpl-opening {
      display:flex; align-items:baseline; gap:var(--spacing-2);
      margin-bottom:var(--spacing-3); padding:var(--spacing-2) var(--spacing-3);
      background:var(--color-background-subtle); border:1px solid var(--color-border-subtle);
      border-radius:var(--radius-md); font-size:var(--font-size-sm);
    }
    .tpl-opening-label { color:var(--color-text-secondary); }
    .tpl-opening-value { font-weight:var(--font-weight-semibold); font-variant-numeric:tabular-nums; }
    :host ::ng-deep .tpl-table.p-datatable .p-datatable-table { min-width:60rem; }
  `
})
export class ThirdPartyLedgerComponent implements OnInit {
  private readonly api = inject(AccountingService);
  private readonly route = inject(ActivatedRoute);

  thirdPartyId = '';
  kind = 1;
  fromStr = '';
  toStr = '';
  readonly ledger = signal<ThirdPartyLedgerDto | null>(null);
  readonly error = signal<string | null>(null);
  readonly loading = signal(false);
  readonly exporting = signal(false);

  ngOnInit(): void {
    const qp = this.route.snapshot.queryParamMap;
    this.thirdPartyId = qp.get('thirdPartyId') ?? '';
    this.kind = Number(qp.get('kind')) || 1;
    this.fromStr = qp.get('from') || firstDayOfYearLocalYmd();
    this.toStr = qp.get('to') || todayLocalYmd();
    if (this.thirdPartyId) this.load();
  }

  load(): void {
    if (!this.thirdPartyId) {
      this.error.set('Aucun tiers sélectionné — passez par la balance auxiliaire.');
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
    this.api.getThirdPartyLedger(this.thirdPartyId, this.kind, from, to).subscribe({
      next: res => {
        this.loading.set(false);
        if (res.success && res.data) this.ledger.set(res.data);
        else this.error.set(res.error ?? 'Erreur');
      },
      error: () => {
        this.loading.set(false);
        this.error.set('Erreur réseau');
      }
    });
  }

  exportCsv(): void {
    if (!this.thirdPartyId) return;
    const from = parseLocalDateString(this.fromStr);
    const to = parseLocalDateString(this.toStr);
    const vr = validateDateRange(from, to);
    if (!vr.valid) {
      this.error.set(vr.message ?? 'Période invalide.');
      return;
    }
    this.exporting.set(true);
    this.api.exportThirdPartyLedger(this.thirdPartyId, this.kind, from, to).subscribe({
      next: blob => {
        this.exporting.set(false);
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = `grand_livre_tiers_${this.fromStr}_${this.toStr}.csv`;
        a.click();
        URL.revokeObjectURL(url);
      },
      error: () => {
        this.exporting.set(false);
        this.error.set("Erreur lors de l'export CSV.");
      }
    });
  }
}
