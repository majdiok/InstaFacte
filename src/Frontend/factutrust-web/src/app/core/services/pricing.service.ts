import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '@environments/environment';
import { createHttpContextSkipGlobalErrorUi } from '@core/http-context';
import { ApiResponse } from './client.service';

/** Origine du prix retenu par le serveur. */
export type PriceSource = 'Catalog' | 'PriceList' | 'ClientPrice';

export interface ResolvedPrice {
  unitPriceHT: number;
  currency: string;
  source: PriceSource;
  /** Vrai si le prix vient d'une grille ou d'un accord client, et non du catalogue. */
  isNegotiated: boolean;
  promotionDiscountPercent?: number | null;
  promotionName?: string | null;
  promotionId?: string | null;
  promotionEligible?: boolean;
  promotionMinQuantityRequired?: number | null;
}

export interface ResolvedPriceLine extends ResolvedPrice {
  productId: string;
}

export interface ResolvePriceItem {
  productId: string;
  quantity: number;
}

export interface PriceListListItem {
  id: string;
  name: string;
  currency: string;
  isActive: boolean;
  validFrom: string | null;
  validUntil: string | null;
  itemCount: number;
  /** Une grille active mais hors periode n'alimente aucun prix. */
  isApplicableToday: boolean;
}

/** Palier quantitatif : a partir de minQuantity, le prix devient unitPriceHT. */
export interface PriceListTier {
  minQuantity: number;
  unitPriceHT: number;
}

export interface PriceListItem {
  productId: string;
  productCode: string;
  productName: string;
  /** Prix de base, applique en deca du premier palier. */
  unitPriceHT: number;
  currency: string;
  /** Prix catalogue, pour montrer l'ecart introduit par la grille. */
  catalogUnitPriceHT: number;
  /** Paliers degressifs, du seuil le plus bas au plus haut. */
  tiers: PriceListTier[];
}

export interface PriceListDetail {
  id: string;
  name: string;
  currency: string;
  isActive: boolean;
  validFrom: string | null;
  validUntil: string | null;
  isApplicableToday: boolean;
  assignedClientCount: number;
  items: PriceListItem[];
}

export interface ClientProductPrice {
  id: string;
  productId: string;
  productCode: string;
  productName: string;
  unitPriceHT: number;
  currency: string;
  catalogUnitPriceHT: number;
  isActive: boolean;
  validFrom: string | null;
  validUntil: string | null;
  isApplicableToday: boolean;
}

export interface ClientPricing {
  clientId: string;
  priceListId: string | null;
  priceListName: string | null;
  negotiatedPrices: ClientProductPrice[];
}

export interface ProductClientPrice {
  id: string;
  clientId: string;
  clientName: string;
  unitPriceHT: number;
  currency: string;
  catalogUnitPriceHT: number;
  isActive: boolean;
  validFrom: string | null;
  validUntil: string | null;
  isApplicableToday: boolean;
}

export interface ProductPricing {
  productId: string;
  productCode: string;
  productName: string;
  catalogUnitPriceHT: number;
  currency: string;
  clientPrices: ProductClientPrice[];
}

export interface CreatePriceListRequest {
  name: string;
  currency?: string | null;
  validFrom?: string | null;
  validUntil?: string | null;
}

export interface UpdatePriceListRequest {
  name: string;
  validFrom: string | null;
  validUntil: string | null;
  isActive: boolean;
}

export interface UpsertClientProductPriceRequest {
  unitPriceHT: number;
  validFrom: string | null;
  validUntil: string | null;
  isActive: boolean;
}

/**
 * Interroge le point de résolution de prix unique du serveur.
 *
 * A appeler avant d'afficher un prix de ligne : le serveur applique la priorite
 * prix negocie -> grille du client -> catalogue. Afficher le prix catalogue sans
 * demander laisserait l'ecran divergent de la facture finalement emise.
 */

export type PromotionDiscountType = 'Percentage' | 'Amount';

export interface Promotion {
  id: string;
  name: string;
  productId: string | null;
  productCategoryId: string | null;
  clientId: string | null;
  discountType: PromotionDiscountType;
  discountPercent: number | null;
  discountAmount: number | null;
  minQuantity: number;
  startsOn: string;
  endsOn: string;
  isActive: boolean;
  priority: number;
  /** Active ET dans sa fenetre : c'est ce qui compte, pas seulement l'activation. */
  isRunningToday: boolean;
  /** Portee lisible : « tous les produits · un client », etc. */
  scopeLabel: string;
}

