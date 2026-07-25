import { Component, OnInit, OnDestroy, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { InputTextModule } from 'primeng/inputtext';
import { InputMaskModule } from 'primeng/inputmask';
import { InputTextarea } from 'primeng/inputtextarea';
import { DropdownModule } from 'primeng/dropdown';
import { ButtonModule } from 'primeng/button';
import { CardModule } from 'primeng/card';
import { DividerModule } from 'primeng/divider';
import { FileUploadModule } from 'primeng/fileupload';
import { ToastService } from '@core/services/toast.service';
import { HttpErrorResponse } from '@angular/common/http';
import { Subject } from 'rxjs';
import { takeUntil } from 'rxjs/operators';
import { PageHeaderComponent } from '@shared/components/page-header/page-header.component';
import { BreadcrumbComponent, BreadcrumbItem } from '@shared/components/breadcrumb/breadcrumb.component';
import { ErrorMessageService } from '@core/services/error-message.service';
import { CompanyService, UpdateCompanyRequest } from '@core/services/company.service';
import { StockService, Warehouse, UpdateWarehouseRequest } from '@core/services/stock.service';
import { ErrorHandlerService } from '@core/services/error-handler.service';
import { TUNISIAN_GOVERNORATE_OPTIONS } from '@shared/validation/validation-rules';

@Component({
  selector: 'app-company',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    RouterModule,
    InputTextModule,
    InputMaskModule,
    InputTextarea,
    DropdownModule,
    ButtonModule,
    CardModule,
    DividerModule,
    FileUploadModule,
    PageHeaderComponent,
    BreadcrumbComponent
  ],
  template: `
    <app-breadcrumb [items]="breadcrumbItems"></app-breadcrumb>
    
    <app-page-header 
      title="Mon entreprise" 
      subtitle="Informations légales et paramètres de facturation">
      <p-button 
        label="Retour" 
        icon="pi pi-arrow-left" 
        [outlined]="true"
        routerLink="/settings">
      </p-button>
    </app-page-header>

    @if (loading()) {
      <div class="loading-container">
        <i class="pi pi-spin pi-spinner" style="font-size: 2rem"></i>
        <p>Chargement des informations...</p>
      </div>
    } @else {
    
    @if (errorMessage() && isRateLimited()) {
      <div class="error-banner">
        <div class="error-content">
          <i class="pi pi-exclamation-triangle error-icon"></i>
          <div class="error-text">
            <strong>Erreur</strong>
            <p>{{ errorMessage() }}</p>
          </div>
        </div>
        <p-button 
          label="Réessayer" 
          icon="pi pi-refresh"
          [disabled]="loading()"
          (onClick)="loadCompanyData()"
          styleClass="p-button-sm">
        </p-button>
      </div>
    }
    
    <form [formGroup]="form" (ngSubmit)="onSubmit()">
      <div class="form-grid">
        <!-- Company Identity -->
        <p-card header="Identité de l'entreprise" styleClass="form-card">
          <div class="logo-section">
            <div class="logo-preview">
              @if (logoUrl()) {
                <img [src]="logoUrl()" alt="Logo">
              } @else {
                <i class="pi pi-image"></i>
              }
            </div>
            <div class="logo-actions">
              <p-fileUpload
                mode="basic"
                accept="image/*"
                [maxFileSize]="1000000"
                chooseLabel="Choisir un logo"
                (onSelect)="onLogoSelect($event)">
              </p-fileUpload>
              <small>PNG, JPG. Max 1MB. Recommandé: 200x200px</small>
            </div>
          </div>

          <p-divider></p-divider>

          <div class="form-group">
            <label for="companyName">Raison sociale <span class="required">*</span></label>
            <input 
              pInputText 
              id="companyName" 
              formControlName="companyName"
              class="w-full"
              [class.ng-invalid]="isInvalid('companyName')">
          </div>

          <div class="form-group">
            <label for="tradeName">Nom commercial</label>
            <input 
              pInputText 
              id="tradeName" 
              formControlName="tradeName"
              placeholder="Si différent de la raison sociale"
              class="w-full">
          </div>

          <div class="form-row">
            <div class="form-group">
              <label for="nif">Matricule fiscal <span class="required">*</span></label>
              <p-inputMask
                id="nif"
                formControlName="nif"
                mask="9999999/a/a/a/999"
                placeholder="1234567/A/B/C/000"
                styleClass="w-full"
                (onBlur)="onNifBlur()">
              </p-inputMask>
            </div>

            <div class="form-group">
              <label for="commerceRegistry">Registre de commerce</label>
              <input 
                pInputText 
                id="commerceRegistry" 
                formControlName="commerceRegistry"
                placeholder="Ex: B123456789"
                class="w-full">
            </div>
          </div>

          <div class="form-group">
            <label for="taxRegime">Régime fiscal <span class="required">*</span></label>
            <p-dropdown 
              id="taxRegime"
              [options]="taxRegimes" 
              formControlName="taxRegime"
              placeholder="Sélectionner"
              styleClass="w-full">
            </p-dropdown>
          </div>
        </p-card>

        <!-- Contact & Address -->
        <p-card header="Coordonnées" styleClass="form-card">
          <div class="form-group">
            <label for="street">Adresse <span class="required">*</span></label>
            <input 
              pInputText 
              id="street" 
              formControlName="street"
              class="w-full"
              [class.ng-invalid]="isInvalid('street')">
          </div>

          <div class="form-group">
            <label for="streetLine2">Complément d'adresse</label>
            <input 
              pInputText 
              id="streetLine2" 
              formControlName="streetLine2"
              class="w-full">
          </div>

          <div class="form-row">
            <div class="form-group">
              <label for="postalCode">Code postal</label>
              <p-inputMask
                id="postalCode"
                formControlName="postalCode"
                mask="9999"
                styleClass="w-full">
              </p-inputMask>
            </div>

            <div class="form-group">
              <label for="city">Ville <span class="required">*</span></label>
              <input 
                pInputText 
                id="city" 
                formControlName="city"
                class="w-full">
            </div>
          </div>

          <div class="form-group">
            <label for="governorate">Gouvernorat <span class="required">*</span></label>
            <p-dropdown 
              id="governorate"
              [options]="governorates" 
              formControlName="governorate"
              placeholder="Sélectionner"
              [filter]="true"
              styleClass="w-full">
            </p-dropdown>
          </div>

          <p-divider></p-divider>

          <div class="form-row">
            <div class="form-group">
              <label for="email">Email <span class="required">*</span></label>
              <input 
                pInputText 
                id="email" 
                type="email"
                formControlName="email"
                class="w-full">
            </div>

            <div class="form-group">
              <label for="phone">Téléphone <span class="required">*</span></label>
              <p-inputMask
                id="phone"
                formControlName="phone"
                mask="99 999 999"
                styleClass="w-full">
              </p-inputMask>
            </div>
          </div>

          <div class="form-group">
            <label for="website">Site web</label>
            <input 
              pInputText 
              id="website" 
              formControlName="website"
              placeholder="https://www.exemple.tn"
              class="w-full">
          </div>
        </p-card>

        <!-- Bank Information -->
        <p-card header="Informations bancaires" styleClass="form-card bank-card">
          <div class="form-group">
            <label for="bankName">Nom de la banque</label>
            <input 
              pInputText 
              id="bankName" 
              formControlName="bankName"
              placeholder="Ex: Banque Internationale Arabe de Tunisie"
              class="w-full">
          </div>

          <div class="form-group">
            <label for="rib">RIB</label>
            <p-inputMask
              id="rib"
              formControlName="rib"
              mask="99 999 9999999999999 99"
              placeholder="XX XXX XXXXXXXXXXXXX XX"
              styleClass="w-full">
            </p-inputMask>
            <small class="form-hint">Relevé d'Identité Bancaire à 20 chiffres</small>
          </div>

          <div class="form-group">
            <label for="iban">IBAN</label>
            <input 
              pInputText 
              id="iban" 
              formControlName="iban"
              placeholder="TN59 XXXX XXXX XXXX XXXX XXXX"
              class="w-full">
          </div>
        </p-card>

        <!-- Invoice Settings -->
        <p-card header="Paramètres de facturation" styleClass="form-card invoice-card">
          <div class="form-group numbering-link">
            <label>Numérotation des documents</label>
            <p class="form-hint">
              Configurez le format et le début de numérotation de vos factures et autres documents.
            </p>
            <a routerLink="/settings/numbering" class="numbering-settings-link">
              <i class="pi pi-sort-numeric-down"></i>
              Ouvrir les numérotations
            </a>
          </div>

          <div class="form-group">
            <label for="defaultPaymentTerms">Conditions de paiement par défaut</label>
            <textarea 
              pInputTextarea 
              id="defaultPaymentTerms" 
              formControlName="defaultPaymentTerms"
              placeholder="Ex: Paiement à 30 jours"
              [rows]="2"
              class="w-full">
            </textarea>
          </div>

          <div class="form-group">
            <label for="invoiceFooter">Pied de page des factures</label>
            <textarea 
              pInputTextarea 
              id="invoiceFooter" 
              formControlName="invoiceFooter"
              placeholder="Texte qui apparaîtra en bas de chaque facture"
              [rows]="3"
              class="w-full">
            </textarea>
          </div>
        </p-card>

        <!-- Stock Configuration -->
        <p-card header="Configuration du Stock" styleClass="form-card stock-card">
          <div class="form-group">
            <label for="warehouseName">Nom de l'entrepôt principal</label>
            <input 
              pInputText 
              id="warehouseName" 
              formControlName="warehouseName"
              placeholder="Entrepôt Principal"
              class="w-full">
            <small class="form-hint">Ce nom sera utilisé pour votre entrepôt par défaut.</small>
            <small class="form-hint warehouse-link">
              Pour gérer plusieurs entrepôts ou leurs adresses,
              <a routerLink="/settings/warehouses">ouvrez la gestion des entrepôts</a>.
            </small>
          </div>
        </p-card>
      </div>

      <!-- Actions -->
      <div class="form-actions">
        <p-button 
          label="Annuler" 
          icon="pi pi-times" 
          [outlined]="true"
          routerLink="/settings">
        </p-button>
        <p-button 
          type="submit" 
          label="Enregistrer" 
          icon="pi pi-check"
          [loading]="saving()"
          [disabled]="form.invalid || saving()">
        </p-button>
      </div>
    </form>
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

    .bank-card {
      grid-column: 1;
    }

    .invoice-card {
      grid-column: 2;

      @media (max-width: 1024px) {
        grid-column: 1;
      }
    }

    .numbering-link {
      margin-bottom: var(--spacing-4);
    }

    .numbering-settings-link {
      display: inline-flex;
      align-items: center;
      gap: var(--spacing-2);
      margin-top: var(--spacing-2);
      color: var(--color-primary-600);
      font-weight: var(--font-weight-medium);
      text-decoration: none;

      &:hover {
        text-decoration: underline;
      }
    }

    .stock-card {
      grid-column: 1 / span 2;
    }

    .logo-section {
      display: flex;
      align-items: center;
      gap: var(--spacing-4);
      margin-bottom: var(--spacing-4);
    }

    .logo-preview {
      display: flex;
      align-items: center;
      justify-content: center;
      width: 100px;
      height: 100px;
      background: var(--color-neutral-100);
      border: 2px dashed var(--color-neutral-300);
      border-radius: var(--radius-lg);
      overflow: hidden;

      img {
        width: 100%;
        height: 100%;
        object-fit: contain;
      }

      i {
        font-size: 2rem;
        color: var(--color-neutral-400);
      }
    }

    .logo-actions {
      display: flex;
      flex-direction: column;
      gap: var(--spacing-2);

      small {
        color: var(--color-neutral-500);
        font-size: var(--font-size-xs);
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
      margin-bottom: var(--spacing-4);

      &:last-child {
        margin-bottom: 0;
      }

      label {
        display: block;
        margin-bottom: var(--spacing-2);
        font-weight: var(--font-weight-medium);
        color: var(--color-neutral-700);
      }

      .required {
        color: var(--color-error-500);
      }
    }

    .form-hint {
      color: var(--color-neutral-500);
      font-size: var(--font-size-sm);
      margin-top: var(--spacing-1);
      display: block;
    }

    .warehouse-link a {
      color: var(--color-primary-600);
      text-decoration: underline;
      font-weight: var(--font-weight-medium);
    }

    .form-actions {
      display: flex;
      justify-content: flex-end;
      gap: var(--spacing-3);
      margin-top: var(--spacing-6);
      padding-top: var(--spacing-6);
      border-top: 1px solid var(--color-neutral-200);
    }

    .loading-container {
      display: flex;
      flex-direction: column;
      align-items: center;
      justify-content: center;
      padding: var(--spacing-8);
      gap: var(--spacing-4);
      color: var(--color-neutral-600);

      p {
        margin: 0;
      }
    }

    .error-banner {
      display: flex;
      align-items: center;
      justify-content: space-between;
      padding: var(--spacing-4);
      margin-bottom: var(--spacing-4);
      background: var(--color-error-50);
      border: 1px solid var(--color-error-200);
      border-radius: var(--radius-md);
      border-left: 4px solid var(--color-error-500);
      animation: slideIn 0.3s ease-out;

      .error-content {
        display: flex;
        align-items: center;
        gap: var(--spacing-3);
        flex: 1;

        .error-icon {
          color: var(--color-error-500);
          font-size: 1.5rem;
        }

        .error-text {
          flex: 1;

          strong {
            display: block;
            color: var(--color-error-700);
            margin-bottom: var(--spacing-1);
          }

          p {
            margin: 0;
            color: var(--color-error-600);
            font-size: var(--font-size-sm);
          }
        }
      }
    }

    @keyframes slideIn {
      from {
        opacity: 0;
        transform: translateY(-10px);
      }
      to {
        opacity: 1;
        transform: translateY(0);
      }
    }

    :host ::ng-deep {
      .form-card {
        .p-card-header {
          padding: var(--spacing-4) var(--spacing-5);
          border-bottom: 1px solid var(--color-neutral-200);
          font-weight: var(--font-weight-semibold);
        }

        .p-card-body {
          padding: var(--spacing-5);
        }
      }

      .p-inputmask,
      .p-dropdown {
        width: 100%;
      }
    }
  `]
})
export class CompanyComponent implements OnInit, OnDestroy {
  private fb = inject(FormBuilder);
  private toastService = inject(ToastService);
  private companyService = inject(CompanyService);
  private stockService = inject(StockService);
  private errorHandler = inject(ErrorHandlerService);
  errorMessageService = inject(ErrorMessageService);

