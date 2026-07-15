import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterModule, ActivatedRoute } from '@angular/router';
import { InputTextModule } from 'primeng/inputtext';
import { InputTextareaModule } from 'primeng/inputtextarea';
import { InputNumberModule } from 'primeng/inputnumber';
import { DropdownModule } from 'primeng/dropdown';
import { ButtonModule } from 'primeng/button';
import { CardModule } from 'primeng/card';
import { InputSwitchModule } from 'primeng/inputswitch';
import { CheckboxModule } from 'primeng/checkbox';
import { FileUploadModule } from 'primeng/fileupload';
import { ToastModule } from 'primeng/toast';
import { ToastService } from '@core/services/toast.service';
import { PageHeaderComponent } from '@shared/components/page-header/page-header.component';
import { BreadcrumbComponent, BreadcrumbItem } from '@shared/components/breadcrumb/breadcrumb.component';
import { FormSectionComponent } from '@shared/components/form-section/form-section.component';
import { ButtonComponent } from '@shared/components/button/button.component';
import { ErrorMessageService } from '@core/services/error-message.service';
import { ProductService, CreateProductRequest, UpdateProductRequest } from '@core/services/product.service';
import { ProductCategoryService } from '@core/services/product-category.service';
import { SupplierService } from '@core/services/supplier.service';
import { ErrorHandlerService } from '@core/services/error-handler.service';
import { AuthService } from '@core/services/auth.service';
import { PERMISSIONS } from '@core/config/permission-keys';
import { AppModule } from '@core/models/app-module';

interface CategoryOption {
  label: string;
  value: string;
}

interface UnitOption {
  label: string;
  value: string;
}

interface VatOption {
  label: string;
  value: number;
}

