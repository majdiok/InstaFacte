import { Component, OnInit, inject, signal, computed, DestroyRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule, ActivatedRoute, Router } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { CardModule } from 'primeng/card';
import { TagModule } from 'primeng/tag';
import { DividerModule } from 'primeng/divider';
import { TableModule } from 'primeng/table';
import { TooltipModule } from 'primeng/tooltip';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { ToastModule } from 'primeng/toast';
import { SkeletonModule } from 'primeng/skeleton';
import { TimelineModule } from 'primeng/timeline';
import { DialogModule } from 'primeng/dialog';
import { ConfirmationService } from '@core/services/confirmation.service';
import { ToastService } from '@core/services/toast.service';
import { AuthService } from '@core/services/auth.service';
import { MenuItem } from '@shared/models/menu-item.model';
import { PageHeaderComponent } from '@shared/components/page-header/page-header.component';
import { BreadcrumbComponent, BreadcrumbItem } from '@shared/components/breadcrumb/breadcrumb.component';
import { ButtonComponent } from '@shared/components/button/button.component';
import { DocumentActionsMenuComponent } from '@shared/components/document-actions-menu/document-actions-menu.component';
import { StatusBadgeComponent, StatusBadgeStatus } from '@shared/components/status-badge/status-badge.component';
import { RecordPaymentDialogComponent } from '@shared/components/record-payment-dialog/record-payment-dialog.component';
import { InvoiceService, InvoiceDetail } from '@core/services/invoice.service';
import { formatLocalDate } from '@core/utils/date.util';
import { StockService, StockFeatures } from '@core/services/stock.service';
import { StockAllocationEditorComponent } from '@shared/components/stock-allocation-editor/stock-allocation-editor.component';
import {
  AllocationRow,
  buildAllocationPayload,
  exitAllocationsValid,
  showLotSection,
  showSerialSection,
  createDefaultLotRow,
  createDefaultSerialRow,
  TRACKING_MODE_SERIAL
} from '@shared/utils/stock-traceability.utils';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';

interface TimelineEvent {
  status: string;
  date: string | null;
  icon: string;
  color: string;
}

