import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpContext, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '@environments/environment';
import { SKIP_ERROR_TOAST } from '@core/http-context';
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
  irppSmigExemption?: number;
  netSalary: number;
  paidAmount?: number;
  remainingToPay?: number;
  paymentStatus?: string;
  paymentStatusDisplay?: string;
  paidAt?: string;
}

export interface PayrollFeatureFlags {
  statutorySickLeaveEnabled: boolean;
  statutoryMaternityLeaveEnabled: boolean;
  statutoryPaternityLeaveEnabled: boolean;
  terminationIndemnityEnabled: boolean;
  hrDocumentsEnabled: boolean;
  annualBonusesEnabled: boolean;
  publicHolidaysEnabled: boolean;
  civpEnhancementsEnabled: boolean;
  cnssCeilingsEnabled: boolean;
  legalPresetsHistoryEnabled: boolean;
}

export interface PayrollCalculationWarning {
  code: string;
  message: string;
  employeeId?: string;
  employeeName?: string;
}

export interface CalculatePayrollRunResult {
  payslipCount: number;
  warnings: PayrollCalculationWarning[];
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
  totalIrppSmigExemption?: number;
  totalNet: number;
  totalCnssEmployer: number;
  totalTfp: number;
  totalFoprolos: number;
  totalCssEmployer: number;
  totalWorkAccident: number;
  totalOtherDeductions?: number;
  calculatedAt?: string;
  validatedAt?: string;
  closedAt?: string;
  totalPaid?: number;
  remainingToPay?: number;
  paymentStatus?: string;
  paymentStatusDisplay?: string;
  hasPayments?: boolean;
  payslips: PayslipListItem[];
  overtimeLines?: PayrollOvertimeLine[];
  variableAllowanceLines?: PayrollVariableAllowanceLine[];
  mealVoucherLines?: PayrollMealVoucherLine[];
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

export interface PayrollVariableAllowanceLine {
  id: string;
  employeeId: string;
  employeeName?: string;
  year: number;
  month: number;
  label: string;
  amount: number;
  taxable: boolean;
  subjectToCnss: boolean;
}

export interface UpsertVariableAllowanceLineRequest {
  employeeId: string;
  year: number;
  month: number;
  label: string;
  amount: number;
  taxable: boolean;
  subjectToCnss: boolean;
}

/**
 * Régularisation IRPP/CSS annuelle.
 * Les écarts sont signés : positif = rappel à prélever, négatif = restitution.
 */
export interface IrppRegularization {
  id: string;
  employeeId: string;
  employeeName?: string;
  employeeNumber?: string;
  year: number;
  month: number;
  reason: number;
  reasonLabel: string;
  monthsCounted: number;
  cumulNetTaxable: number;
  cumulIrppWithheld: number;
  cumulCssWithheld: number;
  irppDue: number;
  cssDue: number;
  computedIrppDelta: number;
  computedCssDelta: number;
  overrideIrppDelta?: number | null;
  overrideCssDelta?: number | null;
  isOverridden: boolean;
  effectiveIrppDelta: number;
  effectiveCssDelta: number;
  effectiveTotalDelta: number;
  isAdditionalWithholding: boolean;
  notes?: string | null;
  months: IrppRegularizationMonth[];
}

export interface IrppRegularizationMonth {
  month: number;
  monthLabel: string;
  monthlyNetTaxable: number;
  irpp: number;
  css: number;
  /** Faux pour le mois en cours de calcul, qui n'est pas encore arrêté. */
  isSettled: boolean;
}

export interface IrppRegularizationPreview {
  employeeId: string;
  employeeName?: string;
  employeeNumber?: string;
  year: number;
  month: number;
  reason: number;
  reasonLabel: string;
  monthsCounted: number;
  cumulNetTaxable: number;
  cumulIrppWithheld: number;
  cumulCssWithheld: number;
  irppDue: number;
  cssDue: number;
  irppDelta: number;
  cssDelta: number;
  totalDelta: number;
  isAdditionalWithholding: boolean;
  /** Vrai si l'exercice n'a pas activé la régularisation : le calcul reste indicatif. */
  isFeatureDisabled: boolean;
  /** Vrai si l'année est incomplète (embauche ou départ en cours d'exercice). */
  isPartialYear: boolean;
  months: IrppRegularizationMonth[];
}

export interface UpsertIrppRegularizationRequest {
  employeeId: string;
  year: number;
  month: number;
  overrideIrppDelta?: number | null;
  overrideCssDelta?: number | null;
  notes?: string | null;
}

export interface GenerateIrppRegularizationsResult {
  created: number;
  updated: number;
  skipped: number;
  preservedOverrides: number;
  totalDelta: number;
}

export interface LeaveBalance {
  employeeId: string;
  year: number;
  openingBalance: number;
  accruedInYear: number;
  totalAcquired: number;
  consumed: number;
  remaining: number;
  pending: number;
  available: number;
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
  prorataWorkedDays?: number;
  prorataNonWorkedDays?: number;
  prorataDeductionAmount?: number;
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
  enableIrppRegularization?: boolean;
  enableAutomaticProrata?: boolean;
  cssRate: number;
  cssAnnualExemptionThreshold: number;
  cssEmployerRate: number;
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
  smigIrppExemptionMode?: string;
  smigIrppExemptionModeDisplay?: string;
  smigIrppExemptionRateOverride?: number | null;
  mealVoucherDailyExemptionCap: number;
  irppBrackets: { lowerBound: number; rate: number }[];
  cnssMonthlyCeiling?: number | null;
  cnssDailyCeiling?: number | null;
  cssMonthlyCeiling?: number | null;
  accidentWorkMonthlyCeiling?: number | null;
  sickLeaveWaitingDays?: number;
  sickLeaveIjRatePercent?: number;
  maternityLeaveDurationDays?: number;
  paternityLeaveDurationDays?: number;
  maternityEmployerTopUpDefault?: number;
}

export interface PayrollLegalPreset {
  fiscalYear: number;
  label: string;
  cnssEmployeeRate: number;
  cnssEmployerRate: number;
  cssRate: number;
  monthlySmig: number;
  irppBrackets: { lowerBound: number; rate: number }[];
}

export interface PayrollGarnishmentBracket {
  lowerBoundMonthlyNet: number;
  seizableFraction: number;
}

export interface SocialFundScheme {
  id: string;
  code: string;
  name: string;
  isActive: boolean;
  employeeRatePercent: number;
  employerRatePercent: number;
  base: string;
  baseDisplay?: string;
  fixedEmployeeAmount: number;
  fixedEmployerAmount: number;
  monthlyEmployeeCap?: number;
  employeeAccountSce: string;
  employerAccountSce: string;
  effectiveFrom?: string;
  effectiveTo?: string;
}

export interface PayrollPublicHoliday {
  id: string;
  year: number;
  date: string;
  label: string;
  kind: 'Fixed' | 'Islamic';
  kindDisplay?: string;
  isPaid: boolean;
  isEstimated: boolean;
  decreeReference?: string;
}

export interface UpsertPayrollPublicHolidayRequest {
  year: number;
  date: string;
  label: string;
  kind: 'Fixed' | 'Islamic';
  isPaid?: boolean;
  isEstimated?: boolean;
  decreeReference?: string;
}

export interface SeedPayrollPublicHolidaysResult {
  insertedCount: number;
  skippedCount: number;
}

export interface UpsertSocialFundSchemeRequest {
  code: string;
  name: string;
  isActive: boolean;
  employeeRatePercent: number;
  employerRatePercent: number;
  base: string;
  fixedEmployeeAmount?: number;
  fixedEmployerAmount?: number;
  monthlyEmployeeCap?: number;
  employeeAccountSce?: string;
  employerAccountSce?: string;
  effectiveFrom?: string;
  effectiveTo?: string;
}

export interface EmployeeSocialFundEnrollment {
  id: string;
  employeeId: string;
  socialFundSchemeId: string;
  schemeCode?: string;
  schemeName?: string;
  startDate: string;
  endDate?: string;
  overrideEmployeeAmount?: number;
  overrideEmployerAmount?: number;
}

export interface UpsertSocialFundEnrollmentRequest {
  socialFundSchemeId: string;
  startDate: string;
  endDate?: string;
  overrideEmployeeAmount?: number;
  overrideEmployerAmount?: number;
}

export interface PayrollMealVoucherLine {
  id: string;
  employeeId: string;
  employeeName?: string;
  year: number;
  month: number;
  days: number;
  faceValue: number;
  employerContributionRate: number;
  totalValue?: number;
  employerContribution?: number;
  employeeContribution?: number;
}

export interface UpsertMealVoucherLineRequest {
  employeeId: string;
  year: number;
  month: number;
  days: number;
  faceValue: number;
  employerContributionRate: number;
}

export interface EmployeeInKindBenefit {
  id: string;
  employeeId: string;
  type: string;
  typeDisplay?: string;
  label: string;
  monthlyValue: number;
  startDate: string;
  endDate?: string;
  description?: string;
}

export interface UpsertInKindBenefitRequest {
  type: string;
  label: string;
  monthlyValue: number;
  startDate: string;
  endDate?: string;
  description?: string;
}

export interface EmployeeLoanInstallment {
  id: string;
  sequenceNumber: number;
  year: number;
  month: number;
  amount: number;
  isSettled: boolean;
}

export interface EmployeeLoan {
  id: string;
  employeeId: string;
  reference: string;
  principal: number;
  installmentCount: number;
  monthlyInstallmentAmount: number;
  startYear: number;
  startMonth: number;
  notes?: string;
  status: string;
  statusDisplay?: string;
  remainingBalance?: number;
  installments?: EmployeeLoanInstallment[];
}

export interface CreateEmployeeLoanRequest {
  reference: string;
  principal: number;
  installmentCount: number;
  startYear: number;
  startMonth: number;
  notes?: string;
}

export interface EmployeeGarnishment {
  id: string;
  employeeId: string;
  type: string;
  typeDisplay?: string;
  reference: string;
  issuedAt: string;
  beneficiaryName: string;
  beneficiaryRib?: string;
  priority: number;
  kind: string;
  kindDisplay?: string;
  fixedAmount?: number;
  percentOfNet?: number;
  totalAmountDue?: number;
  startDate: string;
  endDate?: string;
  status: string;
  statusDisplay?: string;
  totalApplied?: number;
}

export interface UpsertEmployeeGarnishmentRequest {
  type: string;
  reference: string;
  issuedAt: string;
  beneficiaryName: string;
  beneficiaryRib?: string;
  priority: number;
  kind: string;
  fixedAmount?: number;
  percentOfNet?: number;
  totalAmountDue?: number;
  startDate: string;
  endDate?: string;
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

export interface CnssContributionRemittance {
  year: number;
  month: number;
  employerCompanyName: string;
  employerNif: string;
  employerCnssNumber?: string;
  employerAddressLine?: string;
  payrollRunId?: string;
  sourceRunStatus?: number;
  isEligible: boolean;
  hasExistingPayment: boolean;
  paymentStatus?: number;
  paymentStatusDisplay?: string;
  totalCnssEmployee: number;
  totalCnssEmployer: number;
  totalWorkAccident: number;
  totalDue: number;
  employeeCount: number;
  documentReference: string;
  warnings: string[];
  lines: CnssContributionRemittanceLine[];
  payment?: CnssContributionPayment;
}

export interface CnssContributionRemittanceLine {
  employeeId: string;
  employeeName: string;
  cnssNumber?: string;
  cnssableGross: number;
  cnssEmployee: number;
  cnssEmployer: number;
  workAccident: number;
  lineTotal: number;
  warnings: string[];
}

export interface CnssContributionPayment {
  id: string;
  year: number;
  month: number;
  payrollRunId: string;
  totalCnssEmployee: number;
  totalCnssEmployer: number;
  totalWorkAccident: number;
  totalDue: number;
  amount: number;
  paymentDate: string;
  method: string;
  methodDisplay: string;
  bankAccountId?: string;
  reference?: string;
  notes?: string;
  isCancelled: boolean;
}

export interface RecordCnssContributionPaymentRequest {
  year: number;
  month: number;
  paymentDate: string;
  method: number;
  bankAccountId?: string;
  reference?: string;
  notes?: string;
}

export interface PayrollWithholdingCertificateBatch {
  year: number;
  employerCompanyName: string;
  employerNif: string;
  employerAddressLine?: string;
  employeeCount: number;
  includedMonths: number[];
  missingMonths: number[];
  isComplete: boolean;
  totalGross: number;
  totalAnnualNetTaxable: number;
  totalIrppWithheld: number;
  totalCssWithheld: number;
  totalWithholding: number;
  lines: PayrollWithholdingCertificateLine[];
}

export interface PayrollWithholdingCertificateLine {
  employeeId: string;
  employeeNumber: string;
  employeeName: string;
  cin?: string;
  cnssNumber?: string;
  addressLine?: string;
  isHeadOfFamily: boolean;
  monthsCount: number;
  isPartialYear: boolean;
  totalGross: number;
  totalCnssableGross: number;
  totalCnssEmployee: number;
  totalProfessionalExpenses: number;
  totalFamilyDeductions: number;
  annualNetTaxable: number;
  totalIrppWithheld: number;
  totalCssWithheld: number;
  totalWithholding: number;
  totalIrppSmigExemption: number;
  documentReference: string;
  warnings: string[];
}

export interface PayrollProrataPreviewLine {
  employeeId: string;
  employeeName: string;
  employeeNumber: string;
  workedDays: number;
  nonWorkedDays: number;
  deductionAmount: number;
  reason: string;
  warnings: string[];
}

export interface PayrollProrataPreview {
  year: number;
  month: number;
  isEnabled: boolean;
  employeeCount: number;
  totalDeduction: number;
  lines: PayrollProrataPreviewLine[];
}

export interface EmployeePayrollSuspension {
  id: string;
  employeeId: string;
  type: string;
  typeDisplay: string;
  startDate: string;
  endDate?: string;
  isPaid: boolean;
  reason?: string;
  isApproved: boolean;
  approvedAt?: string;
}

export interface TerminateEmployeeRequest {
  terminationDate: string;
  reason?: string;
  closeActiveContract: boolean;
  deactivateNow: boolean;
  createSettlement?: boolean;
  terminationReason?: string;
}

export interface TerminateEmployeeResult {
  isActive: boolean;
  terminationDate: string;
  warning?: string;
  settlementId?: string;
}

export interface TerminationSettlement {
  id: string;
  employeeId: string;
  employeeName?: string;
  year: number;
  month: number;
  terminationDate: string;
  reason: string;
  reasonDisplay: string;
  status: string;
  statusDisplay: string;
  seniorityMonths: number;
  grossMonthlyReference: number;
  legalIndemnityAmount: number;
  noticeIndemnityAmount: number;
  unusedLeaveAmount: number;
  otherIndemnityAmount: number;
  totalIndemnityAmount: number;
  notes?: string;
}

export interface TerminationSettlementPreview {
  employeeId: string;
  employeeName?: string;
  terminationDate: string;
  reason: string;
  reasonDisplay: string;
  seniorityMonths: number;
  indemnityDays: number;
  dailyRate: number;
  grossMonthlyReference: number;
  legalIndemnityAmount: number;
  noticeIndemnityAmount: number;
  unusedLeaveAmount: number;
  otherIndemnityAmount: number;
  totalIndemnityAmount: number;
}

export interface UpsertTerminationSettlementRequest {
  employeeId: string;
  year: number;
  month: number;
  terminationDate: string;
  reason: string;
  legalIndemnityAmount?: number;
  noticeIndemnityAmount: number;
  unusedLeaveAmount: number;
  otherIndemnityAmount: number;
  notes?: string;
  approve: boolean;
}

export interface AnnualBonusRule {
  id: string;
  code: string;
  label: string;
  kind: string;
  kindDisplay: string;
  formula: string;
  formulaDisplay: string;
  paymentMonth: number;
  fixedAmount: number;
  ratePercent: number;
  monthsOfBase: number;
  taxable: boolean;
  subjectToCnss: boolean;
  isActive: boolean;
  fiscalYear?: number;
}

export interface UpsertAnnualBonusRuleRequest {
  code: string;
  label: string;
  kind: string;
  formula: string;
  paymentMonth: number;
  fixedAmount: number;
  ratePercent: number;
  monthsOfBase: number;
  taxable: boolean;
  subjectToCnss: boolean;
  isActive: boolean;
  fiscalYear?: number;
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
  medicalCertificateNumber?: string;
  medicalCertificateDate?: string;
  subrogationEnabled?: boolean;
  employerTopUpPercent?: number;
  employerTopUpDays?: number;
  expectedBirthDate?: string;
  actualBirthDate?: string;
  childBirthCertificateNumber?: string;
}

export interface CnssIjClaim {
  id: string;
  employeeId: string;
  employeeName?: string;
  leaveRequestId: string;
  year: number;
  month: number;
  amount: number;
  status: string;
  statusDisplay: string;
  paidAt?: string;
}

export interface CreateLeaveRequest {
  employeeId: string;
  type: string;
  startDate: string;
  endDate: string;
  days: number;
  reason?: string;
  medicalCertificateNumber?: string;
  medicalCertificateDate?: string;
  subrogationEnabled?: boolean;
  employerTopUpPercent?: number;
  employerTopUpDays?: number;
  expectedBirthDate?: string;
  actualBirthDate?: string;
  childBirthCertificateNumber?: string;
}

export interface DeclareBirthRequest {
  employeeId: string;
  actualBirthDate: string;
  childBirthCertificateNumber?: string;
  createMaternityLeave: boolean;
  createPaternityLeave: boolean;
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

export interface PayrollBankTransferLine {
  employeeId: string;
  payslipId: string;
  employeeNumber: string;
  lastName: string;
  firstName: string;
  fullName: string;
  rib: string;
  iban: string;
  netSalary: number;
  transferLabel: string;
  cin?: string;
  cnssNumber?: string;
}

export interface PayrollBankTransferExcludedLine {
  employeeId: string;
  payslipId?: string;
  employeeNumber: string;
  employeeName: string;
  netSalary: number;
  reason: string;
  reasonDisplay: string;
}

export interface PayrollBankTransferWarning {
  code: string;
  message: string;
}

export interface PayrollBankTransferDebtor {
  bankAccountId?: string;
  rib?: string;
  iban?: string;
  bankName?: string;
  bankCode?: string;
}

export interface PayrollBankTransferPreview {
  payrollRunId: string;
  year: number;
  month: number;
  periodLabel: string;
  transferLabel: string;
  companyName?: string;
  debtorAccount?: PayrollBankTransferDebtor | null;
  eligibleCount: number;
  totalAmount: number;
  lines: PayrollBankTransferLine[];
  excludedLines: PayrollBankTransferExcludedLine[];
  warnings: PayrollBankTransferWarning[];
}

export interface PayrollPaymentLine {
  id: string;
  payslipId: string;
  employeeId: string;
  employeeName: string;
  amount: number;
  employeeAuxiliaryAccount: string;
}

export interface PayrollPayment {
  id: string;
  payrollRunId: string;
  amount: number;
  paymentDate: string;
  method: string;
  methodDisplay: string;
  bankAccountId?: string;
  reference?: string;
  notes?: string;
  isCancelled: boolean;
  cancelledAt?: string;
  cancelledBy?: string;
  cancellationReason?: string;
  createdAt: string;
  createdBy?: string;
  lines: PayrollPaymentLine[];
}

export interface RecordPayrollRunPaymentRequest {
  paymentDate: string;
  method: number;
  bankAccountId?: string;
  reference?: string;
  notes?: string;
  payslipAmounts?: { payslipId: string; amount: number }[];
}

// ── États de contrôle (livre de paie, journal de paie) ──

/** Aligné sur l'enum backend AccountingExportFormat. */
export type PayrollReportExportFormat = 'csv' | 'excel' | 'pdf';

/** Aligné sur l'enum backend PayrollJournalView. */
export type PayrollJournalView = 'ByEmployee' | 'Accounting';

export interface PayrollBookLine {
  employeeId: string;
  employeeNumber: string;
  employeeName: string;
  cin?: string;
  cnssNumber?: string;
  category?: string;
  echelon?: string;
  hireDate?: string;
  monthsCount: number;
  grossSalary: number;
  cnssableGross: number;
  cnssEmployee: number;
  professionalExpenses: number;
  familyDeductions: number;
  netTaxable: number;
  irpp: number;
  irppRegularization: number;
  css: number;
  cssRegularization: number;
  otherDeductions: number;
  nonTaxableAllowances: number;
  netSalary: number;
}

export interface PayrollBook {
  year: number;
  fromMonth: number;
  toMonth: number;
  periodLabel: string;
  includeCalculated: boolean;
  includedMonths: number[];
  missingMonths: number[];
  provisionalMonths: number[];
  isProvisional: boolean;
  employeeCount: number;
  totalGross: number;
  totalCnssableGross: number;
  totalCnssEmployee: number;
  totalProfessionalExpenses: number;
  totalFamilyDeductions: number;
  totalNetTaxable: number;
  totalIrpp: number;
  totalIrppRegularization: number;
  totalCss: number;
  totalCssRegularization: number;
  totalOtherDeductions: number;
  totalNonTaxableAllowances: number;
  totalNetSalary: number;
  totalCnssEmployer: number;
  totalWorkAccident: number;
  totalTfp: number;
  totalFoprolos: number;
  totalCssEmployer: number;
  totalEmployerCharges: number;
  totalEmployerCost: number;
  lines: PayrollBookLine[];
}

export interface PayrollJournalEmployeeLine {
  payslipId: string;
  employeeId: string;
  employeeNumber: string;
  employeeName: string;
  cnssNumber?: string;
  grossSalary: number;
  cnssableGross: number;
  cnssEmployee: number;
  professionalExpenses: number;
  familyDeductions: number;
  monthlyNetTaxable: number;
  irpp: number;
  irppRegularization: number;
  css: number;
  cssRegularization: number;
  otherDeductions: number;
  nonTaxableAllowances: number;
  netSalary: number;
  cnssEmployer: number;
  workAccidentContribution: number;
  tfp: number;
  foprolos: number;
  cssEmployer: number;
  totalEmployerCharges: number;
  totalCost: number;
}

export interface PayrollJournalAccountingLine {
  accountNumber: string;
  accountLabel: string;
  label: string;
  debit: number;
  credit: number;
}

export interface PayrollJournal {
  payrollRunId: string;
  year: number;
  month: number;
  periodLabel: string;
  status: string;
  statusDisplay: string;
  isProvisional: boolean;
  employeeCount: number;
  totalGross: number;
  totalCnssableGross: number;
  totalCnssEmployee: number;
  totalProfessionalExpenses: number;
  totalFamilyDeductions: number;
  totalNetTaxable: number;
  totalIrpp: number;
  totalIrppRegularization: number;
  totalCss: number;
  totalCssRegularization: number;
  totalOtherDeductions: number;
  totalNonTaxableAllowances: number;
  totalNetSalary: number;
  totalCnssEmployer: number;
  totalWorkAccident: number;
  totalTfp: number;
  totalFoprolos: number;
  totalCssEmployer: number;
  totalEmployerCharges: number;
  totalEmployerCost: number;
  totalDebit: number;
  totalCredit: number;
  isBalanced: boolean;
  accountingLinesArePosted: boolean;
  accountingEntryNumber?: number;
  accountingEntryDate?: string;
  accountingJournalCode?: string;
  lines: PayrollJournalEmployeeLine[];
  accountingLines: PayrollJournalAccountingLine[];
}

@Injectable({ providedIn: 'root' })
export class PayrollService {
  private readonly http = inject(HttpClient);
  private readonly runsUrl = `${environment.apiUrl}/payroll/runs`;
  private readonly settingsUrl = `${environment.apiUrl}/payroll/settings`;
  private readonly payrollUrl = `${environment.apiUrl}/payroll`;
  private readonly reportsUrl = `${environment.apiUrl}/payroll/reports`;