@Component({
  selector: 'app-product-form',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    RouterModule,
    InputTextModule,
    InputTextareaModule,
    InputNumberModule,
    DropdownModule,
    ButtonModule,
    CardModule,
    InputSwitchModule,
    CheckboxModule,
    FileUploadModule,
    ToastModule,
    PageHeaderComponent,
    BreadcrumbComponent,
    FormSectionComponent,
    ButtonComponent
  ],
  template: `
    <app-breadcrumb [items]="breadcrumbItems()"></app-breadcrumb>
    
    <app-page-header 
      [title]="isEditMode() ? 'Modifier le produit' : 'Nouveau produit'" 
      [subtitle]="isEditMode() ? 'Modifiez les informations du produit' : 'Ajoutez un nouveau produit à votre catalogue'">
      <app-button 
        variant="outline"
        icon="pi-times"
        iconPos="left"
        routerLink="/products">
        Annuler
      </app-button>
    </app-page-header>

    <form [formGroup]="form" (ngSubmit)="onSubmit()">
      <div class="form-grid">
        <!-- General Information -->
        <app-form-section title="Informations générales" icon="pi-box" [number]="1">
          <div class="form-row">
            <div class="form-group">
              <label for="code">Code produit <span class="required">*</span></label>
              <input 
                pInputText 
                id="code" 
                formControlName="code"
                placeholder="Ex: PROD-001"
                class="w-full"
                [class.ng-invalid]="isInvalid('code')">
              @if (isInvalid('code')) {
                <div class="form-error">
                  <i class="pi pi-exclamation-circle"></i>
                  <span>{{ errorMessageService.getErrorMessage(form.get('code')) }}</span>
                </div>
              }
            </div>

            <div class="form-group">
              <label for="category">Type <span class="required">*</span></label>
              <p-dropdown 
                id="category"
                [options]="categoryOptions" 
                formControlName="category"
                placeholder="Sélectionner"
                styleClass="w-full">
              </p-dropdown>
              @if (isInvalid('category')) {
                <div class="form-error">
                  <i class="pi pi-exclamation-circle"></i>
                  <span>{{ errorMessageService.getErrorMessage(form.get('category')) }}</span>
                </div>
              }
            </div>

            <div class="form-group">
              <label for="productCategoryId">Catégorie</label>
              <p-dropdown 
                id="productCategoryId"
                [options]="productCategoryOptions()"
                formControlName="productCategoryId"
                placeholder="Sélectionner une catégorie"
                optionLabel="label"
                optionValue="value"
                styleClass="w-full">
              </p-dropdown>
              <small class="form-hint">Classification du produit. Par défaut: General</small>
            </div>
          </div>

          <div class="form-group">
            <label for="name">Désignation <span class="required">*</span></label>
            <input 
              pInputText 
              id="name" 
              formControlName="name"
              placeholder="Nom du produit ou service"
              class="w-full"
              [class.ng-invalid]="isInvalid('name')">
            @if (isInvalid('name')) {
              <div class="form-error">
                <i class="pi pi-exclamation-circle"></i>
                <span>{{ errorMessageService.getErrorMessage(form.get('name')) }}</span>
              </div>
            }
          </div>

          <div class="form-group">
            <label for="description">Description</label>
            <textarea 
              pInputTextarea 
              id="description" 
              formControlName="description"
              placeholder="Description détaillée du produit ou service..."
              [rows]="3"
              class="w-full">
            </textarea>
          </div>

          @if (showStockManagementSection()) {
            <div class="form-group">
              <label for="isStockManaged">Gestion de stock</label>
              <div class="status-switch">
                <p-inputSwitch 
                  id="isStockManaged"
                  formControlName="isStockManaged"
                  [disabled]="!isProductCategory()">
                </p-inputSwitch>
                <span [class.active]="form.get('isStockManaged')?.value">
                  {{ form.get('isStockManaged')?.value ? 'Activée' : 'Désactivée' }}
                </span>
              </div>
              @if (!isProductCategory()) {
                <small class="form-hint">Disponible uniquement pour les produits physiques.</small>
              }
            </div>

            @if (isProductCategory()) {
              <div class="form-group">
                <label for="preferredSupplierId">Fournisseur préféré</label>
                <p-dropdown
                  id="preferredSupplierId"
                  [options]="supplierOptions()"
                  formControlName="preferredSupplierId"
                  placeholder="Aucun (à choisir lors de la commande)"
                  optionLabel="label"
                  optionValue="value"
                  [filter]="true"
                  [showClear]="true"
                  styleClass="w-full">
                </p-dropdown>
                <small class="form-hint">
                  Utilisé par Prévisions IA — Réapprovisionnement : les recommandations de ce produit
                  seront pré-rattachées à ce fournisseur, ce qui permet de créer les bons de commande sans
                  saisie manuelle.
                </small>
              </div>
            }
          }

          @if (isProductCategory() && canMutateProduct()) {
            <div class="form-group product-image-group">
              <label>Image produit</label>
              <div class="product-image-section">
                <div class="product-image-preview" [class.has-image]="displayImageUrl()">
                  @if (displayImageUrl()) {
                    <img [src]="resolveProductImageUrl(displayImageUrl())" alt="" />
                  } @else {
                    <div class="product-image-placeholder">
                      <i class="pi pi-image"></i>
                      <span>Ajouter une image</span>
                    </div>
                  }
                </div>
                <div class="product-image-actions">
                  <p-fileUpload
                    mode="basic"
                    accept="image/*"
                    [maxFileSize]="2000000"
                    chooseLabel="Choisir une image"
                    (onSelect)="onProductImageSelect($event)"
                    [disabled]="saving()"
                    aria-label="Choisir une image pour le produit">
                  </p-fileUpload>
                  @if (displayImageUrl() || selectedImageFile()) {
                    <button
                      type="button"
                      class="product-image-remove"
                      (click)="removeProductImage()"
                      [disabled]="saving()"
                      aria-label="Supprimer l'image du produit">
                      Supprimer l'image
                    </button>
                  }
                </div>
                <small class="form-hint">JPEG, PNG ou WebP. Max 2 Mo.</small>
              </div>
            </div>
          }

          @if (isEditMode() && canMutateProduct()) {
            <div class="form-group">
              <label for="isActive">Statut</label>
              <div class="status-switch">
                <p-inputSwitch 
                  id="isActive"
                  formControlName="isActive">
                </p-inputSwitch>
                <span [class.active]="form.get('isActive')?.value">
                  {{ form.get('isActive')?.value ? 'Actif' : 'Inactif' }}
                </span>
              </div>
            </div>
          }
        </app-form-section>

        <!-- Pricing -->
        <app-form-section title="Tarification" icon="pi-dollar" [number]="2">
          <div class="form-row">
            <div class="form-group">
              <label for="unitPrice">Prix unitaire HT <span class="required">*</span></label>
              <p-inputNumber 
                id="unitPrice" 
                formControlName="unitPrice"
                mode="decimal"
                [minFractionDigits]="3"
                [maxFractionDigits]="3"
                suffix=" TND"
                placeholder="0.000"
                styleClass="w-full"
                [class.ng-invalid]="isInvalid('unitPrice')">
              </p-inputNumber>
              @if (isInvalid('unitPrice')) {
                <div class="form-error">
                  <i class="pi pi-exclamation-circle"></i>
                  <span>{{ errorMessageService.getErrorMessage(form.get('unitPrice')) }}</span>
                  @if (errorMessageService.getFieldSuggestion('unitPrice', 'min')) {
                    <small class="form-hint">
                      {{ errorMessageService.getFieldSuggestion('unitPrice', 'min') }}
                    </small>
                  }
                </div>
              }
            </div>

            <div class="form-group">
              <label for="unit">Unité de mesure <span class="required">*</span></label>
              <p-dropdown 
                id="unit"
                [options]="unitOptions" 
                formControlName="unit"
                placeholder="Sélectionner"
                [editable]="true"
                styleClass="w-full">
              </p-dropdown>
              @if (isInvalid('unit')) {
                <div class="form-error">
                  <i class="pi pi-exclamation-circle"></i>
                  <span>{{ errorMessageService.getErrorMessage(form.get('unit')) }}</span>
                </div>
              }
            </div>
          </div>

          <div class="form-group">
            <label for="purchasePrice">Prix d'achat HT</label>
            <p-inputNumber 
              id="purchasePrice" 
              formControlName="purchasePrice"
              mode="decimal"
              [minFractionDigits]="3"
              [maxFractionDigits]="3"
              suffix=" TND"
              placeholder="Optionnel"
              styleClass="w-full"
              [class.ng-invalid]="isInvalid('purchasePrice')">
            </p-inputNumber>
            <small class="form-hint">
              Prix par défaut pour les bons de commande et factures fournisseurs. Si vide, le prix de vente est utilisé.
            </small>
            @if (isInvalid('purchasePrice')) {
              <div class="form-error">
                <i class="pi pi-exclamation-circle"></i>
                <span>{{ errorMessageService.getErrorMessage(form.get('purchasePrice')) }}</span>
              </div>
            }
          </div>

          <div class="form-group">
            <label for="vatRate">Taux de TVA <span class="required">*</span></label>
            <p-dropdown 
              id="vatRate"
              [options]="vatOptions" 
              formControlName="vatRate"
              placeholder="Sélectionner"
              styleClass="w-full">
            </p-dropdown>
            <small class="form-hint">
              Taux de TVA applicable selon la législation tunisienne
            </small>
            @if (isInvalid('vatRate')) {
              <div class="form-error">
                <i class="pi pi-exclamation-circle"></i>
                <span>{{ errorMessageService.getErrorMessage(form.get('vatRate')) }}</span>
              </div>
            }
          </div>

          <div class="form-group fodec-group">
            <div class="fodec-checkbox">
              <p-checkbox
                inputId="isFodecApplicable"
                formControlName="isFodecApplicable"
                [binary]="true">
              </p-checkbox>
              <label for="isFodecApplicable">FODEC applicable (1%)</label>
            </div>
            <small class="form-hint">Fonds de Développement de la Compétitivité</small>
          </div>

          <!-- Calculated preview -->
          <div class="price-preview">
            <div class="preview-row">
              <span>Prix HT</span>
              <span class="value">{{ form.get('unitPrice')?.value | number:'1.3-3' }} TND</span>
            </div>
            @if (form.get('isFodecApplicable')?.value) {
              <div class="preview-row">
                <span>FODEC (1%)</span>
                <span class="value">{{ calculateFodec() | number:'1.3-3' }} TND</span>
              </div>
            }
            <div class="preview-row">
              <span>TVA ({{ form.get('vatRate')?.value || 0 }}%)</span>
              <span class="value">{{ calculateVat() | number:'1.3-3' }} TND</span>
            </div>
            <div class="preview-row total">
              <span>Prix TTC</span>
              <span class="value">{{ calculateTTC() | number:'1.3-3' }} TND</span>
            </div>
          </div>
        </app-form-section>
      </div>

      <!-- Actions -->
      <div class="form-actions">
        <app-button 
          variant="outline"
          icon="pi-times"
          iconPos="left"
          routerLink="/products">
          Annuler
        </app-button>
        @if (canMutateProduct()) {
          <app-button 
            variant="primary"
            type="submit"
            [icon]="saving() ? 'pi-spin pi-spinner' : 'pi-check'"
            iconPos="left"
            [disabled]="form.invalid || saving()">
            {{ isEditMode() ? 'Enregistrer' : 'Créer le produit' }}
          </app-button>
        }
      </div>
    </form>

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

    .form-row {
      display: grid;
      grid-template-columns: 1fr 1fr;
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

      label {
        display: block;
        margin-bottom: var(--spacing-2);
        font-weight: var(--font-weight-semibold);
        font-size: var(--font-size-sm);
        color: var(--color-text-primary);
      }

      .required {
        color: var(--color-error-600);
        margin-left: var(--spacing-1);
      }
    }

    .form-error {
      display: flex;
      align-items: center;
      gap: var(--spacing-2);
      margin-top: var(--spacing-1);
      font-size: var(--font-size-sm);
      color: var(--color-error-600);

      i {
        font-size: var(--font-size-base);
      }

      .form-hint {
        display: block;
        margin-top: var(--spacing-1);
        color: var(--color-neutral-600);
        font-size: var(--font-size-xs);
      }
    }

    .form-hint {
      color: var(--color-neutral-500);
      font-size: var(--font-size-sm);
      margin-top: var(--spacing-1);
      display: block;
    }

    .product-image-group label {
      display: block;
      margin-bottom: var(--spacing-2);
    }

    .product-image-section {
      display: flex;
      flex-direction: column;
      gap: var(--spacing-3);
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

    .product-image-actions {
      display: flex;
      flex-wrap: wrap;
      gap: var(--spacing-2);
      align-items: center;
    }

    .product-image-remove {
      background: none;
      border: none;
      color: var(--color-error-600);
      font-size: var(--font-size-sm);
      cursor: pointer;
      padding: var(--spacing-1) 0;
      text-decoration: underline;
    }

    .product-image-remove:hover:not(:disabled) {
      color: var(--color-error-700);
    }

    .product-image-remove:disabled {
      opacity: 0.6;
      cursor: not-allowed;
    }

    .status-switch {
      display: flex;
      align-items: center;
      gap: var(--spacing-3);

      span {
        color: var(--color-neutral-600);

        &.active {
          color: var(--color-success-600);
          font-weight: var(--font-weight-medium);
        }
      }
    }

    .price-preview {
      margin-top: var(--spacing-4);
      padding: var(--spacing-4);
      background: var(--color-neutral-50);
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

    .form-actions {
      display: flex;
      justify-content: flex-end;
      gap: var(--spacing-4);
      margin-top: var(--spacing-8);
      padding-top: var(--spacing-6);
      border-top: 1px solid var(--color-border-subtle);
    }

    .price-preview {
      background: var(--color-background-subtle);
      border: 1px solid var(--color-border-subtle);
    }

    :host ::ng-deep {
      .p-inputnumber,
      .p-dropdown {
        width: 100%;
      }
    }
  `]
})
export class ProductFormComponent implements OnInit {
  private fb = inject(FormBuilder);
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  private toastService = inject(ToastService);
  private productService = inject(ProductService);
  private productCategoryService = inject(ProductCategoryService);
  private supplierService = inject(SupplierService);
  private errorHandler = inject(ErrorHandlerService);
  errorMessageService = inject(ErrorMessageService);
  private auth = inject(AuthService);

