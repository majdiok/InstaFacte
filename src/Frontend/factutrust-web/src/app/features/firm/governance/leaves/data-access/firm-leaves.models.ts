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
}

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
