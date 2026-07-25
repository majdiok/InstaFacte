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

@Injectable({ providedIn: 'root' })
export class FirmDashboardService {
  private readonly http = inject(HttpClient);
  private readonly url = `${environment.apiUrl}/firm/dashboard`;

  getDashboard(): Observable<ApiResponse<FirmDashboardData>> {
    return this.http.get<ApiResponse<FirmDashboardData>>(this.url);
  }
}
