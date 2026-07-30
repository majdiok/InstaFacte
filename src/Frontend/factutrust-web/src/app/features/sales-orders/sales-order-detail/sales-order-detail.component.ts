import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule, ActivatedRoute } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { TableModule } from 'primeng/table';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { InputTextarea } from 'primeng/inputtextarea';
import { InputNumberModule } from 'primeng/inputnumber';
import { TagModule } from 'primeng/tag';
import { TooltipModule } from 'primeng/tooltip';
import { DialogModule } from 'primeng/dialog';
import { ProgressBarModule } from 'primeng/progressbar';
import { PageHeaderComponent } from '@shared/components/page-header/page-header.component';
import { BreadcrumbComponent, BreadcrumbItem } from '@shared/components/breadcrumb/breadcrumb.component';
import { EmptyStateComponent } from '@shared/components/empty-state/empty-state.component';
import { ButtonComponent } from '@shared/components/button/button.component';
import {
  SalesOrderService,
  SalesOrderDetail,
  SalesOrderLine,
  SalesOrderStatus
} from '@core/services/sales-order.service';
import { ToastService } from '@core/services/toast.service';
import { ErrorHandlerService } from '@core/services/error-handler.service';
import { AuthService } from '@core/services/auth.service';
import { ConfirmationService } from '@core/services/confirmation.service';
import { PERMISSIONS } from '@core/config/permission-keys';

