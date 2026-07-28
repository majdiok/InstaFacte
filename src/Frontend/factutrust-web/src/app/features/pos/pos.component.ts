import { Component, inject, OnDestroy, OnInit, HostListener, ViewChild, effect } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { switchMap, map, catchError, tap } from 'rxjs/operators';
import { throwError, of } from 'rxjs';
import { PosStateService } from './services/pos-state.service';
import { PosOrderLine, PaymentSplit } from './services/pos-state.service';
import { PosBarcodeService } from './services/pos-barcode.service';
import { PosHeldOrdersService } from './services/pos-held-orders.service';
import { PosDualScreenService } from './services/pos-dual-screen.service';
import { PosStockService } from './services/pos-stock.service';
import { PosUpsellService } from './services/pos-upsell.service';
import { PosAudioService } from './services/pos-audio.service';
import { PosVoiceCommandService } from './services/pos-voice-command.service';
import { PosTtsService } from './services/pos-tts.service';
import { PosClientFavoritesService } from './services/pos-client-favorites.service';
import { PosThemeService } from './services/pos-theme.service';
import { PosIdleService } from './services/pos-idle.service';
import { PosDraftAutosaveService } from './services/pos-draft-autosave.service';
import { PosSessionSyncService } from './services/pos-session-sync.service';
import { PosHeaderComponent } from './components/pos-header/pos-header.component';
import { HeldOrdersPanelComponent } from './components/held-orders-panel/held-orders-panel.component';
import { ChangeCalculatorComponent } from './components/change-calculator/change-calculator.component';
import { PosHistoryPanelComponent } from './components/pos-history-panel/pos-history-panel.component';
import { formatLocalDate } from '@core/utils/date.util';
import { createClientUuid } from '@core/utils/safe-random-uuid.util';
import { PosReceiptComponent, PosReceiptModel } from './components/pos-receipt/pos-receipt.component';
import { CompanyService } from '@core/services/company.service';
import { ProductCatalogComponent } from './components/product-catalog/product-catalog.component';
import { OrderPanelComponent } from './components/order-panel/order-panel.component';
import { ProductListItem } from '@core/services/product.service';
import { InvoiceService } from '@core/services/invoice.service';
import { PrintPreviewService } from '@core/services/print-preview.service';
import { ApiResponse } from '@core/services/auth.service';
import { InvoiceWizardService } from '../invoices/invoice-wizard/services/invoice-wizard.service';
import {
  InvoiceType,
  Currency,
  ClientTaxType,
  PaymentMethod,
  PAYMENT_METHOD_OPTIONS,
  AddressInfo,
  InvoiceLine as WizardInvoiceLine
} from '../invoices/invoice-wizard/models/invoice-wizard.models';
import { LinkedInvoiceRef } from '@core/services/invoice-reference-resolver.service';
import { environment } from '@environments/environment';
import { ConfirmationService } from '@core/services/confirmation.service';
import { WarehouseContextService } from '@core/services/warehouse-context.service';

