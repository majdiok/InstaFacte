import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '@environments/environment';

interface ApiResponse<T> {
  success: boolean;
  data?: T;
  error?: string;
  message?: string;
}

/** Santé du fan-out : une vue partielle signalée vaut mieux qu'une page d'erreur. */
export interface FirmFanOutHealth {
  dossiersRead: number;
  dossiersFailed: number;
  isPartial: boolean;
}

export interface FirmRevisionDossierRow {
  companyTenantId: string;
  companyName: string;
  assignedAccountantName?: string | null;
  assignedAccountantUserId?: string | null;
  riskScore: number;
  blockingCount: number;
  warningCount: number;
  infoCount: number;
  totalAnomalies: number;
  impactAmount: number;
  complianceRate?: number | null;
  lastScanAt?: string | null;
  readFailed: boolean;
  neverScanned: boolean;
  hasRevisionNote: boolean;
}

export interface FirmRevisionFamilySlice {
  moduleCode: string;
  label: string;
  count: number;
  blockingCount: number;
  impactAmount: number;
}

export interface FirmRevisionOverview {
  fiscalYear: number;
  dossiersCount: number;
  dossiersWithAnomaliesCount: number;
  dossiersNeverScannedCount: number;
  blockingCount: number;
  warningCount: number;
  infoCount: number;
  totalAnomalies: number;
  totalImpactAmount: number;
  currency: string;
  generatedAt: string;
  dossiers: FirmRevisionDossierRow[];
  byFamily: FirmRevisionFamilySlice[];
  fanOut: FirmFanOutHealth;
}

export interface FirmRevisionNoteItem {
  anomalyId: string;
  ruleCode: string;
  moduleCode: string;
  severity: number;
  title: string;
  /** Null quand la règle ne produit pas de montant chiffrable — à afficher, pas à masquer. */
  impactAmount?: number | null;
  accountRef?: string | null;
  pieceRef?: string | null;
  workingNote: string;
  clientQuestion?: string | null;
  action: string;
  actionLabel: string;
}

export interface FirmRevisionNote {
  id: string;
  runId: string;
  fiscalYear: number;
  generatedAt: string;
  generatedByUserName?: string | null;
  /** Faux = note purement déterministe. L'écran doit le dire. */
  aiGenerated: boolean;
  modelRef?: string | null;
  fallbackReason?: string | null;
  executiveSummary: string;
  totalImpactAmount: number;
  anomalyCount: number;
  blockingCount: number;
  items: FirmRevisionNoteItem[];
}

export interface FirmRevisionAnomaly {
  id: string;
  ruleCode: string;
  moduleCode: string;
  severity: number;
  category: number;
  title: string;
  description: string;
  accountRef?: string | null;
  amount: number;
  status: number;
  detectedAt: string;
}

export interface FirmRevisionDossierDetail {
  companyTenantId: string;
  companyName: string;
  fiscalYear: number;
  complianceRate?: number | null;
  lastScanAt?: string | null;
  impactAmount: number;
  anomalies: FirmRevisionAnomaly[];
  note?: FirmRevisionNote | null;
}

export interface FirmRevisionCollaboratorLoad {
  userId?: string | null;
  name: string;
  dossiersCount: number;
  blockingCount: number;
  totalAnomalies: number;
  impactAmount: number;
  dossiers: FirmRevisionDossierRow[];
}

export interface FirmRevisionWorkQueue {
  fiscalYear: number;
  generatedAt: string;
  collaborators: FirmRevisionCollaboratorLoad[];
  unassigned: FirmRevisionDossierRow[];
  fanOut: FirmFanOutHealth;
}

export interface FirmRevisionSweepResult {
  fiscalYear: number;
  dossiersScanned: number;
  dossiersFailed: number;
  totalAnomalies: number;
  duration: string;
  completedAt: string;
}

@Injectable({ providedIn: 'root' })
export class FirmRevisionService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/firm/revision`;

  getOverview(fiscalYear: number): Observable<ApiResponse<FirmRevisionOverview>> {
    return this.http.get<ApiResponse<FirmRevisionOverview>>(`${this.base}/overview`, {
      params: new HttpParams().set('fiscalYear', fiscalYear)
    });
  }

  getDossier(companyTenantId: string, fiscalYear: number): Observable<ApiResponse<FirmRevisionDossierDetail>> {
    return this.http.get<ApiResponse<FirmRevisionDossierDetail>>(
      `${this.base}/dossiers/${companyTenantId}`,
      { params: new HttpParams().set('fiscalYear', fiscalYear) }
    );
  }

  getWorkQueue(fiscalYear: number): Observable<ApiResponse<FirmRevisionWorkQueue>> {
    return this.http.get<ApiResponse<FirmRevisionWorkQueue>>(`${this.base}/work-queue`, {
      params: new HttpParams().set('fiscalYear', fiscalYear)
    });
  }

  sweep(fiscalYear: number): Observable<ApiResponse<FirmRevisionSweepResult>> {
    return this.http.post<ApiResponse<FirmRevisionSweepResult>>(
      `${this.base}/sweep`, {},
      { params: new HttpParams().set('fiscalYear', fiscalYear) }
    );
  }
}