@Component({
  selector: 'app-sales-order-detail',
  standalone: true,
  imports: [
    CommonModule,
    RouterModule,
    FormsModule,
    TableModule,
    ButtonModule,
    InputTextModule,
    InputTextarea,
    InputNumberModule,
    TagModule,
    TooltipModule,
    DialogModule,
    ProgressBarModule,
    PageHeaderComponent,
    BreadcrumbComponent,
    EmptyStateComponent,
    ButtonComponent
  ],
  template: `
    <app-breadcrumb [items]="breadcrumbItems()"></app-breadcrumb>

    @if (order(); as o) {
      <app-page-header [title]="'Commande ' + o.number" [subtitle]="headerSubtitle()">
        @if (canConfirm()) {
          <app-button variant="primary" icon="pi-check" iconPos="left" (clicked)="confirmOrder()">
            Confirmer
          </app-button>
        }
        @if (canEditDiscount()) {
          <app-button
            variant="secondary"
            icon="pi-percentage"
            iconPos="left"
            (clicked)="openDiscount()"
            pTooltip="La remise réduit la base de TVA et le FODEC, répartie sur les lignes.">
            Remise de pied
          </app-button>
        }
        @if (canClose()) {
          <app-button
            variant="secondary"
            icon="pi-flag"
            iconPos="left"
            (clicked)="openClose()"
            pTooltip="Solder la commande en renonçant au reliquat.">
            Solder
          </app-button>
        }
        @if (canCancel()) {
          <app-button variant="danger" icon="pi-times" iconPos="left" (clicked)="openCancel()">
            Annuler
          </app-button>
        }
      </app-page-header>

      <!-- Statut et provenance -->
      <div class="ft-detail-bar">
        <p-tag [severity]="statusSeverity(o.status)" [value]="o.statusDisplay"></p-tag>

        @if (o.isStockReserved) {
          <span class="ft-chip" pTooltip="Le stock est réservé pour cette commande.">
            <i class="pi pi-box"></i> Stock réservé
          </span>
        }

        @if (o.sourceQuoteId) {
          <a [routerLink]="['/quotes', o.sourceQuoteId]" class="ft-chip ft-chip--link">
            <i class="pi pi-file-import"></i>
            Issue du devis {{ o.sourceQuoteNumber ?? '' }}
          </a>
        }
      </div>

      @if (o.status === 'Cancelled' && o.cancellationReason) {
        <div class="ft-alert ft-alert--danger">
          <i class="pi pi-times-circle"></i>
          <strong>Commande annulée</strong> — {{ o.cancellationReason }}
        </div>
      }

      @if (o.status === 'Closed' && o.closureReason) {
        <div class="ft-alert ft-alert--muted">
          <i class="pi pi-flag"></i>
          <strong>Commande soldée</strong> — {{ o.closureReason }}
          <span class="ft-muted">Le reliquat non livré a été abandonné.</span>
        </div>
      }

      <!-- Avancement : les trois quantités sont ce que ce module apporte -->
      <div class="ft-cards-row">
        <div class="ft-card">
          <div class="ft-card__label">Livraison</div>
          <p-progressBar [value]="deliveryProgress()" [showValue]="true"></p-progressBar>
          <div class="ft-card__hint">
            Reste à livrer : {{ o.totalPendingDeliveryQuantity | number: '1.0-4' }}
          </div>
        </div>

        <div class="ft-card">
          <div class="ft-card__label">Facturation</div>
          <p-progressBar [value]="invoiceProgress()" [showValue]="true"></p-progressBar>
          <div class="ft-card__hint">
            Reste à facturer : {{ o.totalPendingInvoiceQuantity | number: '1.0-4' }}
          </div>
        </div>

        <div class="ft-card">
          <div class="ft-card__label">Carnet</div>
          <div class="ft-card__value">{{ o.backlogAmountHt | number: '1.3-3' }} {{ o.currency }}</div>
          <div class="ft-card__hint">Valeur HT restant à livrer</div>
        </div>
      </div>

      <!-- En-tête -->
      <div class="ft-panel">
        <h3 class="ft-panel__title">Informations</h3>
        <div class="ft-info-grid">
          <div><span class="ft-info__label">Client</span>
            <a [routerLink]="['/clients', o.clientId]" class="ft-link">{{ o.client.name }}</a>
          </div>
          <div><span class="ft-info__label">Date de commande</span>
            {{ o.orderDate | date: 'dd/MM/yyyy' }}
          </div>
          <div><span class="ft-info__label">Livraison prévue</span>
            {{ o.expectedDeliveryDate ? (o.expectedDeliveryDate | date: 'dd/MM/yyyy') : '—' }}
          </div>
          <div><span class="ft-info__label">Référence</span>{{ o.reference || '—' }}</div>
          <div><span class="ft-info__label">Dépôt</span>{{ o.warehouseName || '—' }}</div>
          <div><span class="ft-info__label">Conditions de règlement</span>{{ o.paymentTerms || '—' }}</div>
        </div>

        @if (o.notes) {
          <div class="ft-notes"><span class="ft-info__label">Notes</span>{{ o.notes }}</div>
        }
      </div>

      <!-- Lignes -->
      <div class="ft-table-card">
        <p-table [value]="o.lines" [rowHover]="true" styleClass="ft-table">
          <ng-template pTemplate="header">
            <tr>
              <th>#</th>
              <th>Produit</th>
              <th class="ft-num">Cdé</th>
              <th class="ft-num">Livré</th>
              <th class="ft-num">Facturé</th>
              <th class="ft-num">Reste à livrer</th>
              <th class="ft-num">PU HT</th>
              <th class="ft-num">Remise</th>
              <th class="ft-num">Total HT</th>
            </tr>
          </ng-template>

          <ng-template pTemplate="body" let-line>
            <tr>
              <td>{{ line.lineNumber }}</td>
              <td>
                <span class="ft-muted">{{ line.productCode }}</span> — {{ line.productName }}
                @if (line.notes) {
                  <i class="pi pi-comment ft-icon-hint" [pTooltip]="line.notes"></i>
                }
              </td>
              <td class="ft-num">{{ line.quantity | number: '1.0-4' }}</td>
              <td class="ft-num">
                <span [class.ft-complete]="line.isFullyDelivered">
                  {{ line.deliveredQuantity | number: '1.0-4' }}
                </span>
              </td>
              <td class="ft-num">
                <span [class.ft-complete]="line.isFullyInvoiced">
                  {{ line.invoicedQuantity | number: '1.0-4' }}
                </span>
              </td>
              <td class="ft-num">
                @if (line.pendingDeliveryQuantity > 0) {
                  <strong>{{ line.pendingDeliveryQuantity | number: '1.0-4' }}</strong>
                } @else {
                  <i class="pi pi-check ft-icon-ok" pTooltip="Ligne entièrement livrée"></i>
                }
              </td>
              <td class="ft-num">{{ line.unitPrice | number: '1.3-3' }}</td>
              <td class="ft-num">{{ line.discountPercent ? (line.discountPercent + ' %') : '—' }}</td>
              <td class="ft-num">{{ line.subTotal | number: '1.3-3' }}</td>
            </tr>
          </ng-template>
        </p-table>
      </div>

      <!-- Totaux -->
      <div class="ft-totals">
        @if (o.globalDiscountAmount > 0) {
          <div class="ft-totals__row">
            <span>Sous-total HT</span>
            <span>{{ o.subTotalBeforeGlobalDiscount | number: '1.3-3' }}</span>
          </div>
          <div class="ft-totals__row ft-totals__row--discount">
            <span>
              Remise de pied
              @if (o.globalDiscountPercent) {
                ({{ o.globalDiscountPercent }} %)
              }
            </span>
            <span>- {{ o.globalDiscountAmount | number: '1.3-3' }}</span>
          </div>
          <div class="ft-totals__row">
            <span>HT après remise</span>
            <span>{{ o.subTotal | number: '1.3-3' }}</span>
          </div>
        } @else {
          <div class="ft-totals__row"><span>Sous-total HT</span><span>{{ o.subTotal | number: '1.3-3' }}</span></div>
        }
        @if (o.fodecAmount > 0) {
          <div class="ft-totals__row"><span>FODEC</span><span>{{ o.fodecAmount | number: '1.3-3' }}</span></div>
        }
        @for (vat of o.vatBreakdown; track vat.rate) {
          <div class="ft-totals__row">
            <span>TVA {{ vat.rateDisplay }}</span><span>{{ vat.vatAmount | number: '1.3-3' }}</span>
          </div>
        }
        @if (o.fiscalStampAmount > 0) {
          <div class="ft-totals__row">
            <span>Timbre fiscal</span><span>{{ o.fiscalStampAmount | number: '1.3-3' }}</span>
          </div>
        }
        <div class="ft-totals__row ft-totals__row--grand">
          <span>Total TTC</span><span>{{ o.totalAmount | number: '1.3-3' }} {{ o.currency }}</span>
        </div>
      </div>
    } @else if (!loading()) {
      <app-empty-state
        icon="pi-exclamation-circle"
        title="Commande introuvable"
        message="Cette commande n'existe pas ou a été supprimée.">
      </app-empty-state>
    }

    <!-- Annulation -->
    <p-dialog
      header="Annuler la commande"
      [(visible)]="cancelVisible"
      [modal]="true"
      [style]="{ width: '32rem' }"
      [draggable]="false">
      <p class="ft-dialog-intro">
        L'annulation libère les réservations de stock. Elle suppose qu'aucune livraison n'a eu
        lieu ; sinon, utilisez plutôt « Solder ».
      </p>
      <div class="ft-field">
        <label for="so-cancel-reason">Motif <span class="ft-required">*</span></label>
        <textarea
          id="so-cancel-reason"
          pInputTextarea
          [(ngModel)]="cancelReason"
          rows="3"
          maxlength="500"></textarea>
      </div>

      <ng-template pTemplate="footer">
        <app-button variant="secondary" (clicked)="cancelVisible = false">Retour</app-button>
        <app-button variant="danger" [disabled]="!cancelReason.trim() || acting()" (clicked)="doCancel()">
          Annuler la commande
        </app-button>
      </ng-template>
    </p-dialog>

    <!-- Remise de pied -->
    <p-dialog
      header="Remise de pied de document"
      [(visible)]="discountVisible"
      [modal]="true"
      [style]="{ width: '34rem' }"
      [draggable]="false">
      <p class="ft-dialog-intro">
        La remise est répartie sur les lignes au prorata de leur base HT : elle réduit donc la
        base de TVA et le FODEC. Le timbre fiscal, droit fixe, n'est pas touché.
        Laissez les deux champs vides pour retirer la remise.
      </p>

      <div class="ft-form-grid">
        <div class="ft-field">
          <label for="so-disc-pct">Pourcentage</label>
          <p-inputNumber
            inputId="so-disc-pct"
            [(ngModel)]="discountPercent"
            (onInput)="onPercentInput()"
            mode="decimal"
            [minFractionDigits]="0"
            [maxFractionDigits]="2"
            [min]="0"
            [max]="100"
            suffix=" %"></p-inputNumber>
        </div>

        <div class="ft-field">
          <label for="so-disc-amt">ou Montant HT</label>
          <p-inputNumber
            inputId="so-disc-amt"
            [(ngModel)]="discountAmount"
            (onInput)="onAmountInput()"
            mode="decimal"
            [minFractionDigits]="3"
            [maxFractionDigits]="3"
            [min]="0"></p-inputNumber>
          <small class="ft-hint">Un seul des deux : saisir l'un efface l'autre.</small>
        </div>
      </div>

      <ng-template pTemplate="footer">
        <app-button variant="secondary" (clicked)="discountVisible = false">Annuler</app-button>
        <app-button variant="primary" [disabled]="acting()" (clicked)="saveDiscount()">
          Enregistrer
        </app-button>
      </ng-template>
    </p-dialog>

    <!-- Solde -->
    <p-dialog
      header="Solder la commande"
      [(visible)]="closeVisible"
      [modal]="true"
      [style]="{ width: '32rem' }"
      [draggable]="false">
      <p class="ft-dialog-intro">
        Solder ferme la commande en renonçant au reliquat non livré. Ce qui a déjà été livré et
        facturé n'est pas touché.
      </p>
      <div class="ft-field">
        <label for="so-close-reason">Motif <span class="ft-required">*</span></label>
        <textarea
          id="so-close-reason"
          pInputTextarea
          [(ngModel)]="closeReason"
          rows="3"
          maxlength="500"></textarea>
      </div>

      <ng-template pTemplate="footer">
        <app-button variant="secondary" (clicked)="closeVisible = false">Retour</app-button>
        <app-button variant="primary" [disabled]="!closeReason.trim() || acting()" (clicked)="doClose()">
          Solder
        </app-button>
      </ng-template>
    </p-dialog>
  `
})
export class SalesOrderDetailComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly salesOrderService = inject(SalesOrderService);
  private readonly toastService = inject(ToastService);
  private readonly errorHandler = inject(ErrorHandlerService);
  private readonly auth = inject(AuthService);
  private readonly confirmationService = inject(ConfirmationService);

  readonly order = signal<SalesOrderDetail | null>(null);
  readonly loading = signal(true);
  readonly acting = signal(false);

  cancelVisible = false;
  cancelReason = '';
  discountVisible = false;
  discountPercent: number | null = null;
  discountAmount: number | null = null;
  closeVisible = false;
  closeReason = '';

  private orderId = '';

  readonly breadcrumbItems = computed<BreadcrumbItem[]>(() => [
    { label: 'Ventes' },
    { label: 'Commandes clients', route: '/sales-orders' },
    { label: this.order()?.number ?? 'Détail' }
  ]);

  readonly headerSubtitle = computed(() => {
    const o = this.order();
    if (!o) return '';
    return `${o.client.name} · ${o.lines.length} ligne(s)`;
  });

  /** Confirmable seulement au brouillon : le domaine refuse toute autre transition. */
  readonly canConfirm = computed(
    () => this.order()?.status === 'Draft' && this.auth.hasPermission(PERMISSIONS.salesOrders.update)
  );

  /** Solder n'a de sens que s'il reste effectivement à livrer. */
  readonly canClose = computed(() => {
    const o = this.order();
    if (!o) return false;
    const open = o.status === 'Confirmed' || o.status === 'PartiallyDelivered';
    return open && o.totalPendingDeliveryQuantity > 0
      && this.auth.hasPermission(PERMISSIONS.salesOrders.update);
  });

  /** La remise ne se pose que tant que la commande est modifiable. */
  readonly canEditDiscount = computed(
    () => this.order()?.status === 'Draft' && this.auth.hasPermission(PERMISSIONS.salesOrders.update)
  );

  readonly canCancel = computed(() => {
    const o = this.order();
    if (!o) return false;
    const cancellable = o.status === 'Draft' || o.status === 'Confirmed';
    return cancellable && this.auth.hasPermission(PERMISSIONS.salesOrders.update);
  });

  readonly deliveryProgress = computed(() => this.progress('delivered'));
  readonly invoiceProgress = computed(() => this.progress('invoiced'));

  ngOnInit(): void {
    this.orderId = this.route.snapshot.paramMap.get('id') ?? '';
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.salesOrderService.getSalesOrder(this.orderId).subscribe({
      next: res => {
        this.order.set(res.data ?? null);
        this.loading.set(false);
      },
      error: err => {
        this.showError(err, 'Impossible de charger la commande');
        this.errorHandler.logError('Failed to load sales order', err);
        this.order.set(null);
        this.loading.set(false);
      }
    });
  }

  private progress(kind: 'delivered' | 'invoiced'): number {
    const o = this.order();
    if (!o || o.lines.length === 0) return 0;

    const ordered = o.lines.reduce((s: number, l: SalesOrderLine) => s + l.quantity, 0);
    if (ordered === 0) return 0;

    const done = o.lines.reduce(
      (s: number, l: SalesOrderLine) => s + (kind === 'delivered' ? l.deliveredQuantity : l.invoicedQuantity),
      0
    );

    return Math.round((done / ordered) * 100);
  }

  statusSeverity(status: SalesOrderStatus): 'success' | 'info' | 'warning' | 'danger' | 'secondary' {
    switch (status) {
      case 'Draft':
        return 'secondary';
      case 'Confirmed':
      case 'Delivered':
        return 'info';
      case 'PartiallyDelivered':
        return 'warning';
      case 'Completed':
        return 'success';
      case 'Cancelled':
        return 'danger';
      default:
        return 'secondary';
    }
  }

  confirmOrder(): void {
    this.confirmationService.confirm({
      header: 'Confirmer la commande',
      message:
        'Confirmer engage fermement la commande et rend les lignes non modifiables. ' +
        'Si la réservation de stock est activée, le stock sera réservé.',
      icon: 'pi pi-check-circle',
      acceptLabel: 'Confirmer',
      rejectLabel: 'Retour',
      accept: () => this.doConfirm()
    });
  }

  private doConfirm(): void {
    this.acting.set(true);
    this.salesOrderService.confirmSalesOrder(this.orderId).subscribe({
      next: () => {
        this.toastService.add({ severity: 'success', summary: 'Succès', detail: 'Commande confirmée.' });
        this.acting.set(false);
        this.load();
      },
      error: err => {
        this.showError(err, 'Confirmation impossible');
        this.errorHandler.logError('Confirm sales order failed', err);
        this.acting.set(false);
      }
    });
  }

  openDiscount(): void {
    const o = this.order();
    this.discountPercent = o?.globalDiscountPercent ?? null;
    this.discountAmount = o?.globalDiscountPercent ? null : (o?.globalDiscountAmount || null);
    this.discountVisible = true;
  }

  // Pourcentage et montant sont exclusifs côté serveur : on efface l'autre plutôt que de
  // laisser l'utilisateur soumettre une saisie qui sera refusée.
  onPercentInput(): void {
    if (this.discountPercent) this.discountAmount = null;
  }

  onAmountInput(): void {
    if (this.discountAmount) this.discountPercent = null;
  }

  saveDiscount(): void {
    this.acting.set(true);
    this.salesOrderService
      .setGlobalDiscount(this.orderId, this.discountPercent, this.discountAmount)
      .subscribe({
        next: () => {
          this.toastService.add({
            severity: 'success',
            summary: 'Succès',
            detail: 'Remise de pied enregistrée.'
          });
          this.discountVisible = false;
          this.acting.set(false);
          this.load();
        },
        error: err => {
          this.showError(err, 'Enregistrement de la remise impossible');
          this.errorHandler.logError('Set global discount failed', err);
          this.acting.set(false);
        }
      });
  }

  openCancel(): void {
    this.cancelReason = '';
    this.cancelVisible = true;
  }

  doCancel(): void {
    this.acting.set(true);
    this.salesOrderService.cancelSalesOrder(this.orderId, this.cancelReason.trim()).subscribe({
      next: () => {
        this.toastService.add({ severity: 'success', summary: 'Succès', detail: 'Commande annulée.' });
        this.cancelVisible = false;
        this.acting.set(false);
        this.load();
      },
      error: err => {
        this.showError(err, 'Annulation impossible');
        this.errorHandler.logError('Cancel sales order failed', err);
        this.acting.set(false);
      }
    });
  }

  openClose(): void {
    this.closeReason = '';
    this.closeVisible = true;
  }

  doClose(): void {
    this.acting.set(true);
    this.salesOrderService.closeSalesOrder(this.orderId, this.closeReason.trim()).subscribe({
      next: () => {
        this.toastService.add({ severity: 'success', summary: 'Succès', detail: 'Commande soldée.' });
        this.closeVisible = false;
        this.acting.set(false);
        this.load();
      },
      error: err => {
        this.showError(err, 'Solde impossible');
        this.errorHandler.logError('Close sales order failed', err);
        this.acting.set(false);
      }
    });
  }

  private showError(err: unknown, fallback: string): void {
    const msg = this.errorHandler.extractErrorMessage(err);
    this.toastService.add({ severity: 'error', summary: 'Erreur', detail: msg || fallback });
  }
}
