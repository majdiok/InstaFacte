import { Injectable, inject } from '@angular/core';
import { Observable, forkJoin, of } from 'rxjs';
import { map, catchError } from 'rxjs/operators';
import { ClientService, ClientListItem } from '@core/services/client.service';
import { ProductService, ProductListItem } from '@core/services/product.service';
import { StockService, StockItem } from '@core/services/stock.service';

export interface ClientReportItem {
  id: string;
  name: string;
  code: string;
  totalInvoices: number;
  totalRevenue: number;
  city: string;
}

export interface ProductReportItem {
  id: string;
  name: string;
  code: string;
  category: string;
  unitPrice: number;
  unit: string;
  stockValue: number;
  quantityOnHand: number;
  isStockManaged: boolean;
}

export interface FichesReportsData {
  clients: ClientReportItem[];
  products: ProductReportItem[];
  totalClients: number;
  totalProducts: number;
  topClientsByRevenue: ClientReportItem[];
  productsWithStock: ProductReportItem[];
  currency: string;
}

@Injectable({ providedIn: 'root' })
export class FichesReportsService {
  private clientService = inject(ClientService);
  private productService = inject(ProductService);
  private stockService = inject(StockService);

  loadReportsData(): Observable<FichesReportsData> {
    return forkJoin({
      clients: this.clientService.getClients({ pageSize: 500 }).pipe(
        catchError(() => of({ success: false, data: { items: [] as ClientListItem[] } } as any))
      ),
      products: this.productService.getProducts({ pageSize: 500 }).pipe(
        catchError(() => of({ success: false, data: { items: [] as ProductListItem[] } } as any))
      ),
      stockItems: this.stockService.getStockItems(undefined, false, false, 1, 1000).pipe(
        catchError(() => of({ success: false, data: { items: [] as StockItem[] } } as any))
      )
    }).pipe(
      map(({ clients, products, stockItems }) => {
        const clientList: ClientListItem[] = clients.success ? clients.data.items : [];
        const productList: ProductListItem[] = products.success ? products.data.items : [];
        const stockItemList: StockItem[] = stockItems.success ? stockItems.data.items : [];

        return this.computeReportsData(clientList, productList, stockItemList);
      })
    );
  }

  private computeReportsData(
    clients: ClientListItem[],
    products: ProductListItem[],
    stockItems: StockItem[]
  ): FichesReportsData {
    const currency = 'TND';

    const clientReports: ClientReportItem[] = clients.map(c => ({
      id: c.id,
      name: c.name,
      code: c.code,
      totalInvoices: c.totalInvoices ?? 0,
      totalRevenue: c.totalRevenue ?? 0,
      city: c.city ?? ''
    }));

    const stockByProduct = new Map<string, { value: number; qty: number }>();
    stockItems.forEach(si => {
      const existing = stockByProduct.get(si.productId);
      if (existing) {
        existing.value += si.stockValue;
        existing.qty += si.quantityOnHand;
      } else {
        stockByProduct.set(si.productId, { value: si.stockValue, qty: si.quantityOnHand });
      }
    });

    const productReports: ProductReportItem[] = products.map(p => {
      const stock = stockByProduct.get(p.id) ?? { value: 0, qty: 0 };
      return {
        id: p.id,
        name: p.name,
        code: p.code,
        category: p.category ?? '',
        unitPrice: p.unitPrice,
        unit: p.unit ?? 'Unité',
        stockValue: stock.value,
        quantityOnHand: stock.qty,
        isStockManaged: p.isStockManaged ?? false
      };
    });

    const topClientsByRevenue = [...clientReports]
      .filter(c => c.totalRevenue > 0)
      .sort((a, b) => b.totalRevenue - a.totalRevenue)
      .slice(0, 10);

    const productsWithStock = productReports
      .filter(p => p.quantityOnHand > 0)
      .sort((a, b) => b.stockValue - a.stockValue)
      .slice(0, 15);

    return {
      clients: clientReports,
      products: productReports,
      totalClients: clientReports.length,
      totalProducts: productReports.length,
      topClientsByRevenue,
      productsWithStock,
      currency
    };
  }
}
