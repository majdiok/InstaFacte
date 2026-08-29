import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable, shareReplay, catchError, throwError, map } from 'rxjs';
import { environment } from '@environments/environment';
import { createHttpContextSkipGlobalErrorUi } from '@core/http-context';
import { ApiResponse, PagedResult } from './client.service';

export enum CashOperationStatus {
  Terminee = 0,
  Annulee = 1
}

export enum CashOperationType {
  Debit = 0,
  Credit = 1
}

export enum CashOperationOrigin {
  Manual = 0,
  InvoicePayment = 1,
  SupplierPayment = 2
}

export enum CashExpenseCategory {
  RentPayment = 0,
  SuppliesAndConsumables = 1,
  MaintenanceAndRepair = 2,
  NetSalaries = 3,
  TransportCosts = 4,
  TravelAndTrips = 5,
  VehicleRepairMaintenance = 6,
  VehicleRentalAndTransport = 7,
  UtilitiesAndEnergy = 8,
  ProfessionalFees = 9,
  Insurance = 10,
  TaxesAndDuties = 11,
  MarketingAdvertising = 12,
  ITAndSoftware = 13,
  BankDeposit = 14,
  SupplierInvoicePayment = 15,
  Other = 99
}

export enum CashRevenueCategory {
  CashSalesReceipt = 0,
  ClientReceivablesReceipt = 1,
  PartnerContributionsReceipt = 2,
  BankCreditReceipt = 3,
  Other = 99
}

export interface CashExpenseCategoryOption {
  label: string;
  value: CashExpenseCategory;
  icon: string;
}

export interface CashExpenseCategoryGroup {
  label: string;
  items: CashExpenseCategoryOption[];
}

export interface CashRevenueCategoryOption {
  label: string;
  value: CashRevenueCategory;
  icon: string;
}

export interface CashRevenueCategoryGroup {
  label: string;
  items: CashRevenueCategoryOption[];
}

export const CASH_EXPENSE_CATEGORY_GROUPS: CashExpenseCategoryGroup[] = [
  {
    label: 'Locaux & immobilier',
    items: [
      { label: 'Paiement des loyers', value: CashExpenseCategory.RentPayment, icon: 'pi pi-building' },
      {
        label: 'Charges locatives et énergie (eau, électricité, gaz)',
        value: CashExpenseCategory.UtilitiesAndEnergy,
        icon: 'pi pi-bolt'
      }
    ]
  },
  {
    label: 'Achats & exploitation',
    items: [
      {
        label: "Règlement des factures d'achats de fournitures et consommables",
        value: CashExpenseCategory.SuppliesAndConsumables,
        icon: 'pi pi-shopping-cart'
      },
      {
        label: 'Règlement facture fournisseur',
        value: CashExpenseCategory.SupplierInvoicePayment,
        icon: 'pi pi-file-import'
      },
      {
        label: "Paiement des factures d'entretien et réparation",
        value: CashExpenseCategory.MaintenanceAndRepair,
        icon: 'pi pi-wrench'
      }
    ]
  },
  {
    label: 'Ressources humaines',
    items: [{ label: 'Paiement des salaires nets', value: CashExpenseCategory.NetSalaries, icon: 'pi pi-users' }]
  },
  {
    label: 'Transport & véhicules',
    items: [
      { label: 'Paiement des frais de transport', value: CashExpenseCategory.TransportCosts, icon: 'pi pi-car' },
      { label: 'Voyages et déplacements', value: CashExpenseCategory.TravelAndTrips, icon: 'pi pi-map' },
      {
        label: 'Véhicule (réparation, entretien)',
        value: CashExpenseCategory.VehicleRepairMaintenance,
        icon: 'pi pi-cog'
      },
      {
        label: 'Location véhicules et transport',
        value: CashExpenseCategory.VehicleRentalAndTransport,
        icon: 'pi pi-truck'
      }
    ]
  },
  {
    label: 'Fiscalité & services',
    items: [
      { label: 'Honoraires et prestations externes', value: CashExpenseCategory.ProfessionalFees, icon: 'pi pi-briefcase' },
      { label: 'Assurances', value: CashExpenseCategory.Insurance, icon: 'pi pi-shield' },
      { label: 'Impôts, taxes et redevances', value: CashExpenseCategory.TaxesAndDuties, icon: 'pi pi-percentage' }
    ]
  },
  {
    label: 'Commercial & digital',
    items: [
      { label: 'Marketing et publicité', value: CashExpenseCategory.MarketingAdvertising, icon: 'pi pi-megaphone' },
      { label: 'Informatique et logiciels', value: CashExpenseCategory.ITAndSoftware, icon: 'pi pi-desktop' }
    ]
  },
  {
    label: 'Trésorerie',
    items: [{ label: 'Remise en banque', value: CashExpenseCategory.BankDeposit, icon: 'pi pi-building' }]
  },
  {
    label: 'Autres',
    items: [{ label: 'Autre', value: CashExpenseCategory.Other, icon: 'pi pi-ellipsis-h' }]
  }
];

