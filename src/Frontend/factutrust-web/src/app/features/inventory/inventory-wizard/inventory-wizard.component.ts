import { Component, OnInit, inject, signal, computed, effect, untracked } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule, Router } from '@angular/router';

// Services
import {
    InventoryService,
    ActiveInventoryDto,
    InventorySummaryDto,
    InventoryType,
    RecordCountResult
} from '@core/services/inventory.service';
import { StockService, SimpleStockItem } from '@core/services/stock.service';

// PrimeNG
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { InputNumberModule } from 'primeng/inputnumber';
import { ToastModule } from 'primeng/toast';
import { MessageService } from 'primeng/api';
import { ProgressSpinnerModule } from 'primeng/progressspinner';
import { RippleModule } from 'primeng/ripple';
import { BadgeModule } from 'primeng/badge';
import { ProgressBarModule } from 'primeng/progressbar';
import { CardModule } from 'primeng/card';
import { RadioButtonModule } from 'primeng/radiobutton';
import { CheckboxModule } from 'primeng/checkbox';
import { InputTextModule } from 'primeng/inputtext';

// Shared Components
import { PageHeaderComponent } from '@shared/components/page-header/page-header.component';
import { BreadcrumbComponent, BreadcrumbItem } from '@shared/components/breadcrumb/breadcrumb.component';

type WizardStep = 'start' | 'count' | 'summary' | 'success';

@Component({
    selector: 'app-inventory-wizard',
    standalone: true,
    imports: [
        CommonModule,
        FormsModule,
        RouterModule,
        ButtonModule,
        DialogModule,
        InputNumberModule,
        ToastModule,
        ProgressSpinnerModule,
        RippleModule,
        BadgeModule,
        ProgressBarModule,
        CardModule,
        RadioButtonModule,
        CheckboxModule,
        InputTextModule,
        PageHeaderComponent,
        BreadcrumbComponent
    ],
    templateUrl: './inventory-wizard.component.html',
    styleUrl: './inventory-wizard.component.scss'
})
export class InventoryWizardComponent implements OnInit {
    private inventoryService = inject(InventoryService);
    private stockService = inject(StockService);
    private messageService = inject(MessageService);
    private router = inject(Router);

    // State
    step = signal<WizardStep>('start');
    loading = signal(true);
    submitting = signal(false);

    // Data
    activeInventory = signal<ActiveInventoryDto | null>(null);
    inventorySummary = signal<InventorySummaryDto | null>(null);

    // Start step
    inventoryType = signal<InventoryType>(InventoryType.Complete);
    availableProducts = signal<SimpleStockItem[]>([]);
    selectedProductIds = signal<Set<string>>(new Set());
    productSearchTerm = signal('');
    loadingProducts = signal(false);

    // Count step
    currentProductIndex = signal(0);
    countedQuantity = signal(0);
    lastCountResult = signal<RecordCountResult | null>(null);

    // Computed
    currentProduct = computed(() => {
        const inventory = this.activeInventory();
        const index = this.currentProductIndex();
        if (!inventory || index >= inventory.products.length) return null;
        return inventory.products[index];
    });

    progress = computed(() => {
        const inventory = this.activeInventory();
        if (!inventory) return 0;
        return inventory.progressPercent;
    });

    filteredProducts = computed(() => {
        const products = this.availableProducts();
        const search = this.productSearchTerm().toLowerCase().trim();
        if (!search) return products;
        return products.filter(p =>
            p.productName.toLowerCase().includes(search) ||
            p.productCode.toLowerCase().includes(search)
        );
    });

    canStartPartialInventory = computed(() => {
        return this.inventoryType() !== InventoryType.Partial ||
            this.selectedProductIds().size > 0;
    });

    // Breadcrumb
    breadcrumbItems: BreadcrumbItem[] = [
        { label: 'Tableau de bord', route: '/dashboard', icon: 'pi-home' },
        { label: 'Mon Stock', route: '/stock' },
        { label: 'Inventaire physique' }
    ];

    constructor() {
        // Watch for inventory type changes to load products when partial is selected
        effect(() => {
            const isPartial = this.inventoryType() === InventoryType.Partial;
            const hasNoProducts = this.availableProducts().length === 0;

            if (isPartial && hasNoProducts) {
                // Use untracked to safely call method that writes to signals
                untracked(() => this.loadAvailableProducts());
            }
        });
    }

    ngOnInit(): void {
        this.checkActiveInventory();
    }

    checkActiveInventory(): void {
        this.loading.set(true);
        this.inventoryService.getActiveInventory().subscribe({
            next: (response) => {
                if (response.success && response.data) {
                    this.activeInventory.set(response.data);
                    this.step.set('count');
                    this.findNextUncountedProduct();
                }
                this.loading.set(false);
            },
            error: () => {
                this.loading.set(false);
                this.messageService.add({
                    severity: 'error',
                    summary: 'Erreur',
                    detail: 'Impossible de charger les inventaires'
                });
            }
        });
    }

    // --- START STEP ---

    loadAvailableProducts(): void {
        this.loadingProducts.set(true);
        this.stockService.getSimpleOverview().subscribe({
            next: (response) => {
                if (response.success && response.data) {
                    this.availableProducts.set(response.data.items);
                }
                this.loadingProducts.set(false);
            },
            error: () => {
                this.loadingProducts.set(false);
                this.messageService.add({
                    severity: 'error',
                    summary: 'Erreur',
                    detail: 'Impossible de charger les produits'
                });
            }
        });
    }

    toggleProductSelection(productId: string): void {
        const current = new Set(this.selectedProductIds());
        if (current.has(productId)) {
            current.delete(productId);
        } else {
            current.add(productId);
        }
        this.selectedProductIds.set(current);
    }

