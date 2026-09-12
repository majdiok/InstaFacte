import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { TableModule } from 'primeng/table';
import { PageHeaderComponent } from '@shared/components/page-header/page-header.component';
import { ButtonComponent } from '@shared/components/button/button.component';
import { EmptyStateComponent } from '@shared/components/empty-state/empty-state.component';
import {
  DEPRECIATION_METHOD_LABELS,
  DepreciationRateCategoryDto,
  FIXED_ASSET_STATUS_LABELS,
  FixedAssetDto,
  FixedAssetStatus,
  FixedAssetsService
} from '../services/fixed-assets.service';
import { AccountingStatusBannerComponent } from '../shared/accounting-status-banner.component';
import { ErrorHandlerService } from '@core/services/error-handler.service';

@Component({
  selector: 'app-fixed-assets-list',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    RouterModule,
    TableModule,
    PageHeaderComponent,
    ButtonComponent,
    EmptyStateComponent,
    AccountingStatusBannerComponent
  ],
  template: `
    <app-page-header
      title="Immobilisations"
      subtitle="Registre des actifs et suivi des amortissements (norme tunisienne)" />

    <div class="card accounting-filters-card">
      <div class="accounting-filter-row">
        <input
          type="search"
          class="accounting-filter-input"
          placeholder="Rechercher (désignation, n° inventaire)"
          [(ngModel)]="search"
          (keyup.enter)="reload()" />
        <select class="accounting-filter-input" [(ngModel)]="statusFilter" (ngModelChange)="reload()">
          <option [ngValue]="null">Tous les statuts</option>
          <option [ngValue]="FixedAssetStatus.Draft">Brouillon</option>
          <option [ngValue]="FixedAssetStatus.InService">En service</option>
          <option [ngValue]="FixedAssetStatus.FullyDepreciated">Totalement amorti</option>
          <option [ngValue]="FixedAssetStatus.Disposed">Cédé</option>
        </select>
        <select class="accounting-filter-input" [(ngModel)]="categoryFilter" (ngModelChange)="reload()">
          <option [ngValue]="null">Toutes les catégories</option>
          <option *ngFor="let c of categories()" [ngValue]="c.id">{{ c.label }}</option>
        </select>
        <input
          type="number"
          class="accounting-filter-input year-input"
          placeholder="Exercice"
          min="2000"
          max="2100"
          [(ngModel)]="fiscalYearFilter"
          (keyup.enter)="reload()" />
        <app-button variant="secondary" icon="pi pi-refresh" type="button" (click)="reload()" [disabled]="loading()">
          Actualiser
        </app-button>
        <app-button variant="primary" icon="pi pi-plus" type="button" routerLink="/accounting/fixed-assets/new">
          Nouvelle immobilisation
        </app-button>
        <app-button variant="secondary" icon="pi pi-calculator" type="button" routerLink="/accounting/fixed-assets/depreciation-run">
          Dotations
        </app-button>
        <app-button variant="secondary" icon="pi pi-table" type="button" routerLink="/accounting/fixed-assets/amortization-table">
          Tableau amortissements
        </app-button>
        <app-button variant="secondary" icon="pi pi-cog" type="button" routerLink="/accounting/fixed-assets/settings">
          Exercice comptable
        </app-button>
      </div>
    </div>

    <app-accounting-status-banner [message]="error() ?? ''" variant="error" *ngIf="error()" />

    <div class="card" *ngIf="!loading() && items().length === 0">
      <app-empty-state
        icon="pi pi-building"
        title="Aucune immobilisation"
        message="Créez votre premier actif pour générer un tableau d'amortissement." />
    </div>

    <div class="card" *ngIf="items().length > 0">
      <p-table [value]="items()" styleClass="p-datatable-sm">
        <ng-template pTemplate="header">
          <tr>
            <th>N° inventaire</th>
            <th>Désignation</th>
            <th>Catégorie</th>
            <th>Méthode</th>
            <th class="text-right">Valeur origine</th>
            <th class="text-right">Amort. cumulés</th>
            <th class="text-right">VNC</th>
            <th>Taux</th>
            <th>Statut</th>
            <th></th>
          </tr>
        </ng-template>
        <ng-template pTemplate="body" let-row>
          <tr>
            <td><code>{{ row.inventoryNumber }}</code></td>
            <td>{{ row.label }}</td>
            <td>{{ row.depreciationRateCategoryLabel }}</td>
            <td>{{ methodLabel(row) }}</td>
            <td class="text-right">{{ row.totalCapitalizedCost | number: '1.3-3' }}</td>
            <td class="text-right">{{ row.accumulatedDepreciation | number: '1.3-3' }}</td>
            <td class="text-right">{{ row.netBookValue | number: '1.3-3' }}</td>
            <td>{{ row.depreciationRatePercent | number: '1.2-2' }} %</td>
            <td><span class="status-pill">{{ statusLabel(row) }}</span></td>
            <td>
              <a [routerLink]="['/accounting/fixed-assets', row.id]" class="link-action">Ouvrir</a>
            </td>
          </tr>
        </ng-template>
        <ng-template pTemplate="footer">
          <tr class="totals-row">
            <td colspan="4"><strong>Totaux page {{ page() }} / {{ totalPages() }}</strong></td>
            <td class="text-right"><strong>{{ totals().origin | number: '1.3-3' }}</strong></td>
            <td class="text-right"><strong>{{ totals().accumulated | number: '1.3-3' }}</strong></td>
            <td class="text-right"><strong>{{ totals().nbv | number: '1.3-3' }}</strong></td>
            <td colspan="3"></td>
          </tr>
        </ng-template>
      </p-table>

      <div class="pagination-bar">
        <span class="page-info">
          {{ totalCount() }} immobilisation(s) — page {{ page() }} / {{ totalPages() }}
        </span>
        <div class="page-actions">
          <app-button variant="secondary" type="button" (click)="prevPage()" [disabled]="loading() || page() <= 1">
            Précédent
          </app-button>
          <app-button variant="secondary" type="button" (click)="nextPage()" [disabled]="loading() || page() >= totalPages()">
            Suivant
          </app-button>
        </div>
      </div>
    </div>
  `,
  styles: [
    `
      .accounting-filter-row {
        display: flex;
        flex-wrap: wrap;
        gap: 0.75rem;
        align-items: center;
      }
      .year-input {
        max-width: 110px;
      }
      .text-right {
        text-align: right;
      }
      .status-pill {
        display: inline-block;
        padding: 0.15rem 0.5rem;
        border-radius: 999px;
        background: #e0f2fe;
        color: #0369a1;
        font-size: 0.8rem;
      }
      .link-action {
        color: var(--primary-color, #2563eb);
        text-decoration: none;
        font-weight: 500;
      }
      .totals-row td {
        font-weight: var(--font-weight-bold);
        background: var(--color-background-subtle);
        border-top: 2px solid var(--color-border-default);
      }
      .pagination-bar {
        display: flex;
        justify-content: space-between;
        align-items: center;
        margin-top: 0.75rem;
        flex-wrap: wrap;
        gap: 0.5rem;
      }
      .page-info {
        font-size: 0.85rem;
        color: #64748b;
      }
      .page-actions {
        display: flex;
        gap: 0.5rem;
      }
    `
  ]
})
export class FixedAssetsListComponent implements OnInit {
  private readonly errors = inject(ErrorHandlerService);
  private readonly api = inject(FixedAssetsService);

