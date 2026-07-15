import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '@environments/environment';
import { createHttpContextSkipGlobalErrorUi } from '@core/http-context';

export enum IdentificationType {
  MatriculeFiscal = 1,
  CIN = 2,
  Passport = 3,
  CarteSejour = 4,
  Autre = 5
}

export enum BeneficiaryCategory {
  PersonnePhysique = 0,
  PersonneMorale = 1
}

export enum BeneficiaryType {
  Client = 0,
  Supplier = 1,
  Employee = 2,
  Other = 3
}

export enum TejSubmissionType {
  Initiale = 0,
  Corrective = 1
}

export interface WithholdingTaxTypeDto {
  id: string;
  code: string;
  category: number;
  categoryLabel: string;
  label: string;
  labelAr: string | null;
  defaultRate: number;
  articleReference: string | null;
  applicableToResident: boolean;
  applicableToNonResident: boolean;
  minimumThreshold: number | null;
  isActive: boolean;
  isSystem: boolean;
  displayOrder: number;
}

export interface WithholdingCalculationRequest {
  amountHT: number;
  vatRate?: number;
  operationCode: string;
  isResident: boolean;
  hasCNPC: boolean;
  hasPriseEnCharge: boolean;
}

export interface WithholdingCalculationResult {
  grossAmountHT: number;
  vatAmount: number;
  amountTTC: number;
  withholdingRate: number;
  withholdingAmount: number;
  netAmountPaid: number;
  vatWithholdingAmount: number | null;
}

export interface TejXmlExportRequest {
  exerciceYear: number;
  month: number;
  submissionType: TejSubmissionType;
}

export interface TejXmlPreviewResult {
  fileName: string;
  xmlContent: string;
  validationErrors: string[];
  certificateCount: number;
  isValid: boolean;
}

export interface TejXmlExportLogListItem {
  id: string;
  createdAt: string;
  fileName: string;
  year: number;
  month: number;
  submissionType: number;
  sha256Hex: string;
  certificateCount: number;
  isValid: boolean;
  exportedByEmail: string | null;
}

export interface WithholdingDashboardMonthBreakdownDto {
  month: number;
  certificateCount: number;
  totalHT: number;
  totalWithheld: number;
  totalNetPaid: number;
}

export interface ClientWithholdingSubieMonthDto {
  month: number;
  totalSubie: number;
  paymentCount: number;
}

export interface WithholdingDashboardDto {
  dashboardYear: number;
  totalWithheldForYear: number;
  totalCertificatesForYear: number;
  draftCertificates: number;
  validatedCertificates: number;
  submittedCertificates: number;
  nextTejDeadline: string | null;
  monthlyBreakdown: WithholdingDashboardMonthBreakdownDto[];
  alerts: string[];
  totalClientWithholdingSubieForYear: number;
  clientWithholdingSubieByMonth: ClientWithholdingSubieMonthDto[];
}

export interface WithholdingMonthlyReportDto {
  year: number;
  month: number;
  byCategory: { category: number; categoryLabel: string; totalHT: number; totalWithheld: number; certificateCount: number }[];
  grandTotalHT: number;
  grandTotalWithheld: number;
  totalCertificates: number;
}

@Injectable({
  providedIn: 'root'
})
export class WithholdingTaxService {
  private readonly API_URL = `${environment.apiUrl}/withholding-tax`;
  private http = inject(HttpClient);

  getTypes(activeOnly = true): Observable<WithholdingTaxTypeDto[]> {
    const params = new HttpParams().set('activeOnly', activeOnly.toString());
    return this.http.get<WithholdingTaxTypeDto[]>(`${this.API_URL}/types`, { params });
  }

  calculateWithholding(request: WithholdingCalculationRequest): Observable<WithholdingCalculationResult> {
    return this.http.post<WithholdingCalculationResult>(`${this.API_URL}/calculate`, request);
  }

  getTejEligibleInvoiceCount(year: number, month: number): Observable<{ count: number }> {
    const params = new HttpParams().set('year', year.toString()).set('month', month.toString());
    return this.http.get<{ count: number }>(`${this.API_URL}/tej-export/eligible-count`, { params });
  }

  generateXml(
    request: TejXmlExportRequest,
    options?: { skipGlobalErrorUi?: boolean }
  ): Observable<Blob> {
    return this.http.post(`${this.API_URL}/tej-export/generate`, request, {
      responseType: 'blob',
      ...(options?.skipGlobalErrorUi ? { context: createHttpContextSkipGlobalErrorUi() } : {})
    });
  }

  previewXml(
    request: TejXmlExportRequest,
    options?: { skipGlobalErrorUi?: boolean }
  ): Observable<TejXmlPreviewResult> {
    const httpOptions = options?.skipGlobalErrorUi
      ? { context: createHttpContextSkipGlobalErrorUi() }
      : {};
    return this.http.post<TejXmlPreviewResult>(`${this.API_URL}/tej-export/preview`, request, httpOptions);
  }

  getTejExportHistory(take = 50): Observable<{ items: TejXmlExportLogListItem[] }> {
    const params = new HttpParams().set('take', take.toString());
    return this.http.get<{ items: TejXmlExportLogListItem[] }>(`${this.API_URL}/tej-export/history`, { params });
  }

  getDashboard(year?: number): Observable<WithholdingDashboardDto> {
    let params = new HttpParams();
    if (year !== undefined) params = params.set('year', year.toString());
    return this.http.get<WithholdingDashboardDto>(`${this.API_URL}/dashboard`, { params });
  }

  getMonthlyReport(year: number, month: number): Observable<WithholdingMonthlyReportDto> {
    const params = new HttpParams().set('year', year.toString()).set('month', month.toString());
    return this.http.get<WithholdingMonthlyReportDto>(`${this.API_URL}/monthly-report`, { params });
  }
}
