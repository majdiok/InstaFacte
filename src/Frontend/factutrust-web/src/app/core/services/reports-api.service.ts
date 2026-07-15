import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '@environments/environment';
import { ApiResponse } from './auth.service';

export interface ClientPaymentReportRow {
  paymentId: string;
  paymentDate: string;
  clientName: string;
  invoiceId: string;
  invoiceNumber: string;
  amount: number;
  currency: string;
  methodDisplay: string;
  reference: string | null;
}

export interface SupplierPaymentReportRow {
  paymentId: string;
  paymentDate: string;
  supplierName: string;
  supplierInvoiceId: string;
  invoiceNumber: string;
  amount: number;
  currency: string;
  methodDisplay: string;
  reference: string | null;
}

export interface ClientTransactionReportRow {
  date: string;
  transactionType: string;
  clientName: string;
  reference: string;
  amount: number;
  currency: string;
  invoiceId: string | null;
  paymentId: string | null;
}

export interface SupplierTransactionReportRow {
  date: string;
  transactionType: string;
  supplierName: string;
  reference: string;
  amount: number;
  currency: string;
  supplierInvoiceId: string | null;
  paymentId: string | null;
}

@Injectable({ providedIn: 'root' })
export class ReportsApiService {
  private readonly API_URL = `${environment.apiUrl}/reports`;

  constructor(private http: HttpClient) {}

  getClientPayments(fromDate: string, toDate: string): Observable<ApiResponse<ClientPaymentReportRow[]>> {
    const params = new HttpParams().set('from', fromDate).set('to', toDate);
    return this.http.get<ApiResponse<ClientPaymentReportRow[]>>(`${this.API_URL}/client-payments`, { params });
  }

  getSupplierPayments(fromDate: string, toDate: string): Observable<ApiResponse<SupplierPaymentReportRow[]>> {
    const params = new HttpParams().set('from', fromDate).set('to', toDate);
    return this.http.get<ApiResponse<SupplierPaymentReportRow[]>>(`${this.API_URL}/supplier-payments`, { params });
  }

  getClientTransactions(fromDate: string, toDate: string): Observable<ApiResponse<ClientTransactionReportRow[]>> {
    const params = new HttpParams().set('from', fromDate).set('to', toDate);
    return this.http.get<ApiResponse<ClientTransactionReportRow[]>>(`${this.API_URL}/client-transactions`, { params });
  }

  getSupplierTransactions(fromDate: string, toDate: string): Observable<ApiResponse<SupplierTransactionReportRow[]>> {
    const params = new HttpParams().set('from', fromDate).set('to', toDate);
    return this.http.get<ApiResponse<SupplierTransactionReportRow[]>>(`${this.API_URL}/supplier-transactions`, { params });
  }

  getSalesByLine(fromDate: string, toDate: string): Observable<ApiResponse<SalesByLineReportRow[]>> {
    const params = new HttpParams().set('from', fromDate).set('to', toDate);
    return this.http.get<ApiResponse<SalesByLineReportRow[]>>(`${this.API_URL}/sales-by-line`, { params });
  }

  getSalesVat(fromDate: string, toDate: string): Observable<ApiResponse<SalesVatReportRow[]>> {
    const params = new HttpParams().set('from', fromDate).set('to', toDate);
    return this.http.get<ApiResponse<SalesVatReportRow[]>>(`${this.API_URL}/sales-vat`, { params });
  }

  getSalesRevenueByProduct(fromDate: string, toDate: string, groupBy: 'Product' | 'Category' | 'ProductAndClient'): Observable<ApiResponse<SalesRevenueReportRow[]>> {
    let params = new HttpParams().set('from', fromDate).set('to', toDate);
    if (groupBy) params = params.set('groupBy', groupBy);
    return this.http.get<ApiResponse<SalesRevenueReportRow[]>>(`${this.API_URL}/sales-revenue-by-product`, { params });
  }

  getPurchasesByLine(fromDate: string, toDate: string): Observable<ApiResponse<PurchasesByLineReportRow[]>> {
    const params = new HttpParams().set('from', fromDate).set('to', toDate);
    return this.http.get<ApiResponse<PurchasesByLineReportRow[]>>(`${this.API_URL}/purchases-by-line`, { params });
  }

