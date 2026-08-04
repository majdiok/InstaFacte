import { BillableDossier } from '../services/honoraires.service';

/**
 * Resolves a billable dossier by assignmentId from the active catalogue,
 * falling back to a document snapshot when the dossier is archived or absent.
 */
export function resolveDossier(
  dossiers: BillableDossier[],
  assignmentId: string | null | undefined,
  fallback?: BillableDossier | null
): BillableDossier | null {
  if (!assignmentId) {
    return fallback ?? null;
  }
  return dossiers.find(d => d.assignmentId === assignmentId) ?? fallback ?? null;
}

/** Builds a minimal BillableDossier snapshot from document client fields. */
export function toClientSnapshot(params: {
  assignmentId: string;
  companyName: string;
  nif?: string | null;
  address?: string | null;
  contactEmail?: string | null;
  contactPhone?: string | null;
}): BillableDossier {
  return {
    assignmentId: params.assignmentId,
    companyTenantId: '',
    companyName: params.companyName,
    nif: params.nif || undefined,
    address: params.address || undefined,
    contactEmail: params.contactEmail || undefined,
    contactPhone: params.contactPhone || undefined
  };
}