@Component({
  selector: 'app-pos',
  standalone: true,
  imports: [CommonModule, FormsModule, PosHeaderComponent, ProductCatalogComponent, OrderPanelComponent, HeldOrdersPanelComponent, ChangeCalculatorComponent, PosHistoryPanelComponent, PosReceiptComponent],
  template: `
    <div class="pos-layout">
      <app-pos-header
        [heldCount]="heldService.heldOrderCount()"
        [isDualScreenOpen]="dualScreenService.isOpen()"
        [isQuickMode]="posState.isQuickMode()"
        (onNewOrder)="newOrder()"
        (onOpenHeld)="showHeldPanel = true"
        (onOpenHistory)="showHistoryPanel = true"
        (onDualScreenToggle)="toggleDualScreen()"
        (onQuickModeToggle)="posState.toggleQuickMode()" />

      <div class="pos-content pos-content--animated">
        <div class="pos-content__catalog">
          <app-product-catalog
            #catalog
            (onProductAdd)="addProductToOrder($event)"
            (onProductAddWithQuantity)="addProductWithQuantity($event)" />
        </div>

        <div class="pos-content__order">
          <app-order-panel
            [frequentProducts]="getFrequentProducts()"
            (onValidate)="validateAndPrint()"
            (onSaveDraft)="saveDraft()"
            (onSendEmail)="validateAndPrint()"
            (onHold)="holdOrder()"
            (onCancel)="newOrder()"
            (creditNoteInvoiceSelected)="onCreditNoteInvoiceSelected($event)"
            (onSuggestionAdd)="addProductToOrder($event)"
            (onFrequentProductAdd)="addProductToOrder($event)" />
        </div>
      </div>

      @if (showHeldPanel) {
        <app-held-orders-panel
          (onClose)="showHeldPanel = false"
          (onRecall)="showHeldPanel = false" />
      }

      @if (showHistoryPanel) {
        <app-pos-history-panel (onClose)="showHistoryPanel = false" />
      }

      @if (showChangeCalculator) {
        <app-change-calculator
          [totalToPay]="changeCalculatorTotal"
          [disableFinish]="isRecordingPayment"
          (onClose)="closeChangeCalculator()" />
      }

      @if (showBarcodeToast) {
        <div class="pos-toast" [class.pos-toast--success]="barcodeToastType === 'added'" [class.pos-toast--warning]="barcodeToastType === 'not_found'">
          <div class="pos-toast__content">
            <div class="pos-toast__icon">
              @if (barcodeToastType === 'added') {
                <i class="pi pi-check-circle"></i>
              } @else {
                <i class="pi pi-exclamation-triangle"></i>
              }
            </div>
            <div class="pos-toast__text">
              <span class="pos-toast__title">{{ barcodeToastMessage }}</span>
            </div>
          </div>
          <div class="pos-toast__progress"></div>
        </div>
      }

      <app-pos-receipt #receiptRef [receipt]="receiptModel" />

      @if (showSuccessToast) {
        <div class="pos-toast pos-toast--success">
          <div class="pos-toast__content">
            <div class="pos-toast__icon">
              <i class="pi pi-check-circle"></i>
            </div>
            <div class="pos-toast__text">
              <span class="pos-toast__title">Facture creee avec succes</span>
              <span class="pos-toast__subtitle">{{ successMessage }}</span>
            </div>
          </div>
          <div class="pos-toast__progress"></div>
        </div>
      }

      @if (idleService.locked()) {
        <div class="pos-idle-overlay">
          <div class="pos-idle-box">
            <i class="pi pi-lock"></i>
            <h2>Session verrouillee</h2>
            <p>Inactivite de {{ idleService.idleMinutes() }} min. Entrez le code pour reprendre.</p>
            <input
              type="password"
              inputmode="numeric"
              maxlength="4"
              [(ngModel)]="idleUnlockCode"
              (keydown.enter)="unlockIdle()"
              placeholder="Code"
              class="pos-idle-input"
              aria-label="Code de deverrouillage" />
            <button type="button" class="pos-idle-btn" (click)="unlockIdle()">Reprendre</button>
          </div>
        </div>
      }

      @if (showRestoreDraftModal) {
        <div class="pos-modal-overlay" (click)="showRestoreDraftModal = false">
          <div class="pos-modal pos-modal--draft" (click)="$event.stopPropagation()">
            <h3>Reprendre la commande ?</h3>
            <p>Une commande enregistree localement a ete trouvee. Voulez-vous la reprendre ?</p>
            <div class="pos-modal-actions">
              <button type="button" class="pos-modal-btn pos-modal-btn--primary" (click)="restoreDraft()">Reprendre</button>
              <button type="button" class="pos-modal-btn" (click)="dismissDraft(); showRestoreDraftModal = false">Nouvelle commande</button>
            </div>
          </div>
        </div>
      }

      @if (showRestoreCloudModal) {
        <div class="pos-modal-overlay" (click)="showRestoreCloudModal = false">
          <div class="pos-modal pos-modal--cloud" (click)="$event.stopPropagation()">
            <h3>Reprendre sur cet appareil ?</h3>
            <p>Une commande enregistree sur un autre poste est disponible.</p>
            <div class="pos-modal-actions">
              <button type="button" class="pos-modal-btn pos-modal-btn--primary" (click)="restoreCloudSession()">Reprendre</button>
              <button type="button" class="pos-modal-btn" (click)="dismissCloudSession(); showRestoreCloudModal = false">Ignorer</button>
            </div>
          </div>
        </div>
      }
    </div>
  `,
  styles: [`
    .pos-layout {
      display: flex;
      flex-direction: column;
      height: 100vh;
      overflow: hidden;
      background: var(--color-neutral-50);
    }

    .pos-content {
      display: flex;
      flex: 1;
      min-height: 0;
    }

    .pos-content--animated {
      animation: fadeInUp 400ms cubic-bezier(0.4, 0, 0.2, 1) forwards;
    }

    .pos-content__catalog {
      flex: 6;
      display: flex;
      flex-direction: column;
      min-width: 0;
      background: var(--color-neutral-50);
      background-image: radial-gradient(circle at 1px 1px, rgba(148, 163, 184, 0.12) 1px, transparent 0);
      background-size: 24px 24px;
      background-position: 0 0;
    }

    .pos-content__order {
      flex: 4;
      display: flex;
      flex-direction: column;
      min-width: 340px;
      max-width: 480px;
      min-height: 0;
      background: var(--color-white);
      box-shadow: -8px 0 24px -4px rgba(0, 0, 0, 0.08), -2px 0 6px -2px rgba(0, 0, 0, 0.04);
    }

    .pos-toast {
      position: fixed;
      top: 80px;
      right: 24px;
      z-index: 600;
      min-width: 360px;
      border-radius: var(--radius-xl);
      overflow: hidden;
      box-shadow: var(--shadow-2xl);
      border-left: 3px solid var(--color-success-500);
      animation: slideInToastRight 350ms cubic-bezier(0.34, 1.2, 0.64, 1) forwards,
                 slideOutToastRight 250ms ease-in 2700ms forwards;
    }

    .pos-toast__content {
      display: flex;
      align-items: center;
      gap: var(--spacing-4);
      padding: var(--spacing-5) var(--spacing-6);
      background: rgba(255, 255, 255, 0.98);
      backdrop-filter: blur(12px);
      -webkit-backdrop-filter: blur(12px);
      border: 1px solid var(--color-success-200);
      border-left: none;
    }

    .pos-toast__icon {
      display: flex;
      align-items: center;
      justify-content: center;
      width: 44px;
      height: 44px;
      border-radius: var(--radius-full);
      background: var(--color-success-50);
      color: var(--color-success-600);
      font-size: 1.35rem;
      flex-shrink: 0;
    }

    .pos-toast__text {
      display: flex;
      flex-direction: column;
      gap: 2px;
    }

    .pos-toast__title {
      font-size: var(--font-size-base);
      font-weight: var(--font-weight-semibold);
      color: var(--color-text-primary);
    }

    .pos-toast__subtitle {
      font-size: var(--font-size-sm);
      color: var(--color-text-tertiary);
    }

    .pos-toast__progress {
      height: 3px;
      background: var(--color-success-200);
      position: relative;
      overflow: hidden;
    }

    .pos-toast__progress::after {
      content: '';
      position: absolute;
      top: 0;
      left: 0;
      bottom: 0;
      width: 100%;
      background: var(--color-success-400);
      animation: posToastProgress 3s linear forwards;
    }

    .pos-toast--warning .pos-toast__content {
      border-color: var(--color-error-200, #fecaca);
    }

    .pos-toast--warning .pos-toast__icon {
      background: var(--color-error-50, #fef2f2);
      color: var(--color-error-600, #dc2626);
    }

    .pos-toast--warning .pos-toast__progress {
      background: var(--color-error-200, #fecaca);
    }

    @keyframes posToastProgress {
      from { transform: scaleX(1); transform-origin: left; }
      to { transform: scaleX(0); transform-origin: left; }
    }

    @keyframes slideInToastRight {
      from {
        opacity: 0;
        transform: translateX(32px);
      }
      to {
        opacity: 1;
        transform: translateX(0);
      }
    }

    @keyframes slideOutToastRight {
      from {
        opacity: 1;
        transform: translateX(0);
      }
      to {
        opacity: 0;
        transform: translateX(32px);
      }
    }

    @media (min-width: 1440px) {
      .pos-content__order {
        max-width: 520px;
      }
    }

    @media (max-width: 1280px) {
      .pos-content__order {
        max-width: 420px;
      }
    }

    @media (max-width: 1024px) {
      .pos-content {
        flex-direction: column;
      }

      .pos-content__catalog {
        flex: 1;
        background-image: none;
        border-bottom: 1px solid var(--color-border-subtle);
      }

      .pos-content__order {
        max-width: none;
        min-width: auto;
        max-height: 45vh;
        box-shadow: 0 -4px 24px -4px rgba(0, 0, 0, 0.08), 0 -2px 6px -2px rgba(0, 0, 0, 0.04);
        border-top: 4px solid var(--color-primary-500);
      }
    }

    @media (max-width: 768px) {
      .pos-content__order {
        max-height: 50vh;
        min-width: auto;
      }

      .pos-toast {
        right: 16px;
        left: 16px;
      }

      .pos-toast__content {
        width: 100%;
      }
    }

    .pos-idle-overlay {
      position: fixed;
      inset: 0;
      z-index: 1000;
      display: flex;
      align-items: center;
      justify-content: center;
      background: rgba(15, 23, 42, 0.85);
      backdrop-filter: blur(8px);
      -webkit-backdrop-filter: blur(8px);
    }

    .pos-idle-box {
      background: var(--color-white);
      border-radius: var(--radius-2xl);
      padding: var(--spacing-8);
      text-align: center;
      max-width: 360px;
      box-shadow: var(--shadow-2xl);
    }

    .pos-idle-box i {
      font-size: 3rem;
      color: var(--color-primary-500);
      margin-bottom: var(--spacing-4);
    }

    .pos-idle-box h2 {
      margin: 0 0 var(--spacing-2);
      font-size: var(--font-size-xl);
      color: var(--color-text-primary);
    }

    .pos-idle-box p {
      margin: 0 0 var(--spacing-5);
      font-size: var(--font-size-sm);
      color: var(--color-text-secondary);
    }

    .pos-idle-input {
      width: 120px;
      padding: var(--spacing-3) var(--spacing-4);
      font-size: 1.25rem;
      text-align: center;
      letter-spacing: 0.5em;
      border: 1px solid var(--color-border-default);
      border-radius: var(--radius-lg);
      margin-bottom: var(--spacing-4);
    }

    .pos-idle-btn {
      padding: var(--spacing-3) var(--spacing-6);
      background: var(--color-primary-500);
      color: var(--color-white);
      border: none;
      border-radius: var(--radius-lg);
      font-weight: var(--font-weight-semibold);
      cursor: pointer;
    }

    .pos-modal-overlay {
      position: fixed;
      inset: 0;
      z-index: 900;
      display: flex;
      align-items: center;
      justify-content: center;
      background: rgba(15, 23, 42, 0.4);
      backdrop-filter: blur(4px);
    }

    .pos-modal {
      background: var(--color-white);
      border-radius: var(--radius-2xl);
      padding: var(--spacing-6);
      max-width: 400px;
      box-shadow: var(--shadow-2xl);
    }

    .pos-modal h3 {
      margin: 0 0 var(--spacing-2);
      font-size: var(--font-size-lg);
      color: var(--color-text-primary);
    }

    .pos-modal p {
      margin: 0 0 var(--spacing-5);
      font-size: var(--font-size-sm);
      color: var(--color-text-secondary);
    }

    .pos-modal-actions {
      display: flex;
      gap: var(--spacing-3);
      justify-content: flex-end;
    }

    .pos-modal-btn {
      padding: var(--spacing-2) var(--spacing-5);
      border-radius: var(--radius-lg);
      font-weight: var(--font-weight-medium);
      cursor: pointer;
      border: 1px solid var(--color-border-default);
      background: var(--color-white);
      color: var(--color-text-secondary);
    }

    .pos-modal-btn--primary {
      background: var(--color-primary-500);
      color: var(--color-white);
      border-color: var(--color-primary-500);
    }
  `]
})
export class PosComponent implements OnInit, OnDestroy {
  @ViewChild('catalog') catalogComponent!: ProductCatalogComponent;
  @ViewChild('receiptRef') receiptRef!: PosReceiptComponent;