  saving = signal(false);
  productCategoryOptions = signal<{ label: string; value: string }[]>([]);
  /** Active suppliers for the "fournisseur préféré" dropdown (réapprovisionnement). */
  supplierOptions = signal<{ label: string; value: string }[]>([]);
  productId = signal<string | null>(null);
  currentImageUrl = signal<string | null>(null);
  selectedImageFile = signal<File | null>(null);
  previewDataUrl = signal<string | null>(null);
  imageToRemove = signal(false);

  isEditMode = computed(() => !!this.productId());

  canMutateProduct = computed(() =>
    this.isEditMode()
      ? this.auth.hasPermission(PERMISSIONS.products.update)
      : this.auth.hasPermission(PERMISSIONS.products.create)
  );

  showStockManagementSection = computed(
    () => this.auth.hasModule(AppModule.Stock) && this.canMutateProduct()
  );

  displayImageUrl = computed(() => {
    if (this.imageToRemove()) return null;
    const preview = this.previewDataUrl();
    if (preview) return preview;
    return this.currentImageUrl();
  });

  /** Expose image URL resolution for template; keeps ProductService private. */
  resolveProductImageUrl(url: string | null): string | null {
    return this.productService.resolveProductImageUrl(url);
  }

  breadcrumbItems = computed<BreadcrumbItem[]>(() => {
    const base: BreadcrumbItem[] = [
      { label: 'Tableau de bord', route: '/dashboard', icon: 'pi-home' },
      { label: 'Produits & Services', route: '/products' }
    ];
    if (this.isEditMode()) {
      base.push({ label: 'Modifier' });
    } else {
      base.push({ label: 'Nouveau produit' });
    }
    return base;
  });

