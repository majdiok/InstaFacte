import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { ForecastingService } from './forecasting.service';
import { ReplenishmentFilters } from '../models/forecasting.models';

/**
 * Wraps {@link ForecastingService.exportReplenishment} and triggers a browser download
 * via a temporary anchor element. Kept separate so the component stays declarative and
 * the DOM-level details (Blob, ObjectURL, anchor click) are testable in isolation.
 */
@Injectable({ providedIn: 'root' })
export class ReplenishmentExportService {
  private readonly forecasting = inject(ForecastingService);

  /**
   * Downloads the current replenishment view as CSV.
   * Returns the resolved filename so the caller can show it in a toast.
   */
  async downloadCsv(filters: ReplenishmentFilters = {}): Promise<string> {
    const { blob, fileName } = await firstValueFrom(this.forecasting.exportReplenishment(filters, 'csv'));
    this.triggerBrowserDownload(blob, fileName);
    return fileName;
  }

  /**
   * Pure DOM trigger — extracted so it can be mocked in unit tests via DI replacement.
   * Creates a temporary anchor, clicks it, then revokes the object URL to release memory.
   */
  private triggerBrowserDownload(blob: Blob, fileName: string): void {
    const url = URL.createObjectURL(blob);
    try {
      const anchor = document.createElement('a');
      anchor.href = url;
      anchor.download = fileName;
      anchor.style.display = 'none';
      document.body.appendChild(anchor);
      anchor.click();
      document.body.removeChild(anchor);
    } finally {
      // Some browsers need a microtask delay before revoking; ours doesn't, but be defensive.
      setTimeout(() => URL.revokeObjectURL(url), 0);
    }
  }
}
