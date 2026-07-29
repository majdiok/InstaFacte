import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams, HttpContext } from '@angular/common/http';
import { SKIP_ERROR_TOAST } from '@core/http-context';
import { Observable } from 'rxjs';
import { map } from 'rxjs/operators';
import { environment } from '@environments/environment';
import { ApiResponse } from './auth.service';

export interface Product {
  id: string;
  code: string;
  name: string;
  description: string | null;
  /** Product type display: Produit, Service */
  typeDisplay: string;
  /** Product category ID */
  categoryId: string;
  /** Product category name for display */
  category: string;
  unitPrice: number;
  purchasePrice?: number | null;
  lastPurchasePrice?: number | null;
  weightedAverageCost?: number | null;
  profitMarginPercent?: number | null;
  salePriceTtc?: number;
  unit: string;
  vatRate: number;
  isFodecApplicable?: boolean;
  isDiscountEnabled?: boolean;
  maxDiscountPercent?: number | null;
  isActive: boolean;
  isStockManaged: boolean;
  /** URL of product image (uploaded or from external search) */
  imageUrl?: string | null;
  /** Fournisseur préféré (réapprovisionnement) ; null = aucun. */
  preferredSupplierId?: string | null;
  createdAt?: string;
  updatedAt?: string | null;
}

export interface ProductListItem {
  id: string;
  code: string;
  name: string;
  description: string | null;
  /** Product type display: Produit, Service */
  typeDisplay: string;
  /** Product category ID */
  categoryId: string;
  /** Product category name for display */
  category: string;
  unitPrice: number;
  purchasePrice?: number | null;
  lastPurchasePrice?: number | null;
  weightedAverageCost?: number | null;
  profitMarginPercent?: number | null;
  salePriceTtc?: number;
  unit: string;
  vatRate: number;
  isFodecApplicable?: boolean;
  isDiscountEnabled?: boolean;
  maxDiscountPercent?: number | null;
  isActive: boolean;
  isStockManaged: boolean;
  /** URL of product image from external search (Unsplash/Google) */
  imageUrl?: string | null;
  /** Quantité disponible (entrepôt par défaut) ; null si pas de gestion de stock ou indisponible côté API */
  quantityAvailable?: number | null;
}

export interface ProductSearchParams {
  search?: string;
  /** Filter by product type: Produit, Service */
  category?: string;
  /** Filter by product category (POS). Sent as categoryId to API. */
  productCategoryId?: string;
  isActive?: boolean;
  page?: number;
  pageSize?: number;
  sortBy?: string;
  sortOrder?: 'asc' | 'desc';
  /** When set, stock quantities reflect this warehouse (session context). */
  warehouseId?: string;
}

export interface CreateProductRequest {
  code: string;
  name: string;
  description?: string | null;
  /** Product type: Produit, Service (maps to backend Type enum) */
  category: string;
  /** Product category ID (Guid). Uses default if not provided */
  productCategoryId?: string;
  unitPrice: number;
  purchasePrice?: number | null;
  profitMarginPercent?: number | null;
  unit: string;
  vatRate: number;
  isStockManaged: boolean;
  isFodecApplicable?: boolean;
  isDiscountEnabled?: boolean;
  maxDiscountPercent?: number | null;
  /** Fournisseur préféré (optionnel) ; null/undefined = aucun. */
  preferredSupplierId?: string | null;
}

export interface UpdateProductRequest extends CreateProductRequest {
  isActive: boolean;
}

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  hasNextPage: boolean;
  hasPreviousPage: boolean;
}

@Injectable({
  providedIn: 'root'
})
export class ProductService {
  private readonly API_URL = `${environment.apiUrl}/products`;
  private http = inject(HttpClient);

