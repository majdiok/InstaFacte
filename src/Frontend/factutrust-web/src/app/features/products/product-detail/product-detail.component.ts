import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterModule, ActivatedRoute } from '@angular/router';
import { SkeletonModule } from 'primeng/skeleton';
import { TagModule } from 'primeng/tag';
import { ToastModule } from 'primeng/toast';
import { ToastService } from '@core/services/toast.service';
import { PageHeaderComponent } from '@shared/components/page-header/page-header.component';
import { BreadcrumbComponent, BreadcrumbItem } from '@shared/components/breadcrumb/breadcrumb.component';
import { FormSectionComponent } from '@shared/components/form-section/form-section.component';
import { ButtonComponent } from '@shared/components/button/button.component';
import { ProductService, Product } from '@core/services/product.service';
import { SupplierService } from '@core/services/supplier.service';
import { ErrorHandlerService } from '@core/services/error-handler.service';
import { AuthService } from '@core/services/auth.service';
import { PERMISSIONS } from '@core/config/permission-keys';
import {
  calculateFodecAmount,
  calculateSaleTtc,
  calculateVatAmount
} from '@shared/utils/product-pricing.utils';
import { ProductClientPricesComponent } from '../product-client-prices/product-client-prices.component';

const VAT_OPTIONS: { label: string; value: number }[] = [
  { label: '19% - Taux normal', value: 19 },
  { label: '13% - Taux intermédiaire', value: 13 },
  { label: '7% - Taux réduit', value: 7 },
  { label: '0% - Exonéré', value: 0 }
];

