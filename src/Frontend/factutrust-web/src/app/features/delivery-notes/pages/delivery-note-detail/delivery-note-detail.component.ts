import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule, ActivatedRoute, Router } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { CardModule } from 'primeng/card';
import { TagModule } from 'primeng/tag';
import { DividerModule } from 'primeng/divider';
import { TableModule } from 'primeng/table';
import { TooltipModule } from 'primeng/tooltip';
import { MenuModule } from 'primeng/menu';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { ToastModule } from 'primeng/toast';
import { SkeletonModule } from 'primeng/skeleton';
import { DialogModule } from 'primeng/dialog';
import { CalendarModule } from 'primeng/calendar';
import { formatLocalDate } from '@core/utils/date.util';
import { InputTextModule } from 'primeng/inputtext';
import { InputNumberModule } from 'primeng/inputnumber';
import { ConfirmationService } from '@core/services/confirmation.service';
import { ToastService } from '@core/services/toast.service';
import { MenuItem } from '@shared/models/menu-item.model';
import { PageHeaderComponent } from '@shared/components/page-header/page-header.component';
import { BreadcrumbComponent, BreadcrumbItem } from '@shared/components/breadcrumb/breadcrumb.component';
import { ButtonComponent } from '@shared/components/button/button.component';
import { StatusBadgeComponent, StatusBadgeStatus } from '@shared/components/status-badge/status-badge.component';
import { DeliveryNoteService } from '../../services/delivery-note.service';
import { DeliveryNoteDetailDto, DeliveryNoteStatus, RecordDeliveryDto, RecordDeliveryLineDto } from '../../models/delivery-note.model';