export interface CreatePromotionRequest {
  name: string;
  startsOn: string;
  endsOn: string;
  discountType: PromotionDiscountType;
  discountPercent: number | null;
  discountAmount: number | null;
  productId: string | null;
  productCategoryId: string | null;
  clientId: string | null;
  minQuantity: number;
  priority: number;
}

export interface UpdatePromotionRequest {
  name: string;
  startsOn: string;
  endsOn: string;
  discountType: PromotionDiscountType;
  discountPercent: number | null;
  discountAmount: number | null;
  minQuantity: number;
  priority: number;
  isActive: boolean;
  productId: string | null;
  productCategoryId: string | null;
  clientId: string | null;
}

export type PaymentDueMode = 'NetDays' | 'EndOfMonth' | 'EndOfMonthOnDay';

export interface PaymentTermTemplate {
  id: string;
  name: string;
  delayDays: number;
  dueMode: PaymentDueMode;
  dueDayOfMonth: number | null;
  earlyPaymentDiscountPercent: number | null;
  earlyPaymentDays: number | null;
  isActive: boolean;
  isDefault: boolean;
  /** Libelle tel qu'il s'imprimera sur le document. */
  documentLabel: string;
  /** Echeance qu'aurait un document emis aujourd'hui — rend la regle tangible. */
  sampleDueDate: string;
}

export interface UpsertPaymentTermRequest {
  id: string | null;
  name: string;
  delayDays: number;
  dueMode: PaymentDueMode;
  dueDayOfMonth: number | null;
  earlyPaymentDiscountPercent: number | null;
  earlyPaymentDays: number | null;
  isActive: boolean;
  isDefault: boolean;
}

