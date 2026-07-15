import {
  buildNoDataPayload,
  buildScreenAnalysisPayloadV2,
  computeYoYVariation,
  sampleRowsSmart,
  wrapLegacyAnalyzePayload
} from './ai-screen-payload.factory';
import { VOLATILE_ANALYSIS_MAX_JSON_CHARS, buildVolatileAnalysisJson } from './ai-volatile-analysis-payload.util';
import { resolveScreenAnalysisBackendPrompt } from './ai-screen-analysis-prompts';

describe('ai-screen-payload.factory', () => {
  it('buildScreenAnalysisPayloadV2 includes required v2 fields', () => {
    const payload = buildScreenAnalysisPayloadV2({
      screenId: 'accounting-ledger',
      filters: { from: '2026-01-01' },
      summary: { totalRows: 2 },
      rows: [{ id: 1 }, { id: 2 }],
      sampling: { strategy: 'all', totalAvailable: 2, included: 2, truncated: false },
      dataQuality: { hasData: true, isPartial: false, warnings: [] }
    });

    expect(payload.schemaVersion).toBe('2');
    expect(payload.screenId).toBe('accounting-ledger');
    expect(payload.capturedAt).toBeTruthy();
    expect(payload.summary['totalRows']).toBe(2);
    expect(payload.dataQuality.hasData).toBe(true);
  });

  it('buildNoDataPayload marks hasData false', () => {
    const payload = buildNoDataPayload('accounting-income-statement');
    expect(payload.dataQuality.hasData).toBe(false);
    expect(payload.dataQuality.warnings.length).toBeGreaterThan(0);
  });

  it('sampleRowsSmart truncates large datasets', () => {
    const rows = Array.from({ length: 300 }, (_, i) => ({ amount: i }));
    const { rows: sampled, sampling } = sampleRowsSmart(rows, 200, 'recent', r => r.amount);
    expect(sampled.length).toBe(200);
    expect(sampling.truncated).toBe(true);
  });

  it('computeYoYVariation returns null when previous is zero', () => {
    expect(computeYoYVariation(100, 0)).toBeNull();
  });

  it('wrapLegacyAnalyzePayload converts legacy shape', () => {
    const payload = wrapLegacyAnalyzePayload('accounting-balance', {
      screen: 'accounting-balance',
      filters: { from: '2026-01-01' },
      summary: { closingDebit: 100 },
      rows: [{ accountNumber: '411' }]
    });
    expect(payload.schemaVersion).toBe('2');
    expect(payload.summary['closingDebit']).toBe(100);
    expect(payload.rows?.length).toBe(1);
  });
});

describe('ai-volatile-analysis-payload.util', () => {
  it('buildVolatileAnalysisJson respects max size', () => {
    const huge = { data: 'x'.repeat(VOLATILE_ANALYSIS_MAX_JSON_CHARS + 1000) };
    const json = buildVolatileAnalysisJson('dashboard', huge);
    expect(json.length).toBeLessThanOrEqual(VOLATILE_ANALYSIS_MAX_JSON_CHARS + 30);
    expect(json).toContain('tronqué côté client');
  });
});

describe('ai-screen-analysis-prompts', () => {
  it('resolveScreenAnalysisBackendPrompt returns specialized prompt for ledger', () => {
    const prompt = resolveScreenAnalysisBackendPrompt('accounting-ledger');
    expect(prompt).toContain('grand livre');
    expect(prompt).toContain('Synthèse exécutive');
  });

  it('resolveScreenAnalysisBackendPrompt falls back for unknown screen', () => {
    const prompt = resolveScreenAnalysisBackendPrompt('unknown-screen');
    expect(prompt).toContain('Analyse les données');
  });
});
