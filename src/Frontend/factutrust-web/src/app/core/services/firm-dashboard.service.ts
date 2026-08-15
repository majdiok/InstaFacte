import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '@environments/environment';
import { ApiResponse } from './auth.service';

export interface FirmDashboardClientRow {
  assignmentId: string;
  companyTenantId: string;
  companyName: string;
  activeSince: string;
  lastJournalEntryDate?: string;
  isInactive30Days: boolean;
}

export interface FirmDashboardInvitationRow {
  id: string;
  companyName: string;
  requestedAt: string;
  notes?: string;
}

export interface FirmDashboardData {
  activeClientsCount: number;
  pendingInvitationsCount: number;
  inactiveDossiersCount: number;
  vatDraftsCount: number;
  overdueSchedulesCount: number;
  upcomingWithin7DaysCount: number;
  tejPendingCount: number;
  liasseDraftsCount: number;
  dtsPendingCount: number;
  overdueEstimatedAmount: number;
  upcoming7DaysEstimatedAmount: number;
  clients: FirmDashboardClientRow[];
  pendingInvitations: FirmDashboardInvitationRow[];
}

export interface FirmCriticalFiscalRow {
  entryId: string;
  companyTenantId?: string;
  companyName: string;
  obligationType: number;
  obligationTypeDisplay: string;
  obligationLabel: string;
  dueDate: string;
  daysUntilDue: number;
  isOverdue: boolean;
  estimatedAmount: number;
  currency: string;
}

export interface FirmAtRiskDossierRow {
  assignmentId: string;
  companyTenantId: string;
  companyName: string;
  signals: string[];
  lastJournalEntryDate?: string;
  permanentFileCompletionPercent?: number;
  permanentFileMissingItemsCount: number;
  hasPermanentFile: boolean;
  assignedAccountantName?: string;
  priorityScore: number;
}

export interface FirmNegativeMarginRow {
  firmClientAssignmentId?: string;
  companyTenantId?: string;
  companyName: string;
  year: number;
  collaboratorUserId: string;
  collaboratorName: string;
  budgetAnnuel: number;
  totalHours: number;
  margin: number;
}

export interface FirmPendingTimeSheetRow {
  collaboratorUserId: string;
  collaboratorName: string;
  periodYear: number;
  periodMonth: number;
  submittedHours: number;
  entryCount: number;
  companyNames: string[];
}

export interface FirmSocialAlertRow {
  companyTenantId: string;
  companyName: string;
  employeeCount: number;
  pendingLeaveRequests: number;
  payrollRunsDraftCount: number;
  dtsPendingCount: number;
  alertScore: number;
}

export interface FirmHonorairesAlertRow {
  firmClientAssignmentId: string;
  companyTenantId?: string;
  companyName: string;
  year: number;
  billedYtdAmount: number;
  collectedAmount: number;
  debitBalance: number;
  recoveryRatePercent: number;
  isSnapshotData: boolean;
}

export interface FirmDecisionTablesPartialFailure {
  section: string;
  message: string;
}

export interface FirmDecisionTablesMeta {
  generatedAt: string;
  partialFailures: FirmDecisionTablesPartialFailure[];
}

export interface FirmDecisionTablesData {
  criticalFiscalSchedules: FirmCriticalFiscalRow[];
  atRiskDossiers: FirmAtRiskDossierRow[];
  negativeMargins: FirmNegativeMarginRow[];
  pendingTimeSheets: FirmPendingTimeSheetRow[];
  socialAlerts: FirmSocialAlertRow[];
  honorairesAlerts: FirmHonorairesAlertRow[];
  meta: FirmDecisionTablesMeta;
}

@Injectable({ providedIn: 'root' })
export class FirmDashboardService {
  private readonly http = inject(HttpClient);
  private readonly url = `${environment.apiUrl}/firm/dashboard`;

  getDashboard(): Observable<ApiResponse<FirmDashboardData>> {
    return this.http.get<ApiResponse<FirmDashboardData>>(this.url);
  }

  getDecisionTables(): Observable<ApiResponse<FirmDecisionTablesData>> {
    return this.http.get<ApiResponse<FirmDecisionTablesData>>(`${this.url}/decision-tables`);
  }
}