  breadcrumbItems: BreadcrumbItem[] = [
    { label: 'Tableau de bord', route: '/dashboard', icon: 'pi-home' },
    { label: 'Paramètres', route: '/settings' },
    { label: 'Mon entreprise' }
  ];

  saving = signal(false);
  loading = signal(false);
  logoUrl = signal<string | null>(null);
  errorMessage = signal<string | null>(null);
  isRateLimited = signal(false);
  defaultWarehouseId = signal<string | null>(null);

  // Protection against multiple simultaneous requests
  private isLoadingInProgress = false;
  private lastToastTime = 0;
  private readonly ERROR_DEBOUNCE_MS = 2000; // 2 seconds between error toasts

  // Subject pour nettoyer les subscriptions
  private destroy$ = new Subject<void>();

  taxRegimes = [
    { label: 'Régime réel', value: 0 },
    { label: 'Régime forfaitaire', value: 1 },
    { label: 'Exonéré', value: 2 }
  ];

  governorates = TUNISIAN_GOVERNORATE_OPTIONS;

  form: FormGroup = this.fb.group({
    companyName: ['', Validators.required],
    tradeName: [''],
    nif: ['', Validators.required],
    commerceRegistry: [''],
    taxRegime: [0, Validators.required],
    street: ['', Validators.required],
    streetLine2: [''],
    postalCode: [''],
    city: ['', Validators.required],
    governorate: ['', Validators.required],
    email: ['', [Validators.required, Validators.email]],
    phone: ['', Validators.required],
    website: [''],
    bankName: [''],
    rib: [''],
    iban: [''],
    defaultPaymentTerms: ['Paiement à 30 jours'],
    invoiceFooter: [''],
    warehouseName: ['']
  });