export const CASH_REVENUE_CATEGORY_GROUPS: CashRevenueCategoryGroup[] = [
  {
    label: 'Encaissements',
    items: [
      { label: 'Encaissement ventes au comptant', value: CashRevenueCategory.CashSalesReceipt, icon: 'pi pi-shopping-bag' },
      { label: 'Encaissement créances clients', value: CashRevenueCategory.ClientReceivablesReceipt, icon: 'pi pi-users' },
      { label: 'Encaissements reçus des associés', value: CashRevenueCategory.PartnerContributionsReceipt, icon: 'pi pi-sitemap' },
      { label: 'Encaissement crédit bancaire', value: CashRevenueCategory.BankCreditReceipt, icon: 'pi pi-building' },
      { label: 'Autres encaissements', value: CashRevenueCategory.Other, icon: 'pi pi-ellipsis-h' }
    ]
  }
];

export function getCashExpenseCategoryLabel(value: number | null | undefined): string | undefined {
  if (value === null || value === undefined) return undefined;
  for (const g of CASH_EXPENSE_CATEGORY_GROUPS) {
    const item = g.items.find(i => i.value === value);
    if (item) return item.label;
  }
  return undefined;
}

export function getCashRevenueCategoryLabel(value: number | null | undefined): string | undefined {
  if (value === null || value === undefined) return undefined;
  for (const g of CASH_REVENUE_CATEGORY_GROUPS) {
    const item = g.items.find(i => i.value === value);
    if (item) return item.label;
  }
  return undefined;
}

/** Matches backend PaymentMethod (JsonStringEnumConverter camelCase). */
const PAYMENT_METHOD_FROM_API: Record<string, number> = {
  cash: 0,
  bankTransfer: 1,
  check: 2,
  card: 3,
  mobilePayment: 4,
  other: 99,
  Cash: 0,
  BankTransfer: 1,
  Check: 2,
  Card: 3,
  MobilePayment: 4,
  Other: 99
};

const CASH_OP_TYPE_FROM_API: Record<string, number> = {
  debit: CashOperationType.Debit,
  credit: CashOperationType.Credit,
  Debit: CashOperationType.Debit,
  Credit: CashOperationType.Credit
};

const CASH_OP_STATUS_FROM_API: Record<string, number> = {
  terminee: CashOperationStatus.Terminee,
  annulee: CashOperationStatus.Annulee,
  Terminee: CashOperationStatus.Terminee,
  Annulee: CashOperationStatus.Annulee
};

const CASH_OP_ORIGIN_FROM_API: Record<string, number> = {
  manual: CashOperationOrigin.Manual,
  invoicePayment: CashOperationOrigin.InvoicePayment,
  supplierPayment: CashOperationOrigin.SupplierPayment,
  Manual: CashOperationOrigin.Manual,
  InvoicePayment: CashOperationOrigin.InvoicePayment,
  SupplierPayment: CashOperationOrigin.SupplierPayment
};

function coerceEnumFromApi(
  value: number | string,
  namedMap: Record<string, number>
): number {
  if (typeof value === 'number' && Number.isFinite(value)) return value;
  if (typeof value !== 'string') return Number.NaN;
  const s = value.trim();
  if (/^-?\d+$/.test(s)) return parseInt(s, 10);
  const direct = namedMap[s];
  if (direct !== undefined) return direct;
  const camel = s.length ? s.charAt(0).toLowerCase() + s.slice(1) : s;
  return namedMap[camel] ?? Number.NaN;
}

/**
 * API returns PaymentMethod as string enums; UI compares numeric codes (e.g. cash desk, payloads).
 */
