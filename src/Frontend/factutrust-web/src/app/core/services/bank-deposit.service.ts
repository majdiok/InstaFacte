import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '@environments/environment';
import { createHttpContextSkipGlobalErrorUi } from '@core/http-context';
import { ApiResponse, PagedResult } from './client.service';

export enum BankDepositType {
  Cash = 0,
  Check = 1,
  Draft = 2
}

export enum BankDepositStatus {
  Terminee = 0,
  Annulee = 1
}

export interface BankDepositListItem {
  id: string;
  number: string;
  depositType: number;
  depositTypeDisplay: string;
  depositDate: string;
  bankAccountId: string;
  bankName: string;
  accountDesignation: string | null;
  bankCode: string;
  iban: string;
  amount: number;
  currency: string;
  quantity: number;
  depositSlipReference: string | null;
  notes: string | null;
  status: BankDepositStatus;
  cashOperationNumber: string;
}

export interface CreateBankDepositPayload {
  depositType: number;
  depositDate: string;
  bankAccountId: string;
  amount: number;
  quantity: number;
  depositSlipReference?: string | null;
  notes?: string | null;
}

/** Matches backend BankDepositAvailableBalanceDto (camelCase). */
export interface BankDepositAvailableBalance {
  amount: number;
  currency: string;
}

@Injectable({
  providedIn: 'root'
})
export class BankDepositService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/bank-deposits`;

  create(payload: CreateBankDepositPayload): Observable<ApiResponse<BankDepositListItem>> {
    return this.http.post<ApiResponse<BankDepositListItem>>(this.baseUrl, payload, {
      context: createHttpContextSkipGlobalErrorUi()
    });
  }

  /** Net balance for deposit type up to asOfDate (yyyy-MM-dd), same rule as create validation. */
  getAvailableBalance(
    depositType: BankDepositType,
    asOfDate: string
  ): Observable<ApiResponse<BankDepositAvailableBalance>> {
    const params = new HttpParams()
      .set('depositType', String(depositType))
      .set('asOfDate', asOfDate);
    return this.http.get<ApiResponse<BankDepositAvailableBalance>>(`${this.baseUrl}/available-balance`, {
      params
    });
  }

  list(year: number, month: number, page: number, pageSize: number): Observable<ApiResponse<PagedResult<BankDepositListItem>>> {
    const params = new HttpParams()
      .set('year', year)
      .set('month', month)
      .set('page', page)
      .set('pageSize', pageSize);
    return this.http.get<ApiResponse<PagedResult<BankDepositListItem>>>(this.baseUrl, { params });
  }

  previewNumber(year: number): Observable<ApiResponse<string>> {
    return this.http.get<ApiResponse<string>>(`${this.baseUrl}/preview-number`, {
      params: new HttpParams().set('year', year)
    });
  }

  cancel(id: string, cancellationReason: string): Observable<ApiResponse<object>> {
    // 403 (payments:update) is handled locally by the deposit cancellation screen — suppress the
    // duplicate global "ACCÈS REFUSÉ" modal via SKIP_ERROR_TOAST.
    return this.http.post<ApiResponse<object>>(`${this.baseUrl}/${id}/cancel`, { cancellationReason }, {
      context: createHttpContextSkipGlobalErrorUi()
    });
  }
}