  readonly posState = inject(PosStateService);
  private readonly barcodeService = inject(PosBarcodeService);
  readonly heldService = inject(PosHeldOrdersService);
  readonly dualScreenService = inject(PosDualScreenService);
  private readonly posStockService = inject(PosStockService);
  private readonly upsellService = inject(PosUpsellService);
  private readonly audioService = inject(PosAudioService);
  private readonly invoiceService = inject(InvoiceService);
  private readonly printPreviewService = inject(PrintPreviewService);
  private readonly wizardService = inject(InvoiceWizardService);
  private readonly http = inject(HttpClient);
  private readonly WIZARD_API_URL = `${environment.apiUrl}/invoices/wizard`;
  private readonly voiceCommandService = inject(PosVoiceCommandService);
  readonly ttsService = inject(PosTtsService);
  private readonly clientFavoritesService = inject(PosClientFavoritesService);
  readonly themeService = inject(PosThemeService);
  readonly idleService = inject(PosIdleService);
  private readonly draftAutosaveService = inject(PosDraftAutosaveService);
  private readonly sessionSyncService = inject(PosSessionSyncService);
  private readonly confirmationService = inject(ConfirmationService);
  private readonly companyService = inject(CompanyService);
  private readonly warehouseContext = inject(WarehouseContextService);

  showSuccessToast = false;
  showRestoreDraftModal = false;
  showRestoreCloudModal = false;
  idleUnlockCode = '';
  successMessage = '';
  showBarcodeToast = false;
  barcodeToastMessage = '';
  barcodeToastType: 'added' | 'not_found' | 'multiple' = 'added';
  showHeldPanel = false;
  showHistoryPanel = false;
  showChangeCalculator = false;
  changeCalculatorTotal = 0;
  receiptModel: PosReceiptModel | null = null;
  private cachedInvoiceFooter = '';
  private cachedCompanyReceipt: {
    name: string;
    addressLine: string;
    email: string;
    logoUrl: string | null;
  } | null = null;
  private toastTimeout?: ReturnType<typeof setTimeout>;
  private barcodeToastTimeout?: ReturnType<typeof setTimeout>;
  private sellerLoaded = false;
  /** ID de la facture créée en espèces, pour enregistrer le paiement au clic Terminer. */
  lastCreatedInvoiceIdForCash: string | null = null;
  /** Empêche double envoi lors de l'enregistrement du paiement. */
  isRecordingPayment = false;