  listRuns(year?: number): Observable<ApiResponse<PayrollRunListItem[]>> {
    let params = new HttpParams();
    if (year) params = params.set('year', year);
    return this.http.get<ApiResponse<PayrollRunListItem[]>>(this.runsUrl, { params });
  }

  getRun(id: string): Observable<ApiResponse<PayrollRunDetail>> {
    return this.http.get<ApiResponse<PayrollRunDetail>>(`${this.runsUrl}/${id}`);
  }

  getProrataPreview(runId: string): Observable<ApiResponse<PayrollProrataPreview>> {
    return this.http.get<ApiResponse<PayrollProrataPreview>>(`${this.runsUrl}/${runId}/prorata-preview`);
  }

  createRun(year: number, month: number): Observable<ApiResponse<string>> {
    return this.http.post<ApiResponse<string>>(this.runsUrl, { year, month }, {
      context: new HttpContext().set(SKIP_ERROR_TOAST, true)
    });
  }

  // Le secteur TFP (industrie 1 % / autres 2 %) est lu côté serveur depuis les
  // paramètres de l'exercice — il n'est plus transmis à chaque calcul.
  calculateRun(id: string): Observable<ApiResponse<CalculatePayrollRunResult>> {
    return this.http.post<ApiResponse<CalculatePayrollRunResult>>(`${this.runsUrl}/${id}/calculate`, {
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

  getLegalPreset(fiscalYear: number): Observable<ApiResponse<PayrollLegalPreset>> {
    return this.http.get<ApiResponse<PayrollLegalPreset>>(`${this.settingsUrl}/parameters/${fiscalYear}/preset`);
  }

  getFeatureFlags(): Observable<ApiResponse<PayrollFeatureFlags>> {
    return this.http.get<ApiResponse<PayrollFeatureFlags>>(`${this.settingsUrl}/feature-flags`);
  }

  downloadEmploymentCertificatePdf(employeeId: string): Observable<Blob> {
    return this.http.get(`${this.payrollUrl}/hr-documents/employment-certificate/${employeeId}/pdf`, {
      responseType: 'blob'
    });
  }

  downloadSalaryCertificatePdf(employeeId: string, months: 3 | 6 | 12): Observable<Blob> {
    const params = new HttpParams().set('months', months);
    return this.http.get(`${this.payrollUrl}/hr-documents/salary-certificate/${employeeId}/pdf`, {
      params,
      responseType: 'blob'
    });
  }

  downloadSoldeToutComptePdf(employeeId: string): Observable<Blob> {
    return this.http.get(`${this.payrollUrl}/hr-documents/solde-tout-compte/${employeeId}/pdf`, {
      responseType: 'blob'
    });
  }

  updateParameters(fiscalYear: number, body: Omit<PayrollParameters, 'id' | 'fiscalYear'>): Observable<ApiResponse<unknown>> {
    return this.http.put<ApiResponse<unknown>>(`${this.settingsUrl}/parameters/${fiscalYear}`, body);
  }

  getDts(year: number, quarter: number): Observable<ApiResponse<DtsDeclaration>> {
    const params = new HttpParams().set('year', year).set('quarter', quarter);
    return this.http.get<ApiResponse<DtsDeclaration>>(`${this.payrollUrl}/declarations/dts`, { params });
  }

  createLeave(body: CreateLeaveRequest): Observable<ApiResponse<string>> {
    return this.http.post<ApiResponse<string>>(`${this.payrollUrl}/leaves`, body);
  }

  declareBirth(body: DeclareBirthRequest): Observable<ApiResponse<string>> {
    return this.http.post<ApiResponse<string>>(`${this.payrollUrl}/leaves/declare-birth`, body);
  }

  listCnssIjClaims(employeeId?: string, year?: number, month?: number): Observable<ApiResponse<CnssIjClaim[]>> {
    let params = new HttpParams();
    if (employeeId) params = params.set('employeeId', employeeId);
    if (year) params = params.set('year', year);
    if (month) params = params.set('month', month);
    return this.http.get<ApiResponse<CnssIjClaim[]>>(`${this.payrollUrl}/cnss-ij-claims`, { params });
  }

  markCnssIjClaimPaid(id: string, paidAt: string): Observable<ApiResponse<unknown>> {
    return this.http.post<ApiResponse<unknown>>(`${this.payrollUrl}/cnss-ij-claims/${id}/mark-paid`, { paidAt });
  }

  approveLeave(id: string): Observable<ApiResponse<unknown>> {
    return this.http.post<ApiResponse<unknown>>(`${this.payrollUrl}/leaves/${id}/approve`, {});
  }

  deleteLeave(id: string): Observable<ApiResponse<unknown>> {
    return this.http.delete<ApiResponse<unknown>>(`${this.payrollUrl}/leaves/${id}`);
  }

  listSuspensions(employeeId: string): Observable<ApiResponse<EmployeePayrollSuspension[]>> {
    return this.http.get<ApiResponse<EmployeePayrollSuspension[]>>(
      `${this.payrollUrl}/employees/${employeeId}/suspensions`
    );
  }

  createSuspension(
    employeeId: string,
    body: { type: string; startDate: string; endDate?: string; isPaid: boolean; reason?: string }
  ): Observable<ApiResponse<string>> {
    return this.http.post<ApiResponse<string>>(
      `${this.payrollUrl}/employees/${employeeId}/suspensions`,
      body
    );
  }

  updateSuspension(
    id: string,
    body: { type: string; startDate: string; endDate?: string; isPaid: boolean; reason?: string }
  ): Observable<ApiResponse<unknown>> {
    return this.http.put<ApiResponse<unknown>>(`${this.payrollUrl}/employees/suspensions/${id}`, body);
  }

  deleteSuspension(id: string): Observable<ApiResponse<unknown>> {
    return this.http.delete<ApiResponse<unknown>>(`${this.payrollUrl}/employees/suspensions/${id}`);
  }

  approveSuspension(id: string): Observable<ApiResponse<unknown>> {
    return this.http.post<ApiResponse<unknown>>(`${this.payrollUrl}/employees/suspensions/${id}/approve`, {});
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

  listVariableAllowances(year: number, month: number): Observable<ApiResponse<PayrollVariableAllowanceLine[]>> {
    const params = new HttpParams().set('year', year).set('month', month);
    return this.http.get<ApiResponse<PayrollVariableAllowanceLine[]>>(`${this.payrollUrl}/variable-allowances`, { params });
  }

  createVariableAllowance(body: UpsertVariableAllowanceLineRequest): Observable<ApiResponse<string>> {
    return this.http.post<ApiResponse<string>>(`${this.payrollUrl}/variable-allowances`, body);
  }

  updateVariableAllowance(id: string, body: UpsertVariableAllowanceLineRequest): Observable<ApiResponse<unknown>> {
    return this.http.put<ApiResponse<unknown>>(`${this.payrollUrl}/variable-allowances/${id}`, body);
  }

  deleteVariableAllowance(id: string): Observable<ApiResponse<unknown>> {
    return this.http.delete<ApiResponse<unknown>>(`${this.payrollUrl}/variable-allowances/${id}`);
  }

  // ── Régularisation IRPP annuelle ──

  listIrppRegularizations(year: number, month: number): Observable<ApiResponse<IrppRegularization[]>> {
    const params = new HttpParams().set('year', year).set('month', month);
    return this.http.get<ApiResponse<IrppRegularization[]>>(`${this.payrollUrl}/regularizations`, { params });
  }

  previewIrppRegularization(employeeId: string, year: number, month: number): Observable<ApiResponse<IrppRegularizationPreview>> {
    const params = new HttpParams().set('employeeId', employeeId).set('year', year).set('month', month);
    return this.http.get<ApiResponse<IrppRegularizationPreview>>(`${this.payrollUrl}/regularizations/preview`, { params });
  }

  generateIrppRegularizations(runId: string): Observable<ApiResponse<GenerateIrppRegularizationsResult>> {
    return this.http.post<ApiResponse<GenerateIrppRegularizationsResult>>(
      `${this.runsUrl}/${runId}/regularizations/generate`, {});
  }

  upsertIrppRegularization(body: UpsertIrppRegularizationRequest): Observable<ApiResponse<string>> {
    return this.http.post<ApiResponse<string>>(`${this.payrollUrl}/regularizations`, body);
  }

  deleteIrppRegularization(id: string): Observable<ApiResponse<unknown>> {
    return this.http.delete<ApiResponse<unknown>>(`${this.payrollUrl}/regularizations/${id}`);
  }

  computeLeaveDays(startDate: string, endDate: string): Observable<ApiResponse<number>> {
    const params = new HttpParams().set('startDate', startDate).set('endDate', endDate);
    return this.http.get<ApiResponse<number>>(`${this.payrollUrl}/leaves/compute-days`, { params });
  }

  exportDtsCsv(year: number, quarter: number): Observable<Blob> {
    const params = new HttpParams().set('year', year).set('quarter', quarter);
    return this.http.get(`${this.payrollUrl}/declarations/dts/export`, { params, responseType: 'blob' });
  }

  getCnssRemittance(year: number, month: number): Observable<ApiResponse<CnssContributionRemittance>> {
    const params = new HttpParams().set('year', year).set('month', month);
    return this.http.get<ApiResponse<CnssContributionRemittance>>(
      `${this.payrollUrl}/declarations/cnss-remittance`, { params });
  }

  exportCnssRemittanceCsv(year: number, month: number): Observable<Blob> {
    const params = new HttpParams().set('year', year).set('month', month);
    return this.http.get(`${this.payrollUrl}/declarations/cnss-remittance/export/csv`, { params, responseType: 'blob' });
  }

  exportCnssRemittancePdf(year: number, month: number): Observable<Blob> {
    const params = new HttpParams().set('year', year).set('month', month);
    return this.http.get(`${this.payrollUrl}/declarations/cnss-remittance/export/pdf`, { params, responseType: 'blob' });
  }

  recordCnssRemittancePayment(body: RecordCnssContributionPaymentRequest): Observable<ApiResponse<string>> {
    return this.http.post<ApiResponse<string>>(`${this.payrollUrl}/declarations/cnss-remittance/payment`, body);
  }

  cancelCnssRemittancePayment(paymentId: string, reason: string): Observable<ApiResponse<unknown>> {
    return this.http.post<ApiResponse<unknown>>(
      `${this.payrollUrl}/declarations/cnss-remittance/payment/${paymentId}/cancel`,
      { reason });
  }

  getWithholdingCertificates(year: number): Observable<ApiResponse<PayrollWithholdingCertificateBatch>> {
    const params = new HttpParams().set('year', year);
    return this.http.get<ApiResponse<PayrollWithholdingCertificateBatch>>(
      `${this.payrollUrl}/declarations/withholding-certificates`, { params });
  }

  exportWithholdingCertificatesCsv(year: number): Observable<Blob> {
    const params = new HttpParams().set('year', year);
    return this.http.get(`${this.payrollUrl}/declarations/withholding-certificates/export/csv`, {
      params,
      responseType: 'blob'
    });
  }

  exportWithholdingCertificatesZip(year: number): Observable<Blob> {
    const params = new HttpParams().set('year', year);
    return this.http.get(`${this.payrollUrl}/declarations/withholding-certificates/export/zip`, {
      params,
      responseType: 'blob'
    });
  }

  downloadWithholdingCertificatePdf(year: number, employeeId: string): Observable<Blob> {
    const params = new HttpParams().set('year', year);
    return this.http.get(
      `${this.payrollUrl}/declarations/withholding-certificates/${employeeId}/pdf`,
      { params, responseType: 'blob' });
  }

  getBankTransferPreview(runId: string, bankAccountId?: string, label?: string): Observable<ApiResponse<PayrollBankTransferPreview>> {
    let params = new HttpParams();
    if (bankAccountId) params = params.set('bankAccountId', bankAccountId);
    if (label) params = params.set('label', label);
    return this.http.get<ApiResponse<PayrollBankTransferPreview>>(
      `${this.runsUrl}/${runId}/bank-transfer/preview`,
      { params }
    );
  }

  exportBankTransfer(runId: string, opts?: { format?: 'csv'; bankAccountId?: string; label?: string }): Observable<Blob> {
    let params = new HttpParams().set('format', opts?.format ?? 'csv');
    if (opts?.bankAccountId) params = params.set('bankAccountId', opts.bankAccountId);
    if (opts?.label) params = params.set('label', opts.label);
    return this.http.get(`${this.runsUrl}/${runId}/bank-transfer/export`, { params, responseType: 'blob' });
  }

  listRunPayments(runId: string, includeCancelled = false): Observable<ApiResponse<PayrollPayment[]>> {
    const params = includeCancelled ? new HttpParams().set('includeCancelled', 'true') : undefined;
    return this.http.get<ApiResponse<PayrollPayment[]>>(`${this.payrollUrl}/runs/${runId}/payments`, { params });
  }

  recordRunPayment(runId: string, body: RecordPayrollRunPaymentRequest): Observable<ApiResponse<string>> {
    return this.http.post<ApiResponse<string>>(`${this.payrollUrl}/runs/${runId}/payments`, body);
  }

  cancelPayment(paymentId: string, reason: string): Observable<ApiResponse<unknown>> {
    return this.http.post<ApiResponse<unknown>>(`${this.payrollUrl}/payments/${paymentId}/cancel`, { reason });
  }

  cancelAllRunPayments(runId: string, reason: string): Observable<ApiResponse<unknown>> {
    return this.http.post<ApiResponse<unknown>>(`${this.payrollUrl}/runs/${runId}/payments/cancel-all`, { reason });
  }

  // ── Public holidays ──

  listPublicHolidays(year: number): Observable<ApiResponse<PayrollPublicHoliday[]>> {
    const params = new HttpParams().set('year', year);
    return this.http.get<ApiResponse<PayrollPublicHoliday[]>>(`${this.payrollUrl}/public-holidays`, { params });
  }

  createPublicHoliday(body: UpsertPayrollPublicHolidayRequest): Observable<ApiResponse<string>> {
    return this.http.post<ApiResponse<string>>(`${this.payrollUrl}/public-holidays`, body);
  }

  updatePublicHoliday(id: string, body: UpsertPayrollPublicHolidayRequest): Observable<ApiResponse<unknown>> {
    return this.http.put<ApiResponse<unknown>>(`${this.payrollUrl}/public-holidays/${id}`, body);
  }

  deletePublicHoliday(id: string): Observable<ApiResponse<unknown>> {
    return this.http.delete<ApiResponse<unknown>>(`${this.payrollUrl}/public-holidays/${id}`);
  }

  seedPublicHolidays(years: number[], overwriteExisting = false): Observable<ApiResponse<SeedPayrollPublicHolidaysResult>> {
    return this.http.post<ApiResponse<SeedPayrollPublicHolidaysResult>>(`${this.payrollUrl}/public-holidays/seed`, {
      years,
      overwriteExisting
    });
  }

  // ── Social fund schemes ──

  listSocialFunds(): Observable<ApiResponse<SocialFundScheme[]>> {
    return this.http.get<ApiResponse<SocialFundScheme[]>>(`${this.payrollUrl}/social-funds/schemes`);
  }

  createSocialFund(body: UpsertSocialFundSchemeRequest): Observable<ApiResponse<string>> {
    return this.http.post<ApiResponse<string>>(`${this.payrollUrl}/social-funds/schemes`, body);
  }

  updateSocialFund(id: string, body: UpsertSocialFundSchemeRequest): Observable<ApiResponse<unknown>> {
    return this.http.put<ApiResponse<unknown>>(`${this.payrollUrl}/social-funds/schemes/${id}`, body);
  }

  deleteSocialFund(scheme: SocialFundScheme): Observable<ApiResponse<unknown>> {
    const body: UpsertSocialFundSchemeRequest = {
      code: scheme.code,
      name: scheme.name,
      isActive: false,
      employeeRatePercent: scheme.employeeRatePercent,
      employerRatePercent: scheme.employerRatePercent,
      base: scheme.base,
      fixedEmployeeAmount: scheme.fixedEmployeeAmount,
      fixedEmployerAmount: scheme.fixedEmployerAmount,
      monthlyEmployeeCap: scheme.monthlyEmployeeCap,
      employeeAccountSce: scheme.employeeAccountSce,
      employerAccountSce: scheme.employerAccountSce,
      effectiveFrom: scheme.effectiveFrom,
      effectiveTo: scheme.effectiveTo
    };
    return this.updateSocialFund(scheme.id, body);
  }

  listSocialFundEnrollments(employeeId: string): Observable<ApiResponse<EmployeeSocialFundEnrollment[]>> {
    const params = new HttpParams().set('employeeId', employeeId);
    return this.http.get<ApiResponse<EmployeeSocialFundEnrollment[]>>(
      `${this.payrollUrl}/social-funds/enrollments`,
      { params }
    );
  }

  createSocialFundEnrollment(employeeId: string, body: UpsertSocialFundEnrollmentRequest): Observable<ApiResponse<string>> {
    return this.http.post<ApiResponse<string>>(`${this.payrollUrl}/social-funds/enrollments`, {
      ...body,
      employeeId
    });
  }

  updateSocialFundEnrollment(
    employeeId: string,
    enrollmentId: string,
    body: UpsertSocialFundEnrollmentRequest
  ): Observable<ApiResponse<unknown>> {
    return this.http.put<ApiResponse<unknown>>(`${this.payrollUrl}/social-funds/enrollments/${enrollmentId}`, {
      endDate: body.endDate,
      overrideEmployeeAmount: body.overrideEmployeeAmount,
      overrideEmployerAmount: body.overrideEmployerAmount
    });
  }

  deleteSocialFundEnrollment(employeeId: string, enrollmentId: string): Observable<ApiResponse<unknown>> {
    return this.http.delete<ApiResponse<unknown>>(
      `${this.payrollUrl}/social-funds/enrollments/${enrollmentId}`
    );
  }

  // ── Meal vouchers ──

  listMealVouchers(year: number, month: number): Observable<ApiResponse<PayrollMealVoucherLine[]>> {
    const params = new HttpParams().set('year', year).set('month', month);
    return this.http.get<ApiResponse<PayrollMealVoucherLine[]>>(`${this.payrollUrl}/meal-vouchers`, { params });
  }

  createMealVoucher(body: UpsertMealVoucherLineRequest): Observable<ApiResponse<string>> {
    return this.http.post<ApiResponse<string>>(`${this.payrollUrl}/meal-vouchers`, body);
  }

  updateMealVoucher(id: string, body: UpsertMealVoucherLineRequest): Observable<ApiResponse<unknown>> {
    return this.http.put<ApiResponse<unknown>>(`${this.payrollUrl}/meal-vouchers/${id}`, body);
  }

  deleteMealVoucher(id: string): Observable<ApiResponse<unknown>> {
    return this.http.delete<ApiResponse<unknown>>(`${this.payrollUrl}/meal-vouchers/${id}`);
  }

  // ── In-kind benefits ──

  listInKindBenefits(employeeId: string): Observable<ApiResponse<EmployeeInKindBenefit[]>> {
    const params = new HttpParams().set('employeeId', employeeId);
    return this.http.get<ApiResponse<EmployeeInKindBenefit[]>>(`${this.payrollUrl}/in-kind-benefits`, { params });
  }

  createInKindBenefit(employeeId: string, body: UpsertInKindBenefitRequest): Observable<ApiResponse<string>> {
    return this.http.post<ApiResponse<string>>(`${this.payrollUrl}/in-kind-benefits`, { ...body, employeeId });
  }

  updateInKindBenefit(
    employeeId: string,
    benefitId: string,
    body: UpsertInKindBenefitRequest
  ): Observable<ApiResponse<unknown>> {
    return this.http.put<ApiResponse<unknown>>(`${this.payrollUrl}/in-kind-benefits/${benefitId}`, {
      ...body,
      employeeId
    });
  }

  deleteInKindBenefit(employeeId: string, benefitId: string): Observable<ApiResponse<unknown>> {
    return this.http.delete<ApiResponse<unknown>>(`${this.payrollUrl}/in-kind-benefits/${benefitId}`);
  }

  // ── Employee loans ──

  listEmployeeLoans(employeeId: string): Observable<ApiResponse<EmployeeLoan[]>> {
    const params = new HttpParams().set('employeeId', employeeId);
    return this.http.get<ApiResponse<EmployeeLoan[]>>(`${this.payrollUrl}/loans`, { params });
  }

  getLoan(loanId: string): Observable<ApiResponse<EmployeeLoan>> {
    return this.http.get<ApiResponse<EmployeeLoan>>(`${this.payrollUrl}/loans/${loanId}`);
  }

  createEmployeeLoan(employeeId: string, body: CreateEmployeeLoanRequest): Observable<ApiResponse<string>> {
    return this.http.post<ApiResponse<string>>(`${this.payrollUrl}/loans`, { ...body, employeeId });
  }

  cancelLoan(loanId: string): Observable<ApiResponse<unknown>> {
    return this.http.post<ApiResponse<unknown>>(`${this.payrollUrl}/loans/${loanId}/cancel`, {});
  }

  // ── Garnishments ──

  listGarnishments(employeeId: string): Observable<ApiResponse<EmployeeGarnishment[]>> {
    const params = new HttpParams().set('employeeId', employeeId);
    return this.http.get<ApiResponse<EmployeeGarnishment[]>>(`${this.payrollUrl}/garnishments`, { params });
  }

  createGarnishment(employeeId: string, body: UpsertEmployeeGarnishmentRequest): Observable<ApiResponse<string>> {
    return this.http.post<ApiResponse<string>>(`${this.payrollUrl}/garnishments`, { ...body, employeeId });
  }

  updateGarnishment(
    employeeId: string,
    garnishmentId: string,
    body: UpsertEmployeeGarnishmentRequest
  ): Observable<ApiResponse<unknown>> {
    return this.http.put<ApiResponse<unknown>>(
      `${this.payrollUrl}/employees/${employeeId}/garnishments/${garnishmentId}`,
      body
    );
  }

  deleteGarnishment(employeeId: string, garnishmentId: string): Observable<ApiResponse<unknown>> {
    return this.http.delete<ApiResponse<unknown>>(
      `${this.payrollUrl}/employees/${employeeId}/garnishments/${garnishmentId}`
    );
  }

  getGarnishmentBrackets(fiscalYear: number): Observable<ApiResponse<PayrollGarnishmentBracket[]>> {
    return this.http.get<ApiResponse<PayrollGarnishmentBracket[]>>(
      `${this.settingsUrl}/parameters/${fiscalYear}/garnishment-brackets`
    );
  }

  updateGarnishmentBrackets(
    fiscalYear: number,
    brackets: PayrollGarnishmentBracket[]
  ): Observable<ApiResponse<unknown>> {
    return this.http.put<ApiResponse<unknown>>(
      `${this.settingsUrl}/parameters/${fiscalYear}/garnishment-brackets`,
      { brackets }
    );
  }

  // ── États de contrôle (livre de paie, journal de paie) ──

  getPayrollBook(
    year: number,
    fromMonth: number,
    toMonth: number,
    includeCalculated = false
  ): Observable<ApiResponse<PayrollBook>> {
    const params = new HttpParams()
      .set('year', year)
      .set('fromMonth', fromMonth)
      .set('toMonth', toMonth)
      .set('includeCalculated', includeCalculated);
    return this.http.get<ApiResponse<PayrollBook>>(`${this.reportsUrl}/payroll-book`, { params });
  }

  exportPayrollBook(
    year: number,
    fromMonth: number,
    toMonth: number,
    includeCalculated: boolean,
    format: PayrollReportExportFormat
  ): Observable<Blob> {
    const params = new HttpParams()
      .set('year', year)
      .set('fromMonth', fromMonth)
      .set('toMonth', toMonth)
      .set('includeCalculated', includeCalculated)
      .set('format', format);
    return this.http.get(`${this.reportsUrl}/payroll-book/export`, { params, responseType: 'blob' });
  }

  getPayrollJournal(
    year: number,
    month: number,
    includeCalculated = false
  ): Observable<ApiResponse<PayrollJournal>> {
    const params = new HttpParams()
      .set('year', year)
      .set('month', month)
      .set('includeCalculated', includeCalculated);
    return this.http.get<ApiResponse<PayrollJournal>>(`${this.reportsUrl}/payroll-journal`, {
      params,
      context: new HttpContext().set(SKIP_ERROR_TOAST, true)
    });
  }

  exportPayrollJournal(
    year: number,
    month: number,
    includeCalculated: boolean,
    format: PayrollReportExportFormat,
    view: PayrollJournalView
  ): Observable<Blob> {
    const params = new HttpParams()
      .set('year', year)
      .set('month', month)
      .set('includeCalculated', includeCalculated)
      .set('format', format)
      .set('view', view);
    return this.http.get(`${this.reportsUrl}/payroll-journal/export`, { params, responseType: 'blob' });
  }

  listTerminationSettlements(year?: number, month?: number): Observable<ApiResponse<TerminationSettlement[]>> {
    let params = new HttpParams();
    if (year) params = params.set('year', year);
    if (month) params = params.set('month', month);
    return this.http.get<ApiResponse<TerminationSettlement[]>>(`${this.payrollUrl}/settlements`, { params });
  }

  previewTerminationSettlement(employeeId: string, terminationDate: string, reason: string): Observable<ApiResponse<TerminationSettlementPreview>> {
    const params = new HttpParams()
      .set('employeeId', employeeId)
      .set('terminationDate', terminationDate)
      .set('reason', reason);
    return this.http.get<ApiResponse<TerminationSettlementPreview>>(`${this.payrollUrl}/settlements/preview`, { params });
  }

  upsertTerminationSettlement(body: UpsertTerminationSettlementRequest): Observable<ApiResponse<string>> {
    return this.http.post<ApiResponse<string>>(`${this.payrollUrl}/settlements`, body);
  }

  approveTerminationSettlement(id: string): Observable<ApiResponse<unknown>> {
    return this.http.post<ApiResponse<unknown>>(`${this.payrollUrl}/settlements/${id}/approve`, {});
  }

  deleteTerminationSettlement(id: string): Observable<ApiResponse<unknown>> {
    return this.http.delete<ApiResponse<unknown>>(`${this.payrollUrl}/settlements/${id}`);
  }

  listAnnualBonusRules(fiscalYear?: number): Observable<ApiResponse<AnnualBonusRule[]>> {
    let params = new HttpParams();
    if (fiscalYear) params = params.set('fiscalYear', fiscalYear);
    return this.http.get<ApiResponse<AnnualBonusRule[]>>(`${this.payrollUrl}/annual-bonuses/rules`, { params });
  }

  createAnnualBonusRule(body: UpsertAnnualBonusRuleRequest): Observable<ApiResponse<string>> {
    return this.http.post<ApiResponse<string>>(`${this.payrollUrl}/annual-bonuses/rules`, body);
  }

  updateAnnualBonusRule(id: string, body: UpsertAnnualBonusRuleRequest): Observable<ApiResponse<unknown>> {
    return this.http.put<ApiResponse<unknown>>(`${this.payrollUrl}/annual-bonuses/rules/${id}`, body);
  }

  deleteAnnualBonusRule(id: string): Observable<ApiResponse<unknown>> {
    return this.http.delete<ApiResponse<unknown>>(`${this.payrollUrl}/annual-bonuses/rules/${id}`);
  }
}