@Component({
  selector: 'app-invoice-detail',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    RouterModule,
    ButtonModule,
    CardModule,
    TagModule,
    DividerModule,
    TableModule,
    TooltipModule,
    ConfirmDialogModule,
    ToastModule,
    SkeletonModule,
    TimelineModule,
    DialogModule,
    PageHeaderComponent,
    BreadcrumbComponent,
    ButtonComponent,
    DocumentActionsMenuComponent,
    StatusBadgeComponent,
    RecordPaymentDialogComponent,
    StockAllocationEditorComponent
  ],
  template: `
    <app-breadcrumb [items]="breadcrumbItems()"></app-breadcrumb>
    
    <app-page-header 
      [title]="invoice()?.number || 'Chargement...'" 
      [subtitle]="'Facture du ' + (invoice()?.issueDate | date:'dd/MM/yyyy')">
      <app-button 
        variant="outline"
        icon="pi-arrow-left"
        iconPos="left"
        [routerLink]="listBackRoute">
        Retour
      </app-button>
      @if (primaryAction(); as action) {
        <app-button
          [variant]="action.variant"
          [icon]="action.icon"
          iconPos="left"
          (clicked)="action.command()">
          {{ action.label }}
        </app-button>
      }
      @if (invoice()) {
        <app-document-actions-menu [items]="menuItems" />
      }
    </app-page-header>

    @if (loading()) {
      <div class="detail-grid">
        <div class="main-content">
          <p-card>
            <p-skeleton height="2rem" styleClass="mb-4"></p-skeleton>
            <p-skeleton height="1rem" styleClass="mb-2" width="60%"></p-skeleton>
            <p-skeleton height="1rem" styleClass="mb-2" width="40%"></p-skeleton>
            <p-skeleton height="10rem"></p-skeleton>
          </p-card>
        </div>
        <div class="sidebar">
          <p-skeleton height="12rem" styleClass="mb-4"></p-skeleton>
          <p-skeleton height="8rem"></p-skeleton>
        </div>
      </div>
    } @else if (invoice()) {
      <div class="detail-grid">
        <!-- Main Content -->
        <div class="main-content">
          <!-- Invoice Preview -->
          <p-card styleClass="invoice-card">
            <!-- Header -->
            <div class="invoice-header">
              <div class="invoice-meta">
                <h2>{{ invoice()!.number }}</h2>
                <span class="doc-type-badge"
                      [class.doc-type-badge-avoir]="invoice()!.isCreditNote"
                      [class.doc-type-badge-facture]="!invoice()!.isCreditNote">
                  {{ invoice()!.isCreditNote ? "Facture d'avoir" : 'Facture' }}
                </span>
                <app-status-badge
                  [status]="getStatusBadgeStatus(invoice()!.status)"
                  [label]="invoice()!.statusDisplay">
                </app-status-badge>
              </div>
              <div class="invoice-dates">
                <div class="date-item">
                  <span class="label">Date d'émission</span>
                  <span class="value">{{ invoice()!.issueDate | date:'dd/MM/yyyy' }}</span>
                </div>
                @if (invoice()!.dueDate) {
                  <div class="date-item">
                    <span class="label">Échéance</span>
                    <span class="value" [class.overdue]="isOverdue()">
                      {{ invoice()!.dueDate | date:'dd/MM/yyyy' }}
                    </span>
                  </div>
                }
              </div>
            </div>

            <p-divider></p-divider>

            <!-- Parties -->
            <div class="parties-grid">
              <div class="party-card">
                <h4>Client</h4>
                <p class="party-name">{{ invoice()!.client.name }}</p>
                <p class="party-address">{{ invoice()!.client.address }}</p>
                @if (invoice()!.client.nif) {
                  <p class="party-nif">MF: {{ invoice()!.client.nif }}</p>
                }
                <p class="party-email">{{ invoice()!.client.email }}</p>
              </div>
            </div>

            <p-divider></p-divider>

            <!-- Lines -->
            <div class="lines-section">
              <h4>Lignes de facturation</h4>
              <table class="lines-table">
                <thead>
                  <tr>
                    <th>#</th>
                    <th>Désignation</th>
                    <th class="text-right">Quantité</th>
                    <th class="text-right">Prix unitaire</th>
                    <th class="text-center">TVA</th>
                    <th class="text-right">Total HT</th>
                  </tr>
                </thead>
                <tbody>
                  @for (line of invoice()!.lines; track line.id) {
                    <tr>
                      <td>{{ line.lineNumber }}</td>
                      <td>
                        <span class="line-name">{{ line.productName }}</span>
                        @if (line.productDescription) {
                          <br><small class="line-desc">{{ line.productDescription }}</small>
                        }
                      </td>
                      <td class="text-right">{{ line.quantity | number:'1.0-3' }} {{ line.unit }}</td>
                      <td class="text-right mono">{{ line.unitPrice | number:'1.3-3' }}</td>
                      <td class="text-center">{{ line.vatRatePercent }}%</td>
                      <td class="text-right mono">{{ line.subTotal | number:'1.3-3' }}</td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>

            <!-- Totals -->
            <div class="totals-section">
              <div class="vat-breakdown">
                <h5>Récapitulatif TVA</h5>
                @for (vat of invoice()!.vatBreakdown; track vat.rate) {
                  <div class="vat-row">
                    <span>{{ vat.rateDisplay }}</span>
                    <span class="mono">{{ vat.baseAmount | number:'1.3-3' }}</span>
                    <span class="mono">{{ vat.vatAmount | number:'1.3-3' }}</span>
                  </div>
                }
              </div>

              <div class="totals-box" [class.totals-box-avoir]="invoice()!.isCreditNote">
                <div class="total-row">
                  <span>{{ invoice()!.isCreditNote ? 'Total HT à rembourser' : 'Total HT' }}</span>
                  <span class="mono">{{ invoice()!.subTotal | number:'1.3-3' }} {{ invoice()!.currency }}</span>
                </div>
                <div class="total-row">
                  <span>{{ invoice()!.isCreditNote ? 'Total TVA à rembourser' : 'Total TVA' }}</span>
                  <span class="mono">{{ invoice()!.totalVat | number:'1.3-3' }} {{ invoice()!.currency }}</span>
                </div>
                @if ((invoice()!.fodecAmount ?? 0) > 0.0005 || (invoice()!.fodecAmount ?? 0) < -0.0005) {
                  <div class="total-row">
                    <span>{{ invoice()!.isCreditNote ? 'FODEC à rembourser' : 'FODEC' }}</span>
                    <span class="mono">{{ invoice()!.fodecAmount | number:'1.3-3' }} {{ invoice()!.currency }}</span>
                  </div>
                }
                @if ((invoice()!.fiscalStampAmount ?? 0) > 0.0005 || (invoice()!.fiscalStampAmount ?? 0) < -0.0005) {
                  <div class="total-row">
                    <span>Timbre fiscal</span>
                    <span class="mono">{{ invoice()!.fiscalStampAmount | number:'1.3-3' }} {{ invoice()!.currency }}</span>
                  </div>
                }
                <div class="total-row grand">
                  <span>{{ invoice()!.isCreditNote ? 'Total TTC à rembourser' : 'Total TTC' }}</span>
                  <span class="mono">{{ invoice()!.totalAmount | number:'1.3-3' }} {{ invoice()!.currency }}</span>
                </div>
              </div>
            </div>

            @if (invoice()!.notes || invoice()!.paymentTerms) {
              <p-divider></p-divider>
              <div class="notes-section">
                @if (invoice()!.paymentTerms) {
                  <div class="note">
                    <strong>Conditions de paiement:</strong> {{ invoice()!.paymentTerms }}
                  </div>
                }
                @if (invoice()!.notes) {
                  <div class="note">
                    <strong>Notes:</strong> {{ invoice()!.notes }}
                  </div>
                }
              </div>
            }
          </p-card>
        </div>

        <!-- Sidebar -->
        <div class="sidebar">
          <!-- Status Card -->
          <p-card header="Statut" styleClass="status-card">
            <div class="status-content">
              <app-status-badge 
                [status]="getStatusBadgeStatus(invoice()!.status)"
                [label]="invoice()!.statusDisplay">
              </app-status-badge>
            </div>
          </p-card>

          <!-- Timeline Card -->
          <p-card header="Historique" styleClass="timeline-card">
            <p-timeline [value]="timeline" styleClass="custom-timeline">
              <ng-template pTemplate="content" let-event>
                <div class="timeline-event">
                  <span class="event-label">{{ event.status }}</span>
                  @if (event.date) {
                    <span class="event-date">{{ event.date | date:'dd/MM/yyyy HH:mm' }}</span>
                  }
                </div>
              </ng-template>
              <ng-template pTemplate="marker" let-event>
                <span class="timeline-marker" [style.background]="event.color">
                  <i [class]="event.icon"></i>
                </span>
              </ng-template>
            </p-timeline>
          </p-card>

          <!-- Payment History Card -->
          <p-card header="Historique des paiements" styleClass="payments-card">
            @if (invoice()!.payments && invoice()!.payments!.length > 0) {
              <div class="payments-list">
                @for (payment of invoice()!.payments; track payment.id) {
                  <div class="payment-row" [class.refunded]="payment.isRefunded">
                    <div class="payment-main">
                      <span class="payment-amount">{{ payment.amount | number:'1.3-3' }} {{ payment.currency }}</span>
                      <span class="payment-date">{{ payment.paymentDate | date:'dd/MM/yyyy' }}</span>
                    </div>
                    <div class="payment-meta">
                      <span class="payment-method">{{ payment.methodDisplay }}</span>
                      @if (payment.reference) {
                        <span class="payment-ref">{{ payment.reference }}</span>
                      }
                    </div>
                    @if (payment.clientWithholdingAmount && payment.clientWithholdingAmount > 0) {
                      <div class="payment-withholding">
                        <i class="pi pi-percentage"></i>
                        <span>RS subie : {{ payment.clientWithholdingAmount | number:'1.3-3' }} {{ payment.currency }}</span>
                      </div>
                    }
                    @if (payment.effetStatus !== null && payment.effetStatus !== undefined) {
                      <div class="payment-effet">
                        <span class="effet-badge" [class.en-portefeuille]="payment.effetStatus === 0" [class.encaisse]="payment.effetStatus === 1" [class.impaye]="payment.effetStatus === 2">
                          <i class="pi pi-file"></i>
                          Effet {{ payment.effetStatusDisplay }}
                          @if (payment.effetDueDate) {
                            <span class="effet-due"> · échéance {{ payment.effetDueDate | date:'dd/MM/yyyy' }}</span>
                          }
                        </span>
                        @if (payment.effetStatus === 0 && !payment.isRefunded && canSettleEffet()) {
                          <span class="effet-actions">
                            <button type="button" class="effet-btn encaisse" [disabled]="settlingEffetId() === payment.id" (click)="onSettleClientEffet(payment.id, 1)">Encaisser</button>
                            <button type="button" class="effet-btn impaye" [disabled]="settlingEffetId() === payment.id" (click)="onSettleClientEffet(payment.id, 2)">Impayé</button>
                          </span>
                        }
                      </div>
                    }
                  </div>
                }
              </div>
              <div class="payments-summary">
                <div class="summary-row">
                  <span>Total payé</span>
                  <span class="mono">{{ (invoice()!.totalPaid ?? 0) | number:'1.3-3' }} {{ invoice()!.currency }}</span>
                </div>
                @if ((invoice()!.totalClientWithholding ?? 0) > 0) {
                  <div class="summary-row withholding">
                    <span>Retenue subie</span>
                    <span class="mono">{{ invoice()!.totalClientWithholding | number:'1.3-3' }} {{ invoice()!.currency }}</span>
                  </div>
                }
                @if ((invoice()!.remainingAmount ?? 0) > 0) {
                  <div class="summary-row remaining">
                    <span>Restant dû</span>
                    <span class="mono">{{ invoice()!.remainingAmount | number:'1.3-3' }} {{ invoice()!.currency }}</span>
                  </div>
                }
              </div>
            } @else {
              <div class="payments-empty">
                <i class="pi pi-wallet"></i>
                <p>Aucun paiement enregistré</p>
              </div>
            }

             @if (canRecordPayment(invoice()!)) {
                <app-button 
                  variant="primary"
                  icon="pi-wallet"
                  iconPos="left"
                  (click)="openRecordPaymentDialog()"
                  style="width: 100%; margin-bottom: var(--spacing-2);">
                  Enregistrer un paiement
                </app-button>
              }
          </p-card>

          <app-record-payment-dialog
            [panelMode]="true"
            [(visible)]="recordPaymentDialogVisible"
            [invoiceId]="invoice()?.id ?? ''"
            [invoiceNumber]="invoice()?.number ?? ''"
            [totalAmount]="invoice()?.totalAmount ?? 0"
            [totalPaid]="invoice()?.totalPaid ?? 0"
            [remainingAmount]="invoice()?.remainingAmount ?? (invoice()?.totalAmount ?? 0)"
            [currency]="invoice()?.currency ?? 'TND'"
            invoiceType="client"
            (paymentRecorded)="onPaymentRecorded()">
          </app-record-payment-dialog>

          <!-- Signature Info -->
          @if (invoice()!.signatureHash) {
            <p-card header="Signature électronique" styleClass="signature-card">
              <div class="signature-info">
                <div class="info-row">
                  <i class="pi pi-verified"></i>
                  <span>Facture signée électroniquement</span>
                </div>
                <div class="hash-display">
                  <code>{{ invoice()!.signatureHash!.substring(0, 20) }}...</code>
                </div>
                <small>Signé le {{ invoice()!.signedAt | date:'dd/MM/yyyy HH:mm' }}</small>
              </div>
            </p-card>
          }
        </div>
      </div>

      <p-dialog
        header="Valider la facture"
        [(visible)]="showValidateDialog"
        [modal]="true"
        [style]="{ width: '750px', maxWidth: '95vw' }"
        [draggable]="false"
        [resizable]="false"
        [contentStyle]="{ overflow: 'visible' }">
        <p class="validate-dialog-hint">
          Confirmez la validation de {{ invoice()!.number }}. Sélectionnez les lots ou numéros de série si nécessaire.
        </p>
        @if (needsStockAllocationOnValidate()) {
          <table class="validate-lines-table">
            <thead>
              <tr>
                <th>Produit</th>
                <th class="text-right">Quantité</th>
                <th>Traçabilité</th>
              </tr>
            </thead>
            <tbody>
              @for (lineForm of validateLineForms; track lineForm.lineId) {
                <tr>
                  <td>{{ lineForm.designation }}</td>
                  <td class="text-right mono">{{ lineForm.quantity | number:'1.0-3' }}</td>
                  <td>
                    @if (showValidateLineTraceability(lineForm) && validateWarehouseId()) {
                      <app-stock-allocation-editor
                        mode="exit"
                        [productId]="lineForm.productId"
                        [warehouseId]="validateWarehouseId()!"
                        [lineQuantity]="lineForm.quantity"
                        [trackingMode]="lineForm.trackingMode"
                        [pickingPolicy]="lineForm.pickingPolicy"
                        [features]="stockFeatures()"
                        [allocations]="lineForm.lotAllocations" />
                    } @else {
                      <span class="muted">—</span>
                    }
                  </td>
                </tr>
              }
            </tbody>
          </table>
        }
        <ng-template pTemplate="footer">
          <div class="dialog-footer">
            <app-button variant="outline" icon="pi-times" (click)="showValidateDialog = false">Annuler</app-button>
            <app-button variant="primary" icon="pi-check" [disabled]="!isValidateFormValid()" (click)="submitValidate()">
              Valider
            </app-button>
          </div>
        </ng-template>
      </p-dialog>
    } @else {
      <div class="not-found">
        <i class="pi pi-exclamation-triangle"></i>
        <h2>Facture non trouvée</h2>
        <p>La facture demandée n'existe pas ou a été supprimée.</p>
        <app-button variant="primary" [routerLink]="listBackRoute">Retour à la liste</app-button>
      </div>
    }

  `,
  styles: [`
    .detail-grid {
      display: grid;
      grid-template-columns: 1fr 320px;
      gap: var(--spacing-6);

      @media (max-width: 1024px) {
        grid-template-columns: 1fr;
      }
    }

    .main-content {
      min-width: 0;
    }

    .sidebar {
      display: flex;
      flex-direction: column;
      gap: var(--spacing-4);
    }

    .invoice-header {
      display: flex;
      justify-content: space-between;
      align-items: flex-start;
      flex-wrap: wrap;
      gap: var(--spacing-4);
    }

    .invoice-meta {
      display: flex;
      flex-direction: column;
      align-items: flex-start;
      gap: var(--spacing-2);

      h2 {
        margin: 0;
        font-family: 'JetBrains Mono', monospace;
        font-size: var(--font-size-2xl);
        font-weight: var(--font-weight-bold);
        color: var(--color-text-primary);
      }
    }

    .doc-type-badge {
      display: inline-flex;
      align-items: center;
      padding: var(--spacing-1) var(--spacing-3);
      border-radius: var(--radius-md);
      font-size: var(--font-size-xs);
      font-weight: var(--font-weight-semibold);
      text-transform: uppercase;
      letter-spacing: 0.04em;

      &.doc-type-badge-facture {
        background: var(--color-primary-50);
        color: var(--color-primary-700);
      }

      &.doc-type-badge-avoir {
        background: var(--color-error-50, #fee2e2);
        color: var(--color-error-700, #b91c1c);
        border: 1px solid var(--color-error-200, #fecaca);
      }
    }

    .invoice-dates {
      display: flex;
      gap: var(--spacing-6);
    }

    .date-item {
      display: flex;
      flex-direction: column;
      gap: var(--spacing-1);

      .label {
        font-size: var(--font-size-xs);
        text-transform: uppercase;
        color: var(--color-text-tertiary);
      }

      .value {
        font-weight: var(--font-weight-semibold);
        color: var(--color-text-primary);

        &.overdue {
          color: var(--color-error-600);
        }
      }
    }

    .parties-grid {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(250px, 1fr));
      gap: var(--spacing-4);
      margin: var(--spacing-4) 0;
    }

    .party-card {
      padding: var(--spacing-4);
      background: var(--color-neutral-50);
      border-radius: var(--radius-lg);

      h4 {
        margin: 0 0 var(--spacing-2);
        font-size: var(--font-size-xs);
        text-transform: uppercase;
        color: var(--color-text-tertiary);
      }

      .party-name {
        font-weight: var(--font-weight-semibold);
        color: var(--color-text-primary);
        margin: 0 0 var(--spacing-2);
      }

      .party-address,
      .party-email {
        font-size: var(--font-size-sm);
        color: var(--color-text-secondary);
        margin: 0 0 var(--spacing-1);
      }

      .party-nif {
        font-family: 'JetBrains Mono', monospace;
        font-size: var(--font-size-sm);
        color: var(--color-text-secondary);
        margin: 0 0 var(--spacing-1);
      }
    }

    .lines-section {
      h4 {
        margin: 0 0 var(--spacing-4);
        font-size: var(--font-size-lg);
        font-weight: var(--font-weight-semibold);
        color: var(--color-text-primary);
      }
    }

    .lines-table {
      width: 100%;
      border-collapse: collapse;

      th {
        padding: var(--spacing-4) var(--spacing-3);
        font-size: var(--font-size-xs);
        text-transform: uppercase;
        letter-spacing: 0.05em;
        color: var(--color-text-secondary);
        text-align: left;
        border-bottom: 2px solid var(--color-border-default);
        background: var(--color-background-subtle);
      }

      td {
        padding: var(--spacing-4) var(--spacing-3);
        font-size: var(--font-size-sm);
        color: var(--color-text-primary);
        border-bottom: 1px solid var(--color-border-subtle);
        vertical-align: top;
      }

      .line-name {
        font-weight: var(--font-weight-medium);
      }

      .line-desc {
        color: var(--color-text-tertiary);
      }

      .mono {
        font-family: 'JetBrains Mono', monospace;
      }

      .text-right {
        text-align: right;
      }

      .text-center {
        text-align: center;
      }
    }

    .totals-section {
      display: flex;
      justify-content: space-between;
      gap: var(--spacing-6);
      margin-top: var(--spacing-4);

      @media (max-width: 768px) {
        flex-direction: column;
      }
    }

    .vat-breakdown {
      h5 {
        margin: 0 0 var(--spacing-3);
        font-size: var(--font-size-sm);
        font-weight: var(--font-weight-semibold);
        color: var(--color-text-secondary);
      }

      .vat-row {
        display: flex;
        gap: var(--spacing-4);
        font-size: var(--font-size-sm);
        padding: var(--spacing-1) 0;
      }
    }

    .totals-box {
      min-width: 280px;
      padding: var(--spacing-4);
      background: var(--color-primary-50);
      border-radius: var(--radius-lg);

      &.totals-box-avoir {
        background: var(--color-error-50, #fee2e2);
        border: 1px solid var(--color-error-200, #fecaca);

        .total-row.grand {
          border-top-color: var(--color-error-200, #fecaca);
          color: var(--color-error-700, #b91c1c);
        }
      }
    }

    .total-row {
      display: flex;
      justify-content: space-between;
      padding: var(--spacing-2) 0;
      font-size: var(--font-size-sm);

      &.grand {
        margin-top: var(--spacing-2);
        padding-top: var(--spacing-3);
        border-top: 2px solid var(--color-primary-200);
        font-size: var(--font-size-lg);
        font-weight: var(--font-weight-bold);
        color: var(--color-primary-700);
      }

      .mono {
        font-family: 'JetBrains Mono', monospace;
        font-weight: var(--font-weight-semibold);
      }
    }

    .notes-section {
      .note {
        font-size: var(--font-size-sm);
        color: var(--color-neutral-600);
        margin-bottom: var(--spacing-2);

        strong {
          color: var(--color-neutral-700);
        }
      }
    }

    .status-content {
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: var(--spacing-4);

      .status-tag {
        font-size: var(--font-size-base);
        padding: var(--spacing-2) var(--spacing-4);
      }
    }

    .status-actions {
      display: flex;
      gap: var(--spacing-2);
    }

    .timeline-event {
      display: flex;
      flex-direction: column;
      gap: var(--spacing-1);

      .event-label {
        font-weight: var(--font-weight-medium);
        color: var(--color-neutral-800);
      }

      .event-date {
        font-size: var(--font-size-xs);
        color: var(--color-neutral-500);
      }
    }

    .timeline-marker {
      display: flex;
      align-items: center;
      justify-content: center;
      width: 24px;
      height: 24px;
      border-radius: 50%;
      color: white;

      i {
        font-size: 12px;
      }
    }

    .quick-actions {
      display: flex;
      flex-direction: column;
    }

    .payments-list {
      display: flex;
      flex-direction: column;
      gap: var(--spacing-2);
      margin-bottom: var(--spacing-4);
    }

    .payment-row {
      padding: var(--spacing-3);
      background: var(--color-neutral-50);
      border-radius: var(--radius-md);
      border-left: 3px solid var(--color-success-500);

      &.refunded {
        opacity: 0.7;
        border-left-color: var(--color-neutral-400);
      }
    }

    .payment-main {
      display: flex;
      justify-content: space-between;
      align-items: center;
      margin-bottom: var(--spacing-1);
    }

    .payment-amount {
      font-weight: var(--font-weight-semibold);
      font-family: 'JetBrains Mono', monospace;
      color: var(--color-text-primary);
    }

    .payment-date {
      font-size: var(--font-size-sm);
      color: var(--color-text-secondary);
    }

    .payment-meta {
      display: flex;
      gap: var(--spacing-2);
      font-size: var(--font-size-xs);
      color: var(--color-text-tertiary);
    }

    .payment-withholding {
      display: flex;
      align-items: center;
      gap: var(--spacing-1);
      margin-top: var(--spacing-1);
      font-size: var(--font-size-xs);
      color: var(--color-warning-600);
      font-weight: var(--font-weight-medium);

      i {
        font-size: 0.65rem;
      }
    }

    .payment-effet {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: var(--spacing-2);
      margin-top: var(--spacing-2);
    }

    .effet-badge {
      display: inline-flex;
      align-items: center;
      gap: var(--spacing-1);
      font-size: var(--font-size-xs);
      font-weight: var(--font-weight-medium);
      padding: 2px 8px;
      border-radius: var(--radius-lg);
      background: var(--color-neutral-100);
      color: var(--color-text-secondary);

      i { font-size: 0.65rem; }
      &.en-portefeuille { background: var(--color-warning-50); color: var(--color-warning-700); }
      &.encaisse { background: var(--color-success-50); color: var(--color-success-700); }
      &.impaye { background: var(--color-error-50); color: var(--color-error-700); }
    }

    .effet-due { opacity: 0.85; }

    .effet-actions {
      display: inline-flex;
      gap: var(--spacing-2);
    }

    .effet-btn {
      border: 1px solid var(--color-border-subtle);
      background: var(--color-white);
      border-radius: var(--radius-md);
      padding: 2px 10px;
      font-size: var(--font-size-xs);
      font-weight: var(--font-weight-medium);
      cursor: pointer;
      transition: all 150ms ease;

      &:disabled { opacity: 0.5; cursor: not-allowed; }
      &.encaisse { color: var(--color-success-700); border-color: var(--color-success-200); }
      &.encaisse:hover:not(:disabled) { background: var(--color-success-50); }
      &.impaye { color: var(--color-error-700); border-color: var(--color-error-200); }
      &.impaye:hover:not(:disabled) { background: var(--color-error-50); }
    }

    .payments-summary {
      padding-top: var(--spacing-3);
      border-top: 1px solid var(--color-border-subtle);
    }

    .summary-row {
      display: flex;
      justify-content: space-between;
      padding: var(--spacing-1) 0;
      font-size: var(--font-size-sm);

      &.remaining {
        font-weight: var(--font-weight-semibold);
        color: var(--color-primary-600);
      }

      &.withholding {
        color: var(--color-warning-600);
        font-style: italic;
      }
    }

    .payments-empty {
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: var(--spacing-2);
      padding: var(--spacing-6);
      color: var(--color-text-tertiary);
      text-align: center;

      i {
        font-size: 2rem;
        opacity: 0.5;
      }

      p {
        margin: 0;
        font-size: var(--font-size-sm);
      }
    }

    .signature-info {
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: var(--spacing-2);
      text-align: center;

      .info-row {
        display: flex;
        align-items: center;
        gap: var(--spacing-2);
        color: var(--color-success-600);
        font-weight: var(--font-weight-medium);

        i {
          font-size: var(--font-size-xl);
        }
      }

      .hash-display {
        padding: var(--spacing-2);
        background: var(--color-neutral-100);
        border-radius: var(--radius-md);

        code {
          font-family: 'JetBrains Mono', monospace;
          font-size: var(--font-size-xs);
        }
      }

      small {
        color: var(--color-neutral-500);
      }
    }

    .validate-dialog-hint {
      margin-bottom: 1rem;
      color: var(--color-text-secondary);
      font-size: var(--font-size-sm);
    }

    .validate-lines-table {
      width: 100%;
      border-collapse: collapse;

      th, td {
        padding: var(--spacing-3);
        border-bottom: 1px solid var(--color-border-subtle);
        font-size: var(--font-size-sm);
      }

      th {
        text-transform: uppercase;
        font-size: var(--font-size-xs);
        color: var(--color-text-secondary);
      }
    }

    .dialog-footer {
      display: flex;
      justify-content: flex-end;
      gap: var(--spacing-3);
    }

    .muted {
      color: var(--color-text-secondary);
    }

    .not-found {
      display: flex;
      flex-direction: column;
      align-items: center;
      justify-content: center;
      padding: var(--spacing-12);
      text-align: center;

      i {
        font-size: 4rem;
        color: var(--color-warning-500);
        margin-bottom: var(--spacing-4);
      }

      h2 {
        color: var(--color-neutral-800);
        margin: 0 0 var(--spacing-2);
      }

      p {
        color: var(--color-neutral-600);
        margin: 0 0 var(--spacing-4);
      }
    }

    :host ::ng-deep {
      .invoice-card .p-card-body {
        padding: var(--card-padding);
      }

      .status-card,
      .timeline-card,
      .payments-card,
      .actions-card,
      .signature-card {
        .p-card-header {
          padding: var(--spacing-4) var(--card-padding);
          border-bottom: 1px solid var(--color-border-subtle);
          font-size: var(--font-size-base);
          font-weight: var(--font-weight-semibold);
          color: var(--color-text-primary);
        }

        .p-card-body {
          padding: var(--card-padding);
        }
      }

      .custom-timeline {
        .p-timeline-event-opposite {
          display: none;
        }
      }
    }
  `]
})
export class InvoiceDetailComponent implements OnInit {
  private invoiceService = inject(InvoiceService);
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private confirmationService = inject(ConfirmationService);
  private toastService = inject(ToastService);
  private readonly auth = inject(AuthService);
  private stockService = inject(StockService);
  private destroyRef = inject(DestroyRef);