  getPurchasesVat(fromDate: string, toDate: string): Observable<ApiResponse<PurchasesVatReportRow[]>> {
    const params = new HttpParams().set('from', fromDate).set('to', toDate);
    return this.http.get<ApiResponse<PurchasesVatReportRow[]>>(`${this.API_URL}/purchases-vat`, { params });
  }

  getClientBalances(): Observable<ApiResponse<ClientBalanceReportRow[]>> {
    return this.http.get<ApiResponse<ClientBalanceReportRow[]>>(`${this.API_URL}/client-balances`);
  }

  getSupplierBalances(): Observable<ApiResponse<SupplierBalanceReportRow[]>> {
    return this.http.get<ApiResponse<SupplierBalanceReportRow[]>>(`${this.API_URL}/supplier-balances`);
  }

  getClientWithholdings(fromDate: string, toDate: string): Observable<ApiResponse<ClientWithholdingReportRow[]>> {
    const params = new HttpParams().set('from', fromDate).set('to', toDate);
    return this.http.get<ApiResponse<ClientWithholdingReportRow[]>>(`${this.API_URL}/client-withholdings`, { params });
  }

  getSupplierWithholdings(fromDate: string, toDate: string): Observable<ApiResponse<SupplierWithholdingReportRow[]>> {
    const params = new HttpParams().set('from', fromDate).set('to', toDate);
    return this.http.get<ApiResponse<SupplierWithholdingReportRow[]>>(`${this.API_URL}/supplier-withholdings`, { params });
  }

  getStockMovements(params: {
    warehouseId?: string;
    from?: string;
    to?: string;
    page?: number;
    pageSize?: number;
  }): Observable<ApiResponse<StockMovementsReportResult>> {
    let httpParams = new HttpParams();
    if (params.warehouseId) httpParams = httpParams.set('warehouseId', params.warehouseId);
    if (params.from) httpParams = httpParams.set('from', params.from);
    if (params.to) httpParams = httpParams.set('to', params.to);
    if (params.page != null) httpParams = httpParams.set('page', params.page.toString());
    if (params.pageSize != null) httpParams = httpParams.set('pageSize', params.pageSize.toString());
    return this.http.get<ApiResponse<StockMovementsReportResult>>(`${this.API_URL}/stock-movements`, { params: httpParams });
  }

  getStockSnapshot(params: { asOf: string; warehouseId?: string }): Observable<ApiResponse<StockSnapshotRow[]>> {
    let httpParams = new HttpParams().set('asOf', params.asOf);
    if (params.warehouseId) httpParams = httpParams.set('warehouseId', params.warehouseId);
    return this.http.get<ApiResponse<StockSnapshotRow[]>>(`${this.API_URL}/stock-snapshot`, { params: httpParams });
  }

  getCommercialProfit(fromDate: string, toDate: string, groupBy: 'Line' | 'Product' | 'Month' | 'Piece'): Observable<ApiResponse<CommercialProfitReportRow[]>> {
    let params = new HttpParams().set('from', fromDate).set('to', toDate);
    if (groupBy) params = params.set('groupBy', groupBy);
    return this.http.get<ApiResponse<CommercialProfitReportRow[]>>(`${this.API_URL}/commercial-profit`, { params });
  }

  getProductPerformance(fromDate: string, toDate: string): Observable<ApiResponse<ProductPerformanceReportRow[]>> {
    const params = new HttpParams().set('from', fromDate).set('to', toDate);
    return this.http.get<ApiResponse<ProductPerformanceReportRow[]>>(`${this.API_URL}/product-performance`, { params });
  }

  getProductSalesTrend(fromDate: string, toDate: string): Observable<ApiResponse<ProductSalesTrendReportRow[]>> {
    const params = new HttpParams().set('from', fromDate).set('to', toDate);
    return this.http.get<ApiResponse<ProductSalesTrendReportRow[]>>(`${this.API_URL}/product-sales-trend`, { params });
  }

