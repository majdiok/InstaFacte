import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, throwError, of, Subject } from 'rxjs';
import { catchError, shareReplay, tap, finalize } from 'rxjs/operators';
import { environment } from '@environments/environment';

export interface UsageItem {
  label: string;
  used: number;
  limit: number;
  isUnlimited: boolean;
}

export interface SubscriptionUsage {
  invoices: UsageItem;
  quotes: UsageItem;
  clients: UsageItem;
  products: UsageItem;
  storage: UsageItem;
}

export interface SubscriptionInfo {
  id: string;
  plan: string;
  planDisplay: string;
  status: string;
  statusDisplay: string;
  startDate: string;
  endDate: string | null;
  trialEndDate: string | null;
  monthlyPrice: number | null;
  annualPrice: number | null;
  currency: string;
  invoicesThisMonth: number;
  currentPeriodStart: string;
  usage: SubscriptionUsage;
  /** Server-computed eligibility for invoice creation (preferred over client-side heuristics). */
  canCreateInvoice?: boolean;
  invoiceBlockCode?: string | null;
  invoiceBlockMessage?: string | null;
}

export interface PlanLimits {
  maxInvoicesPerMonth: number;
  maxQuotesPerMonth: number;
  maxClients: number;
  maxProducts: number;
  maxStorageBytes: number;
  electronicSignature: boolean;
  xmlExport: boolean;
  paymentTracking: boolean;
  prioritySupport: boolean;
  isUnlimited: boolean;
}

export interface PlanOption {
  id: string;
  name: string;
  priceMonthly: number;
  priceAnnual: number | null;
  currency: string;
  period: string;
  features: string[];
  disabledFeatures: string[];
  isPopular: boolean;
  isCurrent: boolean;
  savePercentage: number | null;
  limits: PlanLimits;
}

export interface BillingHistoryItem {
  id: string;
  date: string;
  description: string;
  amount: number;
  currency: string;
  status: string;
}

export interface ApiResponse<T> {
  success: boolean;
  data: T;
  message: string | null;
  code?: string | null;
  errors: string[];
}

@Injectable({
  providedIn: 'root'
})
export class SubscriptionService {
  private readonly API_URL = `${environment.apiUrl}/subscription`;
  private http = inject(HttpClient);

  private cachedSubscription: ApiResponse<SubscriptionInfo> | null = null;
  private cachedPlans: ApiResponse<PlanOption[]> | null = null;
  private cacheTimestamp = 0;
  private plansCacheTimestamp = 0;
  private readonly CACHE_DURATION_MS = 30_000;

  private getSubscriptionRequest$: Observable<ApiResponse<SubscriptionInfo>> | null = null;

  /** Emitted when subscription data may have changed (plan change, cache invalidation). */
  readonly subscriptionChanged$ = new Subject<void>();

  /**
   * Returns current subscription. Pass forceRefresh=true before any blocking decision
   * (invoice emit, review step validation).
   */
  getCurrentSubscription(forceRefresh = false): Observable<ApiResponse<SubscriptionInfo>> {
    if (forceRefresh) {
      this.cachedSubscription = null;
      this.cacheTimestamp = 0;
      this.getSubscriptionRequest$ = null;
    } else if (this.cachedSubscription && (Date.now() - this.cacheTimestamp) < this.CACHE_DURATION_MS) {
      return of(this.cachedSubscription);
    }

    if (this.getSubscriptionRequest$) {
      return this.getSubscriptionRequest$;
    }

    this.getSubscriptionRequest$ = this.http.get<ApiResponse<SubscriptionInfo>>(this.API_URL).pipe(
      tap(response => {
        if (response.success && response.data) {
          this.cachedSubscription = response;
          this.cacheTimestamp = Date.now();
        }
      }),
      shareReplay({ bufferSize: 1, refCount: true }),
      catchError(error => {
        this.getSubscriptionRequest$ = null;
        return throwError(() => error);
      }),
      finalize(() => {
        setTimeout(() => { this.getSubscriptionRequest$ = null; }, 100);
      })
    );

    return this.getSubscriptionRequest$;
  }

  getAvailablePlans(forceRefresh = false): Observable<ApiResponse<PlanOption[]>> {
    if (!forceRefresh && this.cachedPlans && (Date.now() - this.plansCacheTimestamp) < this.CACHE_DURATION_MS) {
      return of(this.cachedPlans);
    }

    return this.http.get<ApiResponse<PlanOption[]>>(`${this.API_URL}/plans`).pipe(
      tap(response => {
        if (response.success && response.data) {
          this.cachedPlans = response;
          this.plansCacheTimestamp = Date.now();
        }
      }),
      catchError(error => throwError(() => error))
    );
  }

  changePlan(plan: string): Observable<ApiResponse<SubscriptionInfo>> {
    return this.http.post<ApiResponse<SubscriptionInfo>>(`${this.API_URL}/change`, { plan }).pipe(
      tap(response => {
        if (response.success) {
          this.invalidateCache();
        }
      }),
      catchError(error => throwError(() => error))
    );
  }

  cancelSubscription(reason: string): Observable<ApiResponse<SubscriptionInfo>> {
    return this.http.post<ApiResponse<SubscriptionInfo>>(`${this.API_URL}/cancel`, { reason }).pipe(
      tap(response => {
        if (response.success) {
          this.invalidateCache();
        }
      }),
      catchError(error => throwError(() => error))
    );
  }

  invalidateCache(): void {
    this.cachedSubscription = null;
    this.cachedPlans = null;
    this.cacheTimestamp = 0;
    this.plansCacheTimestamp = 0;
    this.getSubscriptionRequest$ = null;
    this.subscriptionChanged$.next();
  }
}
