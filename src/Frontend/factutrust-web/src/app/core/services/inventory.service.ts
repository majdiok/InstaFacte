import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '@environments/environment';
import { ApiResponse } from './auth.service';

// --- Models ---

export enum InventoryType {
    Complete = 1,
    Partial = 2
}

export enum InventoryStatus {
    InProgress = 1,
    Validated = 2,
    Cancelled = 3
}

export interface InventoryProductItem {
    productId: string;
    productName: string;
    productCode: string | null;
    theoreticalQuantity: number;
    isCounted: boolean;
    countedQuantity: number | null;
    productLotId?: string | null;
    lotNumber?: string | null;
}

export interface ActiveInventoryDto {
    inventoryId: string;
    warehouseId: string;
    warehouseName: string;
    startedAt: string;
    type: InventoryType;
    typeLabel: string;
    totalProducts: number;
    countedProducts: number;
    remainingProducts: number;
    progressPercent: number;
    progressMessage: string;
    products: InventoryProductItem[];
}

export interface InventorySummaryLineDto {
    productId: string;
    productName: string;
    productCode: string | null;
    theoreticalQuantity: number;
    countedQuantity: number | null;
    difference: number;
    humanMessage: string;
    differenceClass: 'positive' | 'negative' | 'neutral';
    productLotId?: string | null;
    lotNumber?: string | null;
}

export interface InventorySummaryDto {
    inventoryId: string;
    totalProducts: number;
    countedProducts: number;
    productsOk: number;
    productsWithDifference: number;
    productsNotCounted: number;
    canValidate: boolean;
    statusMessage: string;
    productsOkList: InventorySummaryLineDto[];
    productsWithDifferenceList: InventorySummaryLineDto[];
    productsNotCountedList: InventorySummaryLineDto[];
}

export interface StartInventoryRequest {
    type: InventoryType;
    warehouseId?: string;
    productIds?: string[];
    notes?: string;
}

export interface StartInventoryResult {
    inventoryId: string;
    totalProducts: number;
    humanMessage: string;
}

export interface RecordCountRequest {
    productId: string;
    countedQuantity: number;
    productLotId?: string | null;
}

export interface RecordCountResult {
    productId: string;
    productName: string;
    previousQuantity: number;
    countedQuantity: number;
    difference: number;
    humanMessage: string;
}

export interface InventoryPendingCount {
    productId: string;
    countedQuantity: number;
    productLotId?: string | null;
}

export interface ValidateInventoryRequest {
    pendingCounts?: InventoryPendingCount[];
}

export interface ValidateInventoryResult {
    inventoryId: string;
    productsAdjusted: number;
    humanMessage: string;
}

export interface CancelInventoryResult {
    inventoryId: string;
    humanMessage: string;
}

/** DTO liste (historique). */
export interface PhysicalInventoryListDto {
    id: string;
    reference: string;
    startedAt: string;
    completedAt: string | null;
    warehouseId: string;
    warehouseName: string;
    type: InventoryType;
    typeLabel: string;
    status: InventoryStatus;
    statusDisplay: string;
    totalProducts: number;
    countedProducts: number;
    progressPercent: number;
}

/** Ligne du détail d'un inventaire. */
export interface PhysicalInventoryDetailLineDto {
    productId: string;
    productCode: string | null;
    productName: string;
    theoreticalQuantity: number;
    countedQuantity: number | null;
    difference: number;
    isCounted: boolean;
    productLotId?: string | null;
    lotNumber?: string | null;
}

/** DTO détail (consultation). */
export interface PhysicalInventoryDetailDto {
    id: string;
    reference: string;
    startedAt: string;
    completedAt: string | null;
    warehouseId: string;
    warehouseName: string;
    type: InventoryType;
    typeLabel: string;
    status: InventoryStatus;
    statusDisplay: string;
    notes: string | null;
    totalProducts: number;
    countedProducts: number;
    lines: PhysicalInventoryDetailLineDto[];
}

export interface PagedResult<T> {
    items: T[];
    page: number;
    pageSize: number;
    totalCount: number;
    totalPages: number;
}

export interface InventorySearchParams {
    search?: string;
    status?: InventoryStatus;
    fromDate?: string;
    toDate?: string;
    warehouseId?: string;
    page?: number;
    pageSize?: number;
}

/** Totaux agrégés (backend) de la liste des inventaires, sur l'ensemble filtré complet. */
export interface InventoryListSummary {
    count: number;
    inProgressCount: number;
    validatedCount: number;
    cancelledCount: number;
    totalProducts: number;
}

