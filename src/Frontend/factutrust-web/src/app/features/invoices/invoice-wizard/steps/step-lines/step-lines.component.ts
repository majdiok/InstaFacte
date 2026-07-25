import { Component, OnInit, OnDestroy, inject, signal, computed } from '@angular/core';
import { CommonModule, DecimalPipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Subject, takeUntil, debounceTime, distinctUntilChanged, switchMap, catchError, of } from 'rxjs';

// PrimeNG
import { TableModule } from 'primeng/table';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { InputNumberModule } from 'primeng/inputnumber';
import { DropdownModule } from 'primeng/dropdown';
import { AutoCompleteModule } from 'primeng/autocomplete';
import { TooltipModule } from 'primeng/tooltip';
import { DividerModule } from 'primeng/divider';
import { CardModule } from 'primeng/card';
import { DialogModule } from 'primeng/dialog';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { ConfirmationService } from '@core/services/confirmation.service';
import { ToastService } from '@core/services/toast.service';
import { QuickCreateProductDialogComponent } from '@shared/components/quick-create-product-dialog/quick-create-product-dialog.component';
import { ButtonComponent } from '@shared/components/button/button.component';

// Services & Models
import { InvoiceWizardService } from '../../services/invoice-wizard.service';
import { ProductService, ProductListItem } from '@core/services/product.service';
import {
  InvoiceLine,
  TunisianVatRate,
  Currency,
  VAT_RATE_OPTIONS
} from '../../models/invoice-wizard.models';

/**
 * Étape 4 - Lignes de facturation
 * 
 * Permet de gérer les lignes de facture avec:
 * - Ajout/modification/suppression de lignes
 * - Sélection de produits depuis le catalogue
 * - Saisie libre de désignations
 * - Calculs automatiques (HT, TVA, TTC)
 * - Gestion des remises (% ou montant)
 * 
 * Conformité tunisienne:
 * - Taux de TVA: 0%, 7%, 13%, 19%
 * - Calculs conformes à la réglementation
 * - Affichage détaillé par taux de TVA
 */