  getProducts(params: ProductSearchParams = {}): Observable<ApiResponse<PagedResult<ProductListItem>>> {
    let httpParams = new HttpParams();
    
    if (params.search) httpParams = httpParams.set('search', params.search);
    
    // Map category string to ProductType enum value
    // Frontend sends: "Produit" (0), "Service" (1), "Abonnement" (not in enum, treat as null)
    if (params.category) {
      let typeValue: number | null = null;
      if (params.category === 'Produit') {
        typeValue = 0; // ProductType.Product
      } else if (params.category === 'Service') {
        typeValue = 1; // ProductType.Service
      }
      // "Abonnement" is not in ProductType enum, so we don't set type filter
      if (typeValue !== null) {
        httpParams = httpParams.set('type', typeValue.toString());
      }
    }
    
    if (params.productCategoryId) httpParams = httpParams.set('categoryId', params.productCategoryId);
    if (params.isActive !== undefined) httpParams = httpParams.set('isActive', params.isActive.toString());
    if (params.page) httpParams = httpParams.set('page', params.page.toString());
    if (params.pageSize) httpParams = httpParams.set('pageSize', params.pageSize.toString());
    if (params.sortBy) httpParams = httpParams.set('sortBy', params.sortBy);
    if (params.sortOrder) httpParams = httpParams.set('sortOrder', params.sortOrder);
    if (params.warehouseId) httpParams = httpParams.set('warehouseId', params.warehouseId);

    return this.http.get<ApiResponse<PagedResult<any>>>(this.API_URL, { params: httpParams }).pipe(
      map(response => {
        if (!response.success || !response.data) {
          return response as ApiResponse<PagedResult<ProductListItem>>;
        }

        // Map backend response to frontend format
        const mappedItems = response.data.items.map((item: any) => ({
          id: item.id,
          code: item.code,
          name: item.name,
          description: item.description || null,
          typeDisplay: this.mapTypeToCategory(item.type),
          categoryId: item.categoryId ?? '',
          category: item.categoryName ?? 'general',
          unitPrice: item.unitPrice,
          purchasePrice: item.purchasePrice ?? null,
          lastPurchasePrice: item.lastPurchasePrice ?? null,
          weightedAverageCost: item.weightedAverageCost ?? null,
          profitMarginPercent: item.profitMarginPercent ?? null,
          salePriceTtc: item.salePriceTtc,
          unit: item.unit || 'Unité',
          vatRate: item.vatRatePercent || item.vatRate,
          isFodecApplicable: item.isFodecApplicable ?? false,
          isDiscountEnabled: item.isDiscountEnabled ?? false,
          maxDiscountPercent: item.maxDiscountPercent ?? null,
          isActive: item.isActive,
          isStockManaged: item.isStockManaged ?? false,
          imageUrl: item.imageUrl ?? null,
          quantityAvailable:
            item.isStockManaged && item.quantityAvailable != null
              ? Number(item.quantityAvailable)
              : null
        }));

        return {
          ...response,
          data: {
            ...response.data,
            items: mappedItems
          }
        } as ApiResponse<PagedResult<ProductListItem>>;
      })
    );
  }

  private mapTypeToCategory(type: string | number): string {
    // Backend returns ProductType enum as string: "Product" or "Service"
    // Or as number: 0 = Product, 1 = Service
    if (type === 0 || type === '0' || type === 'Product' || type === 'PRODUCT') {
      return 'Produit';
    } else if (type === 1 || type === '1' || type === 'Service' || type === 'SERVICE') {
      return 'Service';
    }
    // Default fallback
    return 'Service';
  }