  ngOnInit(): void {
    this.loadCompanyData();
  }

  ngOnDestroy(): void {
    // Nettoyer toutes les subscriptions
    this.destroy$.next();
    this.destroy$.complete();
  }

  isInvalid(field: string): boolean {
    const control = this.form.get(field);
    return !!(control?.invalid && control?.touched);
  }

  onLogoSelect(event: any): void {
    const file = event.files[0];
    if (file) {
      const reader = new FileReader();
      reader.onload = (e) => {
        this.logoUrl.set(e.target?.result as string);
      };
      reader.readAsDataURL(file);
    }
  }

  /**
   * Nettoie et normalise le NIF au blur (aligné sur register.component).
   * Ex: 1234567A/B/C/000 → 1234567/A/B/C/000
   */
  onNifBlur(): void {
    const nifControl = this.form.get('nif');
    if (nifControl) {
      const currentValue = nifControl.value || '';
      const cleaned = this.cleanNifValue(currentValue);
      if (cleaned !== currentValue) {
        nifControl.setValue(cleaned, { emitEvent: false });
        nifControl.updateValueAndValidity({ emitEvent: false });
        nifControl.markAsTouched();
      } else if (cleaned) {
        nifControl.updateValueAndValidity({ emitEvent: false });
      }
    }
  }