    constructor() {
    effect(() => {
      const result = this.barcodeService.lastScanResult();
      if (!result) return;
      this.barcodeToastType = result;
      if (result === 'added') {
        this.barcodeToastMessage = 'Produit ajoute par scan';
        this.audioService.beepScan();
      } else if (result === 'not_found') {
        this.audioService.beepError();
        this.barcodeToastMessage = 'Produit non trouve';
      } else {
        this.barcodeToastMessage = 'Plusieurs produits correspondent - selectionnez dans le catalogue';
        const code = this.barcodeService.lastScannedCode();
        if (code) {
          setTimeout(() => this.catalogComponent?.searchWithCode(code), 0);
        }
      }
      this.showBarcodeToast = true;
      if (this.barcodeToastTimeout) clearTimeout(this.barcodeToastTimeout);
      this.barcodeToastTimeout = setTimeout(() => {
        this.showBarcodeToast = false;
      }, 2500);
    });
  }

  private voiceCommandSub = this.voiceCommandService.onCommand.subscribe(cmd => {
    if (cmd === 'undo_last_line') {
      this.posState.removeLastLine();
      this.audioService.beepSuccess();
    }
  });

  ngOnInit(): void {
    this.wizardService.ensureFiscalStampLoaded();
    this.loadSeller();
    this.companyService.getCompany().subscribe(res => {
      if (!res.success || !res.data) return;
      const c = res.data;
      const addr = c.address;
      const addressLine =
        addr.fullAddress?.trim() ||
        [addr.street, addr.streetLine2, addr.postalCode, addr.city, addr.governorate]
          .filter((x): x is string => !!x?.trim())
          .join(', ');
      this.cachedCompanyReceipt = {
        name: c.companyName,
        addressLine,
        email: c.email?.trim() ?? '',
        logoUrl: c.logoUrl?.trim() || null
      };
      const footer = c.invoiceFooter?.trim();
      if (footer) this.cachedInvoiceFooter = footer;
    });
    this.barcodeService.startListening();
    this.themeService.init();
    this.idleService.startWatching();
    this.draftAutosaveService.start();
    this.draftAutosaveService.checkStored();
    if (this.draftAutosaveService.hasStoredDraft()) {
      this.showRestoreDraftModal = true;
    } else {
      this.sessionSyncService.hasCloudSession().subscribe(has => {
        if (has) this.showRestoreCloudModal = true;
      });
    }
  }

  @HostListener('window:keydown', ['$event'])
  handleKeyboardShortcuts(event: KeyboardEvent): void {
    this.idleService.touch();
    if (event.target instanceof HTMLInputElement || event.target instanceof HTMLTextAreaElement) {
      if (event.key !== 'Escape') return;
    }

    switch (event.key) {
      case 'F2':
        event.preventDefault();
        this.catalogComponent?.focusSearch();
        break;
      case 'F5':
        event.preventDefault();
        this.validateAndPrint();
        break;
      case 'F4':
        event.preventDefault();
        this.posState.toggleQuickMode();
        break;
      case 'F6':
        event.preventDefault();
        this.ttsService.speakTotal();
        break;
      case 'F9':
        event.preventDefault();
        this.newOrder();
        break;
      case 'Escape':
        event.preventDefault();
        break;
    }
  }

  @HostListener('document:click')
  onDocumentClick(): void {
    this.idleService.touch();
  }

  unlockIdle(): void {
    const code = '1234';
    if (this.idleUnlockCode === code) {
      this.idleService.unlock();
      this.idleUnlockCode = '';
    }
  }

  restoreDraft(): void {
    this.draftAutosaveService.restore();
    this.showRestoreDraftModal = false;
  }

  dismissDraft(): void {
    this.draftAutosaveService.clear();
  }

  restoreCloudSession(): void {
    this.sessionSyncService.getFromCloud().subscribe(state => {
      if (state && state.lines?.length) {
        this.posState.restoreSnapshot(state);
        this.sessionSyncService.clearCloudSession();
      }
      this.showRestoreCloudModal = false;
    });
  }

  dismissCloudSession(): void {
    this.sessionSyncService.clearCloudSession();
    this.showRestoreCloudModal = false;
  }

  getFrequentProducts(): ProductListItem[] {
    const clientId = this.posState.client()?.id ?? null;
    const cache = this.catalogComponent?.productCache() ?? new Map();
    return this.clientFavoritesService.getFrequentProducts(clientId, cache);
  }

  generateProforma(): void {
    if (!this.posState.canValidate()) return;
    this.posState.setProcessing(true);
    this.posState.setError(null);
    this.prepareWizardState();
    this.wizardService.saveDraft().subscribe({
      next: draftId => {
        const url = `${this.WIZARD_API_URL}/drafts/${draftId}/pdf`;
        this.http.get(url, { responseType: 'blob' }).pipe(
          catchError(() => throwError(() => new Error('Proforma non disponible')))
        ).subscribe({
          next: blob => {
            this.posState.setProcessing(false);
            const u = URL.createObjectURL(blob);
            const a = document.createElement('a');
            a.href = u;
            a.download = `proforma-${draftId}.pdf`;
            a.click();
            URL.revokeObjectURL(u);
            this.showToast('Proforma telechargee');
          },
          error: () => {
            this.posState.setProcessing(false);
            this.showToast('Brouillon enregistre - ouvrez le brouillon pour imprimer');
          }
        });
      },
      error: err => {
        this.posState.setProcessing(false);
        this.showToast(this.extractErrorMessage(err, 'Erreur proforma'));
      }
    });
  }

  ngOnDestroy(): void {
    if (this.toastTimeout) clearTimeout(this.toastTimeout);
    if (this.barcodeToastTimeout) clearTimeout(this.barcodeToastTimeout);
    this.barcodeService.stopListening();
    this.voiceCommandSub?.unsubscribe();
    this.idleService.stopWatching();
    this.draftAutosaveService.stop();
  }

  addProductToOrder(product: ProductListItem): void {
    const orderIds = this.posState.lines().map(l => l.productId);
    this.upsellService.computeSuggestions(product, orderIds, this.catalogComponent?.productCache() ?? new Map());
    this.posState.addProduct(product);
    this.audioService.beepSuccess();
  }

  addProductWithQuantity(event: { product: ProductListItem; quantity: number }): void {
    const orderIds = this.posState.lines().map(l => l.productId);
    this.upsellService.computeSuggestions(event.product, orderIds, this.catalogComponent?.productCache() ?? new Map());
    this.posState.addProductWithQuantity(event.product, event.quantity);
    this.audioService.beepSuccess();
  }