  readonly FixedAssetStatus = FixedAssetStatus;

  readonly loading = signal(false);
  readonly error = signal<string | null>(null);
  readonly items = signal<FixedAssetDto[]>([]);
  readonly categories = signal<DepreciationRateCategoryDto[]>([]);
  readonly page = signal(1);
  readonly totalCount = signal(0);
  readonly pageSize = 25;

  readonly totalPages = computed(() => Math.max(1, Math.ceil(this.totalCount() / this.pageSize)));
  readonly totals = computed(() => {
    const rows = this.items();
    return {
      origin: rows.reduce((s, r) => s + r.totalCapitalizedCost, 0),
      accumulated: rows.reduce((s, r) => s + r.accumulatedDepreciation, 0),
      nbv: rows.reduce((s, r) => s + r.netBookValue, 0)
    };
  });

  search = '';
  statusFilter: FixedAssetStatus | null = null;
  categoryFilter: string | null = null;
  fiscalYearFilter: number | null = null;

  ngOnInit(): void {
    this.api.getRateCategories().subscribe({
      next: res => this.categories.set(res.data ?? []),
      error: () => this.categories.set([])
    });
    this.load();
  }

  reload(): void {
    this.page.set(1);
    this.load();
  }

  prevPage(): void {
    if (this.page() > 1) {
      this.page.update(p => p - 1);
      this.load();
    }
  }

  nextPage(): void {
    if (this.page() < this.totalPages()) {
      this.page.update(p => p + 1);
      this.load();
    }
  }

  load(): void {
    this.loading.set(true);
    this.error.set(null);
    this.api
      .list({
        page: this.page(),
        pageSize: this.pageSize,
        status: this.statusFilter === null ? undefined : this.statusFilter,
        categoryId: this.categoryFilter ?? undefined,
        fiscalYear: this.fiscalYearFilter ?? undefined,
        search: this.search.trim() || undefined
      })
      .subscribe({
        next: res => {
          this.items.set(res.data?.items ?? []);
          this.totalCount.set(res.data?.totalCount ?? 0);
          this.loading.set(false);
        },
        error: err => {
          this.error.set(this.errors.extractErrorMessage(err, 'Impossible de charger le registre des immobilisations.'));
          this.loading.set(false);
        }
      });
  }

  statusLabel(row: FixedAssetDto): string {
    return FIXED_ASSET_STATUS_LABELS[row.status] ?? '—';
  }

  methodLabel(row: FixedAssetDto): string {
    return DEPRECIATION_METHOD_LABELS[row.depreciationMethod] ?? 'Linéaire';
  }
}