  private cleanNifValue(value: string | null | undefined): string {
    if (!value) return '';
    let cleaned = value
      .replace(/_/g, '')
      .replace(/\s/g, '')
      .replace(/[\u2044\u2215\/]/g, '/')
      .toUpperCase()
      .trim();
    if (!cleaned || cleaned === '/////' || cleaned === '///') return '';
    const noSlashMatch = cleaned.match(/^(\d{7})([A-Z])([A-Z])([A-Z])(\d{3})$/);
    if (noSlashMatch) {
      return `${noSlashMatch[1]}/${noSlashMatch[2]}/${noSlashMatch[3]}/${noSlashMatch[4]}/${noSlashMatch[5]}`;
    }
    const missingFirstSlash = cleaned.match(/^(\d{7})([A-Z])\/([A-Z])\/([A-Z])\/(\d{3})$/);
    if (missingFirstSlash) {
      return `${missingFirstSlash[1]}/${missingFirstSlash[2]}/${missingFirstSlash[3]}/${missingFirstSlash[4]}/${missingFirstSlash[5]}`;
    }
    return cleaned;
  }

  loadCompanyData(): void {
    // Prevent multiple simultaneous requests
    if (this.isLoadingInProgress) {
      return;
    }

    this.isLoadingInProgress = true;
    this.loading.set(true);
    this.errorMessage.set(null);
    this.isRateLimited.set(false);

    this.companyService.getCompany().pipe(
      takeUntil(this.destroy$)
    ).subscribe({
      next: (response) => {
        if (response.success && response.data) {
          const company = response.data;

          // Format phone number for display (add spaces)
          const phoneFormatted = company.phone.length === 8
            ? `${company.phone.substring(0, 2)} ${company.phone.substring(2, 5)} ${company.phone.substring(5)}`
            : company.phone;

          this.form.patchValue({
            companyName: company.companyName,
            tradeName: company.tradeName || '',
            nif: company.nif,
            commerceRegistry: company.commerceRegistry || '',
            taxRegime: company.taxRegime,
            street: company.address.street,
            streetLine2: company.address.streetLine2 || '',
            postalCode: company.address.postalCode || '',
            city: company.address.city,
            governorate: company.address.governorate,
            email: company.email,
            phone: phoneFormatted,
            website: company.website || '',
            bankName: company.bankName || '',
            rib: company.rib || '',
            iban: company.iban || '',
            defaultPaymentTerms: company.defaultPaymentTerms || 'Paiement à 30 jours',
            invoiceFooter: company.invoiceFooter || ''
          });

          const warehouseName = company.warehouseName?.trim();
          if (warehouseName) {
            this.form.patchValue({
              warehouseName
            });
          }

          if (company.logoUrl) {
            this.logoUrl.set(company.logoUrl);
          }
        } else {
          const errorMsg = response.errors?.join(', ') || 'Impossible de charger les informations de l\'entreprise';
          this.errorMessage.set(errorMsg);
          this.showErrorToast('Erreur', errorMsg);
        }
        this.isLoadingInProgress = false;
        this.loading.set(false);
      },
      error: (err: HttpErrorResponse) => {
        this.isLoadingInProgress = false;
        this.loading.set(false);

        // Handle rate limiting specifically
        // Note: Le service gère déjà les retries automatiques (3 tentatives avec backoff exponentiel)
        // On ne fait pas de retry supplémentaire ici pour éviter les boucles infinies
        if (err.status === 429) {
          this.isRateLimited.set(true);
          const errorMsg = 'Trop de requêtes. Veuillez patienter avant de réessayer.';
          this.errorMessage.set(errorMsg);
          // Ne pas afficher de toast ici car l'intercepteur le gère déjà
          // Le composant affiche juste le message inline
        } else {
          const errorMessage = this.errorHandler.extractErrorMessage(err) ||
            'Une erreur est survenue lors du chargement des informations';
          this.errorMessage.set(errorMessage);
          this.showErrorToast('Erreur', errorMessage);
        }
        this.errorHandler.logError('Failed to load company data', err);
      }
    });

    // Load default warehouse
    this.stockService.getWarehouses(true).pipe(
      takeUntil(this.destroy$)
    ).subscribe({
      next: (response) => {
        if (response.success && response.data) {
          const defaultWarehouse = response.data.find(w => w.isDefault);
          if (defaultWarehouse) {
            this.defaultWarehouseId.set(defaultWarehouse.id);
            this.form.patchValue({
              warehouseName: defaultWarehouse.name
            });
          }
        }
      },
      error: (err) => {
        console.error('Failed to load warehouses', err);
      }
    });
  }

