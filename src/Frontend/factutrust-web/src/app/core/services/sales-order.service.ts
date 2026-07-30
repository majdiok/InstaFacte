import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '@environments/environment';
import { ApiResponse, PagedResult } from './client.service';

/**
 * États d'une commande client. Sérialisés en chaîne par l'API.
 *
 * `Closed` n'est pas `Cancelled` : soldée manuellement alors qu'il restait à livrer
 * (le client renonce au reliquat), là où `Cancelled` suppose qu'aucun mouvement n'a eu lieu.
 */
export type SalesOrderStatus =
  | 'Draft'
  | 'Confirmed'
  | 'PartiallyDelivered'
  | 'Delivered'
  | 'Completed'
  | 'Cancelled'
  | 'Closed';

export interface SalesOrderListItem {
  id: string;
  number: string;
  orderDate: string;
  expectedDeliveryDate: string | null;
  status: SalesOrderStatus;
  statusDisplay: string;
  statusCssClass: string;
  clientId: string;
  clientName: string;
  reference: string | null;
  totalAmount: number;
  currency: string;
  lineCount: number;
  /** Reste à livrer, toutes lignes confondues. */
  pendingDeliveryQuantity: number;
  /** Valeur HT du reste à livrer — la part de cette commande dans le carnet. */
  backlogAmountHt: number;
  /** Vrai tant que la commande pèse sur le carnet de commandes. */
  isOpen: boolean;
  isStockReserved: boolean;
  sourceQuoteId: string | null;
}

/** Totaux calculés sur l'ENSEMBLE du jeu filtré, pas sur la page courante. */
export interface SalesOrderListSummary {
  count: number;
  totalHt: number;
  totalVat: number;
  totalTtc: number;
  backlogAmountHt: number;
  openCount: number;
  partiallyDeliveredCount: number;
  completedCount: number;
  currency: string;
}

export interface SalesOrderLine {
  id: string;
  lineNumber: number;
  productId: string;
  productCode: string;
  productName: string;
  productDescription: string | null;
  unit: string | null;
  quantity: number;
  deliveredQuantity: number;
  invoicedQuantity: number;
  pendingDeliveryQuantity: number;
  pendingInvoiceQuantity: number;
  /** Livré mais pas encore facturé — assiette de la facturation périodique. */
  deliveredNotInvoicedQuantity: number;
  unitPrice: number;
  vatRatePercent: number;
  discountPercent: number | null;
  discountAmount: number;
  isFodecApplicable: boolean;
  fodecRatePercent: number;
  fodecAmount: number;
  subTotal: number;
  vatAmount: number;
  total: number;
  isFullyDelivered: boolean;
  isFullyInvoiced: boolean;
  notes: string | null;
}

export interface VatBreakdownRow {
  rate: number;
  rateDisplay: string;
  baseAmount: number;
  vatAmount: number;
}

export interface SalesOrderClientSummary {
  id: string;
  name: string;
  nif: string | null;
  email: string;
  address: string;
}

export interface SalesOrderDetail {
  id: string;
  number: string;
  orderDate: string;
  expectedDeliveryDate: string | null;
  status: SalesOrderStatus;
  statusDisplay: string;
  clientId: string;
  client: SalesOrderClientSummary;
  reference: string | null;
  notes: string | null;
  paymentTerms: string | null;
  warehouseId: string | null;
  warehouseName: string | null;
  sourceQuoteId: string | null;
  sourceQuoteNumber: string | null;
  lines: SalesOrderLine[];
  subTotal: number;
  /** Total HT AVANT remise de pied, affiché au-dessus de la remise. */
  subTotalBeforeGlobalDiscount: number;
  /** Remise de pied en pourcentage, si elle a été saisie ainsi. */
  globalDiscountPercent: number | null;
  /** Montant de la remise de pied effectivement appliquée. */
  globalDiscountAmount: number;
  fodecAmount: number;
  totalVat: number;
  fiscalStampAmount: number;
  totalAmount: number;
  currency: string;
  vatBreakdown: VatBreakdownRow[];
  isFullyDelivered: boolean;
  isFullyInvoiced: boolean;
  totalPendingDeliveryQuantity: number;
  totalPendingInvoiceQuantity: number;
  backlogAmountHt: number;
  isStockReserved: boolean;
  confirmedAt: string | null;
  completedAt: string | null;
  cancelledAt: string | null;
  cancellationReason: string | null;
  closedAt: string | null;
  closureReason: string | null;
  createdAt: string;
  updatedAt: string | null;
}

/** Une ligne du carnet : ce qui reste à livrer, et pour quelle valeur. */
export interface SalesOrderBacklogRow {
  salesOrderId: string;
  orderNumber: string;
  orderDate: string;
  expectedDeliveryDate: string | null;
  clientId: string;
  clientName: string;
  productId: string;
  productCode: string;
  productName: string;
  orderedQuantity: number;
  deliveredQuantity: number;
  pendingQuantity: number;
  pendingAmountHt: number;
  /** Date de livraison prévue dépassée alors qu'il reste à livrer. */
  isLate: boolean;
}

export interface SalesOrderSearchParams {
  search?: string;
  status?: SalesOrderStatus | null;
  clientId?: string | null;
  fromDate?: string | null;
  toDate?: string | null;
  /** Ne garder que les commandes qui pèsent encore sur le carnet. */
  openOnly?: boolean;
  page?: number;
  pageSize?: number;
}