  getBasketMetrics(fromDate: string, toDate: string): Observable<ApiResponse<BasketMetricsReportDto>> {
    const params = new HttpParams().set('from', fromDate).set('to', toDate);
    return this.http.get<ApiResponse<BasketMetricsReportDto>>(`${this.API_URL}/basket-metrics`, { params });
  }

  getProductsNeverSold(fromDate: string, toDate: string): Observable<ApiResponse<ProductNeverSoldReportRow[]>> {
    const params = new HttpParams().set('from', fromDate).set('to', toDate);
    return this.http.get<ApiResponse<ProductNeverSoldReportRow[]>>(`${this.API_URL}/products-never-sold`, { params });
  }
}

export interface StockMovementReportRow {
  id: string;
  productName: string;
  productCode: string;
  warehouseName: string;
  typeDisplay: string;
  reasonDisplay: string;
  quantity: number;
  unitCost: number;
  reference: string | null;
  occurredAt: string;
}

export interface StockMovementsReportResult {
  items: StockMovementReportRow[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface StockSnapshotRow {
  productName: string;
  productCode: string;
  warehouseName: string;
  quantity: number;
  unitCost: number;
  totalValue: number;
}

export interface CommercialProfitReportRow {
  productId?: string;
  productName?: string;
  productCode?: string;
  invoiceNumber?: string;
  issueDate?: string;
  period?: string;
  quantity: number;
  revenue: number;
  cost: number;
  profit: number;
  currency: string;
}

export interface SalesByLineReportRow {
  productId: string;
  productName: string;
  productCode: string;
  categoryName: string | null;
  quantity: number;
  revenue: number;
  vatAmount: number;
  currency: string;
}

export interface SalesVatReportRow {
  vatRatePercent: number;
  vatRateDisplay: string;
  totalVatAmount: number;
  totalTaxableAmount: number;
  currency: string;
}

export interface SalesRevenueReportRow {
  groupKey: string;
  groupKey2: string | null;
  revenue: number;
  quantity: number;
  currency: string;
}

export interface PurchasesByLineReportRow {
  productId: string;
  productName: string;
  productCode: string;
  supplierName: string;
  quantity: number;
  amountTTC: number;
  vatAmount: number;
  currency: string;
}

export interface PurchasesVatReportRow {
  vatRatePercent: number;
  vatRateDisplay: string;
  totalVatAmount: number;
  totalTaxableAmount: number;
  currency: string;
}

export interface ClientBalanceReportRow {
  clientId: string;
  clientName: string;
  totalInvoiced: number;
  totalPaid: number;
  balance: number;
  currency: string;
}

export interface SupplierBalanceReportRow {
  supplierId: string;
  supplierName: string;
  totalInvoiced: number;
  totalPaid: number;
  balance: number;
  currency: string;
}

export interface ClientWithholdingReportRow {
  clientId: string;
  clientName: string;
  paymentCount: number;
  totalWithholding: number;
  currency: string;
}

export interface SupplierWithholdingReportRow {
  supplierId: string;
  supplierName: string;
  invoiceCount: number;
  totalHT: number;
  totalWithholding: number;
  totalNetPaid: number;
  currency: string;
}

export interface ProductPerformanceReportRow {
  productId: string;
  productName: string;
  productCode: string;
  categoryName: string | null;
  quantitySold: number;
  revenue: number;
  unitCost: number;
  totalCost: number;
  profit: number;
  marginPercent: number | null;
  revenueSharePercent: number;
  currency: string;
}

export interface ProductSalesTrendReportRow {
  productId: string;
  productName: string;
  productCode: string;
  categoryName: string | null;
  period: string;
  quantity: number;
  revenue: number;
  currency: string;
}

export interface ProductNeverSoldReportRow {
  productId: string;
  productName: string;
  productCode: string;
  categoryName: string | null;
  unitPrice: number;
  currency: string;
}

export interface BasketMetricsReportDto {
  totalInvoices: number;
  totalRevenue: number;
  totalLines: number;
  averageBasket: number;
  averageLinesPerInvoice: number;
  currency: string;
}
