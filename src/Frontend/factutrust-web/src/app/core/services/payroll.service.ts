import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '@environments/environment';
import type { ApiResponse } from './employee.service';

export interface PayrollRunListItem {
  id: string;
  year: number;
  month: number;
  label: string;
  status: string;
  statusDisplay: string;
  payslipCount: number;
  totalGross: number;
  totalNet: number;
  validatedAt?: string;
  closedAt?: string;
}

export interface PayslipListItem {
  id: string;
  employeeId: string;
  employeeName: string;
  employeeNumber: string;
  grossSalary: number;
  cnssEmployee: number;
  irpp: number;
  css: number;
  netSalary: number;
}

export interface PayrollRunDetail {
  id: string;
  year: number;
  month: number;
  label: string;
  status: string;
  statusDisplay: string;
  parametersFiscalYear: number;
  totalGross: number;
  totalCnssEmployee: number;
  totalIrpp: number;
  totalCss: number;
  totalNet: number;
  totalCnssEmployer: number;
  totalTfp: number;
  totalFoprolos: number;
  totalWorkAccident: number;
  totalOtherDeductions?: number;
  calculatedAt?: string;
  validatedAt?: string;
  closedAt?: string;
  payslips: PayslipListItem[];
  overtimeLines?: PayrollOvertimeLine[];
}

export interface PayrollOvertimeLine {
  id: string;
  employeeId: string;
  employeeName?: string;
  year: number;
  month: number;
  hours: number;
  ratePercent: number;
  ratePercentDisplay: string;
  computedAmount: number;
  overrideAmount?: number;
  isOverridden: boolean;
  effectiveAmount: number;
}

export interface UpsertOvertimeLineRequest {
  employeeId: string;
  year: number;
  month: number;
  hours: number;
  ratePercent: number;
  overrideAmount?: number;
}

export interface LeaveBalance {
  employeeId: string;
  year: number;
  openingBalance: number;
  accruedInYear: number;
  totalAcquired: number;
  consumed: number;
  remaining: number;
}

export interface PayslipDetail extends PayslipListItem {
  payrollRunId: string;
  cnssNumber?: string;
  cin?: string;
  hireDate?: string;
  leaveBalanceRemaining?: number;
  year: number;
  month: number;
  cnssableGross: number;
  taxableBaseAfterCnss: number;
  professionalExpenses: number;
  familyDeductions: number;
  netSalary: number;
  lines: { order: number; label: string; kind: string; amount: number; base?: number; rate?: number }[];
}

export interface PayrollParameters {
  id: string;
  fiscalYear: number;
  cnssEmployeeRate: number;
  cnssEmployerRate: number;
  cnssEmployeeRateRsa: number;
  cnssEmployerRateRsa: number;
  enforceSmigOnContracts: boolean;
  enableExtendedOvertimeRates: boolean;
  enableAllowanceQuadrantMatrix: boolean;
  cssRate: number;
  cssAnnualExemptionThreshold: number;
  professionalExpensesRate: number;
  professionalExpensesAnnualCap: number;
  headOfFamilyAnnualDeduction: number;
  childAnnualDeduction: number;
  maxDeductibleChildren: number;
  studentChildAnnualDeduction: number;
  disabledChildAnnualDeduction: number;
  parentDeductionRatePercent: number;
  parentAnnualDeductionCap: number;
  isIndustrialSector: boolean;
  tfpRateIndustry: number;
  tfpRateOther: number;
  foprolosRate: number;
  monthlySmig: number;
  irppBrackets: { lowerBound: number; rate: number }[];
}

export interface DtsDeclaration {
  year: number;
  quarter: number;
  totalGross: number;
  totalCnssableGross: number;
  totalCnssEmployee: number;
  totalCnssEmployer: number;
  totalContributions: number;
  employeeCount: number;
  includedMonths: number[];
  missingMonths: number[];
  isComplete: boolean;
  lines: {
    employeeId: string;
    employeeName: string;
    cnssNumber?: string;
    totalGross: number;
    totalCnssableGross: number;
    cnssEmployee: number;
    cnssEmployer: number;
    monthsCount: number;
  }[];
}

export interface LeaveRequest {
  id: string;
  employeeId: string;
  type: string;
  typeDisplay: string;
  startDate: string;
  endDate: string;
  days: number;
  reason?: string;
  isApproved: boolean;
  approvedAt?: string;
}

export interface EmployeeAdvance {
  id: string;
  employeeId: string;
  employeeName?: string;
  date: string;
  amount: number;
  reason?: string;
  isSettled: boolean;
}

export interface CreateAdvanceRequest {
  employeeId: string;
  date: string;
  amount: number;
  reason?: string;
}

@Injectable({ providedIn: 'root' })
export class PayrollService {
  private readonly http = inject(HttpClient);
  private readonly runsUrl = `${environment.apiUrl}/payroll/runs`;
  private readonly settingsUrl = `${environment.apiUrl}/payroll/settings`;
  private readonly payrollUrl = `${environment.apiUrl}/payroll`;

  listRuns(year?: number): Observable<ApiResponse<PayrollRunListItem[]>> {
    let params = new HttpParams();
    if (year) params = params.set('year', year);
    return this.http.get<ApiResponse<PayrollRunListItem[]>>(this.runsUrl, { params });
  }

  getRun(id: string): Observable<ApiResponse<PayrollRunDetail>> {
    return this.http.get<ApiResponse<PayrollRunDetail>>(`${this.runsUrl}/${id}`);
  }

