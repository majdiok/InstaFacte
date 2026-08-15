// Types miroir des DTO C# de
// FactuTrust.Application.Features.Treasury.Dtos.CashFlowForecastDtos.
//
// Les énumérations voyagent en snake_case : le backend les sérialise déjà ainsi via
// CashFlowForecastMappings, il n'y a donc aucune normalisation à faire côté client — contrairement
// au module Prévisions IA, où les enums PascalCase imposent un point de conversion dans le service.

export type CashFlowDirection = 'inflow' | 'outflow';

export type CashFlowScenarioKind = 'optimistic' | 'realistic' | 'pessimistic';

export type CashFlowProbabilitySource = 'deterministic' | 'ai_adjusted';

export type CashFlowInsightKind = 'alert' | 'driver' | 'recommendation';

export type CashFlowInsightSeverity = 'info' | 'warning' | 'critical';

export type CashFlowInsightOrigin = 'rule' | 'ai';

export type CashFlowImpactLevel = 'low' | 'medium' | 'high';

export type CashCommitmentFrequency =
  | 'weekly'
  | 'monthly'
  | 'quarterly'
  | 'semi_annual'
  | 'annual';

export type CashFlowSourceType =
  | 'client_invoice'
  | 'client_effet'
  | 'sales_order_backlog'
  | 'supplier_invoice'
  | 'supplier_effet'
  | 'purchase_order_commitment'
  | 'payroll'
  | 'payroll_contribution'
  | 'fiscal_obligation'
  | 'loan_installment'
  | 'recurring_commitment'
  | 'recurring_journal_template'
  | 'manual';

export interface CashFlowBucket {
  sequenceIndex: number;
  periodStart: string;
  periodEnd: string;
  openingBalance: number;
  inflows: number;
  outflows: number;
  netFlow: number;
  closingBalance: number;
  lowClosingBalance: number;
  highClosingBalance: number;
}

export interface CashFlowScenario {
  kind: CashFlowScenarioKind;
  closingBalance: number;
  netFlow: number;
  probabilityPercent: number;
  /** Valeur produite par le moteur seul, affichée en infobulle quand l'IA a pondéré. */
  deterministicProbabilityPercent: number;
  probabilitySource: CashFlowProbabilitySource;
  aiRationale?: string | null;
}

export interface CashFlowInsight {
  kind: CashFlowInsightKind;
  severity: CashFlowInsightSeverity;
  origin: CashFlowInsightOrigin;
  impact?: CashFlowImpactLevel | null;
  impactDirection?: CashFlowDirection | null;
  title: string;
  detail?: string | null;
  periodStart?: string | null;
  estimatedBalance?: number | null;
}

export interface CashFlowLine {
  id: string;
  direction: CashFlowDirection;
  sourceType: CashFlowSourceType;
  sourceId?: string | null;
  sourceReference?: string | null;
  label: string;
  thirdPartyName?: string | null;
  contractualDate: string;
  expectedDate: string;
  amount: number;
  probabilityPercent: number;
  weightedAmount: number;
  isConfirmed: boolean;
}

export interface CashFlowThresholds {
  criticalThreshold: number;
  alertThreshold: number;
  comfortThreshold: number;
  payrollPaymentDayOfMonth?: number | null;
}

export interface CashFlowForecast {
  runId: string;
  periodStart: string;
  periodEnd: string;
  horizonMonths: number;
  currency: string;
  openingBalance: number;
  closingBalance: number;
  totalInflows: number;
  totalOutflows: number;
  netFlow: number;
  confidencePercent: number;
  computedAt: string;
  durationMs: number;
  aiAdjustmentApplied: boolean;
  aiModelRef?: string | null;
  buckets: CashFlowBucket[];
  scenarios: CashFlowScenario[];
  insights: CashFlowInsight[];
  upcomingInflows: CashFlowLine[];
  thresholds: CashFlowThresholds;
}

export interface RecurringCashCommitment {
  id: string;
  label: string;
  direction: CashFlowDirection;
  amount: number;
  currency: string;
  frequency: CashCommitmentFrequency;
  dayOfMonth: number;
  startDate: string;
  endDate?: string | null;
  category?: string | null;
  notes?: string | null;
  isActive: boolean;
}

export interface SaveRecurringCashCommitmentRequest {
  label: string;
  direction: CashFlowDirection;
  amount: number;
  frequency: CashCommitmentFrequency;
  dayOfMonth: number;
  startDate: string;
  endDate?: string | null;
  category?: string | null;
  notes?: string | null;
}

export interface SaveCashFlowThresholdsRequest {
  criticalThreshold: number;
  alertThreshold: number;
  comfortThreshold: number;
  payrollPaymentDayOfMonth?: number | null;
}

/** Position de la jauge « Position de trésorerie » de l'écran. */
export type CashPositionZone = 'critical' | 'alert' | 'comfort';