@Component({
  selector: 'app-step-lines',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    TableModule,
    ButtonModule,
    InputTextModule,
    InputNumberModule,
    DropdownModule,
    AutoCompleteModule,
    TooltipModule,
    DividerModule,
    CardModule,
    DialogModule,
    ConfirmDialogModule,
    DecimalPipe,
    QuickCreateProductDialogComponent,
    ButtonComponent
  ],
  template: `
    <div class="step-lines">
      <!-- Header -->
      <section class="section">
        <div class="section-header">
          <div>
            <h2 class="section-title">
              <i class="pi pi-list"></i>
              Lignes de facturation
            </h2>
            <p class="section-description">
              Ajoutez les produits ou services à facturer
            </p>
          </div>
          <div class="header-actions">
            <button 
              type="button"
              class="mode-toggle"
              [class.active]="!simpleMode()"
              (click)="toggleSimpleMode()"
              pTooltip="Afficher/masquer les colonnes avancées (Unité, Remise, TVA détaillée)">
              <i class="pi" [class.pi-eye]="simpleMode()" [class.pi-eye-slash]="!simpleMode()"></i>
              {{ simpleMode() ? 'Mode avancé' : 'Mode simple' }}
            </button>
            <p-button
              label="Ajouter une ligne"
              icon="pi pi-plus"
              (click)="addNewLine()">
            </p-button>
          </div>
        </div>
      </section>

      <!-- Unmatched Lines Banner (typically after AI import when products are not in the catalog) -->
      @if (unmatchedLines().length > 0) {
        <div class="unmatched-banner" role="alert">
          <i class="pi pi-exclamation-triangle"></i>
          <div class="unmatched-content">
            <strong>{{ unmatchedLines().length }} ligne(s) à associer à un produit</strong>
            <p>Ces articles ne sont pas reliés à votre catalogue. Liez chaque ligne à un produit existant ou créez un nouveau produit pour pouvoir émettre la facture.</p>
          </div>
          <p-button
            label="Résoudre la première"
            icon="pi pi-arrow-right"
            iconPos="right"
            severity="warn"
            (click)="resolveNextUnmatched()">
          </p-button>
        </div>
      }

      <!-- Lines Table -->
      @if (lines().length > 0) {
        <div class="lines-table-container">
          <p-table 
            [value]="lines()" 
            [scrollable]="true"
            scrollHeight="55vh"
            styleClass="p-datatable-sm lines-table"
            [reorderableColumns]="true">
            
            <ng-template pTemplate="header">
              <tr>
                <th style="width: 3%">#</th>
                <th style="width: 28%">Désignation</th>
                <th style="width: 8%" class="text-right">Quantité</th>
                @if (!simpleMode()) {
                  <th style="width: 7%">Unité</th>
                }
                <th style="width: 10%" class="text-right">Prix unit. HT</th>
                @if (!simpleMode()) {
                  <th style="width: 10%">Remise</th>
                  <th style="width: 8%" class="col-vat-rate">TVA</th>
                  <th style="width: 9%" class="text-right">Total HT</th>
                  <th style="width: 6%" class="text-right">TVA</th>
                }
                <th style="width: 10%" class="text-right">Total TTC</th>
                <th style="width: 7%">Actions</th>
              </tr>
            </ng-template>

            <ng-template pTemplate="body" let-line let-index="rowIndex">
              <tr [class.editing]="editingLineId === line.id">
                <!-- Line Number -->
                <td class="line-number">{{ line.lineNumber }}</td>
                
                <!-- Designation -->
                <td>
                  @if (editingLineId === line.id) {
                    <div class="edit-cell">
                      <div class="edit-cell-product-row">
                        <div class="product-cell-input">
                          <p-autoComplete
                            [(ngModel)]="editLine.designation"
                            [suggestions]="productSuggestions()"
                            (completeMethod)="searchProducts($event)"
                            (onSelect)="onProductSelect($event)"
                            field="name"
                            [dropdown]="true"
                            [minLength]="0"
                            placeholder="Rechercher un produit..."
                            appendTo="body"
                            [style]="{ width: '100%' }">
                            <ng-template let-product pTemplate="item">
                              <div class="product-suggestion">
                                <span class="product-code">{{ product.code }}</span>
                                <span class="product-name">{{ product.name }}</span>
                                @if (product.isFodecApplicable) {
                                  <span class="fodec-badge fodec-badge--suggestion">FODEC</span>
                                }
                                <span class="product-price">{{ product.unitPrice | number:'1.3-3' }} TND</span>
                              </div>
                            </ng-template>
                            <ng-template pTemplate="empty">
                              <div class="product-empty">
                                @if (isLoadingProducts()) {
                                  <i class="pi pi-spin pi-spinner"></i>
                                  <span>Chargement des produits...</span>
                                } @else {
                                  <div class="product-empty-content">
                                    <span>Aucun produit trouvé</span>
                                    <app-button
                                      variant="primary"
                                      size="sm"
                                      icon="pi-plus"
                                      [iconOnly]="true"
                                      ariaLabel="Créer un produit"
                                      (click)="openQuickCreateProduct(); $event.stopPropagation()"
                                      class="btn-add-product-circle btn-add-product-circle--dropdown" />
                                  </div>
                                }
                              </div>
                            </ng-template>
                          </p-autoComplete>
                        </div>
                        <app-button
                          variant="primary"
                          size="sm"
                          icon="pi-plus"
                          [iconOnly]="true"
                          ariaLabel="Créer un produit"
                          (click)="openQuickCreateProduct(); $event.stopPropagation()"
                          class="btn-add-product-circle" />
                      </div>
                      <textarea
                        pInputTextarea
                        [(ngModel)]="editLine.description"
                        placeholder="Description (optionnel)"
                        rows="2"
                        class="description-input">
                      </textarea>
                    </div>
                  } @else {
                    <div class="designation-cell">
                      <div class="designation-main-row">
                        <span class="designation-main">{{ line.designation }}</span>
                        @if (!line.productId) {
                          <span class="unmatched-badge" pTooltip="Cette ligne n'est pas associée à un produit du catalogue">
                            <i class="pi pi-exclamation-circle"></i> À lier
                          </span>
                        }
                        @if (simpleMode() && line.isFodecApplicable) {
                          <span class="fodec-badge" pTooltip="FODEC applicable (1%)">FODEC</span>
                        }
                      </div>
                      @if (line.description) {
                        <span class="designation-desc">{{ line.description }}</span>
                      }
                    </div>
                  }
                </td>

                <!-- Quantity -->
                <td class="text-right">
                  @if (editingLineId === line.id) {
                    <p-inputNumber
                      [(ngModel)]="editLine.quantity"
                      [min]="0.001"
                      [minFractionDigits]="0"
                      [maxFractionDigits]="3"
                      mode="decimal"
                      [style]="{ width: '80px' }">
                    </p-inputNumber>
                  } @else {
                    {{ line.quantity | number:'1.0-3' }}
                  }
                </td>

                <!-- Unit -->
                @if (!simpleMode()) {
                <td>
                  @if (editingLineId === line.id) {
                    <input
                      pInputText
                      [(ngModel)]="editLine.unit"
                      placeholder="Unité"
                      [style]="{ width: '80px' }">
                  } @else {
                    {{ line.unit || 'Unité' }}
                  }
                </td>
                }

                <!-- Unit Price HT -->
                <td class="text-right">
                  @if (editingLineId === line.id) {
                    <p-inputNumber
                      [(ngModel)]="editLine.unitPriceHT"
                      [min]="0"
                      [minFractionDigits]="3"
                      [maxFractionDigits]="3"
                      mode="decimal"
                      suffix=" TND"
                      [style]="{ width: '120px' }">
                    </p-inputNumber>
                  } @else {
                    <span class="amount">{{ line.unitPriceHT | number:'1.3-3' }}</span>
                  }
                </td>

                <!-- Discount -->
                @if (!simpleMode()) {
                <td class="col-discount">
                  @if (editingLineId === line.id) {
                    <div class="discount-input">
                      <p-inputNumber
                        [(ngModel)]="editLine.discountValue"
                        [min]="0"
                        [maxFractionDigits]="2"
                        mode="decimal"
                        [style]="{ width: '60px' }">
                      </p-inputNumber>
                      <p-dropdown
                        [(ngModel)]="editLine.discountType"
                        [options]="discountTypeOptions"
                        optionLabel="label"
                        optionValue="value"
                        appendTo="body"
                        styleClass="discount-type-dropdown"
                        [style]="{ width: '56px' }"
                        [panelStyle]="{ minWidth: '72px' }">
                      </p-dropdown>
                    </div>
                  } @else {
                    @if (line.discountAmount > 0) {
                      <span class="discount-badge">
                        -{{ line.discountValue }}{{ line.discountType === 'PERCENT' ? '%' : ' TND' }}
                      </span>
                    } @else {
                      <span class="text-muted">-</span>
                    }
                  }
                </td>
                }

                <!-- VAT Rate -->
                @if (!simpleMode()) {
                <td class="col-vat">
                  @if (editingLineId === line.id) {
                    <p-dropdown
                      [(ngModel)]="editLine.vatRate"
                      [options]="vatRateOptions"
                      optionLabel="label"
                      optionValue="value"
                      appendTo="body"
                      styleClass="vat-rate-dropdown"
                      [style]="{ width: '80px' }"
                      [panelStyle]="{ minWidth: '12rem' }">
                    </p-dropdown>
                  } @else {
                    <span class="vat-badge" [class]="'vat-' + line.vatRate">
                      {{ line.vatRate }}%
                    </span>
                    @if (line.isFodecApplicable) {
                      <span class="fodec-badge" pTooltip="FODEC applicable (1%)">FODEC</span>
                    }
                  }
                </td>
                }

                @if (!simpleMode()) {
                <!-- Total HT -->
                <td class="text-right">
                  <span class="amount">{{ line.totalHT | number:'1.3-3' }}</span>
                </td>

                <!-- VAT Amount -->
                <td class="text-right">
                  <span class="amount vat">{{ line.vatAmount | number:'1.3-3' }}</span>
                </td>
                }

                <!-- Total TTC -->
                <td class="text-right">
                  <span class="amount ttc">{{ line.totalTTC | number:'1.3-3' }}</span>
                </td>

                <!-- Actions -->
                <td>
                  <div class="line-actions">
                    @if (editingLineId === line.id) {
                      <p-button
                        icon="pi pi-check"
                        [text]="true"
                        [rounded]="true"
                        severity="success"
                        pTooltip="Valider"
                        (click)="saveLineEdit()">
                      </p-button>
                      <p-button
                        icon="pi pi-times"
                        [text]="true"
                        [rounded]="true"
                        severity="secondary"
                        pTooltip="Annuler"
                        (click)="cancelLineEdit()">
                      </p-button>
                    } @else {
                      <p-button
                        icon="pi pi-pencil"
                        [text]="true"
                        [rounded]="true"
                        pTooltip="Modifier"
                        (click)="startEditLine(line)">
                      </p-button>
                      <p-button
                        icon="pi pi-trash"
                        [text]="true"
                        [rounded]="true"
                        severity="danger"
                        pTooltip="Supprimer"
                        (click)="deleteLine(line)">
                      </p-button>
                    }
                  </div>
                </td>
              </tr>
            </ng-template>

            <ng-template pTemplate="emptymessage">
              <tr>
                <td colspan="11">
                  <div class="empty-lines">
                    <i class="pi pi-inbox"></i>
                    <p>Aucune ligne de facturation</p>
                    <p-button
                      label="Ajouter votre première ligne"
                      icon="pi pi-plus"
                      (click)="addNewLine()">
                    </p-button>
                  </div>
                </td>
              </tr>
            </ng-template>
          </p-table>
        </div>
      } @else {
        <div class="empty-state-card">
          <i class="pi pi-inbox"></i>
          <h3>Aucune ligne de facturation</h3>
          <p>Ajoutez des produits ou services pour continuer</p>
          <p-button
            label="Ajouter une ligne"
            icon="pi pi-plus"
            (click)="addNewLine()">
          </p-button>
        </div>
      }

      <!-- Totals Summary -->
      @if (lines().length > 0) {
        <p-divider></p-divider>

        <div class="totals-section">
          <div class="totals-grid">
            <!-- VAT Breakdown -->
            <div class="vat-breakdown-card">
              <h3>Récapitulatif TVA</h3>
              <table class="vat-breakdown-table">
                <thead>
                  <tr>
                    <th>Taux</th>
                    <th class="text-right">Base imposable</th>
                    <th class="text-right">TVA</th>
                  </tr>
                </thead>
                <tbody>
                  @for (item of totals().vatBreakdown; track item.rate) {
                    <tr>
                      <td>
                        <span class="vat-badge" [class]="'vat-' + item.rate">
                          {{ item.rateDisplay }}
                        </span>
                      </td>
                      <td class="text-right amount">{{ item.baseAmount | number:'1.3-3' }}</td>
                      <td class="text-right amount">{{ item.vatAmount | number:'1.3-3' }}</td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>

            <!-- Grand Total -->
            <div class="grand-total-card">
              <div class="total-row">
                <span class="total-label">Sous-total HT</span>
                <span class="total-value">{{ totals().subTotalHT | number:'1.3-3' }} {{ currency }}</span>
              </div>
              @if (totals().totalDiscount > 0) {
                <div class="total-row discount">
                  <span class="total-label">Remises</span>
                  <span class="total-value">-{{ totals().totalDiscount | number:'1.3-3' }} {{ currency }}</span>
                </div>
              }
              <div class="total-row">
                <span class="total-label">Total HT</span>
                <span class="total-value">{{ totals().totalHT | number:'1.3-3' }} {{ currency }}</span>
              </div>
              <div class="total-row">
                <span class="total-label">Total TVA</span>
                <span class="total-value">{{ totals().totalVat | number:'1.3-3' }} {{ currency }}</span>
              </div>
              @if (totals().totalFodec > 0.0005 || totals().totalFodec < -0.0005) {
                <div class="total-row">
                  <span class="total-label">FODEC ({{ totals().fodecRatePercent }}%)</span>
                  <span class="total-value">{{ totals().totalFodec | number:'1.3-3' }} {{ currency }}</span>
                </div>
              }
              @if (totals().fiscalStampAmount > 0.0005 || totals().fiscalStampAmount < -0.0005) {
                <div class="total-row">
                  <span class="total-label">Timbre fiscal</span>
                  <span class="total-value">{{ totals().fiscalStampAmount | number:'1.3-3' }} {{ currency }}</span>
                </div>
              }
              <p-divider></p-divider>
              <div class="total-row grand">
                <span class="total-label">Total TTC</span>
                <span class="total-value">{{ totals().totalTTC | number:'1.3-3' }} {{ currency }}</span>
              </div>
            </div>
          </div>
        </div>
      }

      <!-- Compliance Info -->
      <div class="compliance-info">
        <i class="pi pi-info-circle"></i>
        <span>
          Taux de TVA conformes à la législation tunisienne : 0% (exonéré), 7% (réduit), 13% (intermédiaire), 19% (normal)
        </span>
      </div>

      <app-quick-create-product-dialog
        [panelMode]="true"
        [(visible)]="quickCreateProductVisible"
        (productCreated)="onQuickProductCreated($event)">
      </app-quick-create-product-dialog>
    </div>

  `,
  styles: [`
    .step-lines {
      animation: fadeIn 0.3s ease-out;
    }

    @keyframes fadeIn {
      from { opacity: 0; transform: translateY(10px); }
      to { opacity: 1; transform: translateY(0); }
    }

    .section {
      margin-bottom: var(--spacing-4);
    }

    .section-header {
      display: flex;
      justify-content: space-between;
      align-items: flex-start;
      gap: var(--spacing-4);
    }

    .header-actions {
      display: flex;
      align-items: center;
      gap: var(--spacing-3);
    }

    .mode-toggle {
      display: inline-flex;
      align-items: center;
      gap: var(--spacing-2);
      padding: var(--spacing-2) var(--spacing-3);
      background: var(--color-neutral-100);
      border: 1px solid var(--color-neutral-200);
      border-radius: var(--radius-lg);
      cursor: pointer;
      font-size: var(--font-size-sm);
      font-weight: var(--font-weight-medium);
      color: var(--color-neutral-600);
      transition: all var(--transition-fast);
      font-family: inherit;
    }

    .mode-toggle:hover {
      background: var(--color-neutral-200);
      color: var(--color-neutral-800);
    }

    .mode-toggle.active {
      background: var(--color-primary-50);
      border-color: var(--color-primary-300);
      color: var(--color-primary-700);
    }

    .section-title {
      display: flex;
      align-items: center;
      gap: var(--spacing-2);
      margin: 0 0 var(--spacing-2);
      font-size: var(--font-size-lg);
      font-weight: var(--font-weight-semibold);
      color: var(--color-neutral-800);

      i { color: var(--color-primary-500); }
    }

    .section-description {
      margin: 0;
      font-size: var(--font-size-sm);
      color: var(--color-neutral-500);
    }

    // Unmatched lines banner
    .unmatched-banner {
      display: flex;
      align-items: center;
      gap: var(--spacing-3);
      padding: var(--spacing-4);
      margin-bottom: var(--spacing-4);
      background: var(--color-warning-50);
      border: 1px solid var(--color-warning-300);
      border-radius: var(--radius-lg);
      color: var(--color-warning-900);

      i.pi-exclamation-triangle {
        font-size: var(--font-size-xl);
        color: var(--color-warning-600);
        flex-shrink: 0;
      }
    }

    .unmatched-content {
      flex: 1;
    }

    .unmatched-content strong {
      display: block;
      margin-bottom: var(--spacing-1);
      font-weight: var(--font-weight-semibold);
    }

    .unmatched-content p {
      margin: 0;
      font-size: var(--font-size-sm);
    }

    .designation-main-row {
      display: flex;
      align-items: center;
      gap: var(--spacing-2);
      flex-wrap: wrap;
    }

    .unmatched-badge {
      display: inline-flex;
      align-items: center;
      gap: var(--spacing-1);
      padding: 2px var(--spacing-2);
      background: var(--color-warning-100);
      color: var(--color-warning-700);
      font-size: var(--font-size-xs);
      font-weight: var(--font-weight-semibold);
      border-radius: var(--radius-sm);

      i {
        font-size: var(--font-size-xs);
      }
    }

    // Lines Table
    .lines-table-container {
      background: white;
      border-radius: var(--radius-xl);
      border: 1px solid var(--color-neutral-200);
      overflow-x: hidden;
      overflow-y: visible;
    }

    ::ng-deep .lines-table {
      .p-datatable-scrollable-table {
        width: 100%;
        table-layout: fixed;
      }

      .p-datatable-thead > tr > th {
        background: var(--color-neutral-50);
        font-weight: var(--font-weight-semibold);
        font-size: var(--font-size-xs);
        text-transform: uppercase;
        color: var(--color-neutral-600);
        padding: var(--spacing-3) var(--spacing-4);
      }

      th.col-vat-rate,
      .p-datatable-tbody > tr > td.col-vat {
        min-width: 80px;
      }

      .p-datatable-tbody > tr > td {
        padding: var(--spacing-3) var(--spacing-4);
        vertical-align: middle;
      }

      .p-datatable-tbody > tr.editing {
        background: var(--color-primary-50);
      }

      .p-datatable-tbody > tr > td input,
      .p-datatable-tbody > tr > td .p-inputnumber,
      .p-datatable-tbody > tr > td .p-dropdown,
      .p-datatable-tbody > tr > td .p-autocomplete {
        max-width: 100%;
      }

      .p-datatable-tbody > tr > td .p-dropdown {
        min-width: 90px;
      }

      /* Exception: dropdown type remise (%/TND) plus compact */
      .col-discount .p-dropdown.discount-type-dropdown {
        min-width: unset;
      }

      /* Dropdown TVA (taux) : largeur adaptée pour afficher le % */
      .col-vat .p-dropdown.vat-rate-dropdown {
        width: 80px;
        min-width: 80px;
      }

      .col-vat .p-dropdown.vat-rate-dropdown .p-dropdown-label {
        min-width: 0;
        overflow: hidden;
        text-overflow: ellipsis;
        white-space: nowrap;
      }

      .p-dropdown .p-dropdown-label {
        min-width: 2.5rem;
      }
    }

    /* Espacement entre colonne Remise et colonne TVA (taux) */
    ::ng-deep .lines-table .col-discount {
      padding-right: 2.25rem;
    }

    ::ng-deep .lines-table .p-datatable-tbody > tr > td.col-vat {
      padding-left: 0.75rem;
    }

    .line-number {
      font-weight: var(--font-weight-semibold);
      color: var(--color-neutral-500);
      text-align: center;
    }

    .designation-cell {
      display: flex;
      flex-direction: column;
      gap: var(--spacing-1);
    }

    .designation-main {
      font-weight: var(--font-weight-medium);
      color: var(--color-neutral-800);
    }

    .designation-desc {
      font-size: var(--font-size-xs);
      color: var(--color-neutral-500);
    }

    .edit-cell {
      display: flex;
      flex-direction: column;
      gap: var(--spacing-2);
    }

    .description-input {
      font-size: var(--font-size-sm);
      resize: none;
    }

    .amount {
      font-family: 'JetBrains Mono', monospace;
      font-size: var(--font-size-sm);
      font-weight: var(--font-weight-medium);

      &.vat {
        color: var(--color-neutral-600);
      }

      &.ttc {
        color: var(--color-primary-700);
        font-weight: var(--font-weight-semibold);
      }
    }

    .discount-input {
      display: flex;
      flex-wrap: nowrap;
      align-items: center;
      gap: var(--spacing-1);
    }

    .discount-badge {
      display: inline-block;
      padding: var(--spacing-1) var(--spacing-2);
      background: var(--color-error-50);
      color: var(--color-error-600);
      font-size: var(--font-size-xs);
      font-weight: var(--font-weight-medium);
      border-radius: var(--radius-sm);
    }

    .vat-badge {
      display: inline-block;
      padding: var(--spacing-1) var(--spacing-2);
      font-size: var(--font-size-xs);
      font-weight: var(--font-weight-semibold);
      border-radius: var(--radius-sm);

      &.vat-0 {
        background: var(--color-neutral-100);
        color: var(--color-neutral-600);
      }

      &.vat-7 {
        background: var(--color-success-100);
        color: var(--color-success-700);
      }

      &.vat-13 {
        background: var(--color-warning-100);
        color: var(--color-warning-700);
      }

      &.vat-19 {
        background: var(--color-primary-100);
        color: var(--color-primary-700);
      }
    }

    .line-actions {
      display: flex;
      gap: var(--spacing-1);
    }

    // Product Suggestion
    .fodec-badge {
      display: inline-flex;
      align-items: center;
      font-size: var(--font-size-xs);
      font-weight: var(--font-weight-semibold);
      color: var(--color-warning-800);
      background: var(--color-warning-100);
      padding: 0 var(--spacing-1);
      border-radius: var(--radius-sm);
      margin-left: var(--spacing-1);
      vertical-align: middle;
    }

    .fodec-badge--suggestion {
      margin-left: 0;
      flex-shrink: 0;
    }

    .product-suggestion {
      display: flex;
      align-items: center;
      gap: var(--spacing-3);
      padding: var(--spacing-2);
    }

    .product-code {
      font-family: 'JetBrains Mono', monospace;
      font-size: var(--font-size-xs);
      color: var(--color-neutral-500);
      background: var(--color-neutral-100);
      padding: var(--spacing-1);
      border-radius: var(--radius-sm);
    }

    .product-name {
      flex: 1;
      font-weight: var(--font-weight-medium);
    }

    .product-price {
      font-family: 'JetBrains Mono', monospace;
      font-size: var(--font-size-sm);
      color: var(--color-primary-600);
    }

    .product-empty {
      display: flex;
      align-items: center;
      gap: var(--spacing-2);
      padding: var(--spacing-3);
      color: var(--color-neutral-500);
      font-size: var(--font-size-sm);
      text-align: center;
      justify-content: center;

      i {
        color: var(--color-primary-500);
      }
    }

    .product-empty-content {
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: var(--spacing-2);
    }

    .edit-cell-product-row {
      display: flex;
      flex-direction: row;
      align-items: center;
      gap: var(--spacing-2);
    }

    .edit-cell .product-cell-input {
      flex: 1;
      min-width: 0;
    }

    :host ::ng-deep .edit-cell .product-cell-input .p-autocomplete {
      width: 100%;
    }

    .btn-add-product-circle {
      border-radius: var(--radius-full);
      width: 36px;
      height: 36px;
      padding: 0;
      flex-shrink: 0;
    }

    .btn-add-product-circle--dropdown {
      width: 32px;
      height: 32px;
    }

    // Empty States
    .empty-lines {
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: var(--spacing-3);
      padding: var(--spacing-8);
      text-align: center;

      i {
        font-size: 3rem;
        color: var(--color-neutral-300);
      }

      p {
        margin: 0;
        color: var(--color-neutral-500);
      }
    }

    .empty-state-card {
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: var(--spacing-3);
      padding: var(--spacing-10);
      background: var(--color-neutral-50);
      border: 2px dashed var(--color-neutral-300);
      border-radius: var(--radius-xl);
      text-align: center;

      i {
        font-size: 4rem;
        color: var(--color-neutral-300);
      }

      h3 {
        margin: 0;
        color: var(--color-neutral-700);
      }

      p {
        margin: 0;
        color: var(--color-neutral-500);
      }
    }

    // Totals Section
    .totals-section {
      margin-top: var(--spacing-6);
    }

    .totals-grid {
      display: grid;
      grid-template-columns: 1fr 350px;
      gap: var(--spacing-6);

      @media (max-width: 900px) {
        grid-template-columns: 1fr;
      }
    }

    .vat-breakdown-card {
      background: white;
      border: 1px solid var(--color-neutral-200);
      border-radius: var(--radius-xl);
      padding: var(--spacing-4);

      h3 {
        margin: 0 0 var(--spacing-4);
        font-size: var(--font-size-base);
        font-weight: var(--font-weight-semibold);
        color: var(--color-neutral-700);
      }
    }

    .vat-breakdown-table {
      width: 100%;
      border-collapse: collapse;

      th {
        font-size: var(--font-size-xs);
        font-weight: var(--font-weight-semibold);
        text-transform: uppercase;
        color: var(--color-neutral-500);
        padding: var(--spacing-2);
        text-align: left;
        border-bottom: 1px solid var(--color-neutral-200);
      }

      td {
        padding: var(--spacing-3) var(--spacing-2);
        border-bottom: 1px solid var(--color-neutral-100);
      }

      tr:last-child td {
        border-bottom: none;
      }
    }

    .grand-total-card {
      background: var(--color-primary-50);
      border: 1px solid var(--color-primary-200);
      border-radius: var(--radius-xl);
      padding: var(--spacing-4);
    }

    .total-row {
      display: flex;
      justify-content: space-between;
      align-items: center;
      padding: var(--spacing-2) 0;

      &.discount {
        color: var(--color-error-600);
      }

      &.grand {
        padding-top: var(--spacing-3);
        
        .total-label {
          font-size: var(--font-size-lg);
          font-weight: var(--font-weight-bold);
          color: var(--color-primary-800);
        }

        .total-value {
          font-size: var(--font-size-xl);
          font-weight: var(--font-weight-bold);
          color: var(--color-primary-700);
        }
      }
    }

    .total-label {
      font-size: var(--font-size-sm);
      color: var(--color-neutral-600);
    }

    .total-value {
      font-family: 'JetBrains Mono', monospace;
      font-size: var(--font-size-sm);
      font-weight: var(--font-weight-semibold);
      color: var(--color-neutral-800);
    }

    // Compliance Info
    .compliance-info {
      display: flex;
      align-items: center;
      gap: var(--spacing-2);
      margin-top: var(--spacing-4);
      padding: var(--spacing-3);
      background: var(--color-neutral-100);
      border-radius: var(--radius-lg);
      font-size: var(--font-size-sm);
      color: var(--color-neutral-600);

      i {
        color: var(--color-neutral-400);
      }
    }

    .text-right {
      text-align: right;
    }

    .text-muted {
      color: var(--color-neutral-400);
    }
  `]
})
export class StepLinesComponent implements OnInit, OnDestroy {
  readonly wizardService = inject(InvoiceWizardService);
  private readonly productService = inject(ProductService);
  private readonly confirmationService = inject(ConfirmationService);
  private readonly toastService = inject(ToastService);