  private showErrorToast(summary: string, detail: string): void {
    const now = Date.now();
    // Debounce toast notifications to prevent spamming
    if (now - this.lastToastTime < this.ERROR_DEBOUNCE_MS) {
      return;
    }
    this.lastToastTime = now;

    this.toastService.add({
      severity: 'error',
      summary,
      detail,
      life: 5000
    });
  }

  onSubmit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.saving.set(true);

    const formValue = this.form.value;

    // Remove spaces from phone number (backend expects 8 digits without spaces)
    const phoneCleaned = formValue.phone?.replace(/\s/g, '') || '';

    // Normaliser le NIF avant envoi (format 1234567/A/B/C/000)
    const nifCleaned = this.cleanNifValue(formValue.nif ?? this.form.get('nif')?.value ?? '') || (formValue.nif || '').trim();

    const request: UpdateCompanyRequest = {
      companyName: (formValue.companyName || '').trim(),
      tradeName: formValue.tradeName?.trim() || null,
      nif: nifCleaned,
      commerceRegistry: formValue.commerceRegistry?.trim() || null,
      taxRegime: formValue.taxRegime ?? 0,
      street: (formValue.street || '').trim(),
      streetLine2: formValue.streetLine2?.trim() || null,
      city: (formValue.city || '').trim(),
      postalCode: formValue.postalCode?.trim() || null,
      governorate: typeof formValue.governorate === 'string'
        ? formValue.governorate.trim()
        : (formValue.governorate?.value || '').trim(),
      email: (formValue.email || '').trim(),
      phone: phoneCleaned,
      website: formValue.website?.trim() || null,
      logoUrl: this.logoUrl() || null,
      bankName: formValue.bankName?.trim() || null,
      rib: formValue.rib?.trim() || null,
      iban: formValue.iban?.trim() || null,
      defaultPaymentTerms: formValue.defaultPaymentTerms?.trim() || null,
      invoiceFooter: formValue.invoiceFooter?.trim() || null
    };