export function normalizePaymentMethod(value: number | string): number {
  return coerceEnumFromApi(value, PAYMENT_METHOD_FROM_API);
}

export function normalizeCashOperationType(value: number | string): number {
  return coerceEnumFromApi(value, CASH_OP_TYPE_FROM_API);
}

export function normalizeCashOperationStatus(value: number | string): number {
  return coerceEnumFromApi(value, CASH_OP_STATUS_FROM_API);
}

export function normalizeCashOperationOrigin(value: number | string): number {
  return coerceEnumFromApi(value, CASH_OP_ORIGIN_FROM_API);
}

export interface CashOperationListItem {
  id: string;
  operationType: number;
  operationTypeDisplay: string;
  operationDate: string;
  method: number;
  methodDisplay: string;
  label: string;
  category: number | null;
  categoryDisplay: string | null;
  revenueCategory: number | null;
  revenueCategoryDisplay: string | null;
  document: string;
  amount: number;
  currency: string;
  reference: string | null;
  notes: string | null;
  status: CashOperationStatus;
  origin: CashOperationOrigin;
  sourceType: string | null;
  sourceId: string | null;
  sourceInvoiceId: string | null;
  sourceInvoiceNumber: string | null;
  sourceSupplierInvoiceId: string | null;
  sourceSupplierInvoiceNumber: string | null;
  vatRatePercent?: number | null;
  htAmount?: number | null;
  vatAmount?: number | null;
}

/** Taux de TVA proposés pour les encaissements « ventes au comptant » (cf. VatRate backend : Exempt=0, Reduced=7, Intermediate=13, Standard=19). */
export interface VatRateOption {
  label: string;
  value: number | null;
}

export const VAT_RATE_OPTIONS: VatRateOption[] = [
  { label: 'Non renseignée', value: null },
  { label: '0 % Exonéré', value: 0 },
  { label: '7 %', value: 7 },
  { label: '13 %', value: 13 },
  { label: '19 %', value: 19 }
];

export interface CashDeskFeatureFlags {
  vatEnabled: boolean;
}

export interface CashDeskBalanceRow {
  method: number;
  methodDisplay: string;
  amount: number;
  credits: number;
  debits: number;
}

export interface CashDeskBalances {
  currency: string;
  primary: CashDeskBalanceRow[];
  secondary: CashDeskBalanceRow[];
  primaryTotal: number;
  secondaryTotal: number;
  primaryCreditsTotal: number;
  primaryDebitsTotal: number;
  secondaryCreditsTotal: number;
  secondaryDebitsTotal: number;
}

function normalizeBalanceRow(row: CashDeskBalanceRow): CashDeskBalanceRow {
  return {
    ...row,
    method: normalizePaymentMethod(row.method as number | string)
  };
}

function normalizeCashDeskBalancesData(data: CashDeskBalances): CashDeskBalances {
  return {
    ...data,
    primary: data.primary.map(normalizeBalanceRow),
    secondary: data.secondary.map(normalizeBalanceRow)
  };
}

function normalizeCashOperationListItem(op: CashOperationListItem): CashOperationListItem {
  return {
    ...op,
    method: normalizePaymentMethod(op.method as number | string),
    operationType: normalizeCashOperationType(op.operationType as number | string),
    status: normalizeCashOperationStatus(op.status as number | string),
    origin: normalizeCashOperationOrigin(op.origin as number | string),
    sourceSupplierInvoiceId: op.sourceSupplierInvoiceId ?? null,
    sourceSupplierInvoiceNumber: op.sourceSupplierInvoiceNumber ?? null
  };
}

export interface CreateCashOperationPayload {
  operationType: number;
  operationDate: string;
  amount: number;
  method: number;
  label: string;
  category?: number | null;
  revenueCategory?: number | null;
  reference?: string | null;
  notes?: string | null;
  vatRate?: number | null;
}

@Injectable({
  providedIn: 'root'
})
export class CashDeskService {
  private readonly API_URL = `${environment.apiUrl}/cash-desk`;
  private readonly http = inject(HttpClient);

  private balancesCache = new Map<string, Observable<ApiResponse<CashDeskBalances>>>();
  private operationsCache = new Map<string, Observable<ApiResponse<PagedResult<CashOperationListItem>>>>();

