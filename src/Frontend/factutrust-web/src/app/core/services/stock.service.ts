import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { createHttpContextSkipGlobalErrorUi } from '@core/http-context';
import { Observable } from 'rxjs';
import { map } from 'rxjs/operators';
import { environment } from '@environments/environment';
import { ApiResponse } from './auth.service';

// --- Models ---

export interface Warehouse {
    id: string;
    code: string;
    name: string;
    address: string | null;
    isDefault: boolean;
    isActive: boolean;
}

export interface UpdateWarehouseRequest {
    id: string;
    name: string;
    address?: string | null;
    isDefault: boolean;
}

export interface CreateWarehouseRequest {
    code: string;
    name: string;
    address?: string | null;
    isDefault: boolean;
}

export interface StockItem {
    id: string;
    productId: string;
    productCode: string;
    productName: string;
    warehouseId: string;
    warehouseCode: string;
    warehouseName: string;
    quantityOnHand: number;
    quantityAvailable: number;
    quantityReserved: number;
    minimumStock: number;
    maximumStock: number | null;
    averageCost: number;
    stockValue: number;
    isLowStock: boolean;
    isOutOfStock: boolean;
}

export interface StockMovement {
    id: string;
    stockItemId: string;
    type: string; // Entry, Exit, Adjustment
    reason: string;
    quantity: number;
    unitCost: number;
    balanceAfter: number;
    reference: string | null;
    notes: string | null;
    occurredAt: string;
}

export interface StockMovementDto {
    id: string;
    type: string;
    reason: string;
    quantity: number;
    unitCost: number;
    totalCost: number;
    balanceAfter: number;
    reference: string | null;
    notes: string | null;
    occurredAt: string;
}

export interface StockMovementsResult {
    items: StockMovementDto[];
    totalCount: number;
    page: number;
    pageSize: number;
    totalPages: number;
}

export interface StockAlert {
    stockItemId: string;
    productId: string;
    productCode: string;
    productName: string;
    warehouseId: string;
    warehouseName: string;
    quantityOnHand: number;
    minimumStock: number;
    deficit: number;
}

export interface StockAlertsResult {
    lowStockItems: StockAlert[];
    outOfStockItems: StockAlert[];
    totalAlerts: number;
}

export interface StockItemsResult {
    items: StockItem[];
    totalCount: number;
    page: number;
    pageSize: number;
}

export interface RecordEntryRequest {
    productId: string;
    warehouseId?: string;
    quantity: number;
    unitCost: number;
    reason: number; // MovementReason enum value
    reference?: string;
    notes?: string;
}

export interface RecordExitRequest {
    productId: string;
    warehouseId?: string;
    quantity: number;
    reason: number; // MovementReason enum value
    reference?: string;
    notes?: string;
}

export interface AdjustStockRequest {
    productId: string;
    warehouseId?: string;
    newQuantity: number;
    notes?: string;
}

// --- Enums ---

export enum MovementType {
    Entry = 1,
    Exit = 2,
    Adjustment = 3
}

export enum MovementReason {
    // Entries
    Purchase = 1,
    CustomerReturn = 3,
    InitialStock = 8,

    // Exits
    Sale = 2,
    SupplierReturn = 4,
    Damage = 6,

    // Both
    Transfer = 7,
    InventoryAdjustment = 5
}

@Injectable({
    providedIn: 'root'
})
export class StockService {
    private readonly API_URL = `${environment.apiUrl}/stock`;
    private http = inject(HttpClient);

    // --- Stock Items ---

    getStockItems(
        warehouseId?: string,
        lowStockOnly?: boolean,
        outOfStockOnly?: boolean,
        page: number = 1,
        pageSize: number = 20
    ): Observable<ApiResponse<StockItemsResult>> {
        let params = new HttpParams()
            .set('page', page.toString())
            .set('pageSize', pageSize.toString());

        if (warehouseId) params = params.set('warehouseId', warehouseId);
        if (lowStockOnly) params = params.set('lowStockOnly', 'true');
        if (outOfStockOnly) params = params.set('outOfStockOnly', 'true');

        return this.http.get<ApiResponse<StockItemsResult>>(`${this.API_URL}/items`, { params });
    }

    getStockAlerts(
        warehouseId?: string,
        options?: { skipGlobalErrorUi?: boolean }
    ): Observable<ApiResponse<StockAlertsResult>> {
        let params = new HttpParams();
        if (warehouseId) params = params.set('warehouseId', warehouseId);

        const skipGlobalErrorUi = options?.skipGlobalErrorUi === true;
        const httpOpts: { params: HttpParams; context?: ReturnType<typeof createHttpContextSkipGlobalErrorUi> } = {
            params
        };
        if (skipGlobalErrorUi) {
            httpOpts.context = createHttpContextSkipGlobalErrorUi();
        }
        return this.http.get<ApiResponse<StockAlertsResult>>(`${this.API_URL}/alerts`, httpOpts);
    }

    // --- Warehouses ---

    getWarehouses(activeOnly: boolean = true): Observable<ApiResponse<Warehouse[]>> {
        const params = new HttpParams().set('activeOnly', activeOnly.toString());
        return this.http
            .get<ApiResponse<Warehouse[]> | Warehouse[]>(`${this.API_URL}/warehouses`, { params })
            .pipe(
                map((response) => {
                    if (Array.isArray(response)) {
                        return {
                            success: true,
                            data: response,
                            message: null,
                            errors: []
                        } as ApiResponse<Warehouse[]>;
                    }

                    if (response && typeof response === 'object' && 'data' in response) {
                        return response as ApiResponse<Warehouse[]>;
                    }

                    return {
                        success: false,
                        data: [],
                        message: 'Réponse invalide du serveur',
                        errors: ['Réponse invalide du serveur']
                    } as ApiResponse<Warehouse[]>;
                })
            );
    }

