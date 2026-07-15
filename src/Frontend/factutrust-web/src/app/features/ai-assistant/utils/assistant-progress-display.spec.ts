import { dedupeToolSources, getAiToolDisplayLabel } from './assistant-progress-display';

describe('assistant-progress-display', () => {
  describe('getAiToolDisplayLabel', () => {
    it('labels forecasting tools in French', () => {
      expect(getAiToolDisplayLabel('forecast_revenue')).toBe("Prévision du chiffre d'affaires");
      expect(getAiToolDisplayLabel('get_tunisian_commercial_calendar')).toBe('Calendrier commercial tunisien');
      expect(getAiToolDisplayLabel('get_replenishment_recommendations')).toBe('Recommandations de réapprovisionnement');
      expect(getAiToolDisplayLabel('analyze_seasonal_impact')).toBe("Analyse de l'impact saisonnier");
    });

    it('never returns raw snake_case for unknown tools (prettified fallback)', () => {
      const label = getAiToolDisplayLabel('get_super_magic_report');
      expect(label).not.toContain('_');
      expect(label.charAt(0)).toBe(label.charAt(0).toUpperCase());
      expect(label).toBe('Super magic report');
    });
  });

  describe('dedupeToolSources', () => {
    it('aggregates duplicate tools with a count, preserving order', () => {
      const deduped = dedupeToolSources([
        { toolName: 'forecast_product_demand' },
        { toolName: 'forecast_revenue' },
        { toolName: 'forecast_revenue' },
        { toolName: 'forecast_revenue' }
      ]);
      expect(deduped.length).toBe(2);
      expect(deduped[0].toolName).toBe('forecast_product_demand');
      expect(deduped[0].count).toBe(1);
      expect(deduped[1].toolName).toBe('forecast_revenue');
      expect(deduped[1].count).toBe(3);
      expect(deduped[1].label).toBe("Prévision du chiffre d'affaires");
    });

    it('handles null/empty input', () => {
      expect(dedupeToolSources(null)).toEqual([]);
      expect(dedupeToolSources([])).toEqual([]);
    });
  });
});