  createRun(year: number, month: number): Observable<ApiResponse<string>> {
    return this.http.post<ApiResponse<string>>(this.runsUrl, { year, month });
  }

  // Le secteur TFP (industrie 1 % / autres 2 %) est lu côté serveur depuis les
  // paramètres de l'exercice — il n'est plus transmis à chaque calcul.
  calculateRun(id: string): Observable<ApiResponse<unknown>> {
    return this.http.post<ApiResponse<unknown>>(`${this.runsUrl}/${id}/calculate`, {
      settleOutstandingAdvances: true
    });
  }

  validateRun(id: string): Observable<ApiResponse<unknown>> {
    return this.http.post<ApiResponse<unknown>>(`${this.runsUrl}/${id}/validate`, {});
  }

  closeRun(id: string): Observable<ApiResponse<unknown>> {
    return this.http.post<ApiResponse<unknown>>(`${this.runsUrl}/${id}/close`, {});
  }

  reopenRun(id: string): Observable<ApiResponse<unknown>> {
    return this.http.post<ApiResponse<unknown>>(`${this.runsUrl}/${id}/reopen`, {});
  }

  getPayslip(id: string): Observable<ApiResponse<PayslipDetail>> {
    return this.http.get<ApiResponse<PayslipDetail>>(`${this.payrollUrl}/payslips/${id}`);
  }

  downloadPayslipPdf(id: string): Observable<Blob> {
    return this.http.get(`${this.payrollUrl}/payslips/${id}/pdf`, { responseType: 'blob' });
  }

  getParameters(fiscalYear: number): Observable<ApiResponse<PayrollParameters>> {
    return this.http.get<ApiResponse<PayrollParameters>>(`${this.settingsUrl}/parameters/${fiscalYear}`);
  }

  updateParameters(fiscalYear: number, body: Omit<PayrollParameters, 'id' | 'fiscalYear'>): Observable<ApiResponse<unknown>> {
    return this.http.put<ApiResponse<unknown>>(`${this.settingsUrl}/parameters/${fiscalYear}`, body);
  }

  getDts(year: number, quarter: number): Observable<ApiResponse<DtsDeclaration>> {
    const params = new HttpParams().set('year', year).set('quarter', quarter);
    return this.http.get<ApiResponse<DtsDeclaration>>(`${this.payrollUrl}/declarations/dts`, { params });
  }

  createLeave(body: { employeeId: string; type: string; startDate: string; endDate: string; days: number; reason?: string }): Observable<ApiResponse<string>> {
    return this.http.post<ApiResponse<string>>(`${this.payrollUrl}/leaves`, body);
  }

  approveLeave(id: string): Observable<ApiResponse<unknown>> {
    return this.http.post<ApiResponse<unknown>>(`${this.payrollUrl}/leaves/${id}/approve`, {});
  }

  deleteLeave(id: string): Observable<ApiResponse<unknown>> {
    return this.http.delete<ApiResponse<unknown>>(`${this.payrollUrl}/leaves/${id}`);
  }

  createAdvance(body: CreateAdvanceRequest): Observable<ApiResponse<string>> {
    return this.http.post<ApiResponse<string>>(`${this.payrollUrl}/advances`, body);
  }

  deleteAdvance(id: string): Observable<ApiResponse<unknown>> {
    return this.http.delete<ApiResponse<unknown>>(`${this.payrollUrl}/advances/${id}`);
  }

  listOvertime(year: number, month: number): Observable<ApiResponse<PayrollOvertimeLine[]>> {
    const params = new HttpParams().set('year', year).set('month', month);
    return this.http.get<ApiResponse<PayrollOvertimeLine[]>>(`${this.payrollUrl}/overtime`, { params });
  }

  createOvertime(body: UpsertOvertimeLineRequest): Observable<ApiResponse<string>> {
    return this.http.post<ApiResponse<string>>(`${this.payrollUrl}/overtime`, body);
  }

  updateOvertime(id: string, body: UpsertOvertimeLineRequest): Observable<ApiResponse<unknown>> {
    return this.http.put<ApiResponse<unknown>>(`${this.payrollUrl}/overtime/${id}`, body);
  }

  deleteOvertime(id: string): Observable<ApiResponse<unknown>> {
    return this.http.delete<ApiResponse<unknown>>(`${this.payrollUrl}/overtime/${id}`);
  }

  previewOvertime(body: { baseSalary: number; hours: number; ratePercent: number; overrideAmount?: number; employeeId?: string }): Observable<ApiResponse<{ hourlyRate: number; computedAmount: number; effectiveAmount: number; isOverridden: boolean }>> {
    return this.http.post<ApiResponse<{ hourlyRate: number; computedAmount: number; effectiveAmount: number; isOverridden: boolean }>>(`${this.payrollUrl}/overtime/preview`, body);
  }

  computeLeaveDays(startDate: string, endDate: string): Observable<ApiResponse<number>> {
    const params = new HttpParams().set('startDate', startDate).set('endDate', endDate);
    return this.http.get<ApiResponse<number>>(`${this.payrollUrl}/leaves/compute-days`, { params });
  }

  exportDtsCsv(year: number, quarter: number): Observable<Blob> {
    const params = new HttpParams().set('year', year).set('quarter', quarter);
    return this.http.get(`${this.payrollUrl}/declarations/dts/export`, { params, responseType: 'blob' });
  }
}
