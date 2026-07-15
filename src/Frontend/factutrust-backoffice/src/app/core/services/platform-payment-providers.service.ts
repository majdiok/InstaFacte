import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '@environments/environment';
import type { ApiResponse } from '@core/models/api-response.model';
import type {
  InitiateCheckoutRequest,
  InitiateCheckoutResponse,
  PaymentIntentsPageDto,
  PaymentProviderConfigDto,
  PaymentProviderConfigsListDto,
  RegisterWireReceiptRequest,
  UpdatePaymentProviderConfigRequest
} from '@core/models/platform.models';

/**
 * Lot C5 — Service HTTP CRUD providers paiement + intents + checkout + wire.
 *
 * Routes :
 * - `GET    /api/platform/payment-providers`
 * - `GET    /api/platform/payment-providers/{code}`
 * - `PUT    /api/platform/payment-providers/{code}`
 * - `GET    /api/platform/payment-providers/intents?providerCode&status&tenantId&from&to`
 * - `POST   /api/subscription/checkout` (tenant)
 * - `POST   /api/platform/invoices/{id}/wire-receipt` (admin saisit virement)
 */
@Injectable({ providedIn: 'root' })
export class PlatformPaymentProvidersService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/platform/payment-providers`;

  list(): Observable<ApiResponse<PaymentProviderConfigsListDto>> {
    return this.http.get<ApiResponse<PaymentProviderConfigsListDto>>(this.base);
  }

  get(code: string): Observable<ApiResponse<PaymentProviderConfigDto>> {
    return this.http.get<ApiResponse<PaymentProviderConfigDto>>(`${this.base}/${code}`);
  }

  update(code: string, request: UpdatePaymentProviderConfigRequest): Observable<ApiResponse<PaymentProviderConfigDto>> {
    return this.http.put<ApiResponse<PaymentProviderConfigDto>>(`${this.base}/${code}`, request);
  }

  listIntents(
    providerCode?: string | null,
    status?: string | null,
    tenantId?: string | null,
    from?: string | null,
    to?: string | null,
    page = 1,
    pageSize = 25
  ): Observable<ApiResponse<PaymentIntentsPageDto>> {
    let hp = new HttpParams().set('page', String(page)).set('pageSize', String(pageSize));
    if (providerCode) hp = hp.set('providerCode', providerCode);
    if (status) hp = hp.set('status', status);
    if (tenantId) hp = hp.set('tenantId', tenantId);
    if (from) hp = hp.set('from', from);
    if (to) hp = hp.set('to', to);
    return this.http.get<ApiResponse<PaymentIntentsPageDto>>(`${this.base}/intents`, { params: hp });
  }

  /** Tenant : initie un paiement pour une facture émise. */
  initiateCheckout(request: InitiateCheckoutRequest): Observable<ApiResponse<InitiateCheckoutResponse>> {
    return this.http.post<ApiResponse<InitiateCheckoutResponse>>(`${environment.apiUrl}/subscription/checkout`, request);
  }

  /**
   * Sous-lot C5.5 — Admin : initie un checkout depuis le backoffice (déduit le tenant de la facture).
   * Utile pour tester l'intégration provider ou aider un tenant en difficulté.
   */
  initiateAdminCheckout(invoiceId: string, request: InitiateCheckoutRequest): Observable<ApiResponse<InitiateCheckoutResponse>> {
    return this.http.post<ApiResponse<InitiateCheckoutResponse>>(
      `${environment.apiUrl}/platform/invoices/${invoiceId}/checkout`,
      request
    );
  }

  /** Admin : enregistre un reçu de virement reçu manuellement. */
  registerWireReceipt(invoiceId: string, request: RegisterWireReceiptRequest): Observable<ApiResponse<unknown>> {
    return this.http.post<ApiResponse<unknown>>(
      `${environment.apiUrl}/platform/invoices/${invoiceId}/wire-receipt`,
      request
    );
  }
}
