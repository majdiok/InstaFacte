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
}

export const COMPANY_CHECKLIST_IDS = {
  companyProfile: 'company-profile',
  createClient: 'create-client',
  createProduct: 'create-product',
  createInvoice: 'create-invoice',
  numbering: 'numbering',
  inviteUser: 'invite-user'
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
