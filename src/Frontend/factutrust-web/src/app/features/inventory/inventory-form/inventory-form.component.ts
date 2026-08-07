import {
    Component,
    OnInit,
    inject,
    signal,
    computed,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';

import {
    InventoryService,
    InventoryType,
    ActiveInventoryDto,
    InventoryProductItem,
} from '@core/services/inventory.service';
import { StockService, Warehouse } from '@core/services/stock.service';
import { ToastService } from '@core/services/toast.service';
import { ErrorHandlerService } from '@core/services/error-handler.service';

import { SelectModule } from 'primeng/select';
import { InputNumberModule } from 'primeng/inputnumber';
import { Textarea } from 'primeng/textarea';
import { ButtonModule } from 'primeng/button';
import { RippleModule } from 'primeng/ripple';

import { PageHeaderComponent } from '@shared/components/page-header/page-header.component';
import { BreadcrumbComponent, BreadcrumbItem } from '@shared/components/breadcrumb/breadcrumb.component';
import { FormSectionComponent } from '@shared/components/form-section/form-section.component';
import { ButtonComponent } from '@shared/components/button/button.component';

/** Line with local count and last saved value for dirty detection */
export interface InventoryFormLine extends InventoryProductItem {
    /** User input (counted quantity) */
    countedInput: number;
    /** Last value successfully saved to API */
    lastSavedCount: number | null;
}

@Component({
    selector: 'app-inventory-form',
    standalone: true,
    imports: [
        CommonModule,
        FormsModule,
        RouterModule,
        SelectModule,
        InputNumberModule,
        Textarea,
        ButtonModule,
        RippleModule,
        PageHeaderComponent,
        BreadcrumbComponent,
        FormSectionComponent,
        ButtonComponent,
    ],
    templateUrl: './inventory-form.component.html',
    styleUrl: './inventory-form.component.scss',
})
export class InventoryFormComponent implements OnInit {
    private inventoryService = inject(InventoryService);
    private stockService = inject(StockService);
    private toastService = inject(ToastService);
    private errorHandler = inject(ErrorHandlerService);
    private router = inject(Router);
    private route = inject(ActivatedRoute);

    loading = signal(true);
    starting = signal(false);
    saving = signal(false);
    validating = signal(false);
    cancelling = signal(false);

    activeInventory = signal<ActiveInventoryDto | null>(null);
    warehouses: Warehouse[] = [];
    selectedWarehouse = signal<Warehouse | null>(null);
    notes = signal('');

    /** Editable lines derived from active inventory (countedInput + lastSavedCount) */
    lines = signal<InventoryFormLine[]>([]);

    lastSavedAt = signal<Date | null>(null);

    breadcrumbItems: BreadcrumbItem[] = [
        { label: 'Tableau de bord', route: '/dashboard', icon: 'pi-home' },
        { label: 'Mon Stock', route: '/stock' },
        { label: 'Inventaire' },
    ];

    hasActiveInventory = computed(() => this.activeInventory() !== null);

    /** Whether any line has unsaved count */
    hasDirtyLines = computed(() =>
        this.lines().some(
            (line) =>
                line.lastSavedCount === null
                    ? line.countedInput !== line.theoreticalQuantity
                    : line.countedInput !== line.lastSavedCount
        )
    );

    /** All products have a counted value entered (for validation button) */
    canValidate = computed(() => {
        const inv = this.activeInventory();
        if (!inv) return false;
        return inv.products.every((p) => p.isCounted);
    });

    documentDate = computed(() => {
        const inv = this.activeInventory();
        if (inv?.startedAt) {
            return new Date(inv.startedAt);
        }
        return new Date();
    });

    ngOnInit(): void {
        this.loadWarehouses();
    }

    loadWarehouses(): void {
        this.stockService.getWarehouses(true).subscribe({
            next: (res) => {
                if (res.success && res.data?.length) {
                    this.warehouses = res.data;
                    const warehouseIdFromRoute = this.route.snapshot.queryParamMap.get('warehouseId');
                    const targetWh = warehouseIdFromRoute
                        ? res.data.find((w) => w.id === warehouseIdFromRoute)
                        : null;
                    const defaultWh = targetWh ?? res.data.find((w) => w.isDefault) ?? res.data[0];
                    this.selectedWarehouse.set(defaultWh);
                    this.loadActiveInventory(defaultWh.id);
                } else {
                    this.loading.set(false);
                }
            },
            error: (err) => {
                this.errorHandler.logError('Failed warehouses', err);
                this.loading.set(false);
                this.toastService.add({
                    severity: 'error',
                    summary: 'Erreur',
                    detail: this.errorHandler.extractErrorMessage(err) ?? 'Impossible de charger les entrepôts',
                });
            },
        });
    }

    onWarehouseChange(warehouse: Warehouse | null): void {
        this.selectedWarehouse.set(warehouse ?? null);
        if (warehouse) {
            this.loadActiveInventory(warehouse.id);
        } else {
            this.activeInventory.set(null);
            this.lines.set([]);
            this.loading.set(false);
        }
    }

    loadActiveInventory(warehouseId: string): void {
        this.loading.set(true);
        this.inventoryService.getActiveInventory(warehouseId).subscribe({
            next: (res) => {
                if (res.success && res.data) {
                    this.activeInventory.set(res.data);
                    this.buildLines(res.data);
                } else {
                    this.activeInventory.set(null);
                    this.lines.set([]);
                }
                this.loading.set(false);
            },
            error: () => {
                this.loading.set(false);
                this.toastService.add({
                    severity: 'error',
                    summary: 'Erreur',
                    detail: 'Impossible de charger l\'inventaire actif',
                });
            },
        });
    }

    private buildLines(data: ActiveInventoryDto): void {
        const newLines: InventoryFormLine[] = data.products.map((p) => ({
            ...p,
            countedInput:
                p.countedQuantity !== null ? p.countedQuantity : p.theoreticalQuantity,
            lastSavedCount: p.countedQuantity,
        }));
        this.lines.set(newLines);
    }

    startInventory(): void {
        const warehouse = this.selectedWarehouse();
        if (!warehouse) {
            this.toastService.add({
                severity: 'warn',
                summary: 'Attention',
                detail: 'Veuillez sélectionner un entrepôt',
            });
            return;
        }

        this.starting.set(true);
        this.inventoryService
            .startInventory({
                type: InventoryType.Complete,
                warehouseId: warehouse.id,
                notes: this.notes()?.trim() || undefined,
            })
            .subscribe({
                next: (res) => {
                    if (res.success && res.data) {
                        this.toastService.add({
                            severity: 'success',
                            summary: 'Inventaire démarré',
                            detail: res.data.humanMessage,
                        });
                        this.loadActiveInventory(warehouse.id);
                    } else {
                        this.toastService.add({
                            severity: 'error',
                            summary: 'Erreur',
                            detail: res.errors?.join(', ') ?? 'Impossible de démarrer l\'inventaire',
                        });
                    }
                    this.starting.set(false);
                },
                error: (err) => {
                    const msg = this.errorHandler.extractErrorMessage(err);
                    this.toastService.add({
                        severity: 'error',
                        summary: 'Erreur',
                        detail: msg ?? 'Impossible de démarrer l\'inventaire',
                    });
                    this.errorHandler.logError('Start inventory failed', err);
                    this.starting.set(false);
                },
            });
    }

    updateLineCount(productId: string, value: number): void {
        this.lines.update((list) =>
            list.map((line) =>
                line.productId === productId
                    ? { ...line, countedInput: value }
                    : line
            )
        );
    }

    saveCounts(): void {
        const inv = this.activeInventory();
        const list = this.lines();
        if (!inv) return;

        const toSave = list.filter((line) => {
            if (line.lastSavedCount === null) {
                return line.countedInput !== line.theoreticalQuantity;
            }
            return line.countedInput !== line.lastSavedCount;
        });

        if (toSave.length === 0) {
            this.toastService.add({
                severity: 'info',
                summary: 'Rien à enregistrer',
                detail: 'Aucune modification à sauvegarder',
            });
            return;
        }

        this.saving.set(true);
        const queue = [...toSave];
        let index = 0;

        const runNext = (): void => {
            if (index >= queue.length) {
                this.saving.set(false);
                this.lastSavedAt.set(new Date());
                this.toastService.add({
                    severity: 'success',
                    summary: 'Enregistrement terminé',
                    detail: `${queue.length} comptage(s) enregistré(s). Dernière sauvegarde à ${new Date().toLocaleTimeString('fr-FR', { hour: '2-digit', minute: '2-digit' })}`,
                });
                this.loadActiveInventory(inv.warehouseId);
                return;
            }

            const line = queue[index];
            this.inventoryService
                .recordCount(inv.inventoryId, {
                    productId: line.productId,
                    countedQuantity: line.countedInput,
                })
                .subscribe({
                    next: () => {
                        index++;
                        runNext();
                    },
                    error: (err) => {
                        this.saving.set(false);
                        this.toastService.add({
                            severity: 'error',
                            summary: 'Erreur d\'enregistrement',
                            detail:
                                this.errorHandler.extractErrorMessage(err) ??
                                'Impossible d\'enregistrer le comptage',
                        });
                        this.errorHandler.logError('Record count failed', err);
                    },
                });
        };

        runNext();
    }

    validateInventory(): void {
        const inv = this.activeInventory();
        if (!inv || !this.canValidate()) return;

        this.validating.set(true);
        this.inventoryService.validateInventory(inv.inventoryId).subscribe({
            next: (res) => {
                if (res.success && res.data) {
                    this.toastService.add({
                        severity: 'success',
                        summary: 'Inventaire validé',
                        detail: res.data.humanMessage,
                    });
                    this.activeInventory.set(null);
                    this.lines.set([]);
                    this.router.navigate(['/inventory'], { replaceUrl: true });
                }
                this.validating.set(false);
            },
            error: (err) => {
                this.validating.set(false);
                this.toastService.add({
                    severity: 'error',
                    summary: 'Erreur',
                    detail:
                        this.errorHandler.extractErrorMessage(err) ??
                        'Impossible de valider l\'inventaire',
                });
                this.errorHandler.logError('Validate inventory failed', err);
            },
        });
    }

    cancelInventory(): void {
        const inv = this.activeInventory();
        if (!inv) return;

        this.cancelling.set(true);
        this.inventoryService.cancelInventory(inv.inventoryId).subscribe({
            next: (res) => {
                if (res.success && res.data) {
                    this.toastService.add({
                        severity: 'info',
                        summary: 'Inventaire annulé',
                        detail: res.data.humanMessage,
                    });
                    this.activeInventory.set(null);
                    this.lines.set([]);
                }
                this.cancelling.set(false);
            },
            error: (err) => {
                this.cancelling.set(false);
                this.toastService.add({
                    severity: 'error',
                    summary: 'Erreur',
                    detail:
                        this.errorHandler.extractErrorMessage(err) ??
                        'Impossible d\'annuler l\'inventaire',
                });
                this.errorHandler.logError('Cancel inventory failed', err);
            },
        });
    }

    goToStock(): void {
        this.router.navigate(['/stock']);
    }
}
