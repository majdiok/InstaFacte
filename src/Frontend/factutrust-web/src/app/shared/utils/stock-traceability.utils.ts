import { StockFeatures } from '@core/services/stock.service';

export const TRACKING_MODE_NONE = 0;
export const TRACKING_MODE_LOT = 1;
export const TRACKING_MODE_SERIAL = 2;

export const PICKING_NONE = 0;
export const PICKING_FEFO = 1;
export const PICKING_FIFO_PHYSICAL = 2;
export const PICKING_MANUAL = 3;

export const ALLOCATION_QTY_TOLERANCE = 0.0001;

export interface AllocationRow {
  lotNumber: string;
  expiryDate: Date | null;
  quantity: number;
  productLotId?: string | null;
  serialNumber?: string | null;
  serialId?: string | null;
  unitCost?: number | null;
}

export function needsManualLotPicker(pickingPolicy: number): boolean {
  return pickingPolicy === PICKING_NONE || pickingPolicy === PICKING_MANUAL;
}

export function showLotSection(
  trackingMode: number,
  features: StockFeatures | null
): boolean {
  return (features?.lotTrackingEnabled ?? false) && trackingMode === TRACKING_MODE_LOT;
}

export function showSerialSection(
  trackingMode: number,
  features: StockFeatures | null
): boolean {
  return (features?.serialTrackingEnabled ?? false) && trackingMode === TRACKING_MODE_SERIAL;
}

export function allocationSum(allocations: AllocationRow[]): number {
  return allocations.reduce((acc, a) => acc + (a.quantity ?? 0), 0);
}

export function allocationsMatchQuantity(
  allocations: AllocationRow[],
  lineQuantity: number
): boolean {
  return Math.abs(allocationSum(allocations) - lineQuantity) <= ALLOCATION_QTY_TOLERANCE;
}

export function exitAllocationsValid(
  trackingMode: number,
  pickingPolicy: number,
  quantity: number,
  allocations: AllocationRow[],
  features: StockFeatures | null
): boolean {
  if (quantity <= 0) return true;
  const lot = showLotSection(trackingMode, features);
  const serial = showSerialSection(trackingMode, features);
  if (!lot && !serial) return true;

  if (serial) {
    if (allocations.length === 0) return true;
    return allocations.length === Math.round(quantity)
      && allocations.every(a => (a.serialId || a.serialNumber?.trim()));
  }

  if (lot) {
    if (needsManualLotPicker(pickingPolicy)) {
      return allocations.length > 0
        && allocationsMatchQuantity(allocations, quantity)
        && allocations.every(a => a.productLotId || a.lotNumber?.trim());
    }
    if (allocations.length > 0) {
      return allocationsMatchQuantity(allocations, quantity);
    }
    return true;
  }

  return true;
}

export function createDefaultLotRow(quantity = 0): AllocationRow {
  return { lotNumber: '', expiryDate: null, quantity };
}

export function createDefaultSerialRow(): AllocationRow {
  return { lotNumber: '', expiryDate: null, quantity: 1, serialNumber: '' };
}

export function buildAllocationPayload(allocations: AllocationRow[]): Array<{
  quantity: number;
  productLotId?: string;
  lotNumber?: string;
  expiryDate?: string;
  serialId?: string;
  serialNumber?: string;
  unitCost?: number;
}> {
  return allocations
    .filter(a => a.quantity > 0)
    .map(a => {
      const row: {
        quantity: number;
        productLotId?: string;
        lotNumber?: string;
        expiryDate?: string;
        serialId?: string;
        serialNumber?: string;
        unitCost?: number;
      } = { quantity: a.quantity };
      if (a.productLotId) row.productLotId = a.productLotId;
      if (a.lotNumber?.trim()) row.lotNumber = a.lotNumber.trim();
      if (a.expiryDate) row.expiryDate = formatLocalDate(a.expiryDate);
      if (a.serialId) row.serialId = a.serialId;
      if (a.serialNumber?.trim()) row.serialNumber = a.serialNumber.trim();
      if (a.unitCost != null) row.unitCost = a.unitCost;
      return row;
    });
}

function formatLocalDate(d: Date): string {
  const y = d.getFullYear();
  const m = String(d.getMonth() + 1).padStart(2, '0');
  const day = String(d.getDate()).padStart(2, '0');
  return `${y}-${m}-${day}`;
}
