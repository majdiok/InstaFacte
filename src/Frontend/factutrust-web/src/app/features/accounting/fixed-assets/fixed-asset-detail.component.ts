import { Component, ElementRef, OnInit, ViewChild, afterNextRender, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { TableModule } from 'primeng/table';
import { TabsModule } from 'primeng/tabs';
import { PageHeaderComponent } from '@shared/components/page-header/page-header.component';
import { ButtonComponent } from '@shared/components/button/button.component';
import { SupplierService, SupplierListItem } from '@core/services/supplier.service';
import {
  ACCELERATION_COEFFICIENTS,
  DEPRECIATION_METHOD_LABELS,
  DepreciationMethod,
  DepreciationRateCategoryDto,
  FixedAssetDto,
  FixedAssetScheduleDto,
  FixedAssetsService,
  FIXED_ASSET_STATUS_LABELS,
  LOW_VALUE_ASSET_CEILING_TND,
  UpdateFixedAssetRequest
} from '../services/fixed-assets.service';
import {
  canDisposeStatus,
  isActiveAssetStatus,
  isDraftStatus,
  isInServiceOrBeyond,
  parseDepreciationMethod
} from '../services/fixed-asset-enums';
import { AccountingStatusBannerComponent } from '../shared/accounting-status-banner.component';
import { todayLocalYmd } from '../shared/accounting-date-utils';

interface AssetFormModel {
  label: string;
  depreciationRateCategoryId: string;
  description: string;
  location: string;
  depreciationMethod: DepreciationMethod;
  accelerationCoefficient: number;
  depreciationRatePercent: number | null;
  usefulLifeYears: number | null;
  acquisitionDate: string;
  supplierId: string;
  acquisitionCost: number;
  vatAmount: number;
  capitalizedFees: number;
  residualValue: number;
  assetAccountNumber: string;
  depreciationAccountNumber: string;
  expenseAccountNumber: string;
}

@Component({
  selector: 'app-fixed-asset-detail',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    RouterModule,
    TableModule,
    TabsModule,
    PageHeaderComponent,
    ButtonComponent,
    AccountingStatusBannerComponent
  ],
  template: `
    <app-page-header
      [title]="pageTitle()"
      [subtitle]="pageSubtitle()" />

    <app-accounting-status-banner [message]="error() ?? ''" variant="error" *ngIf="error()" />
    <div class="refresh-row" *ngIf="error() && asset()">
      <app-button variant="secondary" type="button" icon="pi pi-refresh" (click)="refreshAsset()" [disabled]="saving()">
        Actualiser la fiche
      </app-button>
    </div>
    <app-accounting-status-banner [message]="success() ?? ''" variant="success" *ngIf="success()" />
    <app-accounting-status-banner
      *ngIf="isDraftAsset()"
      message="Immobilisation en brouillon — enregistrez vos modifications puis mettez en service dans la section ci-dessous."
      variant="info" />

    <!-- ============ Fiche immobilisation (création / brouillon / consultation) ============ -->
    <div class="card" *ngIf="isNew() || asset()">
      <p-tabs [lazy]="true">
        <p-tablist>
          <p-tab [value]="0">Généralités</p-tab>
          <p-tab [value]="1">Acquisition</p-tab>
        </p-tablist>
        <p-tabpanels>
        <p-tabpanel [value]="0">
          <div class="form-grid">
            <label>
              Désignation *
              <input class="accounting-filter-input" [(ngModel)]="form.label" [disabled]="readonly()" />
              <span class="field-error" *ngIf="fieldErrors()['label']">{{ fieldErrors()['label'] }}</span>
            </label>
            <label *ngIf="asset()">
              N° inventaire
              <input class="accounting-filter-input" [value]="asset()!.inventoryNumber" disabled />
            </label>
            <label>
              Catégorie (taux légal tunisien) *
              <select
                class="accounting-filter-input"
                [(ngModel)]="form.depreciationRateCategoryId"
                (ngModelChange)="onCategoryChange($event)"
                [disabled]="readonly()">
                <option value="">— Sélectionner —</option>
                <option *ngFor="let c of categories()" [value]="c.id">
                  {{ c.label }} ({{ c.legalRatePercent | number: '1.2-2' }} %)
                </option>
              </select>
              <span class="field-error" *ngIf="fieldErrors()['category']">{{ fieldErrors()['category'] }}</span>
            </label>
            <label>
              Emplacement
              <input class="accounting-filter-input" [(ngModel)]="form.location" [disabled]="readonly()" />
            </label>
            <label class="span-2">
              Description
              <textarea class="accounting-filter-input" rows="2" [(ngModel)]="form.description" [disabled]="readonly()"></textarea>
            </label>
          </div>

          <fieldset class="form-section">
            <legend>Amortissement</legend>
            <app-accounting-status-banner
              *ngIf="suggestIntegral()"
              [message]="integralSuggestionMessage"
              variant="info" />
            <div class="form-grid">
              <label>
                Type d'amortissement
                <select
                  class="accounting-filter-input"
                  [(ngModel)]="form.depreciationMethod"
                  (ngModelChange)="onMethodChange($event)"
                  [disabled]="readonly() || isNonDepreciable()">
                  <option [ngValue]="DepreciationMethod.Linear">Linéaire</option>
                  <option [ngValue]="DepreciationMethod.Accelerated">Accéléré</option>
                  <option [ngValue]="DepreciationMethod.Integral">Intégral</option>
                </select>
              </label>
              <label *ngIf="form.depreciationMethod === DepreciationMethod.Accelerated">
                Coefficient (matériel industriel multi-équipes) *
                <select class="accounting-filter-input" [(ngModel)]="form.accelerationCoefficient" [disabled]="readonly()">
                  <option *ngFor="let c of ACCELERATION_COEFFICIENTS" [ngValue]="c">
                    {{ c === 1.5 ? '1,5 — deux équipes (taux × 1,5)' : '2 — trois équipes (taux × 2)' }}
                  </option>
                </select>
                <span class="field-error" *ngIf="fieldErrors()['coefficient']">{{ fieldErrors()['coefficient'] }}</span>
                <span class="hint">Décret 2008-492 art. 2 — réservé au matériel industriel des industries manufacturières non saisonnières.</span>
              </label>
              <label>
                Durée d'utilisation (années)
                <input
                  type="number"
                  step="0.5"
                  min="0"
                  class="accounting-filter-input"
                  [(ngModel)]="form.usefulLifeYears"
                  (ngModelChange)="onLifeChange($event)"
                  [disabled]="readonly() || isNonDepreciable()" />
              </label>
              <label>
                Taux d'amortissement (%)
                <input
                  type="number"
                  step="0.01"
                  min="0"
                  max="100"
                  class="accounting-filter-input"
                  [(ngModel)]="form.depreciationRatePercent"
                  (ngModelChange)="onRateChange($event)"
                  [disabled]="readonly() || isNonDepreciable()" />
                <span class="field-error" *ngIf="fieldErrors()['rate']">{{ fieldErrors()['rate'] }}</span>
              </label>
            </div>
            <p class="hint" *ngIf="isNonDepreciable()">Cette catégorie n'est pas amortissable (ex. terrains).</p>
            <p class="hint" *ngIf="!isNonDepreciable()">
              Le taux et la durée sont synchronisés (taux = 100 / durée). Le taux légal de la catégorie est proposé par défaut.
            </p>
          </fieldset>

          <fieldset class="form-section">
            <legend>Comptabilisation</legend>
            <div class="form-grid">
              <label>
                Compte d'immobilisation (21x) *
                <input class="accounting-filter-input" [(ngModel)]="form.assetAccountNumber" [disabled]="readonly()" />
              </label>
              <label>
                Compte d'amortissement (28x) *
                <input class="accounting-filter-input" [(ngModel)]="form.depreciationAccountNumber" [disabled]="readonly()" />
              </label>
              <label>
                Compte de dotation (68x) *
                <input class="accounting-filter-input" [(ngModel)]="form.expenseAccountNumber" [disabled]="readonly()" />
              </label>
            </div>
            <span class="field-error" *ngIf="fieldErrors()['accounts']">{{ fieldErrors()['accounts'] }}</span>
          </fieldset>
        </p-tabpanel>

        <p-tabpanel [value]="1">
          <div class="form-grid">
            <label>
              Date d'acquisition *
              <input type="date" class="accounting-filter-input" [(ngModel)]="form.acquisitionDate" [disabled]="readonly()" />
              <span class="field-error" *ngIf="fieldErrors()['acquisitionDate']">{{ fieldErrors()['acquisitionDate'] }}</span>
            </label>
            <label>
              Fournisseur
              <select class="accounting-filter-input" [(ngModel)]="form.supplierId" [disabled]="readonly()">
                <option value="">— Aucun —</option>
                <option *ngFor="let s of suppliers()" [value]="s.id">{{ s.name }}</option>
              </select>
            </label>
            <label>
              Prix d'achat HT *
              <input type="number" step="0.001" min="0" class="accounting-filter-input" [(ngModel)]="form.acquisitionCost" [disabled]="readonly()" />
              <span class="field-error" *ngIf="fieldErrors()['cost']">{{ fieldErrors()['cost'] }}</span>
            </label>
            <label>
              Montant TVA
              <input type="number" step="0.001" min="0" class="accounting-filter-input" [(ngModel)]="form.vatAmount" [disabled]="readonly()" />
            </label>
            <label>
              Frais capitalisés
              <input type="number" step="0.001" min="0" class="accounting-filter-input" [(ngModel)]="form.capitalizedFees" [disabled]="readonly()" />
            </label>
            <label>
              Valeur résiduelle
              <input type="number" step="0.001" min="0" class="accounting-filter-input" [(ngModel)]="form.residualValue" [disabled]="readonly()" />
              <span class="field-error" *ngIf="fieldErrors()['residual']">{{ fieldErrors()['residual'] }}</span>
            </label>
            <label>
              Montant amortissable
              <input class="accounting-filter-input" [value]="depreciableBase() | number: '1.3-3'" disabled />
            </label>
          </div>
        </p-tabpanel>
        </p-tabpanels>
      </p-tabs>

      <div class="actions" *ngIf="!readonly()">
        <app-button variant="primary" type="button" (click)="save()" [disabled]="saving()">
          {{ isNew() ? 'Enregistrer' : 'Enregistrer les modifications' }}
        </app-button>
        <app-button variant="secondary" type="button" routerLink="/accounting/fixed-assets">
          {{ isNew() ? 'Annuler' : 'Retour au registre' }}
        </app-button>
      </div>
    </div>

    <!-- ============ Simulation du tableau (brouillon) ============ -->
    <div class="card" *ngIf="isDraftAsset() && !isNonDepreciable()">
      <div class="schedule-header">
        <h3>Simulation du tableau d'amortissement</h3>
        <div class="actions-inline">
          <label class="inline-field">
            Date de mise en service hypothétique
            <input type="date" class="accounting-filter-input" [(ngModel)]="previewDate" />
          </label>
          <app-button variant="secondary" type="button" (click)="previewSchedule()" [disabled]="saving()">
            Simuler le tableau
          </app-button>
        </div>
      </div>
      <ng-container *ngIf="preview() as p">
        <ng-container *ngTemplateOutlet="normTable; context: { $implicit: p, simulated: true }"></ng-container>
      </ng-container>
    </div>

    <!-- ============ Mise en service ============ -->
    <div class="card" #putInServiceSection id="put-in-service" *ngIf="isDraftAsset()">
      <h3>Mise en service</h3>
      <p class="hint" *ngIf="!hasPositiveAcquisitionCost()">
        Renseignez un prix d'achat HT positif (onglet Acquisition) avant la mise en service et la comptabilisation JIM.
      </p>
      <p class="hint" *ngIf="isFromSupplierInvoice()">
        Cet actif provient d'une facture fournisseur déjà comptabilisée (journal des achats) :
        aucune écriture d'acquisition supplémentaire ne sera générée.
      </p>
      <div class="form-grid">
        <label>
          Date de mise en service *
          <input type="date" class="accounting-filter-input" [(ngModel)]="inServiceDate" />
        </label>
        <label *ngIf="!isFromSupplierInvoice()">
          Compte de crédit (404, 532, 101…) *
          <input class="accounting-filter-input" [(ngModel)]="creditAccount" placeholder="404" />
        </label>
      </div>
      <app-button variant="primary" type="button" (click)="putInService()" [disabled]="saving()">
        {{ isFromSupplierInvoice() ? 'Mettre en service' : 'Mettre en service et comptabiliser' }}
      </app-button>
    </div>

    <!-- ============ Tableau d'amortissement (actif en service) ============ -->
    <div class="card" *ngIf="isInServiceAsset()">
      <div class="schedule-header">
        <h3>Tableau d'amortissement</h3>
        <div class="actions-inline" *ngIf="canShowScheduleActions()">
          <app-button variant="secondary" type="button" (click)="generateSchedule()" [disabled]="saving() || hasPostedLines()">
            {{ schedule()?.lines?.length ? 'Regénérer' : 'Générer' }} le tableau
          </app-button>
          <app-button
            variant="outline"
            type="button"
            icon="pi-download"
            (click)="exportSchedule()"
            [disabled]="saving() || !schedule()?.lines?.length">
            Export Excel
          </app-button>
        </div>
      </div>
      <p class="hint" *ngIf="hasPostedLines()">
        Des dotations sont déjà comptabilisées : le tableau ne peut plus être regénéré.
      </p>

      <ng-container *ngIf="schedule() as s">
        <ng-container *ngTemplateOutlet="normTable; context: { $implicit: s, simulated: false }"></ng-container>

        <h4 class="detail-title" *ngIf="s.lines.length">Détail CP17</h4>
        <p-table *ngIf="s.lines.length" [value]="s.lines" styleClass="p-datatable-sm">
          <ng-template pTemplate="header">
            <tr>
              <th>Exercice</th>
              <th class="text-right">VNC début</th>
              <th class="text-right">Annuité normale</th>
              <th class="text-right">Amort. antérieurs</th>
              <th class="text-right">Dotation exercice</th>
              <th class="text-right">Cumul</th>
              <th class="text-right">VNC fin</th>
              <th>Comptabilisé</th>
            </tr>
          </ng-template>
          <ng-template pTemplate="body" let-line>
            <tr>
              <td>{{ line.fiscalYear }}</td>
              <td class="text-right">{{ line.openingNbv | number: '1.3-3' }}</td>
              <td class="text-right">{{ line.normalAnnualAmount | number: '1.3-3' }}</td>
              <td class="text-right">{{ line.priorAccumulatedDepreciation | number: '1.3-3' }}</td>
              <td class="text-right">{{ line.depreciationAmount | number: '1.3-3' }}</td>
              <td class="text-right">{{ line.accumulatedDepreciation | number: '1.3-3' }}</td>
              <td class="text-right">{{ line.closingNbv | number: '1.3-3' }}</td>
              <td>{{ line.isPosted ? 'Oui' : 'Non' }}</td>
            </tr>
          </ng-template>
        </p-table>
      </ng-container>
    </div>

    <!-- ============ Tableau au format norme tunisienne (template partagé) ============ -->
    <ng-template #normTable let-s let-simulated="simulated">
      <div class="norm-summary" *ngIf="s.lines.length">
        <div><strong>Nature du bien :</strong> {{ s.label }}</div>
        <div><strong>Montant amortissable :</strong> {{ s.depreciableBase | number: '1.3-3' }} TND</div>
        <div><strong>Date de mise en service :</strong> {{ s.inServiceDate ? (s.inServiceDate | date: 'dd/MM/yyyy') : '—' }}</div>
        <div><strong>Durée de vie :</strong> {{ s.usefulLifeYears | number: '1.0-2' }} ans</div>
        <div><strong>Taux d'amortissement :</strong> {{ s.depreciationRatePercent | number: '1.2-2' }} %</div>
        <div>
          <strong>Méthode :</strong> {{ methodLabel(s.depreciationMethod) }}
          <ng-container *ngIf="s.depreciationMethod === DepreciationMethod.Accelerated"> (coefficient {{ s.accelerationCoefficient | number: '1.1-2' }})</ng-container>
        </div>
      </div>
      <table class="norm-table" *ngIf="s.lines.length">
        <thead>
          <tr>
            <th>Année</th>
            <th class="text-right">Base</th>
            <th class="text-right">Annuité</th>
            <th class="text-right">Annuités cumulées</th>
            <th class="text-right">Valeur nette comptable</th>
          </tr>
        </thead>
        <tbody>
          <tr *ngFor="let line of s.lines">
            <td>{{ line.fiscalYear }}</td>
            <td class="text-right">{{ s.depreciableBase | number: '1.3-3' }}</td>
            <td class="text-right">{{ line.depreciationAmount | number: '1.3-3' }}</td>
            <td class="text-right">{{ line.accumulatedDepreciation | number: '1.3-3' }}</td>
            <td class="text-right">{{ line.closingNbv | number: '1.3-3' }}</td>
          </tr>
          <tr class="total-row">
            <td><strong>TOTAL</strong></td>
            <td></td>
            <td class="text-right"><strong>{{ totalDepreciation(s) | number: '1.3-3' }}</strong></td>
            <td></td>
            <td></td>
          </tr>
        </tbody>
      </table>
      <p class="hint" *ngIf="!s.lines.length">Aucune ligne d'amortissement (bien non amortissable ou base nulle).</p>
    </ng-template>

    <!-- ============ Cession ============ -->
    <div class="card" *ngIf="canDisposeAsset()">
      <h3>Cession / mise au rebut</h3>
      <div class="form-grid">
        <label>
          Date de cession *
          <input type="date" class="accounting-filter-input" [(ngModel)]="disposalDate" />
        </label>
        <label>
          Prix de cession (TND)
          <input type="number" step="0.001" min="0" class="accounting-filter-input" [(ngModel)]="disposalProceeds" />
        </label>
        <label>
          Compte trésorerie (5321, 5411…) *
          <input class="accounting-filter-input" [(ngModel)]="treasuryAccount" placeholder="5321" />
        </label>
      </div>
      <app-button variant="danger" type="button" (click)="dispose()" [disabled]="saving()">Enregistrer la cession</app-button>
    </div>

    <!-- ============ Méta ============ -->
    <div class="card meta" *ngIf="asset()">
      <p><strong>Statut :</strong> {{ statusLabel(asset()!) }}</p>
      <p><strong>Méthode :</strong> {{ methodLabel(asset()!.depreciationMethod) }}
        <ng-container *ngIf="asset()!.depreciationMethod === DepreciationMethod.Accelerated"> (coefficient {{ asset()!.accelerationCoefficient | number: '1.1-2' }})</ng-container>
      </p>
      <p><strong>Comptes :</strong> {{ asset()!.assetAccountNumber }} / {{ asset()!.depreciationAccountNumber }} / {{ asset()!.expenseAccountNumber }}</p>
      <p><strong>VNC actuelle :</strong> {{ asset()!.netBookValue | number: '1.3-3' }} TND</p>
      <p *ngIf="asset()!.supplierInvoiceId">
        <strong>Facture fournisseur :</strong>
        <a [routerLink]="['/supplier-invoices', asset()!.supplierInvoiceId]">Voir la facture source</a>
      </p>
    </div>
  `,
  styles: [
    `
      .form-grid {
        display: grid;
        grid-template-columns: repeat(auto-fit, minmax(220px, 1fr));
        gap: 1rem;
        margin-bottom: 1rem;
      }
      .span-2 {
        grid-column: 1 / -1;
      }
      label {
        display: flex;
        flex-direction: column;
        gap: 0.35rem;
        font-size: 0.85rem;
        font-weight: 500;
      }
      .inline-field {
        flex-direction: row;
        align-items: center;
        gap: 0.5rem;
        white-space: nowrap;
      }
      .form-section {
        border: 1px solid #dbeafe;
        border-radius: 8px;
        padding: 0.75rem 1rem 1rem;
        margin-bottom: 1rem;
      }
      .form-section legend {
        font-size: 0.85rem;
        font-weight: 600;
        color: #1d4ed8;
        padding: 0 0.4rem;
      }
      .field-error {
        color: #dc2626;
        font-size: 0.78rem;
        font-weight: 400;
      }
      .hint {
        font-size: 0.8rem;
        color: #64748b;
        margin: 0.25rem 0 0.75rem;
      }
      .actions,
      .actions-inline {
        display: flex;
        gap: 0.75rem;
        flex-wrap: wrap;
        align-items: center;
      }
      .schedule-header {
        display: flex;
        justify-content: space-between;
        align-items: center;
        margin-bottom: 1rem;
        flex-wrap: wrap;
        gap: 0.75rem;
      }
      .text-right {
        text-align: right;
      }
      .meta p {
        margin: 0.25rem 0;
      }
      .norm-summary {
        display: grid;
        grid-template-columns: repeat(auto-fit, minmax(240px, 1fr));
        gap: 0.35rem 1.5rem;
        font-size: 0.85rem;
        margin-bottom: 0.85rem;
        padding: 0.65rem 0.85rem;
        background: #f8fafc;
        border-radius: 8px;
      }
      .norm-table {
        width: 100%;
        border-collapse: collapse;
        font-size: 0.875rem;
        margin-bottom: 1rem;
      }
      .norm-table th,
      .norm-table td {
        border: 1px solid #cbd5e1;
        padding: 0.4rem 0.65rem;
      }
      .norm-table thead th {
        background: #e0f2fe;
        font-weight: 600;
      }
      .norm-table .total-row td {
        background: #f1f5f9;
      }
      .detail-title {
        margin: 1rem 0 0.5rem;
        font-size: 0.95rem;
      }
      .refresh-row {
        margin: 0 0 0.75rem;
      }
    `
  ]
})
export class FixedAssetDetailComponent implements OnInit {
  private readonly api = inject(FixedAssetsService);
  private readonly supplierApi = inject(SupplierService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  @ViewChild('putInServiceSection') putInServiceSection?: ElementRef<HTMLElement>;

  readonly DepreciationMethod = DepreciationMethod;
  readonly ACCELERATION_COEFFICIENTS = ACCELERATION_COEFFICIENTS;
  readonly LOW_VALUE_ASSET_CEILING_TND = LOW_VALUE_ASSET_CEILING_TND;
  readonly integralSuggestionMessage =
    `Base amortissable ≤ ${LOW_VALUE_ASSET_CEILING_TND} DT : l'amortissement intégral est recommandé (Décret 2008-492 art. 4).`;

  readonly isNew = signal(false);
  readonly asset = signal<FixedAssetDto | null>(null);
  readonly schedule = signal<FixedAssetScheduleDto | null>(null);
  readonly preview = signal<FixedAssetScheduleDto | null>(null);
  readonly categories = signal<DepreciationRateCategoryDto[]>([]);
  readonly suppliers = signal<SupplierListItem[]>([]);
  readonly saving = signal(false);
  readonly error = signal<string | null>(null);
  readonly success = signal<string | null>(null);
  readonly fieldErrors = signal<Record<string, string>>({});

  /** Lecture seule dès que l'actif est en service, totalement amorti ou cédé. */
  readonly readonly = computed(() => {
    const a = this.asset();
    return !this.isNew() && (!a || isInServiceOrBeyond(a.status));
  });

  form: AssetFormModel = this.emptyForm();

  inServiceDate = todayLocalYmd();
  creditAccount = '404';
  previewDate = todayLocalYmd();
  disposalDate = todayLocalYmd();
  disposalProceeds = 0;
  treasuryAccount = '5321';

  ngOnInit(): void {
    this.api.getRateCategories().subscribe(res => this.categories.set(res.data ?? []));

    const id = this.route.snapshot.paramMap.get('id');
    if (this.route.snapshot.data['mode'] === 'new' || id === 'new' || !id) {
      this.isNew.set(true);
      this.loadSuppliers();
      return;
    }
    this.loadAsset(id);
    if (this.route.snapshot.queryParamMap.get('created') === '1') {
      this.success.set('Brouillon créé — complétez la fiche puis procédez à la mise en service.');
      afterNextRender(() => this.scrollToPutInService());
    }
  }

  isDraftAsset(): boolean {
    const a = this.asset();
    return !!a && isDraftStatus(a.status);
  }

  isInServiceAsset(): boolean {
    const a = this.asset();
    return !!a && isInServiceOrBeyond(a.status);
  }

  canShowScheduleActions(): boolean {
    const a = this.asset();
    return !!a && isActiveAssetStatus(a.status);
  }

  canDisposeAsset(): boolean {
    const a = this.asset();
    return !!a && canDisposeStatus(a.status);
  }

  hasPositiveAcquisitionCost(): boolean {
    return (this.form.acquisitionCost || 0) + (this.form.capitalizedFees || 0) > 0;
  }

  private scrollToPutInService(): void {
    setTimeout(() => {
      this.putInServiceSection?.nativeElement.scrollIntoView({ behavior: 'smooth', block: 'start' });
    }, 150);
  }

  // ----------------------------------------------------------------
  // Formulaire
  // ----------------------------------------------------------------

  private emptyForm(): AssetFormModel {
    return {
      label: '',
      depreciationRateCategoryId: '',
      description: '',
      location: '',
      depreciationMethod: DepreciationMethod.Linear,
      accelerationCoefficient: 1.5,
      depreciationRatePercent: null,
      usefulLifeYears: null,
      acquisitionDate: todayLocalYmd(),
      supplierId: '',
      acquisitionCost: 0,
      vatAmount: 0,
      capitalizedFees: 0,
      residualValue: 0,
      assetAccountNumber: '',
      depreciationAccountNumber: '',
      expenseAccountNumber: ''
    };
  }

  private fillFormFromAsset(a: FixedAssetDto): void {
    this.form = {
      label: a.label,
      depreciationRateCategoryId: a.depreciationRateCategoryId,
      description: a.description ?? '',
      location: a.location ?? '',
      depreciationMethod: parseDepreciationMethod(a.depreciationMethod),
      accelerationCoefficient: a.accelerationCoefficient === 2 ? 2 : 1.5,
      depreciationRatePercent: a.depreciationRatePercent || null,
      usefulLifeYears: a.usefulLifeYears || null,
      acquisitionDate: (a.acquisitionDate ?? '').substring(0, 10),
      supplierId: a.supplierId ?? '',
      acquisitionCost: a.acquisitionCost,
      vatAmount: a.vatAmount,
      capitalizedFees: a.capitalizedFees,
      residualValue: a.residualValue,
      assetAccountNumber: a.assetAccountNumber,
      depreciationAccountNumber: a.depreciationAccountNumber,
      expenseAccountNumber: a.expenseAccountNumber
    };
  }

  suggestIntegral(): boolean {
    return !this.isNonDepreciable() && this.depreciableBase() > 0 && this.depreciableBase() <= LOW_VALUE_ASSET_CEILING_TND;
  }

  onMethodChange(method: DepreciationMethod): void {
    if (method === DepreciationMethod.Accelerated && !ACCELERATION_COEFFICIENTS.includes(this.form.accelerationCoefficient)) {
      this.form.accelerationCoefficient = 1.5;
    }
  }

  isNonDepreciable(): boolean {
    const cat = this.categories().find(c => c.id === this.form.depreciationRateCategoryId);
    return cat?.isNonDepreciable ?? false;
  }

  isFromSupplierInvoice(): boolean {
    return !!this.asset()?.supplierInvoiceId;
  }

  hasPostedLines(): boolean {
    return this.schedule()?.lines?.some(l => l.isPosted) ?? false;
  }

  depreciableBase(): number {
    const base = (this.form.acquisitionCost || 0) + (this.form.capitalizedFees || 0) - (this.form.residualValue || 0);
    return base > 0 ? base : 0;
  }

  onCategoryChange(categoryId: string): void {
    const cat = this.categories().find(c => c.id === categoryId);
    if (!cat) return;
    this.form.assetAccountNumber = cat.defaultAssetAccount;
    this.form.depreciationAccountNumber = cat.defaultDepreciationAccount;
    this.form.expenseAccountNumber = cat.defaultExpenseAccount;
    if (cat.isNonDepreciable) {
      this.form.depreciationRatePercent = null;
      this.form.usefulLifeYears = null;
      this.form.depreciationMethod = DepreciationMethod.Linear;
    } else {
      this.form.depreciationRatePercent = cat.legalRatePercent;
      this.form.usefulLifeYears = cat.usefulLifeYears;
    }
  }

  onRateChange(rate: number | null): void {
    if (rate && rate > 0 && rate <= 100) {
      this.form.usefulLifeYears = Math.round((100 / rate) * 100) / 100;
    }
  }

  onLifeChange(years: number | null): void {
    if (years && years > 0) {
      this.form.depreciationRatePercent = Math.round((100 / years) * 10000) / 10000;
    }
  }

  private validate(): boolean {
    const errors: Record<string, string> = {};
    if (!this.form.label.trim()) errors['label'] = 'La désignation est obligatoire.';
    if (!this.form.depreciationRateCategoryId) errors['category'] = 'La catégorie est obligatoire.';
    if (!this.form.acquisitionDate) errors['acquisitionDate'] = "La date d'acquisition est obligatoire.";
    const total = (this.form.acquisitionCost || 0) + (this.form.capitalizedFees || 0);
    if (total <= 0) errors['cost'] = "Le coût d'acquisition doit être positif.";
    if ((this.form.residualValue || 0) >= total && total > 0)
      errors['residual'] = 'La valeur résiduelle doit être inférieure au coût total.';
    if (!this.isNonDepreciable()) {
      const rate = this.form.depreciationRatePercent;
      if (rate !== null && (rate <= 0 || rate > 100)) errors['rate'] = 'Le taux doit être compris entre 0 et 100 %.';
      if (this.form.depreciationMethod === DepreciationMethod.Accelerated && !ACCELERATION_COEFFICIENTS.includes(this.form.accelerationCoefficient))
        errors['coefficient'] = 'Le coefficient accéléré doit être 1,5 ou 2 (Décret 2008-492 art. 2).';
    }
    if (!this.form.assetAccountNumber.trim() || !this.form.depreciationAccountNumber.trim() || !this.form.expenseAccountNumber.trim())
      errors['accounts'] = 'Les trois comptes comptables sont obligatoires.';
    this.fieldErrors.set(errors);
    return Object.keys(errors).length === 0;
  }

  // ----------------------------------------------------------------
  // Actions
  // ----------------------------------------------------------------

  save(): void {
    this.error.set(null);
    this.success.set(null);
    if (!this.validate()) {
      this.error.set('Veuillez corriger les champs en erreur.');
      return;
    }
    if (this.isNew()) this.create();
    else this.updateDraft();
  }

  private create(): void {
    this.saving.set(true);
    this.api
      .create({
        label: this.form.label,
        depreciationRateCategoryId: this.form.depreciationRateCategoryId,
        acquisitionCost: this.form.acquisitionCost,
        capitalizedFees: this.form.capitalizedFees,
        residualValue: this.form.residualValue,
        acquisitionDate: this.form.acquisitionDate,
        description: this.form.description || undefined,
        vatAmount: this.form.vatAmount || 0,
        location: this.form.location || undefined,
        supplierId: this.form.supplierId || undefined,
        assetAccountNumber: this.form.assetAccountNumber || undefined,
        depreciationAccountNumber: this.form.depreciationAccountNumber || undefined,
        expenseAccountNumber: this.form.expenseAccountNumber || undefined,
        depreciationMethod: this.form.depreciationMethod,
        accelerationCoefficient: this.form.depreciationMethod === DepreciationMethod.Accelerated ? this.form.accelerationCoefficient : 1,
        depreciationRatePercent: this.isNonDepreciable() ? null : this.form.depreciationRatePercent,
        usefulLifeYears: this.isNonDepreciable() ? null : this.form.usefulLifeYears
      })
      .subscribe({
        next: res => {
          this.saving.set(false);
          if (res.data) {
            this.router.navigate(['/accounting/fixed-assets', res.data], { queryParams: { created: '1' } });
          } else {
            this.error.set(res.message ?? 'Erreur lors de la création.');
          }
        },
        error: err => {
          this.saving.set(false);
          this.error.set(err?.error?.message ?? 'Erreur lors de la création.');
        }
      });
  }

  private updateDraft(): void {
    const id = this.asset()?.id;
    if (!id) return;
    const req: UpdateFixedAssetRequest = {
      label: this.form.label,
      acquisitionCost: this.form.acquisitionCost,
      capitalizedFees: this.form.capitalizedFees,
      residualValue: this.form.residualValue,
      acquisitionDate: this.form.acquisitionDate,
      description: this.form.description || undefined,
      location: this.form.location || undefined,
      assetAccountNumber: this.form.assetAccountNumber || undefined,
      depreciationAccountNumber: this.form.depreciationAccountNumber || undefined,
      expenseAccountNumber: this.form.expenseAccountNumber || undefined,
      depreciationRateCategoryId: this.form.depreciationRateCategoryId || null,
      depreciationMethod: this.form.depreciationMethod,
      accelerationCoefficient: this.form.depreciationMethod === DepreciationMethod.Accelerated ? this.form.accelerationCoefficient : 1,
      depreciationRatePercent: this.isNonDepreciable() ? null : this.form.depreciationRatePercent,
      usefulLifeYears: this.isNonDepreciable() ? null : this.form.usefulLifeYears,
      vatAmount: this.form.vatAmount || 0
    };
    this.saving.set(true);
    this.api.update(id, req).subscribe({
      next: () => {
        this.saving.set(false);
        this.error.set(null);
        this.success.set('Modifications enregistrées.');
        this.preview.set(null);
        this.loadAsset(id);
      },
      error: err => {
        this.saving.set(false);
        this.error.set(err?.error?.message ?? "Erreur lors de l'enregistrement.");
      }
    });
  }

  previewSchedule(): void {
    const id = this.asset()?.id;
    if (!id) return;
    this.saving.set(true);
    this.error.set(null);
    this.api.previewSchedule(id, this.previewDate).subscribe({
      next: res => {
        this.saving.set(false);
        this.preview.set(res.data ?? null);
      },
      error: err => {
        this.saving.set(false);
        this.error.set(err?.error?.message ?? 'Erreur lors de la simulation.');
      }
    });
  }

  putInService(): void {
    if (this.saving()) return;
    const id = this.asset()?.id;
    if (!id) return;
    this.saving.set(true);
    this.error.set(null);
    this.success.set(null);
    this.api.putInService(id, { inServiceDate: this.inServiceDate, creditAccountNumber: this.creditAccount || '404' }).subscribe({
      next: () => {
        this.saving.set(false);
        this.error.set(null);
        this.success.set(
          this.isFromSupplierInvoice()
            ? "Immobilisation mise en service et tableau d'amortissement généré."
            : "Immobilisation mise en service, écriture JIM générée et tableau d'amortissement créé."
        );
        this.preview.set(null);
        this.loadAsset(id);
      },
      error: err => {
        this.saving.set(false);
        this.success.set(null);
        const body = err?.error;
        if (body?.code === 'CONCURRENCY_CONFLICT') {
          this.error.set(
            'Les données ont été modifiées entre-temps. La fiche a été actualisée — vérifiez le statut puis réessayez si nécessaire.'
          );
        } else {
          this.error.set(body?.message ?? 'Erreur mise en service.');
        }
        this.loadAsset(id);
      }
    });
  }

  refreshAsset(): void {
    const id = this.asset()?.id;
    if (!id) return;
    this.error.set(null);
    this.success.set(null);
    this.loadAsset(id);
  }

  generateSchedule(): void {
    const id = this.asset()?.id;
    if (!id) return;
    this.saving.set(true);
    this.api.generateSchedule(id).subscribe({
      next: res => {
        this.schedule.set(res.data ?? null);
        this.saving.set(false);
        this.success.set("Tableau d'amortissement généré.");
      },
      error: err => {
        this.saving.set(false);
        this.error.set(err?.error?.message ?? 'Erreur génération tableau.');
      }
    });
  }

  exportSchedule(): void {
    const id = this.asset()?.id;
    if (!id) return;
    this.saving.set(true);
    this.error.set(null);
    this.api.exportScheduleExcel(id).subscribe({
      next: blob => {
        this.saving.set(false);
        const label = this.asset()?.inventoryNumber ?? id;
        this.downloadBlob(blob, `tableau-amortissement-${label}.xlsx`);
        this.success.set('Export Excel téléchargé.');
      },
      error: err => {
        this.saving.set(false);
        this.error.set(err?.error?.message ?? 'Erreur export Excel.');
      }
    });
  }

  dispose(): void {
    const id = this.asset()?.id;
    if (!id) return;
    this.saving.set(true);
    this.api
      .dispose(id, {
        disposalDate: this.disposalDate,
        disposalProceeds: this.disposalProceeds,
        treasuryAccountNumber: this.treasuryAccount
      })
      .subscribe({
        next: () => {
          this.saving.set(false);
          this.success.set('Cession enregistrée et écritures comptables générées.');
          this.loadAsset(id);
        },
        error: err => {
          this.saving.set(false);
          this.error.set(err?.error?.message ?? 'Erreur cession.');
        }
      });
  }

  private downloadBlob(blob: Blob, filename: string): void {
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = filename;
    a.click();
    URL.revokeObjectURL(url);
  }

  private loadSuppliers(): void {
    this.supplierApi.getSuppliers({ isActive: true, pageSize: 200, skipGlobalErrorUi: true }).subscribe({
      next: res => this.suppliers.set(res.data?.items ?? []),
      error: () => this.suppliers.set([])
    });
  }

  private loadAsset(id: string): void {
    this.api.getById(id).subscribe({
      next: res => {
        this.asset.set(res.data ?? null);
        if (res.data) {
          this.fillFormFromAsset(res.data);
          if (isDraftStatus(res.data.status)) {
            this.loadSuppliers();
          }
          this.api.getSchedule(id).subscribe(s => this.schedule.set(s.data ?? null));
        }
      },
      error: () => this.error.set('Immobilisation introuvable.')
    });
  }

  totalDepreciation(s: FixedAssetScheduleDto): number {
    return s.lines.reduce((sum, l) => sum + l.depreciationAmount, 0);
  }

  statusLabel(a: FixedAssetDto): string {
    return FIXED_ASSET_STATUS_LABELS[a.status] ?? '—';
  }

  methodLabel(m: DepreciationMethod): string {
    return DEPRECIATION_METHOD_LABELS[m] ?? 'Linéaire';
  }

  pageTitle(): string {
    return this.isNew() ? 'Nouvelle immobilisation' : (this.asset()?.label ?? 'Immobilisation');
  }

  pageSubtitle(): string {
    return this.asset()?.inventoryNumber ?? "Saisie et tableau d'amortissement";
  }
}
