import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '@environments/environment';
import { ApiResponse } from './auth.service';
import { createHttpContextSkipGlobalErrorUi } from '@core/http-context';

export interface PermanentFile {
  id: string;
  firmClientAssignmentId: string;
  companyTenantId: string;
  companyName?: string;
  status: number;
  statusDisplay: string;
  wizardStep: number;
  nif?: string;
  rneIdentifier?: string;
  legalForm?: number;
  legalFormDisplay?: string;
  incorporationDate?: string;
  shareCapital?: number;
  street?: string;
  city?: string;
  governorate?: string;
  postalCode?: string;
  taxOffice?: string;
  taxRegime?: number;
  hasTaxCertificate: boolean;
  fiscalYearStartMonth?: number;
  fiscalYearEndMonth?: number;
  currentLegalAct?: string;
  missionStatus?: string;
  isDigitized: boolean;
  missionResigned: boolean;
  resignationFiscalYear?: number;
  resignationNotes?: string;
  labCompleted: boolean;
  missionAccepted: boolean;
  labCompletedAt?: string;
  missionAcceptedAt?: string;
  annualFeeAmount?: number;
  billingFrequency?: number;
  billingFrequencyDisplay?: string;
  currency?: string;
  billingNotes?: string;
  assignedAccountantUserId?: string;
  assignedAccountantName?: string;
  syncedToTenantAt?: string;
  isBusinessComplete?: boolean;
  missingItems?: string[];
  completionPercent?: number;
  nextRecommendedStep?: number;
  nextActionLabel?: string;
  representatives: LegalRepresentative[];
  shareholders: Shareholder[];
  /** True si le dossier a été créé et est géré par le cabinet (client sans compte plateforme). */
  isFirmManaged?: boolean;
}

export interface LegalRepresentative {
  id?: string;
  lastName: string;
  firstName: string;
  cin?: string;
  nationality?: string;
  email?: string;
  phone?: string;
  cnssNumber?: string;
  role: string;
  isActive?: boolean;
  hasProSpace?: boolean;
}

export interface Shareholder {
  id?: string;
  name: string;
  isLegalEntity: boolean;
  cinOrNif?: string;
  shareCount: number;
  sharePercentage: number;
  isActive?: boolean;
  warnings?: string[];
}

export interface FirmGovernanceDashboard {
  activeDossiersCount: number;
  permanentFilesCompleteCount: number;
  permanentFilesInProgressCount: number;
  totalBillableHoursMonth: number;
  totalBillableHoursYear: number;
  pendingExpenseNotesCount: number;
  overdueFiscalSchedulesCount: number;
  unreadNotificationsCount: number;
}

export interface FirmTimeSheetEntry {
  id: string;
  userId: string;
  userDisplayName: string;
  firmClientAssignmentId?: string;
  clientCompanyName?: string;
  workDate: string;
  hours: number;
  startTime?: string;
  endTime?: string;
  activityCode?: string;
  notes?: string;
  isBillable: boolean;
  workLocation?: string;
  tags?: string;
  status: number;
  statusDisplay: string;
  isValidated: boolean;
  timerStartedAtUtc?: string;
  validatedAt?: string;
  validatedByDisplayName?: string;
  /** Dépassements constatés sans blocage, sur un exercice dont les plafonds ne sont pas opposables. */
  warnings?: string[];
}

/** État de clôture d'un mois de feuilles de temps. */
export interface FirmTimeSheetPeriod {
  year: number;
  month: number;
  isLocked: boolean;
  lockedAt?: string;
  lockedByDisplayName?: string;
  lockReason?: string;
  unlockedAt?: string;
  unlockedByDisplayName?: string;
  unlockReason?: string;
}

export interface FirmTimeSheetBulkValidationResult {
  validated: number;
  skipped: number;
  failures: { entryId: string; error: string }[];
}