    this.companyService.updateCompany(request).pipe(
      takeUntil(this.destroy$)
    ).subscribe({
      next: (response) => {
        if (response.success) {
          this.toastService.add({
            severity: 'success',
            summary: 'Succès',
            detail: response.message || 'Informations de l\'entreprise mises à jour',
            life: 3000
          });

          // Invalider le cache pour forcer un refresh lors du prochain chargement
          this.companyService.invalidateCache();

          // Cela évite une requête supplémentaire qui pourrait déclencher un rate limit

          // Update warehouse if changed
          const warehouseName = formValue.warehouseName?.trim();
          const defaultId = this.defaultWarehouseId();

          if (defaultId && warehouseName) {
            this.stockService.updateWarehouse(defaultId, {
              id: defaultId,
              name: warehouseName,
              isDefault: true
            }).pipe(
              takeUntil(this.destroy$)
            ).subscribe({
              error: (err) => {
                console.error('Failed to update warehouse', err);
                this.showErrorToast('Attention', 'Entreprise mise à jour mais erreur lors de la mise à jour de l\'entrepôt');
              }
            });
          }

          this.saving.set(false);
        } else {
          const errorMsg = response.errors?.join(', ') || 'Une erreur est survenue lors de la mise à jour';
          this.errorMessage.set(errorMsg);
          this.showErrorToast('Erreur', errorMsg);
        }
        this.saving.set(false);
      },
      error: (err: HttpErrorResponse) => {
        this.saving.set(false);

        // Handle rate limiting specifically
        // Note: Le service gère déjà les retries automatiques (3 tentatives avec backoff exponentiel)
        // On ne fait pas de retry supplémentaire ici pour éviter les boucles infinies
        if (err.status === 429) {
          this.isRateLimited.set(true);
          const errorMsg = 'Trop de requêtes. Veuillez patienter avant de réessayer.';
          this.errorMessage.set(errorMsg);
          // Ne pas afficher de toast ici car l'intercepteur le gère déjà
          // Le composant affiche juste le message inline
        } else {
          const errorMessage = this.errorHandler.extractErrorMessage(err) ||
            'Une erreur est survenue lors de la mise à jour';
          this.errorMessage.set(errorMessage);
          this.showErrorToast('Erreur', errorMessage);
        }
        this.errorHandler.logError('Failed to update company', err);
      }
    });
  }
}