@Component({
  selector: 'app-product-detail',
  standalone: true,
  imports: [
    CommonModule,
    RouterModule,
    SkeletonModule,
    TagModule,
    ToastModule,
    PageHeaderComponent,
    BreadcrumbComponent,
    FormSectionComponent,
    ButtonComponent,
    ProductClientPricesComponent
  ],
  template: `
    <app-breadcrumb [items]="breadcrumbItems()"></app-breadcrumb>

    <app-page-header
      [title]="pageTitle()"
      [subtitle]="pageSubtitle()">
      <app-button
        variant="outline"
        icon="pi-arrow-left"
        iconPos="left"
        routerLink="/products">
        Retour
      </app-button>
      @if (product() && canEditProduct()) {
        <app-button
          variant="primary"
          icon="pi-pencil"
          iconPos="left"
          [routerLink]="['/products', product()!.id, 'edit']">
          Modifier
        </app-button>
      }
    </app-page-header>

    @if (loading()) {
      <div class="form-grid">
        <app-form-section title="Informations générales" icon="pi-box" [number]="1">
          <p-skeleton height="2.5rem" styleClass="mb-3"></p-skeleton>
          <p-skeleton height="2.5rem" styleClass="mb-3"></p-skeleton>
          <p-skeleton height="5rem" width="100%"></p-skeleton>
        </app-form-section>
        <app-form-section title="Tarification" icon="pi-dollar" [number]="2">
          <p-skeleton height="2.5rem" styleClass="mb-3"></p-skeleton>
          <p-skeleton height="8rem" width="100%"></p-skeleton>
        </app-form-section>
      </div>
    } @else {
      @if (product(); as p) {
      <div class="form-grid">
        <app-form-section title="Informations générales" icon="pi-box" [number]="1">
          <div class="form-row">
            <div class="form-group">
              <span class="field-label">Code produit</span>
              <p class="field-value mono">{{ p.code }}</p>
            </div>
            <div class="form-group">
              <span class="field-label">Type</span>
              <p class="field-value">
                <p-tag [value]="p.typeDisplay" severity="info"></p-tag>
              </p>
            </div>
            <div class="form-group">
              <span class="field-label">Catégorie</span>
              <p class="field-value">{{ p.category }}</p>
            </div>
          </div>

          <div class="form-group">
            <span class="field-label">Désignation</span>
            <p class="field-value">{{ p.name }}</p>
          </div>

          <div class="form-group">
            <span class="field-label">Description</span>
            <p class="field-value description-block">{{ p.description || '—' }}</p>
          </div>

          <div class="form-group">
            <span class="field-label">Gestion de stock</span>
            <p class="field-value">
              <p-tag
                [value]="p.isStockManaged ? 'Activée' : 'Désactivée'"
                [severity]="p.isStockManaged ? 'success' : 'secondary'">
              </p-tag>
              @if (p.isVariantTemplate) {
                <p-tag value="Modèle de variantes" severity="warn"></p-tag>
              }
              @if (p.parentProductId) {
                <p-tag value="Variante" severity="info"></p-tag>
              }
            </p>
          </div>
          @if (p.trackingMode || p.costingMethod) {
            <div class="form-group">
              <span class="field-label">Traçabilité</span>
              <p class="field-value">
                {{ p.trackingMode === 1 ? 'Lot' : p.trackingMode === 2 ? 'Série' : 'Aucun suivi' }}
                · {{ p.costingMethod === 1 ? 'FIFO' : p.costingMethod === 2 ? 'LIFO' : 'CMUP' }}
              </p>
            </div>
          }

          @if (isProductType(p)) {
            <div class="form-group">
              <span class="field-label">Fournisseur préféré</span>
              <p class="field-value">
                {{ preferredSupplierName() ?? (p.preferredSupplierId ? '—' : 'Aucun') }}
              </p>
            </div>
          }

          @if (isProductType(p)) {
            <div class="form-group product-image-group">
              <span class="field-label">Image produit</span>
              <div class="product-image-preview" [class.has-image]="resolvedImageUrl(p)">
                @if (resolvedImageUrl(p)) {
                  <img [src]="resolvedImageUrl(p)!" [alt]="'Image du produit ' + p.name" />
                } @else {
                  <div class="product-image-placeholder">
                    <i class="pi pi-image" aria-hidden="true"></i>
                    <span>Aucune image</span>
                  </div>
                }
              </div>
            </div>
          }

          <div class="form-group">
            <span class="field-label">Statut</span>
            <p class="field-value">
              <p-tag
                [value]="p.isActive ? 'Actif' : 'Inactif'"
                [severity]="p.isActive ? 'success' : 'secondary'">
              </p-tag>
            </p>
          </div>
        </app-form-section>

        <app-form-section title="Tarification" icon="pi-dollar" [number]="2">
          <div class="form-row">
            <div class="form-group">
              <span class="field-label">Prix d'achat HT</span>
              <p class="field-value mono">{{ formatMoney(p.purchasePrice) }}</p>
            </div>
            <div class="form-group">
              <span class="field-label">Dernier prix d'achat HT</span>
              <p class="field-value mono">{{ formatMoney(p.lastPurchasePrice) }}</p>
            </div>
          </div>

          <div class="form-row">
            <div class="form-group">
              <span class="field-label">CMUP HT</span>
              <p class="field-value mono">{{ formatMoney(p.weightedAverageCost) }}</p>
            </div>
            <div class="form-group">
              <span class="field-label">Marge bénéficiaire</span>
              <p class="field-value mono">
                @if (p.profitMarginPercent != null) {
                  {{ p.profitMarginPercent | number:'1.3-3' }} %
                } @else {
                  —
                }
              </p>
            </div>
          </div>

          <div class="form-row">
            <div class="form-group">
              <span class="field-label">Prix de vente HT</span>
              <p class="field-value mono strong">{{ p.unitPrice | number:'1.3-3' }} TND</p>
            </div>
            <div class="form-group">
              <span class="field-label">Prix de vente TTC</span>
              <p class="field-value mono strong">{{ (p.salePriceTtc ?? calculateTtc(p)) | number:'1.3-3' }} TND</p>
            </div>
          </div>

          <div class="form-row">
            <div class="form-group">
              <span class="field-label">Unité de mesure</span>
              <p class="field-value">{{ p.unit }}</p>
            </div>
            <div class="form-group">
              <span class="field-label">Taux de TVA</span>
              <p class="field-value">{{ vatLabel(p.vatRate) }}</p>
            </div>
          </div>

          <div class="form-group">
            <span class="field-label">FODEC</span>
            <p class="field-value">{{ p.isFodecApplicable ? 'Applicable (1%)' : 'Non applicable' }}</p>
          </div>

          <div class="form-row">
            <div class="form-group">
              <span class="field-label">Remise produit</span>
              <p class="field-value">{{ p.isDiscountEnabled ? 'Activée' : 'Désactivée' }}</p>
            </div>
            <div class="form-group">
              <span class="field-label">Remise maximale</span>
              <p class="field-value mono">
                @if (p.isDiscountEnabled && p.maxDiscountPercent != null) {
                  {{ p.maxDiscountPercent | number:'1.1-1' }} %
                } @else {
                  —
                }
              </p>
            </div>
          </div>

          <div class="price-preview" aria-label="Récapitulatif des prix">
            <div class="preview-row">
              <span>FODEC (1%)</span>
              <span class="value">{{ calculateFodec(p) | number:'1.3-3' }} TND</span>
            </div>
            <div class="preview-row">
              <span>TVA ({{ p.vatRate }}%)</span>
              <span class="value">{{ calculateVat(p) | number:'1.3-3' }} TND</span>
            </div>
          </div>
        </app-form-section>
      </div>

      <div class="form-grid-full">
        <app-form-section title="Tarifs par client" icon="pi-users" [number]="3">
          <app-product-client-prices
            [productId]="p.id"
            [catalogUnitPriceHT]="p.unitPrice"
            [readOnly]="true">
          </app-product-client-prices>
        </app-form-section>
      </div>
      }
    }
  `,
  styles: [`
    .form-grid {
      display: grid;
      grid-template-columns: repeat(2, 1fr);
      gap: var(--spacing-4);

      @media (max-width: 1024px) {
        grid-template-columns: 1fr;
      }
    }

    .form-grid-full {
      margin-top: var(--spacing-4);
    }

    .form-row {
      display: grid;
      grid-template-columns: 1fr 1fr 1fr;
      gap: var(--spacing-4);

      @media (max-width: 640px) {
        grid-template-columns: 1fr;
      }
    }

    .form-group {
      margin-bottom: var(--spacing-5);

      &:last-child {
        margin-bottom: 0;
      }
    }

    .field-label {
      display: block;
      margin-bottom: var(--spacing-2);
      font-weight: var(--font-weight-semibold);
      font-size: var(--font-size-sm);
      color: var(--color-text-primary);
    }

    .field-value {
      margin: 0;
      font-size: var(--font-size-base);
      color: var(--color-neutral-800);
    }

    .field-value.mono {
      font-family: 'JetBrains Mono', monospace;
    }

    .field-value.strong {
      font-weight: var(--font-weight-semibold);
    }

    .description-block {
      white-space: pre-wrap;
      word-break: break-word;
    }

    .product-image-group .field-label {
      margin-bottom: var(--spacing-2);
    }

    .product-image-preview {
      width: 160px;
      height: 160px;
      border-radius: var(--radius-lg);
      overflow: hidden;
      background: var(--color-neutral-100);
      border: 1px dashed var(--color-neutral-300);
      display: flex;
      align-items: center;
      justify-content: center;
    }

    .product-image-preview.has-image img {
      width: 100%;
      height: 100%;
      object-fit: cover;
    }

    .product-image-placeholder {
      display: flex;
      flex-direction: column;
      align-items: center;
      justify-content: center;
      gap: var(--spacing-2);
      color: var(--color-neutral-500);
      font-size: var(--font-size-sm);
    }

    .product-image-placeholder i {
      font-size: 2rem;
    }

    .price-preview {
      margin-top: var(--spacing-4);
      padding: var(--spacing-4);
      background: var(--color-background-subtle);
      border: 1px solid var(--color-border-subtle);
      border-radius: var(--radius-lg);
    }

    .preview-row {
      display: flex;
      justify-content: space-between;
      padding: var(--spacing-2) 0;
      font-size: var(--font-size-sm);

      .value {
        font-family: 'JetBrains Mono', monospace;
        font-weight: var(--font-weight-medium);
      }

      &.total {
        margin-top: var(--spacing-2);
        padding-top: var(--spacing-3);
        border-top: 2px solid var(--color-neutral-200);
        font-weight: var(--font-weight-semibold);

        .value {
          font-size: var(--font-size-lg);
          color: var(--color-primary-700);
        }
      }
    }
  `]
})
export class ProductDetailComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private productService = inject(ProductService);
  private supplierService = inject(SupplierService);
  private toastService = inject(ToastService);
  private errorHandler = inject(ErrorHandlerService);
  private auth = inject(AuthService);

  loading = signal(true);
  product = signal<Product | null>(null);
  /** Resolved name of the preferred supplier (null when none / not yet loaded). */
  preferredSupplierName = signal<string | null>(null);

  canEditProduct = computed(() => this.auth.hasPermission(PERMISSIONS.products.update));

  breadcrumbItems = computed<BreadcrumbItem[]>(() => {
    const items: BreadcrumbItem[] = [
      { label: 'Tableau de bord', route: '/dashboard', icon: 'pi-home' },
      { label: 'Produits & Services', route: '/products' },
      { label: 'Détails' }
    ];
    return items;
  });

  pageTitle = computed(() => (this.loading() ? 'Chargement…' : 'Détails du produit'));

  pageSubtitle = computed(() => {
    const p = this.product();
    if (this.loading() || !p) return 'Consultation des informations du produit';
    return `${p.code} · ${p.name}`;
  });

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (!id) {
      this.toastService.add({
        severity: 'error',
        summary: 'Erreur',
        detail: 'Produit introuvable'
      });
      void this.router.navigate(['/products']);
      return;
    }
    this.loadProduct(id);
  }

  isProductType(p: Product): boolean {
    return p.typeDisplay === 'Produit';
  }

  /** Resolves the preferred supplier's display name (best-effort; silent on failure). */
  private loadPreferredSupplierName(supplierId: string | null): void {
    this.preferredSupplierName.set(null);
    if (!supplierId) return;
    this.supplierService.getSupplier(supplierId).subscribe({
      next: res => this.preferredSupplierName.set(res.data?.name ?? null),
      error: () => this.preferredSupplierName.set(null)
    });
  }

  resolvedImageUrl(p: Product): string | null {
    return this.productService.resolveProductImageUrl(p.imageUrl ?? null);
  }

  vatLabel(rate: number): string {
    const opt = VAT_OPTIONS.find(o => o.value === rate);
    return opt?.label ?? `${rate}%`;
  }

  formatMoney(value: number | null | undefined): string {
    if (value == null) return '—';
    return `${value.toLocaleString('fr-FR', { minimumFractionDigits: 3, maximumFractionDigits: 3 })} TND`;
  }

  calculateFodec(p: Product): number {
    return calculateFodecAmount(p.unitPrice || 0, p.isFodecApplicable ?? false);
  }

  calculateVat(p: Product): number {
    return calculateVatAmount(p.unitPrice || 0, this.calculateFodec(p), p.vatRate || 0);
  }

  calculateTtc(p: Product): number {
    return calculateSaleTtc(p.unitPrice || 0, p.vatRate || 0, p.isFodecApplicable ?? false);
  }

  private loadProduct(id: string): void {
    this.loading.set(true);
    this.productService.getProduct(id).subscribe({
      next: (response) => {
        if (response.success && response.data) {
          this.product.set(response.data);
          this.loadPreferredSupplierName(response.data.preferredSupplierId ?? null);
        } else {
          this.toastService.add({
            severity: 'error',
            summary: 'Erreur',
            detail: response.errors?.join(', ') || 'Impossible de charger le produit'
          });
          void this.router.navigate(['/products']);
        }
        this.loading.set(false);
      },
      error: (error) => {
        const errorMessage = this.errorHandler.extractErrorMessage(error);
        this.toastService.add({
          severity: 'error',
          summary: 'Erreur',
          detail: errorMessage || 'Impossible de charger le produit'
        });
        this.errorHandler.logError('Failed to load product', error);
        void this.router.navigate(['/products']);
        this.loading.set(false);
      }
    });
  }
}
