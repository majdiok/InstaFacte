import { DashboardBlockId, reconcileOrder } from '../dashboard-layout.config';

/**
 * Cache local de la disposition du tableau de bord, scopé par utilisateur + tenant
 * (calqué sur warehouse-storage.ts). Sert d'affichage instantané et de repli
 * gracieux quand l'API est indisponible. La source de vérité reste le backend.
 */
const FT_DASHBOARD_LAYOUT_KEY = 'ft_dashboard_layout';

interface DashboardLayoutPayload {
  userId: string;
  tenantId: string;
  order: string[];
}

export function clearDashboardLayoutStorage(): void {
  try {
    localStorage.removeItem(FT_DASHBOARD_LAYOUT_KEY);
  } catch {
    /* no-op */
  }
}

/**
 * Lit l'ordre stocké quand `userId` et `tenantId` correspondent à la session courante.
 * Renvoie `null` si rien n'est stocké pour cet utilisateur (le réconciliateur appliquera
 * alors l'ordre par défaut). L'ordre renvoyé est déjà réconcilié.
 */
export function readDashboardLayoutForUser(userId: string, tenantId: string): DashboardBlockId[] | null {
  let raw: string | null = null;
  try {
    raw = localStorage.getItem(FT_DASHBOARD_LAYOUT_KEY);
  } catch {
    return null;
  }
  if (!raw) return null;

  try {
    const parsed = JSON.parse(raw) as Partial<DashboardLayoutPayload>;
    if (parsed.userId !== userId || parsed.tenantId !== tenantId || !Array.isArray(parsed.order)) {
      return null;
    }
    return reconcileOrder(parsed.order);
  } catch {
    return null;
  }
}

export function writeDashboardLayoutForUser(
  userId: string,
  tenantId: string,
  order: readonly DashboardBlockId[]
): void {
  const payload: DashboardLayoutPayload = { userId, tenantId, order: [...order] };
  try {
    localStorage.setItem(FT_DASHBOARD_LAYOUT_KEY, JSON.stringify(payload));
  } catch {
    /* quota dépassé / stockage indisponible — ignoré */
  }
}