  getProduct(id: string): Observable<ApiResponse<Product>> {
    return this.http.get<ApiResponse<any>>(`${this.API_URL}/${id}`).pipe(
      map(response => {
        if (!response.success || !response.data) {
          return response as ApiResponse<Product>;
        }

        // Map backend response to frontend format
        const product: Product = {
          id: response.data.id,
          code: response.data.code,
          name: response.data.name,
          description: response.data.description || null,
          typeDisplay: this.mapTypeToCategory(response.data.type),
          categoryId: response.data.categoryId ?? '',
          category: response.data.categoryName ?? 'general',
          unitPrice: response.data.unitPrice,
          purchasePrice: response.data.purchasePrice ?? null,
          lastPurchasePrice: response.data.lastPurchasePrice ?? null,
          weightedAverageCost: response.data.weightedAverageCost ?? null,
          profitMarginPercent: response.data.profitMarginPercent ?? null,
          salePriceTtc: response.data.salePriceTtc,
          unit: response.data.unit || 'Unité',
          vatRate: response.data.vatRatePercent || response.data.vatRate,
          isFodecApplicable: response.data.isFodecApplicable ?? false,
          isDiscountEnabled: response.data.isDiscountEnabled ?? false,
          maxDiscountPercent: response.data.maxDiscountPercent ?? null,
          isActive: response.data.isActive,
          isStockManaged: response.data.isStockManaged ?? false,
          imageUrl: response.data.imageUrl ?? null,
          preferredSupplierId: response.data.preferredSupplierId ?? null,
          createdAt: response.data.createdAt,
          updatedAt: response.data.updatedAt
        };

        return {
          ...response,
          data: product
        } as ApiResponse<Product>;
      })
    );
  }

  /**
   * Recherche un article par code-barres, en correspondance EXACTE côté serveur.
   *
   * Renvoie 404 si aucun article ne porte ce code — jamais un article approchant. C'est la
   * différence de fond avec l'ancien scan, qui interrogeait le code produit interne et
   * retombait sur une correspondance partielle, au risque d'encaisser le mauvais article.
   */
  getProductByBarcode(barcode: string): Observable<ApiResponse<ProductListItem>> {
    const encoded = encodeURIComponent(barcode.trim());
    return this.http
      .get<ApiResponse<any>>(`${this.API_URL}/by-barcode/${encoded}`, {
        // Un code inconnu est un cas nominal en caisse : le service le traite lui-même,
        // sans faire surgir de bandeau d'erreur global au caissier.
        context: new HttpContext().set(SKIP_ERROR_TOAST, true)
      })
      .pipe(
        map(response => {
          if (!response.success || !response.data) {
            return response as ApiResponse<ProductListItem>;
          }

          const d = response.data;
          const item: ProductListItem = {
            id: d.id,
            code: d.code,
            name: d.name,
            description: d.description ?? null,
            typeDisplay: this.mapTypeToCategory(d.type),
            categoryId: d.categoryId ?? '',
            category: d.categoryName ?? 'general',
            unitPrice: d.unitPrice,
            purchasePrice: d.purchasePrice ?? null,
            lastPurchasePrice: d.lastPurchasePrice ?? null,
            weightedAverageCost: d.weightedAverageCost ?? null,
            profitMarginPercent: d.profitMarginPercent ?? null,
            salePriceTtc: d.salePriceTtc,
            unit: d.unit || 'Unité',
            vatRate: d.vatRatePercent ?? d.vatRate,
            isFodecApplicable: d.isFodecApplicable ?? false,
            isDiscountEnabled: d.isDiscountEnabled ?? false,
            maxDiscountPercent: d.maxDiscountPercent ?? null,
            isActive: d.isActive,
            isStockManaged: d.isStockManaged ?? false,
            imageUrl: d.imageUrl ?? null
          };

          return { ...response, data: item } as ApiResponse<ProductListItem>;
        })
      );
  }

