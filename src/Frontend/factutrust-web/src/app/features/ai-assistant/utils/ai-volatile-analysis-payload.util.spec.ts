import {
  appendVolatileScreenBlock,
  buildVolatileAnalysisJson,
  buildVolatileScreenContextBlock,
  VOLATILE_ANALYSIS_MAX_JSON_CHARS
} from './ai-volatile-analysis-payload.util';

describe('ai-volatile-analysis-payload.util', () => {
  it('buildVolatileAnalysisJson includes schemaVersion and screenId', () => {
    const json = buildVolatileAnalysisJson('accounting-balance', { rows: [] });
    const parsed = JSON.parse(json) as { schemaVersion: string; screenId: string };
    expect(parsed.schemaVersion).toBe('2');
    expect(parsed.screenId).toBe('accounting-balance');
  });

  it('buildVolatileAnalysisJson truncates oversized payloads', () => {
    const huge = 'x'.repeat(VOLATILE_ANALYSIS_MAX_JSON_CHARS + 500);
    const json = buildVolatileAnalysisJson('big', { data: huge });
    expect(json.length).toBeLessThanOrEqual(
      VOLATILE_ANALYSIS_MAX_JSON_CHARS + '... [tronqué côté client]'.length + 1
    );
    expect(json).toContain('[tronqué côté client]');
  });

  it('buildVolatileScreenContextBlock wraps JSON with screen markers', () => {
    const block = buildVolatileScreenContextBlock('ledger', '{"rows":[]}');
    expect(block).toContain('[CONTEXTE ÉCRAN — ledger — non contractuel]');
    expect(block).toContain('[FIN CONTEXTE ÉCRAN]');
  });

  it('appendVolatileScreenBlock skips block when deduplicate is true', () => {
    const result = appendVolatileScreenBlock('Analyse', 'ledger', '{"rows":[]}', true);
    expect(result).toBe('Analyse');
    expect(result).not.toContain('[CONTEXTE ÉCRAN');
  });
});