    createWarehouse(request: CreateWarehouseRequest): Observable<ApiResponse<string>> {
        return this.http.post<ApiResponse<string>>(`${this.API_URL}/warehouses`, request);
    }

    updateWarehouse(id: string, request: UpdateWarehouseRequest): Observable<ApiResponse<void>> {
        return this.http.put<ApiResponse<void>>(`${this.API_URL}/warehouses/${id}`, request);
    }

    // --- Operations ---

    recordEntry(request: RecordEntryRequest): Observable<ApiResponse<string>> {
        return this.http.post<ApiResponse<string>>(`${this.API_URL}/entry`, request);
    }

    recordExit(
        request: RecordExitRequest,
        options?: { skipGlobalErrorUi?: boolean }
    ): Observable<ApiResponse<void>> {
        const skipGlobalErrorUi = options?.skipGlobalErrorUi === true;
        const httpOpts: { context?: ReturnType<typeof createHttpContextSkipGlobalErrorUi> } = {};
        if (skipGlobalErrorUi) {
            httpOpts.context = createHttpContextSkipGlobalErrorUi();
        }
        return this.http.post<ApiResponse<void>>(`${this.API_URL}/exit`, request, httpOpts);
    }

    adjustStock(request: AdjustStockRequest): Observable<ApiResponse<void>> {
        return this.http.post<ApiResponse<void>>(`${this.API_URL}/adjust`, request);
    }

    // --- Movements History ---

    getStockMovements(
        stockItemId: string,
        page: number = 1,
        pageSize: number = 20
    ): Observable<ApiResponse<StockMovementsResult>> {
        const params = new HttpParams()
            .set('page', page.toString())
            .set('pageSize', pageSize.toString());

        return this.http.get<ApiResponse<StockMovementsResult>>(
            `${this.API_URL}/${stockItemId}/movements`,
            { params }
        );
    }

    // =====================================================
    // VUE SIMPLIFIÉE (pour utilisateurs non techniciens)
    // =====================================================

    /**
     * Obtient la vue d'ensemble simplifiée du stock avec statuts visuels.
     */
    getSimpleOverview(
        search?: string,
        alertsOnly: boolean = false,
        warehouseId?: string
    ): Observable<ApiResponse<SimpleStockOverview>> {
        let params = new HttpParams();
        if (search) params = params.set('search', search);
        if (alertsOnly) params = params.set('alertsOnly', 'true');
        if (warehouseId) params = params.set('warehouseId', warehouseId);

        return this.http.get<ApiResponse<SimpleStockOverview>>(`${this.API_URL}/overview`, { params });
    }

    /**
     * "Compter mon stock" - Comptage rapide ultra-simplifié.
     * L'utilisateur indique combien il a, le système fait le reste.
     */
    quickCount(request: QuickCountRequest): Observable<ApiResponse<QuickCountResult>> {
        return this.http.post<ApiResponse<QuickCountResult>>(`${this.API_URL}/quick-count`, request);
    }

    /**
     * Vérifie la disponibilité du stock avant validation de facture.
     */
    checkAvailability(
        items: ProductQuantityCheck[],
        warehouseId?: string
    ): Observable<ApiResponse<StockAvailabilityCheck>> {
        const body: { items: ProductQuantityCheck[]; warehouseId?: string } = { items };
        if (warehouseId) body.warehouseId = warehouseId;
        return this.http.post<ApiResponse<StockAvailabilityCheck>>(
            `${this.API_URL}/check-availability`,
            body
        );
    }
}

// =====================================================
// INTERFACES SIMPLIFIÉES (langage humain)
// =====================================================

export type StockStatus = 'InStock' | 'RunningLow' | 'OutOfStock';

export interface SimpleStockItem {
    productId: string;
    productName: string;
    productCode: string;
    imageUrl: string | null;
    quantityAvailable: number;
    status: StockStatus;
    statusLabel: string;     // "En stock", "Presque fini", "Rupture"
    statusIcon: string;      // "🟢", "🟠", "🔴"
    minimumThreshold: number | null;
    alertMessage: string | null;
}

export interface SimpleStockOverview {
    totalProducts: number;
    productsInStock: number;      // 🟢
    productsRunningLow: number;   // 🟠
    productsOutOfStock: number;   // 🔴
    items: SimpleStockItem[];
    alerts: SimpleStockAlert[];
}

export interface SimpleStockAlert {
    productId: string;
    productName: string;
    message: string;
    severity: 'warning' | 'danger';
    actionLabel: string;
    actionRoute: string;
}

export interface QuickCountRequest {
    productId: string;
    actualQuantity: number;
    warehouseId?: string;
    notes?: string;
}

export interface QuickCountResult {
    productId: string;
    productName: string;
    previousQuantity: number;
    newQuantity: number;
    difference: number;
    humanMessage: string;         // Message pédagogique
    isNowLowStock: boolean;
    alertMessage: string | null;
}

export interface ProductQuantityCheck {
    productId: string;
    requestedQuantity: number;
}

export interface StockAvailabilityCheck {
    allAvailable: boolean;
    insufficientCount: number;
    details: ProductAvailabilityDetail[];
    summaryMessage: string;
}

export interface ProductAvailabilityDetail {
    productId: string;
    productName: string;
    productCode: string;
    requestedQuantity: number;
    availableQuantity: number;
    remainingAfterSale: number;
    isAvailable: boolean;
    isStockManaged: boolean;
    humanMessage: string;
    alertLevel: 'ok' | 'warning' | 'insufficient';
}