@Component({
  selector: 'app-delivery-note-detail',
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
    MenuModule,
    ConfirmDialogModule,
    ToastModule,
    SkeletonModule,
    DialogModule,
    CalendarModule,
    InputTextModule,
    InputNumberModule,
    PageHeaderComponent,
    BreadcrumbComponent,
    ButtonComponent,
    StatusBadgeComponent
  ],
  template: `
    <app-breadcrumb [items]="breadcrumbItems()"></app-breadcrumb>
    
    <app-page-header 
      [title]="deliveryNote()?.number || 'Chargement...'" 
      [subtitle]="'Bon de livraison du ' + (deliveryNote()?.issueDate | date:'dd/MM/yyyy')">
      <app-button 
        variant="outline"
        icon="pi-arrow-left"
        iconPos="left"
        routerLink="/delivery-notes">
        Retour
      </app-button>
      @if (deliveryNote()) {
        <app-button 
          variant="secondary"
          icon="pi-ellipsis-v"
          iconPos="left"
          (click)="menu.toggle($event)">
          Actions
        </app-button>
        <p-menu #menu [model]="menuItems" [popup]="true"></p-menu>
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
    } @else if (deliveryNote()) {
      <div class="detail-grid">
        <!-- Main Content -->
        <div class="main-content">
          <p-card styleClass="delivery-note-card">
            <!-- Header -->
            <div class="delivery-note-header">
              <div class="delivery-note-meta">
                <h2>{{ deliveryNote()!.number }}</h2>
                <app-status-badge 
                  [status]="getStatusBadgeStatus(deliveryNote()!.status)"
                  [label]="deliveryNote()!.statusDisplay">
                </app-status-badge>
              </div>
              <div class="delivery-note-dates">
                <div class="date-item">
                  <span class="label">Date d'émission</span>
                  <span class="value">{{ deliveryNote()!.issueDate | date:'dd/MM/yyyy' }}</span>
                </div>
                @if (deliveryNote()!.deliveryDate) {
                  <div class="date-item">
                    <span class="label">Date de livraison</span>
                    <span class="value">{{ deliveryNote()!.deliveryDate | date:'dd/MM/yyyy' }}</span>
                  </div>
                }
              </div>
            </div>

            <p-divider></p-divider>

            <!-- Client Info -->
            <div class="parties-grid">
              <div class="party-card">
                <h4>Client</h4>
                <p class="party-name">{{ deliveryNote()!.clientName }}</p>
                @if (deliveryNote()!.clientEmail) {
                  <p class="party-email">{{ deliveryNote()!.clientEmail }}</p>
                }
              </div>
              <div class="party-card">
                <h4>Adresse de livraison</h4>
                <p class="party-address">{{ deliveryNote()!.deliveryAddress }}</p>
                @if (deliveryNote()!.deliveryCity) {
                  <p class="party-address">{{ deliveryNote()!.deliveryPostalCode }} {{ deliveryNote()!.deliveryCity }}</p>
                }
                @if (deliveryNote()!.recipientName) {
                  <p class="party-recipient"><strong>Destinataire:</strong> {{ deliveryNote()!.recipientName }}</p>
                }
              </div>
            </div>

            <p-divider></p-divider>

            <!-- Lines -->
            <div class="lines-section">
              <h4>Lignes du bon de livraison</h4>
              <table class="lines-table">
                <thead>
                  <tr>
                    <th>#</th>
                    <th>Désignation</th>
                    <th class="text-right">Prix U. HT</th>
                    <th class="text-right">Qté Comm.</th>
                    <th class="text-right">Qté Livrée</th>
                    <th class="text-right">Qté Rejetée</th>
                    <th class="text-right">TVA</th>
                    <th class="text-right">Total HT</th>
                    <th class="text-right">Total TTC</th>
                  </tr>
                </thead>
                <tbody>
                  @for (line of deliveryNote()!.lines; track line.id) {
                    <tr [class.fully-delivered]="line.isFullyDelivered">
                      <td>{{ line.lineNumber }}</td>
                      <td>
                        <span class="line-name">{{ line.designation }}</span>
                        @if (line.description) {
                          <br><small class="line-desc">{{ line.description }}</small>
                        }
                        @if (line.productCode) {
                          <br><small class="line-code">Code: {{ line.productCode }}</small>
                        }
                      </td>
                      <td class="text-right mono">{{ line.unitPriceHT | currency:'EUR':'symbol':'1.2-2' }} / {{ line.unit }}</td>
                      <td class="text-right mono">{{ line.orderedQuantity | number:'1.0-3' }}</td>
                      <td class="text-right mono">{{ line.deliveredQuantity | number:'1.0-3' }}</td>
                      <td class="text-right mono">
                        {{ line.rejectedQuantity | number:'1.0-3' }}
                        @if (line.rejectionReason) {
                          <i class="pi pi-info-circle" [pTooltip]="line.rejectionReason"></i>
                        }
                      </td>
                      <td class="text-right mono">{{ line.vatRatePercent }}%</td>
                      <td class="text-right mono">{{ line.totalHT | currency:'EUR':'symbol':'1.2-2' }}</td>
                      <td class="text-right mono">{{ line.totalTTC | currency:'EUR':'symbol':'1.2-2' }}</td>
                    </tr>
                  }
                </tbody>
              </table>
              
              <div class="totals-section">
                <div class="total-row">
                  <span>Total HT</span>
                  <span class="mono">{{ deliveryNote()!.totalHT | currency:'EUR':'symbol':'1.2-2' }}</span>
                </div>
                <div class="total-row">
                  <span>Total TVA</span>
                  <span class="mono">{{ deliveryNote()!.totalVAT | currency:'EUR':'symbol':'1.2-2' }}</span>
                </div>
                <div class="total-row total-ttc">
                  <span>Total TTC</span>
                  <span class="mono">{{ deliveryNote()!.totalTTC | currency:'EUR':'symbol':'1.2-2' }}</span>
                </div>
              </div>
            </div>

            @if (deliveryNote()!.notes || deliveryNote()!.reference) {
              <p-divider></p-divider>
              <div class="notes-section">
                @if (deliveryNote()!.reference) {
                  <div class="note">
                    <strong>Référence:</strong> {{ deliveryNote()!.reference }}
                  </div>
                }
                @if (deliveryNote()!.notes) {
                  <div class="note">
                    <strong>Notes:</strong> {{ deliveryNote()!.notes }}
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
                [status]="getStatusBadgeStatus(deliveryNote()!.status)"
                [label]="deliveryNote()!.statusDisplay">
              </app-status-badge>
              
              <div class="status-actions">
                @if (deliveryNote()!.status === 'Draft') {
                  <app-button 
                    variant="primary"
                    size="sm"
                    icon="pi-check"
                    iconPos="left"
                    (click)="confirmDeliveryNote()">
                    Valider
                  </app-button>
                }
                @if (deliveryNote()!.status === 'Confirmed') {
                  <app-button 
                    variant="primary"
                    size="sm"
                    icon="pi-truck"
                    iconPos="left"
                    (click)="startDelivery()">
                    Démarrer livraison
                  </app-button>
                }
                @if (deliveryNote()!.status === 'InTransit') {
                  <app-button 
                    variant="primary"
                    size="sm"
                    icon="pi-check-circle"
                    iconPos="left"
                    (click)="openRecordDeliveryDialog()">
                    Enregistrer livraison
                  </app-button>
                }
                @if ((deliveryNote()!.status === 'Delivered' || deliveryNote()!.status === 'PartiallyDelivered') && !deliveryNote()!.invoiceId) {
                  <app-button 
                    variant="primary"
                    size="sm"
                    icon="pi-file"
                    iconPos="left"
                    (click)="generateInvoice()">
                    Générer facture
                  </app-button>
                }
              </div>
            </div>
          </p-card>

          <!-- Actions Card -->
          <p-card header="Actions rapides" styleClass="actions-card">
            <div class="quick-actions">
              @if (deliveryNote()!.status === 'Draft' || deliveryNote()!.status === 'Confirmed') {
                <app-button 
                  variant="outline"
                  icon="pi-times"
                  iconPos="left"
                  (click)="cancelDeliveryNote()"
                  style="width: 100%; margin-bottom: var(--spacing-2);">
                  Annuler
                </app-button>
              }
              @if (deliveryNote()!.invoiceId) {
                <app-button 
                  variant="outline"
                  icon="pi-file"
                  iconPos="left"
                  [routerLink]="['/invoices', deliveryNote()!.invoiceId!]"
                  style="width: 100%;">
                  Voir la facture {{ deliveryNote()!.invoiceNumber }}
                </app-button>
              }
            </div>
          </p-card>

          <!-- Signature Info -->
          @if (deliveryNote()!.signedAt) {
            <p-card header="Signature" styleClass="signature-card">
              <div class="signature-info">
                <div class="info-row">
                  <i class="pi pi-verified"></i>
                  <span>Livraison signée</span>
                </div>
                <small>Signé le {{ deliveryNote()!.signedAt | date:'dd/MM/yyyy HH:mm' }}</small>
                @if (deliveryNote()!.recipientName) {
                  <small>Par: {{ deliveryNote()!.recipientName }}</small>
                }
              </div>
            </p-card>
          }
        </div>
      </div>

      <!-- Delivery Recording Dialog -->
      <p-dialog 
        header="Enregistrer la livraison" 
        [(visible)]="showDeliveryDialog" 
        [modal]="true" 
        [style]="{width: '750px', maxWidth: '95vw'}"
        [draggable]="false"
        [resizable]="false"
        [contentStyle]="{ overflow: 'visible' }">
        <div class="delivery-dialog-content">
          <!-- Context banner -->
          <div class="delivery-info-banner">
            <div class="delivery-info-item">
              <span class="label">Bon de livraison</span>
              <span class="value">{{ deliveryNote()!.number }}</span>
            </div>
          </div>

          <!-- Form fields grid -->
          <div class="delivery-form-grid">
            <div class="delivery-form-group">
              <label for="recipientName">Nom du réceptionnaire <span class="required">*</span></label>
              <input pInputText id="recipientName" [(ngModel)]="deliveryForm.recipientName" 
                placeholder="Nom et prénom du réceptionnaire" class="w-full" />
              <small class="field-hint">Personne ayant signé la réception</small>
            </div>
            <div class="delivery-form-group">
              <label for="deliveryDate">Date de livraison <span class="required">*</span></label>
              <p-calendar
                id="deliveryDate"
                [(ngModel)]="deliveryForm.deliveryDate"
                name="deliveryDate"
                dateFormat="dd/mm/yy"
                [showIcon]="true"
                placeholder="Sélectionnez la date"
                styleClass="w-full">
              </p-calendar>
              <small class="field-hint">Date effective de la réception des marchandises</small>
            </div>
          </div>
          
          <h4 class="delivery-lines-title">Quantités livrées par ligne</h4>
          <div class="delivery-lines-table-wrap">
            <table class="delivery-lines-form">
              <thead>
                <tr>
                  <th>Produit</th>
                  <th class="text-right">Qté commandée</th>
                  <th class="text-right">Qté livrée</th>
                  <th class="text-right">Qté rejetée</th>
                  <th class="motif-col">Motif refus</th>
                </tr>
              </thead>
              <tbody>
                @for (lineForm of deliveryLinesForms; track lineForm.lineId) {
                  <tr>
                    <td>{{ lineForm.designation }}</td>
                    <td class="text-right mono">{{ lineForm.orderedQuantity }}</td>
                    <td class="text-right">
                      <p-inputNumber [(ngModel)]="lineForm.deliveredQuantity" 
                        [min]="0" [max]="lineForm.orderedQuantity" 
                        [minFractionDigits]="0" [maxFractionDigits]="3"
                        inputStyleClass="input-sm text-right" />
                    </td>
                    <td class="text-right">
                      <p-inputNumber [(ngModel)]="lineForm.rejectedQuantity" 
                        [min]="0" [max]="lineForm.orderedQuantity" 
                        [minFractionDigits]="0" [maxFractionDigits]="3"
                        inputStyleClass="input-sm text-right" />
                    </td>
                    <td class="motif-col">
                      @if (lineForm.rejectedQuantity > 0) {
                        <input pInputText [(ngModel)]="lineForm.rejectionReason" 
                          placeholder="Motif du refus" class="input-sm w-full" />
                      }
                    </td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
        </div>
        <ng-template pTemplate="footer">
          <div class="dialog-footer">
            <app-button variant="outline" icon="pi-times" iconPos="left" (click)="showDeliveryDialog = false">Annuler</app-button>
            <app-button variant="primary" icon="pi-check" iconPos="left"
              [disabled]="!isDeliveryFormValid()" (click)="submitDelivery()">
              Valider la livraison
            </app-button>
          </div>
        </ng-template>
      </p-dialog>
    } @else {
      <div class="not-found">
        <i class="pi pi-exclamation-triangle"></i>
        <h2>Bon de livraison non trouvé</h2>
        <p>Le bon de livraison demandé n'existe pas ou a été supprimé.</p>
        <app-button variant="primary" routerLink="/delivery-notes">Retour à la liste</app-button>
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

    .delivery-note-header {
      display: flex;
      justify-content: space-between;
      align-items: flex-start;
      flex-wrap: wrap;
      gap: var(--spacing-4);
    }

    .delivery-note-meta {
      h2 {
        margin: 0 0 var(--spacing-2);
        font-family: 'JetBrains Mono', monospace;
        font-size: var(--font-size-2xl);
        font-weight: var(--font-weight-bold);
        color: var(--color-text-primary);
      }
    }

    .delivery-note-dates {
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
      .party-email,
      .party-recipient {
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

      tr.fully-delivered {
        background: var(--color-success-50);
      }

      .line-name {
        font-weight: var(--font-weight-medium);
      }

      .line-desc,
      .line-code {
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
      margin-top: var(--spacing-6);
      border-top: 1px solid var(--color-border-subtle);
      padding-top: var(--spacing-4);
      display: flex;
      flex-direction: column;
      align-items: flex-end;
      gap: var(--spacing-2);

      .total-row {
        display: flex;
        justify-content: space-between;
        width: 300px;
        font-size: var(--font-size-sm);
        color: var(--color-text-secondary);

        &.total-ttc {
          font-weight: var(--font-weight-bold);
          color: var(--color-text-primary);
          font-size: var(--font-size-lg);
          margin-top: var(--spacing-2);
          padding-top: var(--spacing-2);
          border-top: 2px solid var(--color-border-subtle);
        }
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
    }

    .status-actions {
      display: flex;
      gap: var(--spacing-2);
    }

    .quick-actions {
      display: flex;
      flex-direction: column;
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

      small {
        color: var(--color-neutral-500);
      }
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

    .delivery-dialog-content {
      padding: var(--spacing-5) var(--spacing-6);
      display: flex;
      flex-direction: column;
      gap: var(--spacing-5);
    }

    .delivery-info-banner {
      background: var(--color-background-elevated, #fff);
      border: 1px solid var(--color-border-subtle);
      border-left: 4px solid var(--color-primary-500);
      border-radius: var(--radius-xl);
      padding: var(--spacing-4) var(--spacing-5);
      box-shadow: var(--shadow-sm);
    }

    .delivery-info-item .label {
      font-size: var(--font-size-xs);
      color: var(--color-text-tertiary);
      font-weight: var(--font-weight-semibold);
      text-transform: uppercase;
      letter-spacing: 0.05em;
    }

    .delivery-info-item .value {
      font-size: var(--font-size-base);
      font-weight: var(--font-weight-semibold);
      color: var(--color-text-primary);
      font-family: 'JetBrains Mono', monospace;
    }

    .delivery-form-grid {
      display: grid;
      grid-template-columns: 1fr 1fr;
      gap: var(--spacing-5);
    }

    @media (max-width: 600px) {
      .delivery-form-grid {
        grid-template-columns: 1fr;
      }
    }

    .delivery-form-group {
      display: flex;
      flex-direction: column;
      gap: var(--spacing-2);

      label {
        font-size: var(--font-size-xs);
        font-weight: var(--font-weight-semibold);
        color: var(--color-text-secondary);
        text-transform: uppercase;
        letter-spacing: 0.05em;
      }
    }

    .delivery-form-group .required {
      color: var(--color-error-600);
    }

    .field-hint {
      font-size: var(--font-size-xs);
      color: var(--color-text-tertiary);
      margin-top: 2px;
    }

    .delivery-lines-title {
      margin: var(--spacing-4) 0 var(--spacing-3);
      font-size: var(--font-size-base);
      font-weight: var(--font-weight-semibold);
      color: var(--color-text-primary);
    }

    .delivery-lines-table-wrap {
      overflow-x: auto;
    }

    .delivery-lines-form {
      width: 100%;
      border-collapse: collapse;

      th {
        padding: var(--spacing-3) var(--spacing-3);
        font-size: var(--font-size-xs);
        text-transform: uppercase;
        letter-spacing: 0.05em;
        color: var(--color-text-secondary);
        text-align: left;
        border-bottom: 2px solid var(--color-border-default);
        background: var(--color-background-subtle);
        font-weight: var(--font-weight-semibold);
      }

      th.motif-col {
        min-width: 140px;
      }

      td {
        padding: var(--spacing-3);
        font-size: var(--font-size-sm);
        border-bottom: 1px solid var(--color-border-subtle);
        vertical-align: middle;
      }

      td.motif-col {
        min-width: 140px;
      }
    }

    .dialog-footer {
      display: flex;
      justify-content: flex-end;
      gap: var(--spacing-3);
    }

    .input-sm {
      width: 80px;
      font-size: var(--font-size-sm);
    }

    .w-full {
      width: 100%;
    }

    :host ::ng-deep .delivery-dialog-content .p-calendar {
      width: 100%;
    }

    :host ::ng-deep {
      .p-dialog {
        border-radius: var(--radius-xl);
        box-shadow: var(--shadow-xl);
        overflow: hidden;
      }

      .p-dialog-content {
        border-radius: 0;
        padding: 0;
      }

      .p-dialog-header {
        padding: var(--spacing-4) var(--spacing-6);
        border-bottom: 1px solid var(--color-border-subtle);
        background: var(--color-background-elevated);
      }

      .p-dialog-header .p-dialog-title {
        font-size: var(--font-size-lg);
        font-weight: var(--font-weight-semibold);
        color: var(--color-text-primary);
      }

      .p-dialog-footer {
        padding: var(--spacing-4) var(--spacing-6);
        border-top: 1px solid var(--color-border-subtle);
        background: var(--color-background-subtle);
      }

      .delivery-note-card .p-card-body {
        padding: var(--card-padding);
      }

      .status-card,
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
    }
  `]
})
export class DeliveryNoteDetailComponent implements OnInit {
  private deliveryNoteService = inject(DeliveryNoteService);
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private confirmationService = inject(ConfirmationService);
  private toastService = inject(ToastService);