  loading = signal(true);
  invoice = signal<InvoiceDetail | null>(null);
  stockFeatures = signal<StockFeatures | null>(null);
  traceabilityByProduct = signal<Map<string, { trackingMode: number; pickingPolicy: number }>>(new Map());

  showValidateDialog = false;
  validateLineForms: Array<{
    lineId: string;
    productId: string;
    designation: string;
    quantity: number;
    trackingMode: number;
    pickingPolicy: number;
    lotAllocations: AllocationRow[];
  }> = [];

  validateWarehouseId = computed(() => this.invoice()?.warehouseId ?? null);

  menuItems: MenuItem[] = [];
  timeline: TimelineEvent[] = [];

  private get unpaidOnlyFromRoute(): boolean {
    return this.route.snapshot.data['unpaidOnly'] === true;
  }

  private get creditNotesOnlyFromRoute(): boolean {
    return this.route.snapshot.data['creditNotesOnly'] === true;
  }

  get listBackRoute(): string {
    if (this.creditNotesOnlyFromRoute) {
      return '/invoices/credit-notes';
    }
    return this.unpaidOnlyFromRoute ? '/invoices/unpaid' : '/invoices';
  }

  breadcrumbItems = computed<BreadcrumbItem[]>(() => {
    const inv = this.invoice();
    const base: BreadcrumbItem[] = [
      { label: 'Tableau de bord', route: '/dashboard', icon: 'pi-home' }
    ];
    if (this.creditNotesOnlyFromRoute) {
      base.push({ label: 'Avoirs de vente', route: '/invoices/credit-notes' });
    } else {
      base.push({ label: 'Factures', route: '/invoices' });
      if (this.unpaidOnlyFromRoute) {
        base.push({ label: 'Factures impayées', route: '/invoices/unpaid' });
      }
    }
    base.push({ label: inv?.number || (this.creditNotesOnlyFromRoute ? 'Avoir' : 'Facture') });
    return base;
  });

