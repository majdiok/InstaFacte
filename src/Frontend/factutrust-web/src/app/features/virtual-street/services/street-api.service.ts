import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable, catchError, map, of, throwError } from 'rxjs';
import { createHttpContextSkipGlobalErrorUi } from '@core/http-context';
import { environment } from '../../../../environments/environment';

export interface ApiResponse<T> {
  success: boolean;
  data?: T;
  message?: string | null;
  errors?: string[];
  code?: string | null;
}

export interface StreetMapEntry {
  slug: string;
  displayName: string;
  tagline?: string | null;
  publicLogoUrl?: string | null;
  category: number;
  streetPositionIndex: number;
  facadeTheme: number;
  brandPrimaryColorHex: string;
  brandSecondaryColorHex: string;
}

export interface StorefrontDetail {
  id: string;
  slug: string;
  displayName: string;
  tagline?: string | null;
  descriptionMarkdown?: string | null;
  brandPrimaryColorHex: string;
  brandSecondaryColorHex: string;
  publicLogoUrl?: string | null;
  publicCoverImageUrl?: string | null;
  category: number;
  facadeTheme: number;
  publicContactEmail: string;
  publicContactPhone?: string | null;
  publicContactWhatsApp?: string | null;
  orderSubmissionEnabled: boolean;
  streetPositionIndex: number;
}

export interface StorefrontProduct {
  id: string;
  slug: string;
  name: string;
  descriptionSanitized?: string | null;
  priceAmount: number;
  priceCurrency: string;
  publicImageUrl?: string | null;
  categoryLabel?: string | null;
  stockDisplayStatus: number;
}

export interface PagedProducts {
  items: StorefrontProduct[];
  page: number;
  pageSize: number;
  totalCount: number;
}

@Injectable({ providedIn: 'root' })
export class StreetApiService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/public/street`;
  /** Passed on every public-street request: 404/empty map are expected UX, not global toast errors (see errorInterceptor). */
  private readonly publicStreetContext = createHttpContextSkipGlobalErrorUi();

  getMap(): Observable<StreetMapEntry[]> {
    return this.http
      .get<ApiResponse<StreetMapEntry[]>>(`${this.base}/map`, { context: this.publicStreetContext })
      .pipe(
        map(r => r.data ?? []),
        catchError((e: HttpErrorResponse) => (e.status === 404 ? of([]) : throwError(() => e)))
      );
  }

  getStorefront(slug: string): Observable<StorefrontDetail | null> {
    return this.http
      .get<ApiResponse<StorefrontDetail>>(`${this.base}/storefronts/${encodeURIComponent(slug)}`, {
        context: this.publicStreetContext
      })
      .pipe(
        map(r => r.data ?? null),
        catchError((e: HttpErrorResponse) => (e.status === 404 ? of(null) : throwError(() => e)))
      );
  }

  getProducts(slug: string, page = 1, pageSize = 24, category?: string | null): Observable<PagedProducts> {
    const params: Record<string, string> = {
      page: String(page),
      pageSize: String(pageSize)
    };
    if (category) params['category'] = category;
    const empty: PagedProducts = { items: [], page, pageSize, totalCount: 0 };
    return this.http
      .get<ApiResponse<PagedProducts>>(`${this.base}/storefronts/${encodeURIComponent(slug)}/products`, {
        params,
        context: this.publicStreetContext
      })
      .pipe(
        map(r => r.data ?? empty),
        catchError((e: HttpErrorResponse) => (e.status === 404 ? of(empty) : throwError(() => e)))
      );
  }
}
