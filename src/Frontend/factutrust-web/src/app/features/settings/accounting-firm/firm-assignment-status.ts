import { StatusBadgeStatus } from '@shared/components/status-badge/status-badge.component';

/**
 * Statuts d'une liaison société ↔ cabinet (miroir de l'enum backend
 * FirmAssignmentStatus). Le front n'affiche PAS le `statusDisplay` renvoyé
 * par l'API : il mappe la valeur numérique vers un libellé FR cohérent + un
 * style de badge du design system (StatusBadgeComponent).
 */
export enum FirmAssignmentStatusCode {
  PendingFirmApproval = 0,
  Active = 1,
  Rejected = 2,
  RevokedByCompany = 3,
  RevokedByFirm = 4,
  CancelledByCompany = 5
}

export interface FirmAssignmentStatusView {
  label: string;
  badge: StatusBadgeStatus;
}

const STATUS_VIEWS: Record<number, FirmAssignmentStatusView> = {
  [FirmAssignmentStatusCode.PendingFirmApproval]: { label: "En attente d'acceptation", badge: 'pending' },
  [FirmAssignmentStatusCode.Active]: { label: 'Liaison active', badge: 'active' },
  [FirmAssignmentStatusCode.Rejected]: { label: 'Refusée', badge: 'rejected' },
  [FirmAssignmentStatusCode.RevokedByCompany]: { label: 'Révoquée par la société', badge: 'inactive' },
  [FirmAssignmentStatusCode.RevokedByFirm]: { label: 'Résiliée par le cabinet', badge: 'inactive' },
  [FirmAssignmentStatusCode.CancelledByCompany]: { label: 'Annulée', badge: 'cancelled' }
};

const FALLBACK_VIEW: FirmAssignmentStatusView = { label: 'Inconnu', badge: 'inactive' };

export function firmAssignmentStatusView(status: number): FirmAssignmentStatusView {
  return STATUS_VIEWS[status] ?? FALLBACK_VIEW;
}

/** Une liaison est « ouverte » (bloque une nouvelle demande) si elle est en attente ou active. */
export function isOpenAssignment(status: number): boolean {
  return status === FirmAssignmentStatusCode.PendingFirmApproval || status === FirmAssignmentStatusCode.Active;
}