export interface CreateSalesOrderLineRequest {
  productId: string;
  quantity: number;
  /** 0 ou absent = prix résolu par le serveur (prix négocié → grille → catalogue). */
  unitPrice: number;
  discountPercent?: number | null;
  notes?: string | null;
}

export interface CreateSalesOrderRequest {
  clientId: string;
  orderDate: string;
  expectedDeliveryDate?: string | null;
  reference?: string | null;
  notes?: string | null;
  paymentTerms?: string | null;
  warehouseId?: string | null;
  sourceQuoteId?: string | null;
  lines: CreateSalesOrderLineRequest[];
}

export interface UpdateSalesOrderRequest {
  expectedDeliveryDate?: string | null;
  reference?: string | null;
  notes?: string | null;
  paymentTerms?: string | null;
}

@Injectable({ providedIn: 'root' })
export class SalesOrderService {
  private readonly API_URL = `${environment.apiUrl}/sales-orders`;
  private readonly http = inject(HttpClient);

  getSalesOrders(
    params: SalesOrderSearchParams = {}
  ): Observable<ApiResponse<PagedResult<SalesOrderListItem>>> {
    return this.http.get<ApiResponse<PagedResult<SalesOrderListItem>>>(this.API_URL, {
      params: this.buildParams(params)
    });
  }

  /** Totaux respectant les mêmes filtres que {@link getSalesOrders} (calcul backend). */
  getSalesOrdersSummary(
    params: SalesOrderSearchParams = {}
  ): Observable<ApiResponse<SalesOrderListSummary>> {
    return this.http.get<ApiResponse<SalesOrderListSummary>>(`${this.API_URL}/summary`, {
      params: this.buildParams(params, { includePaging: false })
    });
  }

  /** Carnet de commandes : le reste à livrer, ligne par ligne. */
  getBacklog(
    clientId?: string | null,
    dueBefore?: string | null
  ): Observable<ApiResponse<SalesOrderBacklogRow[]>> {
    let httpParams = new HttpParams();
    if (clientId) httpParams = httpParams.set('clientId', clientId);
    if (dueBefore) httpParams = httpParams.set('dueBefore', dueBefore);

    return this.http.get<ApiResponse<SalesOrderBacklogRow[]>>(`${this.API_URL}/backlog`, {
      params: httpParams
    });
  }

  getSalesOrder(id: string): Observable<ApiResponse<SalesOrderDetail>> {
    return this.http.get<ApiResponse<SalesOrderDetail>>(`${this.API_URL}/${id}`);
  }

  createSalesOrder(request: CreateSalesOrderRequest): Observable<ApiResponse<string>> {
    return this.http.post<ApiResponse<string>>(this.API_URL, request);
  }

  /** L'en-tête seul : les lignes ne sont modifiables qu'à l'état brouillon, côté domaine. */
  updateSalesOrder(id: string, request: UpdateSalesOrderRequest): Observable<ApiResponse<object>> {
    return this.http.put<ApiResponse<object>>(`${this.API_URL}/${id}`, request);
  }

  /** Confirme la commande : engagement ferme, et réservation de stock si le drapeau est actif. */
  confirmSalesOrder(id: string): Observable<ApiResponse<object>> {
    return this.http.post<ApiResponse<object>>(`${this.API_URL}/${id}/confirm`, {});
  }

  cancelSalesOrder(id: string, reason: string): Observable<ApiResponse<object>> {
    return this.http.post<ApiResponse<object>>(`${this.API_URL}/${id}/cancel`, { reason });
  }

  /**
   * Pose ou retire la remise de pied. Pourcentage et montant sont exclusifs ; les deux à
   * `null` retirent la remise. Le serveur la répartit sur les lignes, de sorte que le FODEC
   * et la base de TVA portent sur ce qui est réellement facturé.
   */
  setGlobalDiscount(
    id: string,
    percent: number | null,
    amount: number | null
  ): Observable<ApiResponse<object>> {
    return this.http.put<ApiResponse<object>>(`${this.API_URL}/${id}/global-discount`, {
      percent,
      amount
    });
  }

  /** Solde la commande en renonçant au reliquat — distinct d'une annulation. */
  closeSalesOrder(id: string, reason: string): Observable<ApiResponse<object>> {
    return this.http.post<ApiResponse<object>>(`${this.API_URL}/${id}/close`, { reason });
  }

  private buildParams(
    params: SalesOrderSearchParams,
    options: { includePaging?: boolean } = {}
  ): HttpParams {
    const includePaging = options.includePaging !== false;
    let httpParams = new HttpParams();

    if (params.search) httpParams = httpParams.set('search', params.search);
    if (params.status) httpParams = httpParams.set('status', params.status);
    if (params.clientId) httpParams = httpParams.set('clientId', params.clientId);
    if (params.fromDate) httpParams = httpParams.set('fromDate', params.fromDate);
    if (params.toDate) httpParams = httpParams.set('toDate', params.toDate);
    if (params.openOnly) httpParams = httpParams.set('openOnly', 'true');

    if (includePaging) {
      if (params.page) httpParams = httpParams.set('page', params.page.toString());
      if (params.pageSize) httpParams = httpParams.set('pageSize', params.pageSize.toString());
    }

    return httpParams;
  }
}