  createProduct(request: CreateProductRequest): Observable<ApiResponse<string>> {
    // Map category string to ProductType enum
    // Frontend: "Produit" (0), "Service" (1), "Abonnement" (treat as Service for now)
    let type: number;
    if (request.category === 'Produit') {
      type = 0; // ProductType.Product
    } else {
      // Service or Abonnement -> treat as Service
      type = 1; // ProductType.Service
    }

    const backendRequest = {
      code: request.code,
      name: request.name,
      description: request.description,
      type: type,
      categoryId: request.productCategoryId,
      unitPrice: request.unitPrice,
      purchasePrice: request.purchasePrice ?? undefined,
      profitMarginPercent: request.profitMarginPercent ?? undefined,
      vatRate: request.vatRate, // Backend expects VatRate enum value (0, 7, 13, 19)
      unit: request.unit,
      isStockManaged: request.isStockManaged,
      isFodecApplicable: request.isFodecApplicable ?? false,
      isDiscountEnabled: request.isDiscountEnabled ?? false,
      maxDiscountPercent: request.isDiscountEnabled ? request.maxDiscountPercent ?? undefined : undefined,
      preferredSupplierId: request.preferredSupplierId ?? undefined
    };

    return this.http.post<ApiResponse<string>>(this.API_URL, backendRequest);
  }

  updateProduct(id: string, request: UpdateProductRequest): Observable<ApiResponse<Product>> {
    const backendRequest = {
      name: request.name,
      description: request.description,
      unitPrice: request.unitPrice,
      purchasePrice: request.purchasePrice ?? undefined,
      profitMarginPercent: request.profitMarginPercent ?? undefined,
      vatRate: request.vatRate, // Backend expects VatRate enum value (0, 7, 13, 19)
      unit: request.unit,
      isActive: request.isActive,
      isStockManaged: request.isStockManaged,
      isFodecApplicable: request.isFodecApplicable ?? false,
      isDiscountEnabled: request.isDiscountEnabled ?? false,
      maxDiscountPercent: request.isDiscountEnabled ? request.maxDiscountPercent ?? undefined : undefined,
      categoryId: request.productCategoryId,
      preferredSupplierId: request.preferredSupplierId ?? null
    };

    return this.http.put<ApiResponse<Product>>(`${this.API_URL}/${id}`, backendRequest);
  }

  deleteProduct(id: string): Observable<ApiResponse<object | null>> {
    const context = new HttpContext().set(SKIP_ERROR_TOAST, true);
    return this.http.delete<ApiResponse<object | null>>(`${this.API_URL}/${id}`, { context });
  }

  toggleActive(id: string): Observable<ApiResponse<Product>> {
    return this.http.patch<ApiResponse<Product>>(`${this.API_URL}/${id}/toggle-active`, {});
  }

  /**
   * Resolves relative product image URLs to absolute using the API origin.
   * Leaves data:, http:, and https: URLs unchanged.
   */
  resolveProductImageUrl(url: string | null | undefined): string | null {
    if (url == null || url === '') return null;
    if (url.startsWith('data:') || url.startsWith('http://') || url.startsWith('https://')) return url;
    try {
      const origin = new URL(environment.apiUrl).origin;
      return origin + (url.startsWith('/') ? url : '/' + url);
    } catch {
      return url;
    }
  }

  /**
   * Gets the product image URL. If the product has no cached image, the backend searches
   * external APIs (Unsplash, Google) and caches the result.
   */
  getProductImageUrl(productId: string): Observable<ApiResponse<string | null>> {
    return this.http.get<ApiResponse<string | null>>(`${this.API_URL}/${productId}/image-url`);
  }

  /**
   * Uploads a product image. Allowed: JPEG, PNG, WebP; max 2 MB.
   * Returns the absolute image URL for immediate display.
   */
  uploadProductImage(productId: string, file: File): Observable<ApiResponse<string>> {
    const formData = new FormData();
    formData.append('file', file, file.name);
    return this.http.post<ApiResponse<string>>(`${this.API_URL}/${productId}/image`, formData);
  }

  /**
   * Removes the product image.
   */
  deleteProductImage(productId: string): Observable<ApiResponse<object | null>> {
    return this.http.delete<ApiResponse<object | null>>(`${this.API_URL}/${productId}/image`);
  }
}