  newOrder(): void {
    if (this.posState.isDirty() && this.posState.lines().length > 0) {
      this.confirmationService.confirm({
        header: 'Abandonner la commande ?',
        message: 'La commande en cours sera perdue. Voulez-vous continuer ?',
        icon: 'pi pi-exclamation-triangle',
        acceptLabel: 'Abandonner',
        rejectLabel: 'Annuler',
        accept: () => {
          this.upsellService.clearSuggestions();
          this.posState.resetOrder();
        }
      });
      return;
    }
    this.upsellService.clearSuggestions();
    this.posState.resetOrder();
  }

  holdOrder(): void {
    this.heldService.holdCurrentOrder().then(() => {
      this.showHeldPanel = true;
    });
  }

  toggleDualScreen(): void {
    if (this.dualScreenService.isOpen()) {
      this.dualScreenService.close();
    } else {
      this.dualScreenService.open();
      this.posState.broadcastDisplayState();
    }
  }

  /**
   * Uses the draft+submit pipeline exclusively:
   * 1. saveDraft() -> POST /api/invoices/wizard/drafts -> draftId
   * 2. POST /api/invoices/wizard/drafts/{draftId}/submit -> invoiceId
   *
   * The direct POST /api/invoices endpoint fails with an EF Core
   * InvalidOperationException (misreported as "Erreur de contexte")
   * because CreateInvoiceCommand loads entities across separate
   * TenantDbContext instances. The draft+submit pipeline uses
   * SubmitInvoiceCommand which loads everything in a single UnitOfWork.
   */
  validateAndPrint(): void {
    if (!this.posState.canValidate()) return;

    if (this.posState.isDemoMode()) {
      this.showToast('Mode demo : facture simulee (aucune ecriture)');
      this.audioService.beepSuccess();
      return;
    }

    if (!this.sellerLoaded) {
      this.posState.setError('Chargement des informations entreprise en cours. Veuillez reessayer.');
      this.audioService.beepError();
      this.loadSeller();
      return;
    }

    this.posState.setProcessing(true);
    this.posState.setError(null);

    this.posStockService.checkOrderStock().subscribe(check => {
      if (check && !check.allAvailable && check.details?.length) {
        const msg = check.details
          .filter(d => !d.isAvailable)
          .map(d => `${d.productName}: demande ${d.requestedQuantity}, disponible ${d.availableQuantity}`)
          .join('\n');
        this.confirmationService.confirm({
          header: 'Stock insuffisant',
          message: `Stock insuffisant pour certains produits:\n\n${msg}\n\nVoulez-vous forcer la validation ?`,
          icon: 'pi pi-exclamation-triangle',
          acceptLabel: 'Forcer la validation',
          rejectLabel: 'Annuler',
          accept: () => {
            if (this.posState.isCreditNote()) {
              this.runCreditNoteValidationPipeline();
            } else {
              this.doValidateAndPrint(false);
            }
          },
          reject: () => this.posState.setProcessing(false)
        });
        return;
      }
      if (this.posState.isCreditNote()) {
        this.runCreditNoteValidationPipeline();
      } else {
        this.doValidateAndPrint(false);
      }
    });
  }

  onCreditNoteInvoiceSelected(linkedInvoice: LinkedInvoiceRef): void {
    const activate = () => this.activateCreditNoteMode(linkedInvoice);

    if (this.posState.lines().length > 0) {
      this.confirmationService.confirm({
        header: 'Remplacer le panier ?',
        message: 'Le panier actuel sera remplacé par les lignes de la facture sélectionnée.',
        icon: 'pi pi-exclamation-triangle',
        acceptLabel: 'Continuer',
        rejectLabel: 'Annuler',
        accept: activate
      });
      return;
    }

    activate();
  }

  private activateCreditNoteMode(linkedInvoice: LinkedInvoiceRef): void {
    this.posState.enableCreditNoteMode(linkedInvoice);
    this.posState.setProcessing(true);
    this.posState.setError(null);

    this.wizardService.initForCreditNote(linkedInvoice.id).subscribe({
      next: () => {
        this.populatePosCartFromWizardLines();
        this.posState.setProcessing(false);
        this.showToast('Panier pré-rempli — modifiez les quantités pour un avoir partiel');
      },
      error: err => {
        this.posState.setProcessing(false);
        this.posState.disableCreditNoteMode();
        const message = this.extractErrorMessage(err, 'Impossible de charger la facture liée.');
        this.posState.setError(message);
        this.audioService.beepError();
      }
    });
  }

  private runCreditNoteValidationPipeline(): void {
    const linked = this.posState.linkedInvoice();
    if (!linked?.id) {
      this.posState.setProcessing(false);
      this.posState.setError('Facture liée invalide. Veuillez sélectionner une facture.');
      this.audioService.beepError();
      return;
    }

    if (this.posState.lines().length === 0) {
      this.posState.setProcessing(false);
      this.posState.setError('Ajoutez au moins une ligne à rembourser.');
      this.audioService.beepError();
      return;
    }

    this.wizardService.initForCreditNote(linked.id).pipe(
      tap(() => this.prepareCreditNoteWizardState()),
      switchMap(() => this.wizardService.saveDraft()),
      switchMap(draftId => {
        const idempotencyKey = `pos-${Date.now()}-${createClientUuid()}`.slice(0, 64);
        return this.http.post<
          ApiResponse<{ invoiceId: string; invoiceNumber: string }>
        >(`${this.WIZARD_API_URL}/drafts/${draftId}/submit`, { idempotencyKey }).pipe(
          map(res => {
            const data = res?.data as Record<string, unknown> | undefined;
            const rawId = data?.['invoiceId'] ?? data?.['InvoiceId'];
            const rawNum = data?.['invoiceNumber'] ?? data?.['InvoiceNumber'];
            const invoiceId = typeof rawId === 'string' ? rawId : String(rawId ?? '');
            const invoiceNumber = typeof rawNum === 'string' ? rawNum : String(rawNum ?? '');
            return { invoiceId, invoiceNumber };
          })
        );
      }),
      catchError(err => {
        this.posState.setProcessing(false);
        const message = this.extractErrorMessage(err, 'Erreur lors de la création de l\'avoir');
        this.posState.setError(message);
        this.audioService.beepError();
        return throwError(() => err);
      })
    ).subscribe({
      next: payload => this.onInvoiceCreated(payload),
      error: () => {}
    });
  }

