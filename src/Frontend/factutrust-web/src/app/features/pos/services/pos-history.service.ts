import { Injectable, signal, computed } from '@angular/core';
import { InvoiceService, InvoiceListItem } from '@core/services/invoice.service';
import { formatLocalDate } from '@core/utils/date.util';

@Injectable({
  providedIn: 'root'
})
export class PosHistoryService {
  constructor(private readonly invoiceService: InvoiceService) {}
  private readonly todayInvoices = signal<InvoiceListItem[]>([]);
  private readonly loading = signal(false);

  readonly todayTransactions = computed(() => this.todayInvoices());
  readonly todayCount = computed(() => this.todayInvoices().length);
  readonly todayTotal = computed(() =>
    this.todayInvoices().reduce((sum, inv) => sum + inv.totalAmount, 0)
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

  refreshHistory(): void {
    this.loadHistory();
  }
}