  categoryOptions: CategoryOption[] = [
    { label: 'Produit', value: 'Produit' },
    { label: 'Service', value: 'Service' },
    { label: 'Abonnement', value: 'Abonnement' }
  ];

  unitOptions: UnitOption[] = [
    { label: 'Unité', value: 'Unité' },
    { label: 'Heure', value: 'Heure' },
    { label: 'Jour', value: 'Jour' },
    { label: 'Mois', value: 'Mois' },
    { label: 'An', value: 'An' },
    { label: 'Kg', value: 'Kg' },
    { label: 'Litre', value: 'Litre' },
    { label: 'Mètre', value: 'Mètre' },
    { label: 'M²', value: 'M²' }
  ];

  vatOptions: VatOption[] = [
    { label: '19% - Taux normal', value: 19 },
    { label: '13% - Taux intermédiaire', value: 13 },
    { label: '7% - Taux réduit', value: 7 },
    { label: '0% - Exonéré', value: 0 }
  ];

  form: FormGroup = this.fb.group({
    code: ['', [Validators.required, Validators.minLength(2)]],
    name: ['', [Validators.required, Validators.minLength(2)]],
    description: [''],
    category: ['Service', Validators.required],
    productCategoryId: [null as string | null],
    unitPrice: [0, [Validators.required, Validators.min(0)]],
    purchasePrice: [null as number | null, [Validators.min(0)]],
    unit: ['Unité', Validators.required],
    vatRate: [19, Validators.required],
    isFodecApplicable: [false],
    isStockManaged: [false],
    preferredSupplierId: [null as string | null],
    isActive: [true]
  });