    isProductSelected(productId: string): boolean {
        return this.selectedProductIds().has(productId);
    }

    selectAllProducts(): void {
        const allIds = new Set(this.filteredProducts().map(p => p.productId));
        this.selectedProductIds.set(allIds);
    }

    clearProductSelection(): void {
        this.selectedProductIds.set(new Set());
    }

    startInventory(): void {
        this.submitting.set(true);
        const request: any = {
            type: this.inventoryType()
        };

        // Add productIds for partial inventory
        if (this.inventoryType() === InventoryType.Partial) {
            request.productIds = Array.from(this.selectedProductIds());
        }

        this.inventoryService.startInventory(request).subscribe({
            next: (response) => {
                if (response.success && response.data) {
                    this.messageService.add({
                        severity: 'success',
                        summary: 'Inventaire lancé !',
                        detail: response.data.humanMessage
                    });
                    this.checkActiveInventory();
                }
                this.submitting.set(false);
            },
            error: (error) => {
                this.submitting.set(false);
                // Extract the actual error message from the backend response
                const errorMessage = error?.error?.message
                    || error?.error?.error
                    || error?.error?.globalErrors?.[0]
                    || 'Impossible de démarrer l\'inventaire';
                this.messageService.add({
                    severity: 'error',
                    summary: 'Erreur',
                    detail: errorMessage
                });
            }
        });
    }

    // --- COUNT STEP ---

    findNextUncountedProduct(): void {
        const inventory = this.activeInventory();
        if (!inventory) return;

        const index = inventory.products.findIndex(p => !p.isCounted);
        if (index !== -1) {
            this.currentProductIndex.set(index);
            const product = inventory.products[index];
            this.countedQuantity.set(product.theoreticalQuantity);
        }
    }

    navigateToProduct(index: number): void {
        this.currentProductIndex.set(index);
        const product = this.activeInventory()?.products[index];
        if (product) {
            this.countedQuantity.set(product.isCounted && product.countedQuantity !== null
                ? product.countedQuantity
                : product.theoreticalQuantity);
        }
    }

    submitCount(): void {
        const inventory = this.activeInventory();
        const product = this.currentProduct();
        if (!inventory || !product) return;

        this.submitting.set(true);
        this.inventoryService.recordCount(inventory.inventoryId, {
            productId: product.productId,
            countedQuantity: this.countedQuantity()
        }).subscribe({
            next: (response) => {
                if (response.success && response.data) {
                    this.lastCountResult.set(response.data);
                    this.messageService.add({
                        severity: 'success',
                        summary: 'Comptage enregistré',
                        detail: response.data.humanMessage
                    });
                    // Refresh and move to next
                    this.refreshAndMoveNext();
                }
                this.submitting.set(false);
            },
            error: () => {
                this.submitting.set(false);
                this.messageService.add({
                    severity: 'error',
                    summary: 'Erreur',
                    detail: 'Impossible d\'enregistrer le comptage'
                });
            }
        });
    }

    private refreshAndMoveNext(): void {
        this.inventoryService.getActiveInventory().subscribe({
            next: (response) => {
                if (response.success && response.data) {
                    this.activeInventory.set(response.data);

                    // Check if all products are counted
                    if (response.data.remainingProducts === 0) {
                        this.goToSummary();
                    } else {
                        this.findNextUncountedProduct();
                    }
                }
            }
        });
    }

    // --- SUMMARY STEP ---

    goToSummary(): void {
        const inventory = this.activeInventory();
        if (!inventory) return;

        this.loading.set(true);
        this.inventoryService.getSummary(inventory.inventoryId).subscribe({
            next: (response) => {
                if (response.success && response.data) {
                    this.inventorySummary.set(response.data);
                    this.step.set('summary');
                }
                this.loading.set(false);
            },
            error: () => {
                this.loading.set(false);
                this.messageService.add({
                    severity: 'error',
                    summary: 'Erreur',
                    detail: 'Impossible de charger le résumé'
                });
            }
        });
    }

    validateInventory(): void {
        const inventory = this.activeInventory();
        if (!inventory) return;

        this.submitting.set(true);
        this.inventoryService.validateInventory(inventory.inventoryId).subscribe({
            next: (response) => {
                if (response.success && response.data) {
                    this.messageService.add({
                        severity: 'success',
                        summary: 'Inventaire validé !',
                        detail: response.data.humanMessage
                    });
                    this.step.set('success');
                }
                this.submitting.set(false);
            },
            error: () => {
                this.submitting.set(false);
                this.messageService.add({
                    severity: 'error',
                    summary: 'Erreur',
                    detail: 'Impossible de valider l\'inventaire'
                });
            }
        });
    }

    cancelInventory(): void {
        const inventory = this.activeInventory();
        if (!inventory) return;

        this.submitting.set(true);
        this.inventoryService.cancelInventory(inventory.inventoryId).subscribe({
            next: (response) => {
                if (response.success && response.data) {
                    this.messageService.add({
                        severity: 'info',
                        summary: 'Inventaire annulé',
                        detail: response.data.humanMessage
                    });
                    this.activeInventory.set(null);
                    this.step.set('start');
                }
                this.submitting.set(false);
            },
            error: () => {
                this.submitting.set(false);
                this.messageService.add({
                    severity: 'error',
                    summary: 'Erreur',
                    detail: 'Impossible d\'annuler l\'inventaire'
                });
            }
        });
    }

    goToStock(): void {
        this.router.navigate(['/stock']);
    }

    goToList(): void {
        this.router.navigate(['/inventory'], { replaceUrl: true });
    }

    startNew(): void {
        this.activeInventory.set(null);
        this.inventorySummary.set(null);
        this.step.set('start');
    }
}
