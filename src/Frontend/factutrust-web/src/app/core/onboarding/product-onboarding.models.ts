export type ProductOnboardingStatus =
  | 'NotStarted'
  | 'InProgress'
  | 'Completed'
  | 'Dismissed';

export interface ProductOnboardingChecklist {
  dismissed: boolean;
  doneIds: string[];
}

export interface ProductOnboardingState {
  enabled: boolean;
  status: ProductOnboardingStatus;
  version: number;
  checklist: ProductOnboardingChecklist;
  autoCompletedIds: string[];
}

export interface PatchProductOnboardingRequest {
  status?: ProductOnboardingStatus;
  checklistDoneId?: string;
  checklistDismissed?: boolean;
}

export interface ProductTourStepDef {
  id: string;
  title?: string;
  description: string;
  /** CSS selector. Omitted = centered welcome popover. */
  selector?: string;
  /** Open this sidebar section (tourId) while the step is visible. */
  expandNavTourId?: string;
  /** Include in the short mobile catalogue. */
  compact?: boolean;
}

export interface OnboardingChecklistItemDef {
  id: string;
  label: string;
  description: string;
  route: string;
  permission?: string;
  adminOnly?: boolean;
  managerOnly?: boolean;
  /** AppModule enum ids (plan WP-F4). All listed modules must be enabled for the item to show. */
  modules?: number[];
  /** Company segment kebab codes (plan WP-F4). Item shows only for these segments. */
  segments?: string[];
  /**
   * Phase 3 (plan §3.5) — minimum tenant age in days before the item appears. Used for
   * "progressive profiling" nudges that should not surface on day 0. Defensive: when the
   * tenant creation date is absent/unparseable the item is shown (fail-open = pre-Phase-3
   * behavior — no new hiding from an unknown date).
   */
  minAgeDays?: number;
}

export const COMPANY_CHECKLIST_IDS = {
  companyProfile: 'company-profile',
  createClient: 'create-client',
  createProduct: 'create-product',
  createInvoice: 'create-invoice',
  numbering: 'numbering',
  inviteUser: 'invite-user',
  checkDefaultWarehouse: 'check-default-warehouse',
  commerceStockReceipt: 'commerce-stock-receipt',
  btpFirstProject: 'btp-first-project',
  recurringContractSetup: 'recurring-contract-setup',
  /** Phase 3 (plan §3.5) — progressive profiling nudge (minAgeDays-gated). */
  completeCompanyProfile: 'complete-company-profile'
} as const;

export const FIRM_CHECKLIST_IDS = {
  firmSettings: 'firm-settings',
  addCollaborator: 'add-collaborator',
  clientDossier: 'client-dossier',
  fiscalSchedule: 'fiscal-schedule',
  chefDeMission: 'chef-de-mission'
} as const;

export function normalizeOnboardingStatus(raw: unknown): ProductOnboardingStatus {
  if (typeof raw === 'number') {
    return (['NotStarted', 'InProgress', 'Completed', 'Dismissed'][raw] ?? 'Completed') as ProductOnboardingStatus;
  }
  if (typeof raw === 'string') {
    const lower = raw.trim().toLowerCase();
    if (lower === 'notstarted') return 'NotStarted';
    if (lower === 'inprogress') return 'InProgress';
    if (lower === 'completed') return 'Completed';
    if (lower === 'dismissed') return 'Dismissed';
  }
  return 'Completed';
}

export function shouldAutoStartTour(status: ProductOnboardingStatus | undefined): boolean {
  return status === 'NotStarted' || status === 'InProgress';
}

export interface ProductTourStartContext {
  uiEnabled: boolean;
  isReplay: boolean;
  status: ProductOnboardingStatus | undefined;
  hideLayout: boolean;
  isDelegated: boolean;
  isOnboardingRoute: boolean;
  hasBlockingModal: boolean;
  hasNavItems: boolean;
}

export function canStartProductTour(ctx: ProductTourStartContext): boolean {
  if (!ctx.uiEnabled) {
    return false;
  }
  if (ctx.hideLayout || ctx.isDelegated || !ctx.isOnboardingRoute) {
    return false;
  }
  if (ctx.hasBlockingModal || !ctx.hasNavItems) {
    return false;
  }
  return ctx.isReplay || shouldAutoStartTour(ctx.status);
}

export function hasBlockingModalOpen(): boolean {
  if (typeof document === 'undefined') {
    return false;
  }
  return !!document.querySelector(
    '.p-dialog-mask, .p-dialog-visible, ngb-modal-window, .modal.show'
  );
}

/**
 * Phase 3 (plan §3.5) — age gate for "progressive profiling" checklist items. Returns `true`
 * (eligible/visible) when:
 * - `minAgeDays` is absent/≤ 0 (no age gate — always shown), OR
 * - the tenant is at least `minAgeDays` days old.
 *
 * **Fail-open**: when `tenantCreatedAtUtc` is absent or unparseable, returns `true` — matching
 * the pre-Phase-3 behavior (an unknown creation date must not newly hide items).
 */
export function isItemAgeEligible(
  minAgeDays: number | undefined,
  tenantCreatedAtUtc: string | null | undefined
): boolean {
  if (!minAgeDays || minAgeDays <= 0) return true;
  if (!tenantCreatedAtUtc) return true;

  const created = new Date(tenantCreatedAtUtc);
  if (Number.isNaN(created.getTime())) return true;

  const ageMs = Date.now() - created.getTime();
  return ageMs >= minAgeDays * 24 * 60 * 60 * 1000;
}