  loading = signal(true);
  deliveryNote = signal<DeliveryNoteDetailDto | null>(null);

  menuItems: MenuItem[] = [];

  // Delivery dialog
  showDeliveryDialog = false;
  deliveryForm: { recipientName: string; deliveryDate: Date } = { recipientName: '', deliveryDate: new Date() };
  deliveryLinesForms: Array<{
    lineId: string;
    designation: string;
    orderedQuantity: number;
    deliveredQuantity: number;
    rejectedQuantity: number;
    rejectionReason: string;
  }> = [];

  breadcrumbItems = computed<BreadcrumbItem[]>(() => {
    const note = this.deliveryNote();
    return [
      { label: 'Tableau de bord', route: '/dashboard', icon: 'pi-home' },
      { label: 'Bons de Livraison', route: '/delivery-notes' },
      { label: note?.number || 'Bon de livraison' }
    ];
  });

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
      this.loadDeliveryNote(id);
    } else {
      this.router.navigate(['/delivery-notes']);
    }
  }

  private loadDeliveryNote(id: string): void {
    this.loading.set(true);

    this.deliveryNoteService.getDeliveryNote(id).subscribe({
      next: (response) => {
        if (response.success) {
          this.deliveryNote.set(response.data);
          this.buildMenu(response.data);
        } else {
          this.deliveryNote.set(null);
        }
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.deliveryNote.set(null);
      }
    });
  }

  private buildMenu(note: DeliveryNoteDetailDto): void {
    this.menuItems = [];

    if (note.status === DeliveryNoteStatus.Draft) {
      this.menuItems.push({
        label: 'Valider',
        icon: 'pi pi-check',
        command: () => this.confirmDeliveryNote()
      });
    }

    if (note.status === DeliveryNoteStatus.Confirmed) {
      this.menuItems.push({
        label: 'Démarrer livraison',
        icon: 'pi pi-truck',
        command: () => this.startDelivery()
      });
    }

    if (note.status === DeliveryNoteStatus.InTransit) {
      this.menuItems.push({
        label: 'Enregistrer livraison',
        icon: 'pi pi-check-circle',
        command: () => this.openRecordDeliveryDialog()
      });
    }

    if (note.status === DeliveryNoteStatus.Draft || note.status === DeliveryNoteStatus.Confirmed) {
      this.menuItems.push({
        label: 'Annuler',
        icon: 'pi pi-times',
        command: () => this.cancelDeliveryNote()
      });
    }

    if ((note.status === DeliveryNoteStatus.Delivered || note.status === DeliveryNoteStatus.PartiallyDelivered) && !note.invoiceId) {
      this.menuItems.push({
        label: 'Générer facture',
        icon: 'pi pi-file',
        command: () => this.generateInvoice()
      });
    }
  }

  confirmDeliveryNote(): void {
    const note = this.deliveryNote();
    if (!note) return;

    this.confirmationService.confirm({
      header: 'Confirmer le bon de livraison',
      message: `Êtes-vous sûr de vouloir confirmer le bon de livraison ${note.number} ?`,
      acceptLabel: 'Confirmer',
      rejectLabel: 'Annuler',
      icon: 'pi pi-check-circle',
      accept: () => {
        this.deliveryNoteService.confirmDeliveryNote(note.id).subscribe({
          next: (response) => {
            if (response.success) {
              this.toastService.add({
                severity: 'success',
                summary: 'Succès',
                detail: 'Bon de livraison confirmé avec succès'
              });
              this.loadDeliveryNote(note.id);
            } else {
              this.toastService.add({
                severity: 'error',
                summary: 'Erreur',
                detail: (response as any).message || 'Impossible de confirmer le bon de livraison'
              });
            }
          }
          // No error handler: the global error interceptor already shows the API error toast.
        });
      }
    });
  }

  startDelivery(): void {
    const note = this.deliveryNote();
    if (!note) return;

    this.confirmationService.confirm({
      header: 'Démarrer la livraison',
      message: `Êtes-vous sûr de vouloir démarrer la livraison du bon ${note.number} ?`,
      acceptLabel: 'Démarrer',
      rejectLabel: 'Annuler',
      icon: 'pi pi-truck',
      accept: () => {
        this.deliveryNoteService.startDelivery(note.id).subscribe({
          next: (response) => {
            if (response.success) {
              this.toastService.add({
                severity: 'success',
                summary: 'Succès',
                detail: 'Livraison démarrée avec succès'
              });
              this.loadDeliveryNote(note.id);
            } else {
              this.toastService.add({
                severity: 'error',
                summary: 'Erreur',
                detail: (response as any).message || 'Impossible de démarrer la livraison'
              });
            }
          }
          // No error handler: the global error interceptor already shows the API error toast.
        });
      }
    });
  }

  openRecordDeliveryDialog(): void {
    const note = this.deliveryNote();
    if (!note) return;

    this.deliveryForm = { recipientName: '', deliveryDate: new Date() };

    // Pre-fill line forms: default delivered = ordered, rejected = 0
    this.deliveryLinesForms = note.lines.map(line => ({
      lineId: line.id,
      designation: line.designation,
      orderedQuantity: line.orderedQuantity,
      deliveredQuantity: line.orderedQuantity,
      rejectedQuantity: 0,
      rejectionReason: ''
    }));

    this.showDeliveryDialog = true;
  }

  isDeliveryFormValid(): boolean {
    if (!this.deliveryForm.recipientName.trim() || !this.deliveryForm.deliveryDate) return false;
    // Check each line: delivered + rejected <= ordered
    for (const line of this.deliveryLinesForms) {
      if (line.deliveredQuantity + line.rejectedQuantity > line.orderedQuantity) {
        return false;
      }
      if (line.rejectedQuantity > 0 && !line.rejectionReason.trim()) {
        return false;
      }
    }
    return true;
  }

  submitDelivery(): void {
    const note = this.deliveryNote();
    if (!note || !this.isDeliveryFormValid()) return;

    const lines: RecordDeliveryLineDto[] = this.deliveryLinesForms.map(lf => ({
      lineId: lf.lineId,
      deliveredQuantity: lf.deliveredQuantity,
      rejectedQuantity: lf.rejectedQuantity,
      rejectionReason: lf.rejectedQuantity > 0 ? lf.rejectionReason : undefined
    }));

    const dto: RecordDeliveryDto = {
      deliveryDate: this.formatDate(this.deliveryForm.deliveryDate),
      recipientName: this.deliveryForm.recipientName.trim(),
      lines
    };

    this.deliveryNoteService.recordDelivery(note.id, dto).subscribe({
      next: (response) => {
        if (response.success) {
          this.showDeliveryDialog = false;
          this.toastService.add({
            severity: 'success',
            summary: 'Succès',
            detail: 'Livraison enregistrée avec succès'
          });
          this.loadDeliveryNote(note.id);
        } else {
          this.toastService.add({
            severity: 'error',
            summary: 'Erreur',
            detail: (response as any).message || 'Impossible d\'enregistrer la livraison'
          });
        }
      }
      // No error handler: the global error interceptor already shows the API error toast.
    });
  }

  cancelDeliveryNote(): void {
    const note = this.deliveryNote();
    if (!note) return;

    this.confirmationService.confirm({
      header: 'Annuler le bon de livraison',
      message: `Êtes-vous sûr de vouloir annuler le bon de livraison ${note.number} ?`,
      acceptLabel: 'Annuler le bon',
      rejectLabel: 'Retour',
      icon: 'pi pi-exclamation-triangle',
      acceptButtonStyleClass: 'btn-danger',
      accept: () => {
        this.deliveryNoteService.cancelDeliveryNote(note.id, { reason: 'Annulation manuelle' }).subscribe({
          next: (response) => {
            if (response.success) {
              this.toastService.add({
                severity: 'success',
                summary: 'Succès',
                detail: 'Bon de livraison annulé'
              });
              this.loadDeliveryNote(note.id);
            } else {
              this.toastService.add({
                severity: 'error',
                summary: 'Erreur',
                detail: (response as any).message || 'Impossible d\'annuler le bon de livraison'
              });
            }
          }
          // No error handler: the global error interceptor already shows the API error toast.
        });
      }
    });
  }

  generateInvoice(): void {
    const note = this.deliveryNote();
    if (!note) return;

    this.confirmationService.confirm({
      header: 'Générer facture',
      message: `Voulez-vous générer une facture pour le bon de livraison ${note.number} ?`,
      acceptLabel: 'Générer',
      rejectLabel: 'Annuler',
      icon: 'pi pi-file',
      accept: () => {
        this.deliveryNoteService.generateInvoice(note.id).subscribe({
          next: (response) => {
            if (response.success) {
              this.toastService.add({
                severity: 'success',
                summary: 'Succès',
                detail: 'Facture générée avec succès'
              });
              this.loadDeliveryNote(note.id);
              if (response.data) {
                this.router.navigate(['/invoices', response.data]);
              }
            } else {
              this.toastService.add({
                severity: 'error',
                summary: 'Erreur',
                detail: (response as any).message || 'Impossible de générer la facture'
              });
            }
          }
          // No error handler: the global error interceptor already shows the API error toast.
        });
      }
    });
  }

  private formatDate(d: Date): string {
    return formatLocalDate(d);
  }

  getStatusBadgeStatus(status: DeliveryNoteStatus): StatusBadgeStatus {
    const statusMap: Record<DeliveryNoteStatus, StatusBadgeStatus> = {
      [DeliveryNoteStatus.Draft]: 'draft',
      [DeliveryNoteStatus.Confirmed]: 'validated',
      [DeliveryNoteStatus.InTransit]: 'sent',
      [DeliveryNoteStatus.Delivered]: 'accepted',
      [DeliveryNoteStatus.PartiallyDelivered]: 'pending',
      [DeliveryNoteStatus.Failed]: 'rejected',
      [DeliveryNoteStatus.Refused]: 'rejected',
      [DeliveryNoteStatus.Cancelled]: 'cancelled',
      [DeliveryNoteStatus.Invoiced]: 'converted'
    };
    return statusMap[status] || 'draft';
  }
}
