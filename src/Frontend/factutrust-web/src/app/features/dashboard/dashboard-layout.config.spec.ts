import {
  DEFAULT_DASHBOARD_BLOCK_ORDER,
  DashboardBlockId,
  blockLabel,
  isDashboardBlockId,
  reconcileOrder,
  reorderFullOrder
} from './dashboard-layout.config';

describe('dashboard-layout.config', () => {
  describe('isDashboardBlockId', () => {
    it('accepts known ids and rejects unknown values', () => {
      expect(isDashboardBlockId('kpi')).toBe(true);
      expect(isDashboardBlockId('bottom-grid')).toBe(true);
      expect(isDashboardBlockId('unknown')).toBe(false);
      expect(isDashboardBlockId(42)).toBe(false);
      expect(isDashboardBlockId(null)).toBe(false);
    });
  });

  describe('reconcileOrder', () => {
    it('returns the default order for null/empty input', () => {
      expect(reconcileOrder(null)).toEqual([...DEFAULT_DASHBOARD_BLOCK_ORDER]);
      expect(reconcileOrder([])).toEqual([...DEFAULT_DASHBOARD_BLOCK_ORDER]);
    });

    it('preserves a valid stored order', () => {
      const stored: DashboardBlockId[] = [
        'chart',
        'kpi',
        'urgent',
        'quick-actions',
        'accounting',
        'crm',
        'bottom-grid'
      ];
      expect(reconcileOrder(stored)).toEqual(stored);
    });

    it('appends missing known blocks at their default position', () => {
      // 'urgent' (default index 1) is missing → must be reinserted right after 'kpi'.
      const stored: DashboardBlockId[] = [
        'kpi',
        'quick-actions',
        'accounting',
        'crm',
        'chart',
        'bottom-grid'
      ];
      expect(reconcileOrder(stored)).toEqual([
        'kpi',
        'urgent',
        'quick-actions',
        'accounting',
        'crm',
        'chart',
        'bottom-grid'
      ]);
    });

    it('ignores unknown ids and de-duplicates', () => {
      const result = reconcileOrder(['bottom-grid', 'ghost', 'bottom-grid', 'kpi']);
      expect(result.filter((id) => id === 'bottom-grid').length).toBe(1);
      expect(result).not.toContain('ghost' as DashboardBlockId);
      // Toujours exactement une fois chaque bloc connu.
      expect([...result].sort()).toEqual([...DEFAULT_DASHBOARD_BLOCK_ORDER].sort());
    });
  });

  describe('reorderFullOrder', () => {
    const base: DashboardBlockId[] = [
      'kpi',
      'urgent',
      'quick-actions',
      'accounting',
      'crm',
      'chart',
      'bottom-grid'
    ];

    it('moves a block down to the target position', () => {
      expect(reorderFullOrder(base, 'kpi', 'quick-actions')).toEqual([
        'urgent',
        'quick-actions',
        'kpi',
        'accounting',
        'crm',
        'chart',
        'bottom-grid'
      ]);
    });

    it('moves a block up to the target position', () => {
      expect(reorderFullOrder(base, 'chart', 'urgent')).toEqual([
        'kpi',
        'chart',
        'urgent',
        'quick-actions',
        'accounting',
        'crm',
        'bottom-grid'
      ]);
    });

    it('is a no-op when moved equals target or ids are unknown', () => {
      expect(reorderFullOrder(base, 'kpi', 'kpi')).toEqual(base);
      expect(reorderFullOrder(base, 'kpi', 'ghost' as DashboardBlockId)).toEqual(base);
    });
  });

  describe('blockLabel', () => {
    it('returns a human label for every default block', () => {
      for (const id of DEFAULT_DASHBOARD_BLOCK_ORDER) {
        expect(blockLabel(id).length).toBeGreaterThan(0);
      }
    });
  });
});
