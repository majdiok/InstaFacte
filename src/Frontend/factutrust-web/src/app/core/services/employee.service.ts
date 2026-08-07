import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '@environments/environment';
import type { EmployeeAdvance, LeaveRequest, LeaveBalance, TerminateEmployeeRequest, TerminateEmployeeResult } from './payroll.service';

export interface ApiResponse<T> {
  success: boolean;
  data: T;
  message?: string;
  errors?: string[];
}

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

export interface EmployeeListItem {
  id: string;
  employeeNumber: string;
  firstName: string;
  lastName: string;
  fullName: string;
  cnssNumber?: string;
  jobTitle?: string;
  currentBaseSalary?: number;
  currentWeeklyRegime?: string;
  hireDate: string;
  isActive: boolean;
  parentClaimsStatus?: string;
}

export interface DependentParentClaim {
  id?: string;
  parentCin: string;
  kinship: string;
  firstName?: string;
  lastName?: string;
}

export interface ContractAllowance {
  id?: string;
  label: string;
  amount: number;
  taxable: boolean;
  subjectToCnss: boolean;
}

export interface EmploymentContract {
  id: string;
  type: string;
  typeDisplay: string;
  regime: string;
  regimeDisplay: string;
  weeklyRegime: string;
  weeklyRegimeDisplay: string;
  startDate: string;
  endDate?: string;
  baseSalary: number;
  workAccidentRate: number;
  jobTitle?: string;
  isActive: boolean;
  allowances: ContractAllowance[];
  civpStartDate?: string;
  civpEndDate?: string;
  civpStateGrant?: number;
  civpEmployerAllowance?: number;
  anetiReference?: string;
}

export interface EmployeeDetail {
  id: string;
  employeeNumber: string;
  firstName: string;
  lastName: string;
  fullName: string;
  cin?: string;
  cnssNumber?: string;
  category?: string;
  echelon?: string;
  dateOfBirth?: string;
  hireDate: string;
  terminationDate?: string;
  maritalStatus: string;
  maritalStatusDisplay: string;
  isHeadOfFamily: boolean;
  dependentChildren: number;
  studentChildren: number;
  disabledChildren: number;
  dependentParents: number;
  dependentParentClaims?: DependentParentClaim[];
  parentClaimsStatus?: string;
  address?: {
    street?: string;
    streetLine2?: string;
    city?: string;
    postalCode?: string;
    governorate?: string;
    country?: string;
    fullAddress?: string;
  };
  email?: string;
  phone?: string;
  rib?: string;
  isActive: boolean;
  contracts: EmploymentContract[];
}

export interface CreateEmployeeRequest {
  employeeNumber: string;
  firstName: string;
  lastName: string;
  cin?: string;
  cnssNumber?: string;
  category?: string;
  echelon?: string;
  dateOfBirth?: string;
  hireDate: string;
  maritalStatus?: string;
  isHeadOfFamily?: boolean;
  dependentChildren?: number;
  studentChildren?: number;
  disabledChildren?: number;
  dependentParents?: number;
  dependentParentClaims?: DependentParentClaim[];
  street?: string;
  streetLine2?: string;
  city?: string;
  postalCode?: string;
  governorate?: string;
  email?: string;
  phone?: string;
  rib?: string;
}

export interface UpdateEmployeeRequest extends Omit<CreateEmployeeRequest, 'employeeNumber' | 'hireDate'> {}

export interface CreateContractRequest {
  type: string;
  regime: string;
  weeklyRegime?: string;
  startDate: string;
  endDate?: string;
  baseSalary: number;
  workAccidentRate: number;
  jobTitle?: string;
  allowances?: ContractAllowance[];
  civpStartDate?: string;
  civpEndDate?: string;
  civpStateGrant?: number;
  civpEmployerAllowance?: number;
  anetiReference?: string;
}

@Injectable({ providedIn: 'root' })
export class EmployeeService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/payroll/employees`;

  list(search?: string, isActive?: boolean, page = 1, pageSize = 20): Observable<ApiResponse<PagedResult<EmployeeListItem>>> {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    if (search) params = params.set('search', search);
    if (isActive !== undefined) params = params.set('isActive', isActive);
    return this.http.get<ApiResponse<PagedResult<EmployeeListItem>>>(this.baseUrl, { params });
  }

  getById(id: string): Observable<ApiResponse<EmployeeDetail>> {
    return this.http.get<ApiResponse<EmployeeDetail>>(`${this.baseUrl}/${id}`);
  }

  create(body: CreateEmployeeRequest): Observable<ApiResponse<string>> {
    return this.http.post<ApiResponse<string>>(this.baseUrl, body);
  }

  update(id: string, body: UpdateEmployeeRequest): Observable<ApiResponse<unknown>> {
    return this.http.put<ApiResponse<unknown>>(`${this.baseUrl}/${id}`, body);
  }

  toggleActive(id: string): Observable<ApiResponse<boolean>> {
    return this.http.patch<ApiResponse<boolean>>(`${this.baseUrl}/${id}/toggle-active`, {});
  }

  delete(id: string): Observable<ApiResponse<unknown>> {
    return this.http.delete<ApiResponse<unknown>>(`${this.baseUrl}/${id}`);
  }

  addContract(employeeId: string, body: CreateContractRequest): Observable<ApiResponse<string>> {
    return this.http.post<ApiResponse<string>>(`${this.baseUrl}/${employeeId}/contracts`, body);
  }

  updateContract(contractId: string, body: CreateContractRequest & { isActive: boolean }): Observable<ApiResponse<unknown>> {
    return this.http.put<ApiResponse<unknown>>(`${this.baseUrl}/contracts/${contractId}`, body);
  }

  exportCivpAttestationPdf(contractId: string): Observable<Blob> {
    return this.http.get(`${this.baseUrl}/contracts/${contractId}/civp-attestation/pdf`, { responseType: 'blob' });
  }

  deleteContract(contractId: string): Observable<ApiResponse<unknown>> {
    return this.http.delete<ApiResponse<unknown>>(`${this.baseUrl}/contracts/${contractId}`);
  }

  listLeaves(employeeId: string): Observable<ApiResponse<LeaveRequest[]>> {
    return this.http.get<ApiResponse<LeaveRequest[]>>(`${this.baseUrl}/${employeeId}/leaves`);
  }

  listAdvances(employeeId: string): Observable<ApiResponse<EmployeeAdvance[]>> {
    return this.http.get<ApiResponse<EmployeeAdvance[]>>(`${this.baseUrl}/${employeeId}/advances`);
  }

  getLeaveBalance(employeeId: string, year: number): Observable<ApiResponse<LeaveBalance>> {
    const params = new HttpParams().set('year', year);
    return this.http.get<ApiResponse<LeaveBalance>>(`${this.baseUrl}/${employeeId}/leave-balance`, { params });
  }

  setLeaveOpeningBalance(employeeId: string, openingBalanceDays: number): Observable<ApiResponse<unknown>> {
    return this.http.put<ApiResponse<unknown>>(`${this.baseUrl}/${employeeId}/leave-balance/opening`, { openingBalanceDays });
  }

  terminate(id: string, body: TerminateEmployeeRequest): Observable<ApiResponse<TerminateEmployeeResult>> {
    return this.http.post<ApiResponse<TerminateEmployeeResult>>(`${this.baseUrl}/${id}/terminate`, body);
  }
}