  getBalances(year: number, month: number): Observable<ApiResponse<CashDeskBalances>> {
    const cacheKey = `${year}-${month}`;
    const cached = this.balancesCache.get(cacheKey);
    if (cached) return cached;

    const request$ = this.http
      .get<ApiResponse<CashDeskBalances>>(`${this.API_URL}/balances`, {
        params: new HttpParams().set('year', year).set('month', month)
      })
      .pipe(
        map(res =>
          res.success && res.data ? { ...res, data: normalizeCashDeskBalancesData(res.data) } : res
        ),
        shareReplay(1),
        catchError(err => {
          this.balancesCache.delete(cacheKey);
          return throwError(() => err);
        })
      );

    this.balancesCache.set(cacheKey, request$);
    return request$;
  }

  getOperations(
    year: number,
    month: number,
    page: number,
    pageSize: number
  ): Observable<ApiResponse<PagedResult<CashOperationListItem>>> {
    const cacheKey = `${year}-${month}-${page}-${pageSize}`;
    const cached = this.operationsCache.get(cacheKey);
    if (cached) return cached;

    const params = new HttpParams()
      .set('year', year)
      .set('month', month)
      .set('page', page)
      .set('pageSize', pageSize);

    const request$ = this.http
      .get<ApiResponse<PagedResult<CashOperationListItem>>>(`${this.API_URL}/operations`, { params })
      .pipe(
        map(res =>
          res.success && res.data
            ? {
                ...res,
                data: {
                  ...res.data,
                  items: res.data.items.map(normalizeCashOperationListItem)
                }
              }
            : res
        ),
        shareReplay(1),
        catchError(err => {
          this.operationsCache.delete(cacheKey);
          return throwError(() => err);
        })
      );

    this.operationsCache.set(cacheKey, request$);
    return request$;
  }

  createOperation(payload: CreateCashOperationPayload): Observable<ApiResponse<CashOperationListItem>> {
    // 403 (payments:create) is handled locally by the dialog (French inline message) — suppress the
    // duplicate global "ACCÈS REFUSÉ" modal via SKIP_ERROR_TOAST.
    return this.http.post<ApiResponse<CashOperationListItem>>(`${this.API_URL}/operations`, payload, {
      context: createHttpContextSkipGlobalErrorUi()
    });
  }

  getFeatureFlags(): Observable<ApiResponse<CashDeskFeatureFlags>> {
    return this.http.get<ApiResponse<CashDeskFeatureFlags>>(`${this.API_URL}/feature-flags`);
  }

  cancelOperation(operationId: string, reason: string): Observable<ApiResponse<object>> {
    // 403 (payments:update) is handled locally by the cancel dialog — suppress the duplicate global modal.
    return this.http.post<ApiResponse<object>>(
      `${this.API_URL}/operations/${operationId}/cancel`,
      { cancellationReason: reason },
      { context: createHttpContextSkipGlobalErrorUi() }
    );
  }

  invalidateCachesForPeriod(year: number, month: number): void {
    const balancesKey = `${year}-${month}`;
    this.balancesCache.delete(balancesKey);

    const prefix = `${year}-${month}-`;
    for (const key of Array.from(this.operationsCache.keys())) {
      if (key.startsWith(prefix)) this.operationsCache.delete(key);
    }
  }

  /**
   * Clears cached HTTP streams for cash desk data affected by a movement on operationDateIso (YYYY-MM-DD).
   * Balance panels for months m..12 in the same year share a YTD window that includes this date — all those balance keys are dropped.
   * Operation rows are listed by operation month only — keys for that month are dropped.
   */
  invalidateCachesAfterCashLedgerMutation(operationDateIso: string): void {
    const trimmed = operationDateIso?.trim();
    if (!trimmed) return;

    const parts = trimmed.split('-');
    if (parts.length < 2) return;

    const year = Number(parts[0]);
    const month = Number(parts[1]);
    if (!Number.isFinite(year) || !Number.isFinite(month) || month < 1 || month > 12) return;

    for (let m = month; m <= 12; m++) {
      this.balancesCache.delete(`${year}-${m}`);
    }

    const opPrefix = `${year}-${month}-`;
    for (const key of Array.from(this.operationsCache.keys())) {
      if (key.startsWith(opPrefix)) this.operationsCache.delete(key);
    }
  }
}