/** Code de la nomenclature des diligences du cabinet. */
export interface FirmActivityCode {
  id: string;
  code: string;
  label: string;
  category: number;
  categoryDisplay: string;
  isBillableByDefault: boolean;
  /** Tarif unitaire HT suggéré à la facturation (TND). null = non configuré. */
  defaultUnitPrice?: number | null;
  isActive: boolean;
  sortOrder: number;
}

export interface SaveFirmActivityCodeBody {
  code: string;
  label: string;
  category: number;
  isBillableByDefault: boolean;
  defaultUnitPrice?: number | null;
  sortOrder: number;
}

export interface FirmTimeSheetYearSettings {
  year: number;
  weeklyRegime: number;
  weeklyRegimeDisplay: string;
  maxDailyHours: number;
  maxWeeklyHours: number;
  allowFutureEntryDays: number;
  maxBackdatingDays: number;
  enforceHardLimits: boolean;
  paidLeaveDaysPerYear: number;
  publicHolidayDaysPerYear: number;
  productivityRatePercent: number;
  cnssEmployerRate: number;
  tfpRate: number;
  foprolosRate: number;
  workAccidentRate: number;
  annualBaseHours: number;
  dailyHours: number;
  annualProductiveHours: number;
  totalEmployerChargeRate: number;
}

/** Coût employeur annuel d'un collaborateur et taux horaire qui en découle. */
export interface FirmCollaboratorYearCost {
  collaboratorUserId: string;
  collaboratorName: string;
  year: number;
  grossAnnualSalary: number;
  employerContributions: number;
  payrollExtras: number;
  totalEmployerCost: number;
  source: number;
  sourceDisplay: string;
  importedAt?: string;
  hourlyRateOverride?: number;
  overrideJustification?: string;
  effectiveHourlyRate: number;
  hourlyRateSource: number;
  hourlyRateSourceDisplay: string;
  hourlyRateBasis: string;
  annualProductiveHours: number;
  payrollEmployeeId?: string;
}

export interface FirmPayrollEmployeeCost {
  payrollEmployeeId: string;
  employeeName: string;
  grossAnnualSalary: number;
  employerContributions: number;
  payslipCount: number;
  totalEmployerCost: number;
}

export interface FirmPayrollCostSnapshot {
  isAvailable: boolean;
  unavailableReason?: string;
  employees: FirmPayrollEmployeeCost[];
}

export interface FirmPayrollImportResult {
  imported: number;
  unlinked: number;
  payrollAvailable: boolean;
  unavailableReason?: string;
}

export interface FirmExpenseNote {
  id: string;
  firmClientAssignmentId: string;
  companyName: string;
  periodYear: number;
  periodMonth: number;
  status: number;
  statusDisplay: string;
  totalToReimburse: number;
  mixedCharges: number;
  operatingExpenses: number;
  mileageAllowance: number;
  salesAmount: number;
  notes?: string;
}

export interface FirmSocialOverview {
  clients: FirmSocialClientRow[];
}

export interface FirmSocialClientRow {
  companyTenantId: string;
  companyName: string;
  employeeCount: number;
  pendingLeaveRequests: number;
  payrollRunsDraftCount: number;
  dtsPendingCount: number;
}

/** 0=Tous, 1=En attente, 2=Affectés */
export type DossierAssignmentFilter = 0 | 1 | 2;

export interface FirmDossierAssignmentRow {
  assignmentId: string;
  companyTenantId: string;
  companyName: string;
  activeSince: string;
  hasPermanentFile: boolean;
  permanentFileStatus?: number | null;
  permanentFileStatusDisplay?: string | null;
  assignedAccountantUserId?: string | null;
  assignedAccountantName?: string | null;
  isAwaitingAccountantAssignment: boolean;
}

export interface FirmAssignableAccountant {
  id: string;
  fullName: string;
  email: string;
  role: number;
  roleDisplay: string;
}

export interface AssignDossierManagerBulkResult {
  succeeded: number;
  failed: { assignmentId: string; error: string }[];
}

/** 0=Toutes, 1=Négatives, 2=Positives */
export type FirmMarginSignFilter = 0 | 1 | 2;

export interface FirmChartSlice {
  label: string;
  value: number;
}