@Injectable({
    providedIn: 'root'
})
export class InventoryService {
    private readonly API_URL = `${environment.apiUrl}/inventory`;
    private http = inject(HttpClient);

    /**
     * Démarre un nouvel inventaire physique.
     */
    startInventory(request: StartInventoryRequest): Observable<ApiResponse<StartInventoryResult>> {
        return this.http.post<ApiResponse<StartInventoryResult>>(`${this.API_URL}/start`, request);
    }

    /**
     * Récupère l'inventaire actif (en cours) s'il existe.
     */
    getActiveInventory(warehouseId?: string): Observable<ApiResponse<ActiveInventoryDto | null>> {
        let params = new HttpParams();
        if (warehouseId) {
            params = params.set('warehouseId', warehouseId);
        }
        return this.http.get<ApiResponse<ActiveInventoryDto | null>>(`${this.API_URL}/active`, { params });
    }

    /**
     * Enregistre le comptage d'un produit.
     */
    recordCount(inventoryId: string, request: RecordCountRequest): Observable<ApiResponse<RecordCountResult>> {
        return this.http.post<ApiResponse<RecordCountResult>>(
            `${this.API_URL}/${inventoryId}/count`,
            request
        );
    }

    /**
     * Récupère le résumé pédagogique de l'inventaire avant validation.
     */
    getSummary(inventoryId: string): Observable<ApiResponse<InventorySummaryDto>> {
        return this.http.get<ApiResponse<InventorySummaryDto>>(`${this.API_URL}/${inventoryId}/summary`);
    }

    /**
     * Valide l'inventaire et applique les ajustements de stock.
     */
    validateInventory(
        inventoryId: string,
        request: ValidateInventoryRequest = {}
    ): Observable<ApiResponse<ValidateInventoryResult>> {
        return this.http.post<ApiResponse<ValidateInventoryResult>>(
            `${this.API_URL}/${inventoryId}/validate`,
            request
        );
    }

    /**
     * Annule l'inventaire en cours sans modifier le stock.
     */
    cancelInventory(inventoryId: string): Observable<ApiResponse<CancelInventoryResult>> {
        return this.http.post<ApiResponse<CancelInventoryResult>>(
            `${this.API_URL}/${inventoryId}/cancel`,
            {}
        );
    }

    /**
     * Liste paginée des inventaires avec filtres (historique).
     */
    getInventories(params: InventorySearchParams = {}): Observable<ApiResponse<PagedResult<PhysicalInventoryListDto>>> {
        let httpParams = new HttpParams();
        if (params.search != null) httpParams = httpParams.set('search', params.search);
        if (params.status != null) httpParams = httpParams.set('status', String(params.status));
        if (params.fromDate != null) httpParams = httpParams.set('fromDate', params.fromDate);
        if (params.toDate != null) httpParams = httpParams.set('toDate', params.toDate);
        if (params.warehouseId != null) httpParams = httpParams.set('warehouseId', params.warehouseId);
        if (params.page != null) httpParams = httpParams.set('page', String(params.page));
        if (params.pageSize != null) httpParams = httpParams.set('pageSize', String(params.pageSize));
        return this.http.get<ApiResponse<PagedResult<PhysicalInventoryListDto>>>(this.API_URL, { params: httpParams });
    }

    /** Totaux agrégés respectant les mêmes filtres que {@link getInventories} (calcul backend). */
    getInventoriesSummary(params: InventorySearchParams = {}): Observable<ApiResponse<InventoryListSummary>> {
        let httpParams = new HttpParams();
        if (params.search != null) httpParams = httpParams.set('search', params.search);
        if (params.status != null) httpParams = httpParams.set('status', String(params.status));
        if (params.fromDate != null) httpParams = httpParams.set('fromDate', params.fromDate);
        if (params.toDate != null) httpParams = httpParams.set('toDate', params.toDate);
        if (params.warehouseId != null) httpParams = httpParams.set('warehouseId', params.warehouseId);
        return this.http.get<ApiResponse<InventoryListSummary>>(`${this.API_URL}/list-summary`, { params: httpParams });
    }

    /**
     * Récupère un inventaire par id (consultation).
     */
    getInventoryById(id: string): Observable<ApiResponse<PhysicalInventoryDetailDto>> {
        return this.http.get<ApiResponse<PhysicalInventoryDetailDto>>(`${this.API_URL}/${id}`);
    }
}
