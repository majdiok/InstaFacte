import { Injectable, signal, computed, inject } from '@angular/core';
import { InvoiceService, InvoiceListItem } from '@core/services/invoice.service';
import { formatLocalDate } from '@core/utils/date.util';

export type PosHistoryFilter = 'today' | 'session';

@Injectable({
  providedIn: 'root'
})
export class PosHistoryService {
  private readonly invoiceService = inject(InvoiceService);
  private readonly todayInvoices = signal<InvoiceListItem[]>([]);
  private readonly loading = signal(false);
  private readonly filter = signal<PosHistoryFilter>('today');
  private readonly sessionInvoiceIds = signal<Set<string>>(new Set());

  readonly activeFilter = this.filter.asReadonly();
  readonly todayTransactions = computed(() => {
    const items = this.todayInvoices();
    if (this.filter() !== 'session') {
      return items;
    }
    const ids = this.sessionInvoiceIds();
    if (ids.size === 0) {
      return [];
    }
    return items.filter(inv => ids.has(inv.id));
  });
  readonly todayCount = computed(() => this.todayTransactions().length);
  readonly todayTotal = computed(() =>
    this.todayTransactions().reduce((sum, inv) => sum + inv.totalAmount, 0)
  );
  readonly isLoading = computed(() => this.loading());

  loadHistory(): void {
    this.loading.set(true);
    const today = new Date();
    const from = formatLocalDate(today);
    const to = from;

    this.invoiceService.getInvoices({
      fromDate: from,
      toDate: to,
      page: 1,
      pageSize: 50
    }).subscribe({
      next: res => {
        if (res.success && res.data) {
          this.todayInvoices.set(res.data.items);
        }
        this.loading.set(false);
      },
      error: () => this.loading.set(false)
    });
  }

  setFilter(filter: PosHistoryFilter): void {
    this.filter.set(filter);
  }

  setSessionInvoiceIds(ids: string[]): void {
    this.sessionInvoiceIds.set(new Set(ids));
  }

  refreshHistory(): void {
    this.loadHistory();
  }
}