  private populatePosCartFromWizardLines(): void {
    const posLines = this.mapWizardLinesToPosLines(this.wizardService.lines());
    this.posState.setLines(posLines);
  }

  private mapWizardLinesToPosLines(lines: WizardInvoiceLine[]): PosOrderLine[] {
    return lines.map(line => ({
      id: createClientUuid(),
      productId: line.productId ?? '',
      productCode: '',
      designation: line.designation,
      description: line.description,
      quantity: line.quantity,
      unit: line.unit ?? '',
      unitPriceHT: line.unitPriceHT,
      vatRate: line.vatRate,
      isFodecApplicable: line.isFodecApplicable ?? false,
      fodecAmount: line.fodecAmount,
      totalHT: line.totalHT,
      vatAmount: line.vatAmount,
      totalTTC: line.totalTTC,
      discountType: line.discountType,
      discountValue: line.discountValue,
      discountAmount: line.discountAmount,
      notes: ''
    }));
  }

  private prepareCreditNoteWizardState(): void {
    const linked = this.posState.linkedInvoice();
    if (!linked?.id) {
      return;
    }

    if (!this.wizardService.seller() && this.sellerLoaded) {
      this.loadSeller();
    }

    this.wizardService.updateMetadata({
      type: InvoiceType.CreditNote,
      linkedInvoiceId: linked.id,
      issueDate: new Date(),
      dueDate: new Date(),
      currency: Currency.TND,
      internalReference: `POS-AVO-${this.posState.sessionId()}`
    });

    const posClient = this.posState.client();
    if (posClient && !posClient.isWalkIn) {
      this.wizardService.selectClient({
        id: posClient.id,
        isNewClient: false,
        name: posClient.name,
        taxType: posClient.nif ? ClientTaxType.TaxSubject : ClientTaxType.NonTaxSubject,
        address: {
          street: '',
          streetLine2: null,
          postalCode: null,
          city: '',
          governorate: '',
          country: 'Tunisie'
        },
        nif: posClient.nif || null,
        email: posClient.email,
        phone: posClient.phone || null,
        contactPerson: null
      });
    }

    for (const line of [...this.wizardService.lines()]) {
      this.wizardService.removeLine(line.id);
    }

    const posLines = this.posState.lines();
    const totals = this.posState.totals();
    const subTotalHT = totals.subTotalHT;
    const globalDiscount = totals.totalDiscount;

    posLines.forEach(line => {
      let discountType = line.discountType;
      let discountValue = line.discountValue;
      if (globalDiscount > 0 && subTotalHT > 0) {
        const lineShare = (line.totalHT / subTotalHT) * globalDiscount;
        const effectiveDiscount = line.discountAmount + lineShare;
        discountType = 'AMOUNT';
        discountValue = effectiveDiscount;
      }
      this.wizardService.addLine({
        productId: line.productId,
        designation: line.designation,
        description: (line.description?.trim()) || null,
        quantity: line.quantity,
        unit: line.unit,
        unitPriceHT: line.unitPriceHT,
        vatRate: line.vatRate,
        isFodecApplicable: line.isFodecApplicable ?? false,
        discountType: discountType ?? null,
        discountValue: discountValue ?? null
      });
    });

    let paymentMethod = this.posState.paymentMethod();
    let paymentTerms = 'Remboursement comptant';
    if (this.posState.isSplitPayment() && this.posState.paymentSplits().length > 0) {
      paymentMethod = this.posState.paymentSplits()[0].method;
      const parts = this.posState.paymentSplits().map(s =>
        `${s.method}: ${this.posState.formatAmount(s.amount)} TND`
      );
      paymentTerms = `Remboursement fractionné : ${parts.join(', ')}`;
    }

    this.wizardService.updatePayment({
      method: paymentMethod,
      terms: paymentTerms,
      daysUntilDue: 0
    });
  }

  private doValidateAndPrint(skipPrepare = false): void {
    if (!skipPrepare) {
      this.prepareWizardState();
    }

    this.wizardService.saveDraft().pipe(
      switchMap(draftId => {
        const idempotencyKey = `pos-${Date.now()}-${createClientUuid()}`.slice(0, 64);
        return this.http.post<
          ApiResponse<{ invoiceId: string; invoiceNumber: string }>
        >(`${this.WIZARD_API_URL}/drafts/${draftId}/submit`, { idempotencyKey }).pipe(
          map(res => {
            const data = res?.data as Record<string, unknown> | undefined;
            const rawId = data?.['invoiceId'] ?? data?.['InvoiceId'];
            const rawNum = data?.['invoiceNumber'] ?? data?.['InvoiceNumber'];
            const invoiceId = typeof rawId === 'string' ? rawId : String(rawId ?? '');
            const invoiceNumber = typeof rawNum === 'string' ? rawNum : String(rawNum ?? '');
            return { invoiceId, invoiceNumber };
          })
        );
      }),
      catchError(err => {
        this.posState.setProcessing(false);
        const message = this.extractErrorMessage(err, 'Erreur lors de la creation de la facture');
        this.posState.setError(message);
        this.audioService.beepError();
        return throwError(() => err);
      })
    ).subscribe({
      next: payload => this.onInvoiceCreated(payload),
      error: () => {}
    });
  }

  saveDraft(): void {
    if (!this.posState.canSaveDraft()) return;
    this.posState.setProcessing(true);
    this.posState.setError(null);

    this.prepareWizardState();

    this.wizardService.saveDraft().subscribe({
      next: () => {
        this.posState.setProcessing(false);
        this.showToast('Brouillon sauvegarde');
      },
      error: err => {
        this.posState.setProcessing(false);
        const message = this.extractErrorMessage(err, 'Erreur lors de la sauvegarde');
        this.posState.setError(message);
        this.audioService.beepError();
      }
    });
  }

  private loadSeller(): void {
    this.wizardService.loadSellers().subscribe(sellers => {
      if (sellers.length > 0) {
        this.wizardService.selectSeller(sellers[0]);
        this.sellerLoaded = true;
      }
    });
  }

