/** Legacy keys (pre user-scoping) — cleared on logout and migrated on read. */
export const FT_SELECTED_WAREHOUSE_ID_KEY = 'ft_selected_warehouse_id';
export const FT_SELECTED_WAREHOUSE_TENANT_KEY = 'ft_selected_warehouse_tenant_id';

const FT_WAREHOUSE_SELECTION_KEY = 'ft_warehouse_selection';

export interface WarehouseSelectionPayload {
  userId: string;
  tenantId: string;
  warehouseId: string;
}

export function clearWarehouseStorage(): void {
  localStorage.removeItem(FT_SELECTED_WAREHOUSE_ID_KEY);
  localStorage.removeItem(FT_SELECTED_WAREHOUSE_TENANT_KEY);
  localStorage.removeItem(FT_WAREHOUSE_SELECTION_KEY);
}

/**
 * Reads stored warehouse id when userId and tenantId match the current session.
 * Migrates legacy tenant-only storage once into the new payload.
 */
export function readWarehouseIdForUser(userId: string, tenantId: string): string | null {
  const raw = localStorage.getItem(FT_WAREHOUSE_SELECTION_KEY);
  if (raw) {
    try {
      const parsed = JSON.parse(raw) as Partial<WarehouseSelectionPayload>;
      if (
        parsed.userId === userId &&
        parsed.tenantId === tenantId &&
        typeof parsed.warehouseId === 'string' &&
        parsed.warehouseId.length > 0
      ) {
        return parsed.warehouseId;
      }
      if (parsed.tenantId !== tenantId || parsed.userId !== userId) {
        return null;
      }
    } catch {
      return null;
    }
  }

  // Legacy: tenant-only pair
  const legacyTenant = localStorage.getItem(FT_SELECTED_WAREHOUSE_TENANT_KEY);
  const legacyId = localStorage.getItem(FT_SELECTED_WAREHOUSE_ID_KEY);
  if (legacyId && legacyTenant === tenantId) {
    writeWarehouseIdForUser(userId, tenantId, legacyId);
    localStorage.removeItem(FT_SELECTED_WAREHOUSE_ID_KEY);
    localStorage.removeItem(FT_SELECTED_WAREHOUSE_TENANT_KEY);
    return legacyId;
  }

  return null;
}

export function writeWarehouseIdForUser(userId: string, tenantId: string, warehouseId: string): void {
  const payload: WarehouseSelectionPayload = { userId, tenantId, warehouseId };
  localStorage.setItem(FT_WAREHOUSE_SELECTION_KEY, JSON.stringify(payload));
}