export interface FirmDossierTimeProfitabilityRow {
  firmClientAssignmentId?: string | null;
  companyName: string;
  year: number;
  collaboratorUserId: string;
  collaboratorName: string;
  budgetAnnuel: number;
  totalHours: number;
  hourlyRate: number;
  cost: number;
  margin: number;
  budgetFromFallback: boolean;
  billableHours: number;
  nonBillableHours: number;
  billableRatioPercent: number;
  /** 1 calculé, 2 imposé, 3 profil, 4 défaut cabinet. */
  hourlyRateSource: number;
  hourlyRateSourceDisplay: string;
  /** Détail du calcul du taux, à afficher en regard du montant. */
  hourlyRateBasis: string;
}

export interface FirmDossierTimeProfitabilityReport {
  rows: FirmDossierTimeProfitabilityRow[];
  totalHours: number;
  totalBillableHours: number;
  totalNonBillableHours: number;
  uniqueBudgetSum: number;
  hoursByYear: FirmChartSlice[];
  hoursByCompany: FirmChartSlice[];
}

export interface FirmHourlyRateSettings {
  defaultHourlyCostRate: number;
}

export interface FirmCollaboratorRentabilityListItem {
  id: string;
  collaboratorUserId: string;
  collaboratorName: string;
  year: number;
  attachedCollaboratorsCount: number;
  companiesCount: number;
  totalRevenue: number;
  payrollCost: number;
  adminPayrollCharge: number;
  itManagementCharge: number;
  operatingCharge: number;
  clientDebitBalance: number;
  clientCreditBalance: number;
  rentability: number;
  /** Rentabilité diminuée des honoraires non recouvrés. Dérivé, jamais stocké. */
  collectedRentability: number;
  recoveryRatePercent: number;
}

export interface FirmCollaboratorRentabilityList {
  items: FirmCollaboratorRentabilityListItem[];
  totals: FirmCollaboratorRentabilityListItem;
  /** Alertes de cohérence sur les totaux, notamment le double comptage des charges. */
  warnings?: string[];
}

export interface FirmRentabilityPortfolioRow {
  firmClientAssignmentId: string;
  companyName: string;
  collaboratorName: string;
  annualFeeHt: number;
  clientDebitBalance: number;
  clientCreditBalance: number;
  /** Heures passées sur ce dossier par le collaborateur et ses rattachés. */
  portfolioHours: number;
  /** Heures passées par tout le cabinet — dénominateur de la quote-part. */
  totalDossierHours: number;
  /** Honoraires × heures du portefeuille ÷ heures totales. */
  revenueShare: number;
}

export interface FirmRentabilityPayrollRow {
  collaboratorUserId: string;
  collaboratorName: string;
  grossSalary: number;
  employerContributions: number;
  payrollExtras: number;
  annualTotal?: number;
}

export interface FirmCollaboratorRentabilityDetail {
  id?: string | null;
  collaboratorUserId: string;
  collaboratorName: string;
  year: number;
  attachedCollaboratorsCount: number;
  totalRevenue: number;
  calculatedTotalRevenue: number;
  payrollCost: number;
  calculatedPayrollCost: number;
  adminPayrollCharge: number;
  itManagementCharge: number;
  operatingCharge: number;
  clientDebitBalance: number;
  clientCreditBalance: number;
  rentability: number;
  /** Quote-part de structure calculée, affichée en lecture seule. */
  calculatedSupportShare: number;
  portfolio: FirmRentabilityPortfolioRow[];
  payrollRows: FirmRentabilityPayrollRow[];
}

export interface SaveFirmCollaboratorRentability {
  collaboratorUserId: string;
  year: number;
  totalRevenue: number;
  payrollCost: number;
  adminPayrollCharge: number;
  itManagementCharge: number;
  operatingCharge: number;
  clientDebitBalance: number;
  clientCreditBalance: number;
  companiesCount: number;
  attachedCollaboratorsCount: number;
  payrollRows?: FirmRentabilityPayrollRow[];
}