  ngOnInit(): void {
    this.loadProductCategories();
    this.loadSuppliers();

    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
      this.productId.set(id);
      this.loadProduct(id);
    }

    const categoryControl = this.form.get('category');
    this.syncStockManagement(categoryControl?.value ?? null, true);
    categoryControl?.valueChanges.subscribe((value) => {
      this.syncStockManagement(value ?? null);
    });
  }

  isInvalid(field: string): boolean {
    const control = this.form.get(field);
    return !!(control?.invalid && control?.touched);
  }

  onProductImageSelect(event: { files: File[] }): void {
    const file = event.files?.[0];
    if (!file) return;
    this.imageToRemove.set(false);
    this.selectedImageFile.set(file);
    const reader = new FileReader();
    reader.onload = (e) => this.previewDataUrl.set((e.target?.result as string) ?? null);
    reader.readAsDataURL(file);
  }

  removeProductImage(): void {
    this.selectedImageFile.set(null);
    this.previewDataUrl.set(null);
    this.imageToRemove.set(true);
    this.currentImageUrl.set(null);
  }

  calculateFodec(): number {
    if (!this.form.get('isFodecApplicable')?.value) return 0;
    const price = this.form.get('unitPrice')?.value || 0;
    return this.round3(price * 0.01);
  }

  calculateVat(): number {
    const price = this.form.get('unitPrice')?.value || 0;
    const vatRate = this.form.get('vatRate')?.value || 0;
    const vatBase = price + this.calculateFodec();
    return this.round3(vatBase * (vatRate / 100));
  }

  calculateTTC(): number {
    const price = this.form.get('unitPrice')?.value || 0;
    return this.round3(price + this.calculateFodec() + this.calculateVat());
  }

  private round3(n: number): number {
    return Math.round(n * 1000) / 1000;
  }

  private loadProductCategories(): void {
    this.productCategoryService.getCategoryOptionsForDropdown().subscribe({
      next: options => {
        this.productCategoryOptions.set(options);
        if (options.length > 0 && !this.productId()) {
          const defaultCategory = options.find(o => o.label === 'General') ?? options[0];
          this.form.patchValue({ productCategoryId: defaultCategory.value });
        }
      },
      error: () => this.productCategoryOptions.set([])
    });
  }

  /** Loads active suppliers for the "fournisseur préféré" dropdown (best-effort). */
  private loadSuppliers(): void {
    this.supplierService.getSuppliers({ pageSize: 200, isActive: true }).subscribe({
      next: res => this.supplierOptions.set(
        (res.data?.items ?? []).map(s => ({ label: s.name, value: s.id }))
      ),
      error: () => this.supplierOptions.set([])
    });
  }

  private loadProduct(id: string): void {
    this.productService.getProduct(id).subscribe({
      next: (response) => {
        if (response.success && response.data) {
          const product = response.data;
          this.form.patchValue({
            code: product.code,
            name: product.name,
            description: product.description || '',
            category: product.typeDisplay || 'Service',
            productCategoryId: product.categoryId || null,
            unitPrice: product.unitPrice,
            purchasePrice: product.purchasePrice ?? null,
            unit: product.unit || 'Unité',
            vatRate: product.vatRate,
            isFodecApplicable: product.isFodecApplicable ?? false,
            isStockManaged: product.isStockManaged ?? false,
            preferredSupplierId: product.preferredSupplierId ?? null,
            isActive: product.isActive ?? true
          });

          this.syncStockManagement(product.typeDisplay || 'Service', true);
          this.currentImageUrl.set(product.imageUrl ?? null);
          this.selectedImageFile.set(null);
          this.previewDataUrl.set(null);
          this.imageToRemove.set(false);
        } else {
          this.toastService.add({
            severity: 'error',
            summary: 'Erreur',
            detail: response.errors?.join(', ') || 'Impossible de charger le produit'
          });
          this.router.navigate(['/products']);
        }
      },
      error: (error) => {
        const errorMessage = this.errorHandler.extractErrorMessage(error);
        this.toastService.add({
          severity: 'error',
          summary: 'Erreur',
          detail: errorMessage || 'Impossible de charger le produit'
        });
        this.errorHandler.logError('Failed to load product', error);
        this.router.navigate(['/products']);
      }
    });
  }

  private navigateToProducts(): void {
    this.router.navigate(['/products']);
  }

  onSubmit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.saving.set(true);

    const formValue = this.form.value;
    
    if (this.isEditMode()) {
      // Update existing product
      const updateRequest: UpdateProductRequest = {
        code: formValue.code.trim(),
        name: formValue.name.trim(),
        description: formValue.description?.trim() || null,
        category: formValue.category,
        productCategoryId: formValue.productCategoryId || undefined,
        unitPrice: formValue.unitPrice,
        purchasePrice: formValue.purchasePrice ?? null,
        unit: formValue.unit,
        vatRate: formValue.vatRate,
        isFodecApplicable: formValue.isFodecApplicable ?? false,
        isStockManaged: formValue.isStockManaged ?? false,
        preferredSupplierId: formValue.preferredSupplierId || null,
        isActive: formValue.isActive ?? true
      };

      this.productService.updateProduct(this.productId()!, updateRequest).subscribe({
        next: (response) => {
          if (response.success) {
            this.toastService.add({
              severity: 'success',
              summary: 'Succès',
              detail: response.message || 'Produit mis à jour avec succès'
            });
            const id = this.productId()!;
            const file = this.selectedImageFile();
            const toRemove = this.imageToRemove();
            if (file) {
              this.productService.uploadProductImage(id, file).subscribe({
                next: () => {
                  this.saving.set(false);
                  this.navigateToProducts();
                },
                error: (err) => {
                  this.toastService.add({
                    severity: 'warn',
                    summary: 'Image',
                    detail: 'Produit mis à jour ; l\'image n\'a pas pu être enregistrée.'
                  });
                  this.saving.set(false);
                  this.navigateToProducts();
                }
              });
            } else if (toRemove) {
              this.productService.deleteProductImage(id).subscribe({
                next: () => {
                  this.saving.set(false);
                  this.navigateToProducts();
                },
                error: () => {
                  this.saving.set(false);
                  this.navigateToProducts();
                }
              });
            } else {
              this.saving.set(false);
              this.navigateToProducts();
            }
          } else {
            this.toastService.add({
              severity: 'error',
              summary: 'Erreur',
              detail: response.errors?.join(', ') || 'Une erreur est survenue lors de la mise à jour'
            });
            this.saving.set(false);
          }
        },
        error: (error) => {
          const errorMessage = this.errorHandler.extractErrorMessage(error);
          this.toastService.add({
            severity: 'error',
            summary: 'Erreur',
            detail: errorMessage || 'Une erreur est survenue lors de la mise à jour'
          });
          this.errorHandler.logError('Failed to update product', error);
          this.saving.set(false);
        }
      });
    } else {
      // Create new product
      const createRequest: CreateProductRequest = {
        code: formValue.code.trim(),
        name: formValue.name.trim(),
        description: formValue.description?.trim() || null,
        category: formValue.category,
        productCategoryId: formValue.productCategoryId || undefined,
        unitPrice: formValue.unitPrice,
        purchasePrice: formValue.purchasePrice ?? null,
        unit: formValue.unit,
        vatRate: formValue.vatRate,
        isFodecApplicable: formValue.isFodecApplicable ?? false,
        isStockManaged: formValue.isStockManaged ?? false,
        preferredSupplierId: formValue.preferredSupplierId || null
      };

      this.productService.createProduct(createRequest).subscribe({
        next: (response) => {
          if (response.success && response.data) {
            const newId = response.data;
            this.toastService.add({
              severity: 'success',
              summary: 'Succès',
              detail: response.message || 'Produit créé avec succès'
            });
            const file = this.selectedImageFile();
            if (file) {
              this.productService.uploadProductImage(newId, file).subscribe({
                next: (uploadRes) => {
                  if (!uploadRes.success) {
                    this.toastService.add({
                      severity: 'warn',
                      summary: 'Image',
                      detail: 'Produit créé ; l\'image n\'a pas pu être enregistrée.'
                    });
                  }
                  this.saving.set(false);
                  this.navigateToProducts();
                },
                error: () => {
                  this.toastService.add({
                    severity: 'warn',
                    summary: 'Image',
                    detail: 'Produit créé ; l\'image n\'a pas pu être enregistrée.'
                  });
                  this.saving.set(false);
                  this.navigateToProducts();
                }
              });
            } else {
              this.saving.set(false);
              this.navigateToProducts();
            }
          } else {
            this.toastService.add({
              severity: 'error',
              summary: 'Erreur',
              detail: response.errors?.join(', ') || 'Une erreur est survenue lors de la création'
            });
            this.saving.set(false);
          }
        },
        error: (error) => {
          const errorMessage = this.errorHandler.extractErrorMessage(error);
          this.toastService.add({
            severity: 'error',
            summary: 'Erreur',
            detail: errorMessage || 'Une erreur est survenue lors de la création'
          });
          this.errorHandler.logError('Failed to create product', error);
          this.saving.set(false);
        }
      });
    }
  }

  isProductCategory(): boolean {
    return this.form.get('category')?.value === 'Produit';
  }

  private syncStockManagement(category: string | null, preserveValue: boolean = false): void {
    const control = this.form.get('isStockManaged');
    if (!control) return;

    const isProduct = category === 'Produit';

    if (!isProduct) {
      control.setValue(false, { emitEvent: false });
      control.disable({ emitEvent: false });
      return;
    }

    control.enable({ emitEvent: false });

    if (!preserveValue && !this.isEditMode()) {
      control.setValue(true, { emitEvent: false });
    }
  }
}