  /** Action workflow principale affichée dans l'en-tête (Valider / Signer). */
  readonly primaryAction = computed(() => {
    const inv = this.invoice();
    if (!inv) return null;
    if (inv.status === 'Draft') {
      return { label: 'Valider', icon: 'pi-check', variant: 'primary' as const, command: () => this.validateInvoice() };
    }
    if (inv.status === 'Validated') {
      return { label: 'Signer', icon: 'pi-pencil', variant: 'primary' as const, command: () => this.signInvoice() };
    }
    return null;
  });

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
      this.loadInvoice(id);
    } else {
      this.router.navigate([this.listBackRoute]);
    }
  }

  private loadInvoice(id: string): void {
    this.loading.set(true);

    this.invoiceService.getInvoice(id).subscribe({
      next: (response) => {
        if (response.success) {
          this.invoice.set(response.data);
          this.buildTimeline(response.data);
          this.buildMenu(response.data);
        } else {
          this.invoice.set(null);
        }
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.invoice.set(null);
      }
    });
  }

  private buildTimeline(invoice: InvoiceDetail): void {
    this.timeline = [
      {
        status: 'Créée',
        date: invoice.createdAt,
        icon: 'pi pi-file',
        color: 'var(--color-neutral-400)'
      },
      {
        status: 'Validée',
        date: invoice.status !== 'Draft' ? invoice.createdAt : null,
        icon: 'pi pi-check',
        color: invoice.status !== 'Draft' ? 'var(--color-primary-500)' : 'var(--color-neutral-300)'
      },
      {
        status: 'Signée',
        date: invoice.signedAt,
        icon: 'pi pi-pencil',
        color: invoice.signedAt ? 'var(--color-success-500)' : 'var(--color-neutral-300)'
      },
      {
        status: 'Payée',
        date: invoice.paidAt,
        icon: 'pi pi-wallet',
        color: invoice.paidAt ? 'var(--color-success-600)' : 'var(--color-neutral-300)'
      }
    ];
  }

  private buildMenu(invoice: InvoiceDetail): void {
    this.menuItems = [
      {
        label: 'Télécharger PDF',
        icon: 'pi pi-download',
        command: () => this.downloadPdf()
      },
      {
        label: 'Envoyer par email',
        icon: 'pi pi-envelope',
        command: () => this.sendByEmail()
      },
      {
        label: 'Enregistrer un paiement',
        icon: 'pi pi-wallet',
        visible: this.canRecordPayment(invoice),
        command: () => this.openRecordPaymentDialog()
      },
      { separator: true },
      {
        label: 'Dupliquer',
        icon: 'pi pi-copy',
        command: () => this.duplicateInvoice()
      },
      {
        label: "Créer un avoir",
        icon: 'pi pi-replay',
        visible: this.canCreateCreditNote(invoice.status),
        routerLink: ['/invoices', invoice.id, 'credit-note']
      }
    ];
  }

  getStatusSeverity(status: string): 'success' | 'info' | 'warn' | 'danger' | 'secondary' | 'contrast' {
    switch (status) {
      case 'Draft': return 'secondary';
      case 'Validated': return 'info';
      case 'Signed': return 'success';
      case 'Paid': return 'success';
      case 'PartiallyPaid': return 'info';
      case 'Overdue': return 'danger';
      case 'Cancelled': return 'warn';
      case 'Archived': return 'secondary';
      default: return 'secondary';
    }
  }

  getStatusBadgeStatus(status: string): StatusBadgeStatus {
    switch (status) {
      case 'Draft': return 'draft';
      case 'Validated': return 'validated';
      case 'Signed': return 'signed';
      case 'Paid': return 'paid';
      case 'PartiallyPaid': return 'partial';
      case 'Overdue': return 'overdue';
      case 'Cancelled': return 'cancelled';
      case 'Archived': return 'inactive';
      default: return 'draft';
    }
  }

  isOverdue(): boolean {
    const invoice = this.invoice();
    if (!invoice?.dueDate) return false;
    const nonPaidStatuses = ['Draft', 'Validated', 'Signed', 'PartiallyPaid', 'Overdue'];
    return new Date(invoice.dueDate) < new Date() && nonPaidStatuses.includes(invoice.status);
  }

  canCreateCreditNote(status: string): boolean {
    // Only allow creating an AVO from a regular FAC — no chained avoir-of-avoir.
    if (this.invoice()?.isCreditNote) return false;
    return ['Validated', 'Signed', 'Paid', 'PartiallyPaid', 'Overdue'].includes(status);
  }

  canRecordPayment(invoice: InvoiceDetail): boolean {
    if (this.auth.isFirmDelegatedReadonly()) return false;
    if (invoice.status === 'Paid') return false;
    return ['Signed', 'Validated', 'PartiallyPaid', 'Overdue'].includes(invoice.status);
  }

  recordPaymentDialogVisible = false;

  openRecordPaymentDialog(): void {
    if (this.auth.isFirmDelegatedReadonly()) return;
    this.recordPaymentDialogVisible = true;
  }

  onPaymentRecorded(): void {
    const inv = this.invoice();
    if (inv) {
      this.toastService.add({
        severity: 'success',
        summary: 'Succès',
        detail: 'Paiement enregistré avec succès'
      });
      this.loadInvoice(inv.id);
    }
  }

  readonly settlingEffetId = signal<string | null>(null);

  canSettleEffet(): boolean {
    return !this.auth.isFirmDelegatedReadonly();
  }

  /** Règle un effet client à échéance : encaissé (outcome=1) ou impayé (outcome=2). */
  onSettleClientEffet(paymentId: string, outcome: 1 | 2): void {
    if (this.settlingEffetId()) return;
    if (this.auth.isFirmDelegatedReadonly()) return;
    const inv = this.invoice();
    if (!inv) return;

    this.confirmationService.confirm({
      header: outcome === 1 ? 'Encaisser l\'effet' : 'Marquer l\'effet impayé',
      message: outcome === 1
        ? 'Confirmer l\'encaissement de cet effet à échéance ? L\'écriture bancaire (532/413) sera générée.'
        : 'Confirmer le retour impayé de cet effet ? La créance client sera réouverte (4111/413).',
      icon: outcome === 1 ? 'pi pi-check-circle' : 'pi pi-exclamation-triangle',
      acceptLabel: outcome === 1 ? 'Encaisser' : 'Marquer impayé',
      rejectLabel: 'Annuler',
      accept: () => {
        this.settlingEffetId.set(paymentId);
        this.invoiceService.settleEffet(inv.id, paymentId, {
          settlementDate: formatLocalDate(new Date()),
          outcome
        }).subscribe({
          next: (res) => {
            this.settlingEffetId.set(null);
            if (res.success) {
              this.toastService.add({
                severity: 'success',
                summary: 'Effet réglé',
                detail: outcome === 1 ? 'Effet encaissé avec succès' : 'Effet marqué impayé'
              });
              this.loadInvoice(inv.id);
            } else {
              this.toastService.add({ severity: 'error', summary: 'Erreur', detail: res.message ?? 'Échec du règlement de l\'effet' });
            }
          },
          error: () => {
            this.settlingEffetId.set(null);
            this.toastService.add({ severity: 'error', summary: 'Erreur', detail: 'Échec du règlement de l\'effet' });
          }
        });
      }
    });
  }

  validateInvoice(): void {
    const invoice = this.invoice();
    if (!invoice) return;

    this.validateLineForms = invoice.lines.map(line => ({
      lineId: line.id,
      productId: line.productId,
      designation: line.productName,
      quantity: line.quantity,
      trackingMode: 0,
      pickingPolicy: 0,
      lotAllocations: [createDefaultLotRow(line.quantity)]
    }));

    if (!this.stockFeatures()) {
      this.stockService.getFeatures()
        .pipe(takeUntilDestroyed(this.destroyRef))
        .subscribe(res => {
          if (res.success && res.data) this.stockFeatures.set(res.data);
        });
    }

    if (this.needsStockAllocationOnValidate() && invoice.warehouseId) {
      this.loadValidateTraceabilityContext(invoice);
    }

    this.showValidateDialog = true;
  }

  needsStockAllocationOnValidate(): boolean {
    const inv = this.invoice();
    return !!inv && !inv.isCreditNote && !!inv.warehouseId;
  }

  showValidateLineTraceability(line: { trackingMode: number; quantity: number }): boolean {
    const f = this.stockFeatures();
    if (!f || !this.validateWarehouseId()) return false;
    return line.quantity > 0
      && (showLotSection(line.trackingMode, f) || showSerialSection(line.trackingMode, f));
  }

  isValidateFormValid(): boolean {
    if (!this.needsStockAllocationOnValidate()) return true;
    const features = this.stockFeatures();
    for (const line of this.validateLineForms) {
      if (this.showValidateLineTraceability(line)) {
        if (!exitAllocationsValid(
          line.trackingMode,
          line.pickingPolicy,
          line.quantity,
          line.lotAllocations,
          features
        )) {
          return false;
        }
      }
    }
    return true;
  }

  submitValidate(): void {
    const invoice = this.invoice();
    if (!invoice || !this.isValidateFormValid()) return;

    const lineAllocations = this.needsStockAllocationOnValidate()
      ? this.validateLineForms
          .filter(lf => this.showValidateLineTraceability(lf))
          .map(lf => ({
            lineId: lf.lineId,
            allocations: buildAllocationPayload(lf.lotAllocations)
          }))
          .filter(la => la.allocations.length > 0)
      : undefined;

    this.invoiceService.validateInvoice(
      invoice.id,
      lineAllocations && lineAllocations.length > 0 ? { lineAllocations } : undefined
    ).subscribe({
      next: (response) => {
        if (response.success) {
          this.showValidateDialog = false;
          this.toastService.add({
            severity: 'success',
            summary: 'Succès',
            detail: 'Facture validée avec succès'
          });
          this.loadInvoice(invoice.id);
        } else {
          this.toastService.add({
            severity: 'error',
            summary: 'Erreur',
            detail: (response as any).message || 'Impossible de valider la facture'
          });
        }
      }
    });
  }

  private loadValidateTraceabilityContext(invoice: InvoiceDetail): void {
    const warehouseId = invoice.warehouseId;
    if (!warehouseId || invoice.lines.length === 0) return;

    const productIds = invoice.lines.map(l => l.productId);
    this.stockService.getTraceabilityContext(productIds, warehouseId)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(res => {
        if (!res.success || !res.data) return;
        const map = new Map(this.traceabilityByProduct());
        for (const ctx of res.data) {
          map.set(ctx.productId, { trackingMode: ctx.trackingMode, pickingPolicy: ctx.pickingPolicy });
        }
        this.traceabilityByProduct.set(map);
        for (const lineForm of this.validateLineForms) {
          const ctx = map.get(lineForm.productId);
          if (ctx) {
            lineForm.trackingMode = ctx.trackingMode;
            lineForm.pickingPolicy = ctx.pickingPolicy;
            if (ctx.trackingMode === TRACKING_MODE_SERIAL) {
              lineForm.lotAllocations = Array.from(
                { length: Math.max(1, Math.round(lineForm.quantity)) },
                () => createDefaultSerialRow()
              );
            }
          }
        }
      });
  }

  signInvoice(): void {
    const invoice = this.invoice();
    if (!invoice) return;

    this.invoiceService.signInvoice(invoice.id).subscribe({
      next: (response) => {
        if (response.success) {
          this.toastService.add({
            severity: 'success',
            summary: 'Succès',
            detail: 'Facture signée électroniquement'
          });
          this.loadInvoice(invoice.id);
        }
      },
      error: () => {
        this.toastService.add({
          severity: 'error',
          summary: 'Erreur',
          detail: 'Impossible de signer la facture'
        });
      }
    });
  }

  downloadPdf(): void {
    const invoice = this.invoice();
    if (!invoice) {
      this.toastService.add({
        severity: 'error',
        summary: 'Erreur',
        detail: 'Aucune facture disponible pour le téléchargement'
      });
      return;
    }

    this.invoiceService.downloadPdf(invoice.id).subscribe({
      next: (blob) => {
        // Vérifier que le blob est valide
        if (!blob || blob.size === 0) {
          this.toastService.add({
            severity: 'error',
            summary: 'Erreur',
            detail: 'Le fichier PDF est vide ou invalide'
          });
          return;
        }

        try {
          const url = window.URL.createObjectURL(blob);
          const a = document.createElement('a');
          a.href = url;
          a.download = `Facture_${invoice.number}.pdf`;
          document.body.appendChild(a);
          a.click();
          document.body.removeChild(a);
          window.URL.revokeObjectURL(url);

          this.toastService.add({
            severity: 'success',
            summary: 'Succès',
            detail: 'PDF téléchargé avec succès'
          });
        } catch (error) {
          this.toastService.add({
            severity: 'error',
            summary: 'Erreur',
            detail: 'Erreur lors de la création du lien de téléchargement'
          });
        }
      },
      error: (error: Error) => {
        const errorMessage = error.message || 'Impossible de télécharger le PDF';
        this.toastService.add({
          severity: 'error',
          summary: 'Erreur',
          detail: errorMessage
        });
      }
    });
  }

  sending = signal(false);

  sendByEmail(): void {
    const invoice = this.invoice();
    if (!invoice) return;

    this.confirmationService.confirm({
      message: `Envoyer la facture ${invoice.number} par email à ${invoice.client.email} ?`,
      header: 'Envoi par email',
      icon: 'pi pi-envelope',
      acceptLabel: 'Envoyer',
      rejectLabel: 'Annuler',
      accept: () => {
        this.sending.set(true);
        this.invoiceService.sendByEmail(invoice.id).subscribe({
          next: (response) => {
            this.sending.set(false);
            if (response.success) {
              this.toastService.add({
                severity: 'success',
                summary: 'Envoyé',
                detail: `Email envoyé à ${invoice.client.email}`
              });
              this.loadInvoice(invoice.id);
            }
          },
          error: (err) => {
            this.sending.set(false);
            this.toastService.add({
              severity: 'error',
              summary: 'Erreur',
              detail: err?.error?.errors?.[0] || 'Erreur lors de l\'envoi'
            });
          }
        });
      }
    });
  }

  duplicateInvoice(): void {
    this.toastService.add({
      severity: 'info',
      summary: 'Information',
      detail: 'Fonctionnalité en cours de développement'
    });
  }
}