/** Compte rendu d'un recalcul global des marges collaborateur. */
export interface FirmRentabilityRecalculationResult {
  recalculated: number;
  /** Snapshots laissés intacts, avec leur motif — typiquement un exercice sans feuille de temps. */
  skipped: { id: string; collaboratorName: string; year: number; reason: string }[];
}

export interface DuplicateFirmRentabilityResult {
  duplicated: number;
  skipped: number;
}

@Injectable({ providedIn: 'root' })
export class FirmGovernanceService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/firm/governance`;

  getDashboard(): Observable<ApiResponse<FirmGovernanceDashboard>> {
    return this.http.get<ApiResponse<FirmGovernanceDashboard>>(`${this.base}/dashboard`);
  }

  listPermanentFiles(): Observable<ApiResponse<PermanentFile[]>> {
    return this.http.get<ApiResponse<PermanentFile[]>>(`${this.base}/permanent-files`);
  }

  getPermanentFile(assignmentId: string): Observable<ApiResponse<PermanentFile>> {
    return this.http.get<ApiResponse<PermanentFile>>(`${this.base}/permanent-files/${assignmentId}`);
  }

  upsertPermanentFile(assignmentId: string, body: Partial<PermanentFile> & { wizardStep: number; requestCompletion?: boolean }): Observable<ApiResponse<PermanentFile>> {
    return this.http.put<ApiResponse<PermanentFile>>(`${this.base}/permanent-files/${assignmentId}`, body);
  }

  syncPermanentFile(assignmentId: string): Observable<ApiResponse<unknown>> {
    return this.http.post<ApiResponse<unknown>>(`${this.base}/permanent-files/${assignmentId}/sync`, {});
  }

  addRepresentative(assignmentId: string, rep: LegalRepresentative): Observable<ApiResponse<LegalRepresentative>> {
    return this.http.post<ApiResponse<LegalRepresentative>>(`${this.base}/permanent-files/${assignmentId}/representatives`, rep);
  }

  updateRepresentative(assignmentId: string, representativeId: string, rep: LegalRepresentative): Observable<ApiResponse<LegalRepresentative>> {
    return this.http.put<ApiResponse<LegalRepresentative>>(
      `${this.base}/permanent-files/${assignmentId}/representatives/${representativeId}`, rep);
  }

  addShareholder(assignmentId: string, sh: Shareholder): Observable<ApiResponse<Shareholder>> {
    return this.http.post<ApiResponse<Shareholder>>(`${this.base}/permanent-files/${assignmentId}/shareholders`, sh);
  }

  updateShareholder(assignmentId: string, shareholderId: string, sh: Shareholder): Observable<ApiResponse<Shareholder>> {
    return this.http.put<ApiResponse<Shareholder>>(
      `${this.base}/permanent-files/${assignmentId}/shareholders/${shareholderId}`, sh);
  }

  deactivateRepresentative(assignmentId: string, representativeId: string): Observable<ApiResponse<unknown>> {
    return this.http.post<ApiResponse<unknown>>(
      `${this.base}/permanent-files/${assignmentId}/representatives/${representativeId}/deactivate`, {});
  }

  deactivateShareholder(assignmentId: string, shareholderId: string): Observable<ApiResponse<unknown>> {
    return this.http.post<ApiResponse<unknown>>(
      `${this.base}/permanent-files/${assignmentId}/shareholders/${shareholderId}/deactivate`, {});
  }

  archivePermanentFile(assignmentId: string): Observable<ApiResponse<unknown>> {
    return this.http.post<ApiResponse<unknown>>(`${this.base}/permanent-files/${assignmentId}/archive`, {});
  }

  listTimeSheets(year?: number, month?: number, userId?: string, assignmentId?: string): Observable<ApiResponse<FirmTimeSheetEntry[]>> {
    const params: Record<string, string> = {};
    if (year) params['year'] = String(year);
    if (month) params['month'] = String(month);
    if (userId) params['userId'] = userId;
    if (assignmentId) params['assignmentId'] = assignmentId;
    return this.http.get<ApiResponse<FirmTimeSheetEntry[]>>(`${this.base}/time-sheets`, { params });
  }

  createTimeSheet(body: {
    workDate: string;
    hours: number;
    startTime?: string;
    endTime?: string;
    firmClientAssignmentId?: string;
    activityCode?: string;
    notes?: string;
    isBillable?: boolean;
    workLocation?: string;
    tags?: string;
    targetUserId?: string;
  }): Observable<ApiResponse<FirmTimeSheetEntry>> {
    // Toast géré par TimeSheetsFacade (anomalies + message métier).
    return this.http.post<ApiResponse<FirmTimeSheetEntry>>(`${this.base}/time-sheets`, body, {
      context: createHttpContextSkipGlobalErrorUi()
    });
  }

  updateTimeSheet(id: string, body: {
    workDate: string;
    hours: number;
    startTime?: string;
    endTime?: string;
    firmClientAssignmentId?: string;
    activityCode?: string;
    notes?: string;
    isBillable?: boolean;
    workLocation?: string;
    tags?: string;
  }): Observable<ApiResponse<FirmTimeSheetEntry>> {
    return this.http.put<ApiResponse<FirmTimeSheetEntry>>(`${this.base}/time-sheets/${id}`, body, {
      context: createHttpContextSkipGlobalErrorUi()
    });
  }

  deleteTimeSheet(id: string): Observable<ApiResponse<unknown>> {
    return this.http.delete<ApiResponse<unknown>>(`${this.base}/time-sheets/${id}`);
  }

  submitTimeSheet(id: string): Observable<ApiResponse<FirmTimeSheetEntry>> {
    return this.http.post<ApiResponse<FirmTimeSheetEntry>>(`${this.base}/time-sheets/${id}/submit`, {});
  }

  startTimeSheetTimer(body: {
    workDate?: string;
    firmClientAssignmentId?: string;
    activityCode?: string;
    isBillable?: boolean;
    targetUserId?: string;
  } = {}): Observable<ApiResponse<FirmTimeSheetEntry>> {
    return this.http.post<ApiResponse<FirmTimeSheetEntry>>(`${this.base}/time-sheets/timer/start`, body);
  }

  stopTimeSheetTimer(body: { entryId?: string; targetUserId?: string } = {}): Observable<ApiResponse<FirmTimeSheetEntry>> {
    return this.http.post<ApiResponse<FirmTimeSheetEntry>>(`${this.base}/time-sheets/timer/stop`, body);
  }

  duplicateTimeSheetWeek(body: {
    sourceWeekStart: string;
    targetWeekStart: string;
    userId?: string;
  }): Observable<ApiResponse<FirmTimeSheetEntry[]>> {
    return this.http.post<ApiResponse<FirmTimeSheetEntry[]>>(`${this.base}/time-sheets/duplicate-week`, body);
  }

  validateTimeSheet(id: string): Observable<ApiResponse<FirmTimeSheetEntry>> {
    return this.http.post<ApiResponse<FirmTimeSheetEntry>>(`${this.base}/time-sheets/${id}/validate`, {});
  }

  validateTimeSheetsBulk(ids: string[]): Observable<ApiResponse<number>> {
    return this.http.post<ApiResponse<number>>(`${this.base}/time-sheets/validate-bulk`, { ids });
  }

  /** Variante détaillée : indique aussi ce qui n'a pas pu être validé, et pourquoi. */
  validateTimeSheetsBulkDetailed(ids: string[]): Observable<ApiResponse<FirmTimeSheetBulkValidationResult>> {
    return this.http.post<ApiResponse<FirmTimeSheetBulkValidationResult>>(
      `${this.base}/time-sheets/validate-bulk/detailed`, { ids });
  }

  unvalidateTimeSheet(id: string): Observable<ApiResponse<FirmTimeSheetEntry>> {
    return this.http.post<ApiResponse<FirmTimeSheetEntry>>(`${this.base}/time-sheets/${id}/unvalidate`, {});
  }

  // ---- Clôture mensuelle ----

  listTimeSheetPeriods(year: number): Observable<ApiResponse<FirmTimeSheetPeriod[]>> {
    return this.http.get<ApiResponse<FirmTimeSheetPeriod[]>>(`${this.base}/time-sheets/periods/${year}`);
  }

  lockTimeSheetPeriod(year: number, month: number, reason?: string): Observable<ApiResponse<FirmTimeSheetPeriod>> {
    return this.http.post<ApiResponse<FirmTimeSheetPeriod>>(
      `${this.base}/time-sheets/periods/${year}/${month}/lock`, { reason });
  }

  unlockTimeSheetPeriod(year: number, month: number, reason: string): Observable<ApiResponse<FirmTimeSheetPeriod>> {
    return this.http.post<ApiResponse<FirmTimeSheetPeriod>>(
      `${this.base}/time-sheets/periods/${year}/${month}/unlock`, { reason });
  }

  // ---- Paramètres d'exercice ----

  getTimeSheetYearSettings(year: number): Observable<ApiResponse<FirmTimeSheetYearSettings>> {
    return this.http.get<ApiResponse<FirmTimeSheetYearSettings>>(`${this.base}/time-sheets/settings/${year}`);
  }

  saveTimeSheetYearSettings(
    year: number,
    body: Omit<FirmTimeSheetYearSettings,
      'year' | 'weeklyRegimeDisplay' | 'annualBaseHours' | 'dailyHours'
      | 'annualProductiveHours' | 'totalEmployerChargeRate'>
  ): Observable<ApiResponse<FirmTimeSheetYearSettings>> {
    return this.http.put<ApiResponse<FirmTimeSheetYearSettings>>(
      `${this.base}/time-sheets/settings/${year}`, body);
  }

  // ---- Référentiel des codes activité ----

  listActivityCodes(includeInactive = false, billableOnly = false): Observable<ApiResponse<FirmActivityCode[]>> {
    const params: Record<string, string> = {};
    if (includeInactive) params['includeInactive'] = 'true';
    if (billableOnly) params['billableOnly'] = 'true';
    return this.http.get<ApiResponse<FirmActivityCode[]>>(`${this.base}/activity-codes`, { params });
  }

  seedDefaultActivityCodes(): Observable<ApiResponse<FirmActivityCode[]>> {
    return this.http.post<ApiResponse<FirmActivityCode[]>>(`${this.base}/activity-codes/seed-defaults`, {});
  }

  createActivityCode(body: SaveFirmActivityCodeBody): Observable<ApiResponse<FirmActivityCode>> {
    return this.http.post<ApiResponse<FirmActivityCode>>(`${this.base}/activity-codes`, body);
  }

  updateActivityCode(id: string, body: SaveFirmActivityCodeBody): Observable<ApiResponse<FirmActivityCode>> {
    return this.http.put<ApiResponse<FirmActivityCode>>(`${this.base}/activity-codes/${id}`, body);
  }

  deactivateActivityCode(id: string): Observable<ApiResponse<FirmActivityCode>> {
    return this.http.delete<ApiResponse<FirmActivityCode>>(`${this.base}/activity-codes/${id}`);
  }

  activateActivityCode(id: string): Observable<ApiResponse<FirmActivityCode>> {
    return this.http.post<ApiResponse<FirmActivityCode>>(`${this.base}/activity-codes/${id}/activate`, {});
  }

  listExpenseNotes(): Observable<ApiResponse<FirmExpenseNote[]>> {
    return this.http.get<ApiResponse<FirmExpenseNote[]>>(`${this.base}/expense-notes`);
  }

  upsertExpenseNote(body: {
    firmClientAssignmentId: string;
    periodYear: number;
    periodMonth: number;
    totalToReimburse: number;
    mixedCharges: number;
    operatingExpenses: number;
    mileageAllowance: number;
    salesAmount: number;
    notes?: string;
  }): Observable<ApiResponse<FirmExpenseNote>> {
    return this.http.post<ApiResponse<FirmExpenseNote>>(`${this.base}/expense-notes`, body);
  }

  processExpenseNote(noteId: string, approve: boolean): Observable<ApiResponse<unknown>> {
    return this.http.post<ApiResponse<unknown>>(`${this.base}/expense-notes/${noteId}/process?approve=${approve}`, {});
  }

  submitExpenseNote(noteId: string): Observable<ApiResponse<unknown>> {
    return this.http.post<ApiResponse<unknown>>(`${this.base}/expense-notes/${noteId}/submit`, {});
  }

  markExpenseNoteReimbursed(noteId: string): Observable<ApiResponse<unknown>> {
    return this.http.post<ApiResponse<unknown>>(`${this.base}/expense-notes/${noteId}/reimburse`, {});
  }

  getSocialOverview(): Observable<ApiResponse<FirmSocialOverview>> {
    return this.http.get<ApiResponse<FirmSocialOverview>>(`${this.base}/social-overview`);
  }

  listDossierAssignments(
    assignmentFilter: DossierAssignmentFilter = 1,
    name?: string
  ): Observable<ApiResponse<FirmDossierAssignmentRow[]>> {
    const params: Record<string, string> = { assignmentFilter: String(assignmentFilter) };
    if (name?.trim()) params['name'] = name.trim();
    return this.http.get<ApiResponse<FirmDossierAssignmentRow[]>>(`${this.base}/dossier-assignments`, { params });
  }

  listAssignableAccountants(): Observable<ApiResponse<FirmAssignableAccountant[]>> {
    return this.http.get<ApiResponse<FirmAssignableAccountant[]>>(`${environment.apiUrl}/firm/users/assignable`);
  }

  assignManager(assignmentId: string, accountantUserId: string | null): Observable<ApiResponse<unknown>> {
    return this.http.post<ApiResponse<unknown>>(`${this.base}/assign-manager`, {
      assignmentId,
      accountantUserId
    });
  }

  assignManagerBulk(
    assignmentIds: string[],
    accountantUserId: string | null
  ): Observable<ApiResponse<AssignDossierManagerBulkResult>> {
    return this.http.post<ApiResponse<AssignDossierManagerBulkResult>>(`${this.base}/assign-manager-bulk`, {
      assignmentIds,
      accountantUserId
    });
  }

  // ── Rentabilité dossiers / collaborateurs ─────────────────────────────────────────

  getDossierTimeProfitability(filters: {
    company?: string;
    year?: number;
    collaboratorUserId?: string;
    margin?: FirmMarginSignFilter;
  } = {}): Observable<ApiResponse<FirmDossierTimeProfitabilityReport>> {
    const params: Record<string, string> = {};
    if (filters.company?.trim()) params['company'] = filters.company.trim();
    if (filters.year != null) params['year'] = String(filters.year);
    if (filters.collaboratorUserId) params['collaboratorUserId'] = filters.collaboratorUserId;
    if (filters.margin != null && filters.margin !== 0) params['margin'] = String(filters.margin);
    return this.http.get<ApiResponse<FirmDossierTimeProfitabilityReport>>(
      `${this.base}/dossier-time-profitability`, { params });
  }

  exportDossierTimeProfitabilityPdf(filters: {
    company?: string;
    year?: number;
    collaboratorUserId?: string;
    margin?: FirmMarginSignFilter;
  } = {}): Observable<Blob> {
    const params: Record<string, string> = {};
    if (filters.company?.trim()) params['company'] = filters.company.trim();
    if (filters.year != null) params['year'] = String(filters.year);
    if (filters.collaboratorUserId) params['collaboratorUserId'] = filters.collaboratorUserId;
    if (filters.margin != null && filters.margin !== 0) params['margin'] = String(filters.margin);
    return this.http.get(`${this.base}/dossier-time-profitability/export-pdf`, {
      params,
      responseType: 'blob'
    });
  }

  getHourlyRateSettings(): Observable<ApiResponse<FirmHourlyRateSettings>> {
    return this.http.get<ApiResponse<FirmHourlyRateSettings>>(`${this.base}/hourly-rate-settings`);
  }

  listRentability(year?: number, collaboratorUserId?: string): Observable<ApiResponse<FirmCollaboratorRentabilityList>> {
    const params: Record<string, string> = {};
    if (year != null) params['year'] = String(year);
    if (collaboratorUserId) params['collaboratorUserId'] = collaboratorUserId;
    return this.http.get<ApiResponse<FirmCollaboratorRentabilityList>>(`${this.base}/rentability`, { params });
  }

  getRentability(id: string): Observable<ApiResponse<FirmCollaboratorRentabilityDetail>> {
    return this.http.get<ApiResponse<FirmCollaboratorRentabilityDetail>>(`${this.base}/rentability/${id}`);
  }

  prefillRentability(collaboratorUserId: string, year: number): Observable<ApiResponse<FirmCollaboratorRentabilityDetail>> {
    return this.http.get<ApiResponse<FirmCollaboratorRentabilityDetail>>(`${this.base}/rentability/prefill`, {
      params: { collaboratorUserId, year: String(year) }
    });
  }

  createRentability(body: SaveFirmCollaboratorRentability): Observable<ApiResponse<FirmCollaboratorRentabilityDetail>> {
    return this.http.post<ApiResponse<FirmCollaboratorRentabilityDetail>>(`${this.base}/rentability`, body);
  }

  updateRentability(id: string, body: SaveFirmCollaboratorRentability): Observable<ApiResponse<FirmCollaboratorRentabilityDetail>> {
    return this.http.put<ApiResponse<FirmCollaboratorRentabilityDetail>>(`${this.base}/rentability/${id}`, body);
  }

  deleteRentability(id: string): Observable<ApiResponse<unknown>> {
    return this.http.delete<ApiResponse<unknown>>(`${this.base}/rentability/${id}`);
  }

  duplicateRentability(ids: string[]): Observable<ApiResponse<DuplicateFirmRentabilityResult>> {
    return this.http.post<ApiResponse<DuplicateFirmRentabilityResult>>(`${this.base}/rentability/duplicate`, { ids });
  }

  /** Recalcule toutes les marges enregistrées selon le modèle de marge sur coût direct. */
  recalculateRentability(): Observable<ApiResponse<FirmRentabilityRecalculationResult>> {
    return this.http.post<ApiResponse<FirmRentabilityRecalculationResult>>(
      `${this.base}/rentability/recalculate`, {});
  }

  // ---- Coût employeur et taux horaire par collaborateur ----

  listCollaboratorCosts(year: number): Observable<ApiResponse<FirmCollaboratorYearCost[]>> {
    return this.http.get<ApiResponse<FirmCollaboratorYearCost[]>>(`${this.base}/collaborator-costs/${year}`);
  }

  saveCollaboratorCost(year: number, collaboratorUserId: string, body: {
    grossAnnualSalary: number;
    employerContributions?: number | null;
    payrollExtras: number;
    hourlyRateOverride?: number | null;
    overrideJustification?: string | null;
  }): Observable<ApiResponse<FirmCollaboratorYearCost>> {
    return this.http.put<ApiResponse<FirmCollaboratorYearCost>>(
      `${this.base}/collaborator-costs/${year}/${collaboratorUserId}`, body);
  }

  listPayrollEmployees(year: number): Observable<ApiResponse<FirmPayrollCostSnapshot>> {
    return this.http.get<ApiResponse<FirmPayrollCostSnapshot>>(
      `${this.base}/collaborator-costs/${year}/payroll-employees`);
  }

  linkPayrollEmployee(collaboratorUserId: string, payrollEmployeeId: string | null): Observable<ApiResponse<unknown>> {
    return this.http.put<ApiResponse<unknown>>(
      `${this.base}/collaborator-costs/${collaboratorUserId}/payroll-link`, { payrollEmployeeId });
  }

  importPayrollCosts(year: number): Observable<ApiResponse<FirmPayrollImportResult>> {
    return this.http.post<ApiResponse<FirmPayrollImportResult>>(
      `${this.base}/collaborator-costs/${year}/import-payroll`, {});
  }
}
