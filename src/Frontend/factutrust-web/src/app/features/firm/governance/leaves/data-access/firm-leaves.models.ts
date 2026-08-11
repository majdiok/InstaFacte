export interface FirmLeaveType {
  id: string;
  code: string;
  label: string;
  colorHex: string;
  deductsBalance: boolean;
  requiresApproval: boolean;
  isSystem: boolean;
  isActive: boolean;
  sortOrder: number;
  /** Type de congé de paie créé à l'approbation. Null = aucun effet sur le bulletin. */
  payrollLeaveType?: number | null;
  payrollEffectDisplay?: string;
  /** Décompté du temps de présence productif (dénominateur du taux horaire). */
  countsAsAbsence?: boolean;
}

export interface FirmLeaveSettings {
  id: string;
  year: number;
  defaultAnnualPaidDays: number;
  allowHalfDays: boolean;
  minNoticeDays: number;
  blockOverlap: boolean;
  carryOverEnabled: boolean;
  maxCarryOverDays: number;
}

export interface FirmLeaveBalance {
  id?: string;
  userId: string;
  collaboratorName: string;
  year: number;
  openingBalanceDays: number;
  adjustmentDays: number;
  entitlementDays: number;
  consumedDays: number;
  remainingDays: number;
  notes?: string;
}

export interface FirmLeaveRequest {
  id: string;
  userId: string;
  collaboratorName: string;
  leaveTypeId: string;
  leaveTypeCode: string;
  leaveTypeLabel: string;
  leaveTypeColorHex: string;
  deductsBalance: boolean;
  startDate: string;
  endDate: string;
  startUnit: number;
  endUnit: number;
  days: number;
  reason?: string;
  status: number;
  statusDisplay: string;
  createdAt: string;
  submittedAt?: string;
  processedAt?: string;
  processedByUserId?: string;
  processedByName?: string;
  rejectionReason?: string;
  /** État persistant du report vers la paie interne du cabinet. */
  payrollMirrorState?: number;
  payrollMirrorStateDisplay?: string;
  payrollMirrorMessage?: string;
  payrollMirroredAt?: string;
  /** Issue du report déclenché par l'approbation en cours (absent en lecture). */
  payrollMirror?: FirmLeaveMirrorResult;
}

export interface FirmLeaveMirrorResult {
  state: number;
  stateDisplay: string;
  message?: string;
  payrollLeaveRequestId?: string;
  isApplied: boolean;
}

export interface FirmLeaveReconciliationRow {
  leaveRequestId: string;
  userId: string;
  collaboratorName: string;
  leaveTypeLabel: string;
  startDate: string;
  endDate: string;
  days: number;
  mirrorState: number;
  mirrorStateDisplay: string;
  mirrorMessage?: string;
  mirroredAt?: string;
  /** Faux sur un mois de paie arrêté : seule une régularisation peut le traiter. */
  canReplay: boolean;
}

export interface FirmLeaveReconciliation {
  year: number;
  mirroredCount: number;
  noPayrollEffectCount: number;
  pending: FirmLeaveReconciliationRow[];
  payrollAvailable: boolean;
  unavailableReason?: string;
}

export interface FirmLeaveReplayResult {
  replayed: number;
  succeeded: number;
  messages: string[];
}

/** Voir FirmLeavePayrollMirrorState côté serveur. */
export const FIRM_LEAVE_MIRROR_STATE = {
  notMirrored: 0,
  mirrored: 1,
  noPayrollEffect: 2,
  blockedFrozenPayroll: 3,
  failed: 4,
  revoked: 5
} as const;

export interface FirmLeaveOverview {
  year: number;
  totalPaidBalanceDays: number;
  takenDays: number;
  pendingDays: number;
  absenteeismRatePercent: number;
  pendingRequests: FirmLeaveRequest[];
  topBalances: FirmLeaveBalance[];
  typeSummaries: { leaveTypeId: string; label: string; colorHex: string; takenDays: number; balanceDays: number }[];
}

export interface FirmLeaveCalendarEntry {
  requestId: string;
  userId: string;
  collaboratorName: string;
  qualification?: string;
  leaveTypeId: string;
  leaveTypeLabel: string;
  colorHex: string;
  startDate: string;
  endDate: string;
  startUnit: number;
  endUnit: number;
  days: number;
  status: number;
}

export const FIRM_LEAVE_STATUS = {
  Draft: 0,
  Submitted: 1,
  Approved: 2,
  Rejected: 3,
  Cancelled: 4
} as const;

export const FIRM_LEAVE_DAY_UNITS = [
  { label: 'Journée entière', value: 0 },
  { label: 'Matin', value: 1 },
  { label: 'Après-midi', value: 2 }
];