  private destroy$ = new Subject<void>();
  private searchSubject$ = new Subject<string>();

  lines = computed(() => this.wizardService.lines());
  totals = computed(() => this.wizardService.totals());
  unmatchedLines = computed(() => this.lines().filter(l => !l.productId));
  currency = Currency.TND;

  // Options
  vatRateOptions = VAT_RATE_OPTIONS;
  discountTypeOptions = [
    { label: '%', value: 'PERCENT' },
    { label: 'TND', value: 'AMOUNT' }
  ];

  // Edit state
  editingLineId: string | null = null;
  editLine: Partial<InvoiceLine> = {};
  productSuggestions = signal<any[]>([]);
  quickCreateProductVisible = false;
  isLoadingProducts = signal<boolean>(false);
  simpleMode = signal<boolean>(true);

  ngOnInit(): void {
    this.currency = this.wizardService.metadata().currency;

    // Setup product search with debounce
    this.setupProductSearch();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  toggleSimpleMode(): void {
    this.simpleMode.update(v => !v);
  }

  private setupProductSearch(): void {
    this.searchSubject$.pipe(
      debounceTime(300),
      distinctUntilChanged(),
      switchMap((query: string) => {
        this.isLoadingProducts.set(true);

        // If query is empty, fetch all active products
        const searchParams = {
          search: query || undefined,
          isActive: true,
          page: 1,
          pageSize: 50 // Limit to 50 results for autocomplete
        };

        return this.productService.getProducts(searchParams).pipe(
          catchError((error) => {
            console.error('Error fetching products:', error);
            this.isLoadingProducts.set(false);
            return of({ success: false, data: { items: [], totalCount: 0 } });
          })
        );
      }),
      takeUntil(this.destroy$)
    ).subscribe({
      next: (response) => {
        this.isLoadingProducts.set(false);

        if (response.success && response.data) {
          const mappedProducts = response.data.items.map((product: ProductListItem) =>
            this.mapProductToSuggestion(product)
          );

          this.productSuggestions.set(mappedProducts);
        } else {
          this.productSuggestions.set([]);
        }
      },
      error: (error) => {
        console.error('Error in product search:', error);
        this.isLoadingProducts.set(false);
        this.productSuggestions.set([]);
      }
    });
  }

  private mapProductToSuggestion(product: ProductListItem) {
    return {
      id: product.id,
      code: product.code,
      name: product.name,
      unitPrice: product.unitPrice,
      vatRate: this.mapVatRateToEnum(product.vatRate),
      unit: product.unit || 'Unité',
      isFodecApplicable: product.isFodecApplicable ?? false
    };
  }

  private mapVatRateToEnum(vatRate: number): TunisianVatRate {
    // Map numeric VAT rate to enum
    switch (vatRate) {
      case 0:
        return TunisianVatRate.Exempt;
      case 7:
        return TunisianVatRate.Reduced;
      case 13:
        return TunisianVatRate.Intermediate;
      case 19:
        return TunisianVatRate.Standard;
      default:
        return TunisianVatRate.Standard;
    }
  }

  addNewLine(): void {
    const newLine: Partial<InvoiceLine> = {
      designation: '',
      description: null,
      quantity: 1,
      unit: 'Unité',
      unitPriceHT: 0,
      discountType: null,
      discountValue: null,
      vatRate: TunisianVatRate.Standard
    };

    this.wizardService.addLine(newLine);

    // Start editing the new line
    const lines = this.lines();
    if (lines.length > 0) {
      this.startEditLine(lines[lines.length - 1]);
      // Trigger initial product search
      this.searchSubject$.next('');
    }
  }

  startEditLine(line: InvoiceLine): void {
    this.editingLineId = line.id;
    this.editLine = { ...line };
    // If the line has no product yet (typical after AI import), seed the autocomplete
    // with the existing designation so the user lands on relevant matches immediately.
    const seed = !line.productId && line.designation ? line.designation : '';
    this.searchSubject$.next(seed);
  }

  saveLineEdit(): void {
    if (this.editingLineId && this.editLine.designation) {
      this.wizardService.updateLine(this.editingLineId, {
        productId: this.editLine.productId,
        designation: this.editLine.designation,
        description: this.editLine.description,
        quantity: this.editLine.quantity || 1,
        unit: this.editLine.unit,
        unitPriceHT: this.editLine.unitPriceHT || 0,
        discountType: this.editLine.discountType,
        discountValue: this.editLine.discountValue,
        vatRate: this.editLine.vatRate ?? TunisianVatRate.Standard,
        isFodecApplicable: this.editLine.productId
          ? (this.editLine.isFodecApplicable ?? false)
          : false
      });
    }
    this.cancelLineEdit();
  }

  cancelLineEdit(): void {
    const lineId = this.editingLineId;
    const line = lineId ? this.lines().find(l => l.id === lineId) : undefined;
    const isOrphanLine = line
      && !line.productId
      && !line.designation.trim()
      && line.unitPriceHT === 0;

    this.editingLineId = null;
    this.editLine = {};

    if (isOrphanLine && lineId) {
      this.wizardService.removeLine(lineId);
    }
  }

  deleteLine(line: InvoiceLine): void {
    this.confirmationService.confirm({
      message: `Supprimer la ligne "${line.designation}" ?`,
      header: 'Confirmation',
      icon: 'pi pi-exclamation-triangle',
      acceptLabel: 'Supprimer',
      rejectLabel: 'Annuler',
      acceptButtonStyleClass: 'p-button-danger',
      accept: () => {
        this.wizardService.removeLine(line.id);
      }
    });
  }

  searchProducts(event: any): void {
    const query = event.query?.trim() || '';
    // Emit search query to subject (will be debounced)
    this.searchSubject$.next(query);
  }

  onProductSelect(event: { originalEvent?: Event; value: any }): void {
    // PrimeNG AutoComplete onSelect émet { originalEvent, value }; le produit est dans value
    const product = event?.value;
    if (!product || !this.editingLineId) return;

    // Mettre à jour immédiatement la ligne dans le service pour que les calculs soient effectués
    this.wizardService.updateLine(this.editingLineId, {
      productId: product.id,
      designation: product.name,
      unitPriceHT: product.unitPrice,
      vatRate: product.vatRate,
      isFodecApplicable: product.isFodecApplicable ?? false,
      productIsDiscountEnabled: product.isDiscountEnabled ?? false,
      productMaxDiscountPercent: product.maxDiscountPercent ?? null,
      unit: product.unit || 'Unité',
      quantity: this.editLine.quantity || 1,
      description: this.editLine.description,
      discountType: this.editLine.discountType,
      discountValue: this.editLine.discountValue
    });

    // Recharger editLine depuis la ligne mise à jour dans le service pour rester synchronisé
    const updatedLine = this.lines().find(l => l.id === this.editingLineId);
    if (updatedLine) {
      this.editLine = { ...updatedLine };
    }
  }

  openQuickCreateProduct(): void {
    this.quickCreateProductVisible = true;
  }

  resolveNextUnmatched(): void {
    const next = this.unmatchedLines()[0];
    if (next) {
      this.startEditLine(next);
    }
  }

  onQuickProductCreated(product: ProductListItem): void {
    if (!this.editingLineId) return;

    const vatRateEnum = this.mapVatRateToEnum(product.vatRate);
    this.wizardService.updateLine(this.editingLineId, {
      productId: product.id,
      designation: product.name,
      unitPriceHT: product.unitPrice,
      vatRate: vatRateEnum,
      isFodecApplicable: product.isFodecApplicable ?? false,
      productIsDiscountEnabled: product.isDiscountEnabled ?? false,
      productMaxDiscountPercent: product.maxDiscountPercent ?? null,
      unit: product.unit || 'Unité',
      quantity: this.editLine.quantity || 1,
      description: this.editLine.description ?? product.description ?? null,
      discountType: this.editLine.discountType,
      discountValue: this.editLine.discountValue
    });

    const updatedLine = this.lines().find(l => l.id === this.editingLineId);
    if (updatedLine) {
      this.editLine = { ...updatedLine };
    }

    const mappedProduct = this.mapProductToSuggestion(product);
    this.productSuggestions.set([mappedProduct, ...this.productSuggestions()]);
    this.quickCreateProductVisible = false;
    this.toastService.add({
      severity: 'success',
      summary: 'Produit créé',
      detail: 'Le produit a été créé et ajouté à la ligne.',
    });
  }
}