@Injectable({ providedIn: 'root' })
export class PricingService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/pricing`;

  /** Prix applicable pour un produit. `clientId` absent = vente sans client identifie. */
  resolve(
    productId: string,
    clientId: string | null,
    quantity = 1,
    date?: string
  ): Observable<ApiResponse<ResolvedPrice>> {
    let params = new HttpParams()
      .set('productId', productId)
      .set('quantity', String(quantity));

    if (clientId) {
      params = params.set('clientId', clientId);
    }
    if (date) {
      params = params.set('date', date);
    }

    return this.http.get<ApiResponse<ResolvedPrice>>(`${this.baseUrl}/resolve`, {
      params,
      context: createHttpContextSkipGlobalErrorUi()
    });
  }

  /**
   * Prix applicables pour plusieurs produits en une requete — utilise par la caisse pour
   * retarifer un ticket entier lorsqu'il est rattache a un client.
   */
  resolveBatch(
    items: ResolvePriceItem[],
    clientId: string | null,
    date?: string
  ): Observable<ApiResponse<ResolvedPriceLine[]>> {
    return this.http.post<ApiResponse<ResolvedPriceLine[]>>(
      `${this.baseUrl}/resolve-batch`,
      { clientId, items, date },
      { context: createHttpContextSkipGlobalErrorUi() }
    );
  }

  // ─────────────────────── Grilles tarifaires ───────────────────────

  getPriceLists(): Observable<ApiResponse<PriceListListItem[]>> {
    return this.http.get<ApiResponse<PriceListListItem[]>>(`${this.baseUrl}/price-lists`);
  }

  getPriceList(id: string): Observable<ApiResponse<PriceListDetail>> {
    return this.http.get<ApiResponse<PriceListDetail>>(`${this.baseUrl}/price-lists/${id}`);
  }

  createPriceList(request: CreatePriceListRequest): Observable<ApiResponse<string>> {
    return this.http.post<ApiResponse<string>>(`${this.baseUrl}/price-lists`, request);
  }

  updatePriceList(id: string, request: UpdatePriceListRequest): Observable<ApiResponse<object>> {
    return this.http.put<ApiResponse<object>>(`${this.baseUrl}/price-lists/${id}`, request);
  }

  /**
   * Supprime une grille. Le serveur refuse si elle est encore affectee a des clients :
   * ils retomberaient au catalogue sans que personne le voie.
   */
  deletePriceList(id: string): Observable<ApiResponse<object>> {
    return this.http.delete<ApiResponse<object>>(`${this.baseUrl}/price-lists/${id}`);
  }

  setPriceListItem(
    id: string,
    productId: string,
    unitPriceHT: number
  ): Observable<ApiResponse<object>> {
    return this.http.put<ApiResponse<object>>(
      `${this.baseUrl}/price-lists/${id}/items/${productId}`,
      { unitPriceHT }
    );
  }

  setPriceListTier(
    id: string,
    productId: string,
    minQuantity: number,
    unitPriceHT: number
  ): Observable<ApiResponse<object>> {
    return this.http.put<ApiResponse<object>>(
      `${this.baseUrl}/price-lists/${id}/items/${productId}/tiers/${minQuantity}`,
      { unitPriceHT }
    );
  }

  removePriceListTier(
    id: string,
    productId: string,
    minQuantity: number
  ): Observable<ApiResponse<object>> {
    return this.http.delete<ApiResponse<object>>(
      `${this.baseUrl}/price-lists/${id}/items/${productId}/tiers/${minQuantity}`
    );
  }

  removePriceListItem(id: string, productId: string): Observable<ApiResponse<object>> {
    return this.http.delete<ApiResponse<object>>(
      `${this.baseUrl}/price-lists/${id}/items/${productId}`
    );
  }

  // ─────────────────────── Tarification d'un client ───────────────────────

  getClientPricing(clientId: string): Observable<ApiResponse<ClientPricing>> {
    return this.http.get<ApiResponse<ClientPricing>>(`${this.baseUrl}/clients/${clientId}`);
  }

  /** `priceListId` a `null` fait revenir le client au tarif catalogue. */
  assignClientPriceList(
    clientId: string,
    priceListId: string | null
  ): Observable<ApiResponse<object>> {
    return this.http.put<ApiResponse<object>>(
      `${this.baseUrl}/clients/${clientId}/price-list`,
      { priceListId }
    );
  }

  upsertClientProductPrice(
    clientId: string,
    productId: string,
    request: UpsertClientProductPriceRequest
  ): Observable<ApiResponse<string>> {
    return this.http.put<ApiResponse<string>>(
      `${this.baseUrl}/clients/${clientId}/products/${productId}`,
      request
    );
  }

  deleteClientProductPrice(id: string): Observable<ApiResponse<object>> {
    return this.http.delete<ApiResponse<object>>(`${this.baseUrl}/client-prices/${id}`);
  }

  // ─────────────────────── Tarification d'un produit ───────────────────────

  getProductPricing(productId: string): Observable<ApiResponse<ProductPricing>> {
    return this.http.get<ApiResponse<ProductPricing>>(`${this.baseUrl}/products/${productId}`);
  }

  // ─────────────────────── Promotions ───────────────────────

  getPromotions(): Observable<ApiResponse<Promotion[]>> {
    return this.http.get<ApiResponse<Promotion[]>>(`${this.baseUrl}/promotions`);
  }

  createPromotion(request: CreatePromotionRequest): Observable<ApiResponse<string>> {
    return this.http.post<ApiResponse<string>>(`${this.baseUrl}/promotions`, request);
  }

  /** Desactiver une promotion n'affecte aucun document emis : la remise y est figee. */
  updatePromotion(id: string, request: UpdatePromotionRequest): Observable<ApiResponse<object>> {
    return this.http.put<ApiResponse<object>>(`${this.baseUrl}/promotions/${id}`, request);
  }

  deletePromotion(id: string): Observable<ApiResponse<object>> {
    return this.http.delete<ApiResponse<object>>(`${this.baseUrl}/promotions/${id}`);
  }

  // ─────────────────── Conditions de reglement ───────────────────

  getPaymentTerms(activeOnly = false): Observable<ApiResponse<PaymentTermTemplate[]>> {
    const params = new HttpParams().set('activeOnly', String(activeOnly));
    return this.http.get<ApiResponse<PaymentTermTemplate[]>>(`${this.baseUrl}/payment-terms`, {
      params
    });
  }

  upsertPaymentTerm(request: UpsertPaymentTermRequest): Observable<ApiResponse<string>> {
    return this.http.put<ApiResponse<string>>(`${this.baseUrl}/payment-terms`, request);
  }

  deletePaymentTerm(id: string): Observable<ApiResponse<object>> {
    return this.http.delete<ApiResponse<object>>(`${this.baseUrl}/payment-terms/${id}`);
  }
}