  private prepareWizardState(): void {
    const lines = this.posState.lines();
    const client = this.posState.client();
    let paymentMethod = this.posState.paymentMethod();
    let paymentTerms = 'Paiement comptant';

    if (this.posState.isSplitPayment() && this.posState.paymentSplits().length > 0) {
      // La ventilation n'est plus concaténée dans PaymentTerms : chaque mode donne lieu à
      // sa propre ligne Payment, enregistrée via recordPayments (cf. recordSplitPayments).
      // Le mode retenu ici n'est qu'un libellé d'en-tête de facture.
      paymentMethod = this.posState.paymentSplits()[0].method;
      paymentTerms = 'Paiement fractionne';
    }
    const orderNotes = this.posState.orderNotes()?.trim();
    if (orderNotes) {
      paymentTerms = paymentTerms ? `${paymentTerms} | ${orderNotes}` : orderNotes;
    }

    const schedule = this.posState.paymentSchedule();
    const dueDate = new Date();
    if (schedule === '2x' || schedule === '3x') {
      const months = schedule === '2x' ? 1 : 2;
      dueDate.setMonth(dueDate.getMonth() + months);
      paymentTerms = paymentTerms
        ? `${paymentTerms} | Paiement en ${schedule} sans frais. 2e echeance: ${dueDate.toLocaleDateString('fr-FR')}`
        : `Paiement en ${schedule} sans frais. 2e echeance: ${dueDate.toLocaleDateString('fr-FR')}`;
    }

    const currentSeller = this.wizardService.seller();

    this.wizardService.reset();

    if (currentSeller) {
      this.wizardService.selectSeller(currentSeller);
    }

    this.wizardService.updateMetadata({
      type: InvoiceType.Invoice,
      issueDate: new Date(),
      dueDate,
      currency: Currency.TND,
      internalReference: `POS-${this.posState.sessionId()}`,
      warehouseId: this.warehouseContext.selectedWarehouseId()
    });

    if (client) {
      this.wizardService.selectClient({
        id: client.id,
        isNewClient: false,
        name: client.name,
        taxType: client.nif ? ClientTaxType.TaxSubject : ClientTaxType.NonTaxSubject,
        address: {
          street: '',
          streetLine2: null,
          postalCode: null,
          city: '',
          governorate: '',
          country: 'Tunisie'
        },
        nif: client.nif || null,
        email: client.email,
        phone: client.phone || null,
        contactPerson: null
      });
    } else {
      this.wizardService.selectClient({
        id: null,
        isNewClient: true,
        name: 'Client passager',
        taxType: ClientTaxType.NonTaxSubject,
        address: {
          street: 'Non spécifié',
          streetLine2: null,
          postalCode: null,
          city: 'Non spécifié',
          governorate: 'Non spécifié',
          country: 'Tunisie'
        },
        nif: null,
        email: 'passager@factutrust.local',
        phone: null,
        contactPerson: null
      });
    }

    const totals = this.posState.totals();
    const subTotalHT = totals.subTotalHT;
    const globalDiscount = totals.totalDiscount;

    lines.forEach(line => {
      let discountType = line.discountType;
      let discountValue = line.discountValue;
      if (globalDiscount > 0 && subTotalHT > 0) {
        const lineShare = (line.totalHT / subTotalHT) * globalDiscount;
        const effectiveDiscount = line.discountAmount + lineShare;
        discountType = 'AMOUNT';
        discountValue = effectiveDiscount;
      }
      const description = (line.description?.trim()) || null;
      this.wizardService.addLine({
        productId: line.productId,
        designation: line.designation,
        description,
        quantity: line.quantity,
        unit: line.unit,
        unitPriceHT: line.unitPriceHT,
        vatRate: line.vatRate,
        isFodecApplicable: line.isFodecApplicable ?? false,
        discountType: discountType ?? null,
        discountValue: discountValue ?? null
      });
    });

    this.wizardService.updatePayment({
      method: paymentMethod,
      terms: paymentTerms,
      daysUntilDue: 0
    });
  }

  private onInvoiceCreated(payload: { invoiceId: string; invoiceNumber: string }): void {
    const soldProductIds = [...new Set(this.posState.lines().map(l => l.productId))];
    const { invoiceId, invoiceNumber } = payload;
    // Capturé AVANT resetOrder(), qui vide la ventilation.
    const splits = this.posState.isSplitPayment() ? [...this.posState.paymentSplits()] : [];
    const client = this.posState.client();
    if (client && !client.isWalkIn) {
      const productIds = this.posState.lines().map(l => l.productId);
      this.clientFavoritesService.recordOrder(client.id, productIds);
    }
    this.upsellService.clearSuggestions();
    this.posState.setProcessing(false);
    const totalTTC = this.posState.totals().totalTTC;
    const paymentMethod = this.posState.paymentMethod();
    const printMode = this.posState.printMode();

    if (printMode === 'receipt' || printMode === 'both') {
      this.receiptModel = this.buildPosReceiptModel(invoiceId, invoiceNumber);
      setTimeout(() => this.receiptRef?.print(), 50);
    }

    if (printMode === 'pdf' || printMode === 'both') {
      this.downloadPdf(invoiceId);
    }

    this.posState.resetOrder();

    this.posStockService.invalidateOrderStockCache();
    this.posStockService.loadCatalogAlerts();
    this.catalogComponent?.refreshStockAfterSale(soldProductIds);

    if (splits.length > 0) {
      // Encaissement fractionné : une ligne Payment par mode, en une seule transaction.
      this.recordSplitPayments(invoiceId, splits);
      return;
    }

    if (paymentMethod === PaymentMethod.Cash) {
      this.lastCreatedInvoiceIdForCash = invoiceId;
      this.invoiceService.getInvoice(invoiceId).subscribe({
        next: res => {
          const fromServer = res.success && res.data?.remainingAmount != null
            ? Number(res.data.remainingAmount)
            : null;
          this.changeCalculatorTotal =
            fromServer != null && fromServer > 0 ? fromServer : totalTTC;
          this.showChangeCalculator = true;
        },
        error: () => {
          this.changeCalculatorTotal = totalTTC;
          this.showChangeCalculator = true;
        }
      });
    } else {
      const label = invoiceNumber?.trim() || invoiceId.substring(0, 8);
      this.showToast(`Facture N° ${label}`);
    }
  }

