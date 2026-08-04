import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '@environments/environment';
import { ApiResponse } from './auth.service';

export interface AccountingFirmDirectoryItem {
  firmTenantId: string;
  displayName: string;
  city: string;
  governorate: string;
  description?: string;
  professionalRegistrationNumber?: string;
}

export interface CompanyProfileSnapshot {
  schemaVersion: number;
  capturedAtUtc: string;
  companyName: string;
  tradeName?: string;
  nif: string;
  taxRegime: number;
  rneIdentifier?: string;
  street: string;
  streetLine2?: string;
  city: string;
  governorate: string;
  postalCode?: string;
  email: string;
  phone?: string;
}

export interface FirmClientAssignment {
  id: string;
  companyTenantId: string;
  companyName: string;
  firmTenantId: string;
  firmDisplayName: string;
  status: number | string;
  statusDisplay: string;
  requestedAt: string;
  respondedAt?: string;
  revokedAt?: string;
  notes?: string;
  rejectionReason?: string;
  companyProfile?: CompanyProfileSnapshot;
  /** True si le dossier a été créé et est géré par le cabinet (client sans compte plateforme). */
  isFirmManaged?: boolean;
}

export interface FirmClientDossier {
  assignmentId: string;
  companyTenantId: string;
  companyName: string;
  activeSince: string;
  hasPermanentFile?: boolean;
  permanentFileStatus?: number | null;
  permanentFileStatusDisplay?: string | null;
  assignedAccountantUserId?: string | null;
  assignedAccountantName?: string | null;
  isAwaitingAccountantAssignment?: boolean;
  /** True si le dossier a été créé et est géré par le cabinet (client sans compte plateforme). */
  isFirmManaged?: boolean;
}

@Injectable({ providedIn: 'root' })
export class FirmAssignmentService {
  private readonly http = inject(HttpClient);
  private readonly firmsUrl = `${environment.apiUrl}/accounting-firms`;
  private readonly assignmentsUrl = `${environment.apiUrl}/firm-assignments`;

  searchDirectory(search?: string): Observable<ApiResponse<AccountingFirmDirectoryItem[]>> {
    const params: Record<string, string> = {};
    if (search) params['search'] = search;
    return this.http.get<ApiResponse<AccountingFirmDirectoryItem[]>>(`${this.firmsUrl}/directory`, { params });
  }

  getCompanyCurrent(): Observable<ApiResponse<FirmClientAssignment | null>> {
    return this.http.get<ApiResponse<FirmClientAssignment | null>>(`${this.assignmentsUrl}/company/current`);
  }

  getCompanyHistory(): Observable<ApiResponse<FirmClientAssignment[]>> {
    return this.http.get<ApiResponse<FirmClientAssignment[]>>(`${this.assignmentsUrl}/company/history`);
  }

  requestAssignment(firmTenantId: string, notes?: string): Observable<ApiResponse<FirmClientAssignment>> {
    return this.http.post<ApiResponse<FirmClientAssignment>>(this.assignmentsUrl, { firmTenantId, notes });
  }

  revokeByCompany(): Observable<ApiResponse<unknown>> {
    return this.http.delete<ApiResponse<unknown>>(`${this.assignmentsUrl}/company/current`);
  }

  cancelPendingRequest(): Observable<ApiResponse<unknown>> {
    return this.http.delete<ApiResponse<unknown>>(`${this.assignmentsUrl}/company/pending`);
  }

  getIncomingInvitations(): Observable<ApiResponse<FirmClientAssignment[]>> {
    return this.http.get<ApiResponse<FirmClientAssignment[]>>(`${this.assignmentsUrl}/firm/incoming`);
  }

  getActiveClients(): Observable<ApiResponse<FirmClientDossier[]>> {
    return this.http.get<ApiResponse<FirmClientDossier[]>>(`${this.assignmentsUrl}/firm/clients`);
  }

  acceptInvitation(assignmentId: string): Observable<ApiResponse<unknown>> {
    return this.http.post<ApiResponse<unknown>>(`${this.assignmentsUrl}/firm/${assignmentId}/accept`, {});
  }

  rejectInvitation(assignmentId: string, reason?: string): Observable<ApiResponse<unknown>> {
    return this.http.post<ApiResponse<unknown>>(`${this.assignmentsUrl}/firm/${assignmentId}/reject`, { reason });
  }

  revokeClient(assignmentId: string): Observable<ApiResponse<unknown>> {
    return this.http.delete<ApiResponse<unknown>>(`${this.assignmentsUrl}/firm/${assignmentId}`);
  }
}