  private buildPosReceiptModel(invoiceId: string, invoiceNumber: string): PosReceiptModel {
    const seller = this.wizardService.seller();
    const fallback = this.cachedCompanyReceipt;
    const companyName = seller?.companyName?.trim() || fallback?.name || environment.appName;
    const companyAddressLine = seller
      ? this.formatSellerAddress(seller.address)
      : fallback?.addressLine || '';
    const companyEmail = (seller?.email?.trim() || fallback?.email?.trim() || '').trim();
    const logoUrl = seller?.logo?.trim() || fallback?.logoUrl || null;

    const posClient = this.posState.client();
    const clientName = posClient?.name?.trim() || 'Client passager';
    const clientIdDisplay = posClient?.id
      ? `#${posClient.id.replace(/-/g, '').slice(0, 8).toUpperCase()}`
      : '#PASSAGER';

    const num = invoiceNumber?.trim();
    const displayInvoiceNumber = num || invoiceId.replace(/-/g, '').slice(0, 12).toUpperCase();

    const totals = this.posState.totals();
    const lines = this.posState.lines();
    const timbreFiscal = this.wizardService.getFiscalStampSignedAmountForCreditNote(
      this.posState.isCreditNote());
    const grandTotal = totals.totalTTC + timbreFiscal;

    const isCredit = this.posState.isCreditNote();
    const documentBanner = isCredit
      ? '---------- AVOIR (DÉTAIL) ----------'
      : '---------- REÇU DE DÉTAIL ----------';

    const remise = totals.totalDiscount + totals.firstPurchaseDiscount;
    const footerLegal =
      this.cachedInvoiceFooter.trim() || 'Merci pour votre confiance.';

    return {
      companyName,
      companyAddressLine,
      companyEmail,
      logoUrl,
      documentBanner,
      clientName,
      clientIdDisplay,
      invoiceNumber: displayInvoiceNumber,
      issuedAt: new Date(),
      lines: lines.map(l => {
        const q = l.quantity > 0 ? l.quantity : 1;
        return {
          designation: l.designation,
          quantity: l.quantity,
          unitPriceTTC: l.totalTTC / q,
          lineTotalTTC: l.totalTTC
        };
      }),
      subTotalHT: totals.subTotalHT,
      remise,
      baseTVA: totals.totalHT,
      totalTVA: totals.totalVat,
      vatDetails: totals.vatBreakdown.map(v => ({
        rateDisplay: v.rateDisplay,
        vatAmount: v.vatAmount
      })),
      timbreFiscal,
      totalTTC: totals.totalTTC,
      grandTotal,
      paymentLabel: this.getReceiptPaymentLabel(),
      footerLegal,
      poweredByLabel: `Powered by ${environment.appName}`
    };
  }

  private formatSellerAddress(addr: AddressInfo): string {
    const parts = [
      addr.street,
      addr.streetLine2,
      addr.postalCode,
      addr.city,
      addr.governorate,
      addr.country?.trim() && addr.country.trim() !== 'Tunisie' ? addr.country.trim() : null
    ].filter((p): p is string => !!p?.trim());
    return parts.join(', ');
  }

  private getReceiptPaymentLabel(): string {
    if (this.posState.isSplitPayment() && this.posState.paymentSplits().length > 0) {
      return this.posState
        .paymentSplits()
        .map(s => {
          const label =
            PAYMENT_METHOD_OPTIONS.find(o => o.value === s.method)?.label ?? String(s.method);
          return `${label}: ${this.posState.formatAmount(s.amount)} TND`;
        })
        .join(' | ');
    }
    const m = this.posState.paymentMethod();
    return PAYMENT_METHOD_OPTIONS.find(o => o.value === m)?.label ?? String(m);
  }

  closeChangeCalculator(): void {
    if (this.isRecordingPayment) return;

    if (this.lastCreatedInvoiceIdForCash) {
      this.isRecordingPayment = true;
      const invoiceId = this.lastCreatedInvoiceIdForCash;
      this.invoiceService.recordPayment(invoiceId, {
        paymentDate: formatLocalDate(new Date()),
        method: 0
      }).subscribe({
        next: () => {
          this.lastCreatedInvoiceIdForCash = null;
          this.showChangeCalculator = false;
          this.changeCalculatorTotal = 0;
          this.isRecordingPayment = false;
          this.showToast('Paiement enregistré');
        },
        error: err => {
          this.isRecordingPayment = false;
          const message = this.extractErrorMessage(err, "Erreur lors de l'enregistrement du paiement");
          this.posState.setError(message);
          this.audioService.beepError();
        }
      });
      return;
    }

    this.showChangeCalculator = false;
    this.changeCalculatorTotal = 0;
    this.showToast('Transaction terminee');
  }

  /**
   * Enregistre la ventilation d'un encaissement fractionné : un règlement par mode, dans une
   * seule transaction serveur. Auparavant seul le premier mode survivait, le reste finissant
   * concaténé dans le champ texte des conditions de règlement — ce qui rendait le
   * rapprochement bancaire et le comptage de caisse invérifiables.
   */
  private recordSplitPayments(invoiceId: string, splits: PaymentSplit[]): void {
    const paymentDate = formatLocalDate(new Date());
    const requests = splits.map(split => ({
      paymentDate,
      method: Number(split.method),
      amount: split.amount
    }));

    this.isRecordingPayment = true;
    this.invoiceService.recordPayments(invoiceId, requests).subscribe({
      next: () => {
        this.isRecordingPayment = false;
        this.showToast(`Encaissement fractionné enregistré (${splits.length} règlements)`);
      },
      error: err => {
        this.isRecordingPayment = false;
        const message = this.extractErrorMessage(
          err, "Erreur lors de l'enregistrement de l'encaissement fractionné");
        this.posState.setError(message);
        this.audioService.beepError();
      }
    });
  }

  private downloadPdf(invoiceId: string): void {
    this.invoiceService.downloadPdf(invoiceId).subscribe({
      next: blob => this.printPreviewService.openPdfForPrintPreview(blob, `facture-${invoiceId}.pdf`),
      error: () => {}
    });
  }

  private showToast(message: string): void {
    this.successMessage = message;
    this.showSuccessToast = true;
    if (this.toastTimeout) clearTimeout(this.toastTimeout);
    this.toastTimeout = setTimeout(() => {
      this.showSuccessToast = false;
    }, 3000);
  }

  private extractErrorMessage(err: any, fallback: string): string {
    if (err?.error?.error) return err.error.error;
    if (err?.error?.message) return err.error.message;
    if (err?.error?.errors?.length) return err.error.errors[0];
    if (err?.error?.globalErrors?.length) return err.error.globalErrors[0];
    if (err?.message) return err.message;
    return fallback;
  }
}
