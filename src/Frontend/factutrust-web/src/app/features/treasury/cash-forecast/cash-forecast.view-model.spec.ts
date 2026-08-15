import {
  bucketLabel,
  buildAnalyzePayload,
  drivers,
  formatAmount,
  formatCompactAmount,
  monthlyTotals,
  orderedAlerts,
  orderedScenarios,
  positionZoneLabel,
  realisticScenario,
  recommendations,
  resolvePositionZone,
  scenarioLabel,
  severityTone,
  sourceLabel
} from './cash-forecast.view-model';
import {
  CashFlowBucket,
  CashFlowForecast,
  CashFlowInsight,
  CashFlowThresholds
} from '../models/cash-forecast.models';

function bucket(over: Partial<CashFlowBucket> = {}): CashFlowBucket {
  return {
    sequenceIndex: 0,
    periodStart: '2026-06-01',
    periodEnd: '2026-06-30',
    openingBalance: 100,
    inflows: 50,
    outflows: 20,
    netFlow: 30,
    closingBalance: 130,
    lowClosingBalance: 120,
    highClosingBalance: 140,
    ...over
  };
}

function insight(over: Partial<CashFlowInsight> = {}): CashFlowInsight {
  return {
    kind: 'alert',
    severity: 'info',
    origin: 'rule',
    title: 'Titre',
    ...over
  };
}

function forecast(over: Partial<CashFlowForecast> = {}): CashFlowForecast {
  return {
    runId: 'run-1',
    periodStart: '2026-06-01',
    periodEnd: '2026-11-30',
    horizonMonths: 6,
    currency: 'TND',
    openingBalance: 87540.25,
    closingBalance: 112430.78,
    totalInflows: 1245780.5,
    totalOutflows: 1220890,
    netFlow: 24890.5,
    confidencePercent: 87,
    computedAt: '2026-06-01T08:00:00Z',
    durationMs: 120,
    aiAdjustmentApplied: false,
    buckets: [],
    scenarios: [],
    insights: [],
    upcomingInflows: [],
    thresholds: { criticalThreshold: -20000, alertThreshold: 20000, comfortThreshold: 100000 },
    ...over
  };
}

/**
 * `Intl.NumberFormat('fr-TN')` sépare les milliers par une espace insécable, dont le point de code
 * varie selon la version d'ICU du navigateur. Les assertions portent donc sur les chiffres, jamais
 * sur le caractère de séparation.
 */
function normalizeSpaces(value: string): string {
  return value.replace(/\s/g, ' ');
}

describe('cash-forecast view-model — formatage', () => {
  it('formate un montant au millime avec sa devise', () => {
    expect(normalizeSpaces(formatAmount(87540.25))).toBe('87 540,250 TND');
  });

  it('rend un tiret pour une valeur absente', () => {
    expect(formatAmount(null)).toBe('—');
    expect(formatAmount(undefined)).toBe('—');
    expect(formatAmount(Number.NaN)).toBe('—');
  });

  it('formate un montant négatif sans le masquer', () => {
    expect(normalizeSpaces(formatAmount(-12230))).toContain('12 230,000');
    expect(formatAmount(-12230).startsWith('-')).toBeTrue();
  });

  it('abrège les montants pour les axes du graphique', () => {
    expect(formatCompactAmount(12500)).toBe('12,5K');
    expect(formatCompactAmount(1_250_000)).toBe('1,3M');
    expect(formatCompactAmount(850)).toBe('850');
    expect(formatCompactAmount(-12500)).toBe('-12,5K');
  });
});

describe('cash-forecast view-model — jauge de position', () => {
  const thresholds: CashFlowThresholds = {
    criticalThreshold: 0,
    alertThreshold: 20000,
    comfortThreshold: 100000
  };

  it('classe un solde négatif en critique', () => {
    expect(resolvePositionZone(-5000, thresholds)).toBe('critical');
  });

  it('classe un solde exactement au seuil critique en critique', () => {
    // Convention explicite : un solde posé sur le seuil ne doit pas basculer au gré des arrondis.
    expect(resolvePositionZone(0, thresholds)).toBe('critical');
  });

  it('classe un solde intermédiaire en vigilance', () => {
    expect(resolvePositionZone(50000, thresholds)).toBe('alert');
  });

  it('classe un solde au seuil de confort en confortable', () => {
    expect(resolvePositionZone(100000, thresholds)).toBe('comfort');
  });

  it('nomme les zones en français', () => {
    expect(positionZoneLabel('critical')).toBe('Critique');
    expect(positionZoneLabel('alert')).toBe('Vigilance');
    expect(positionZoneLabel('comfort')).toBe('Confortable');
  });
});

describe('cash-forecast view-model — scénarios et analyses', () => {
  it('ordonne les scénarios optimiste, réaliste, pessimiste', () => {
    const f = forecast({
      scenarios: [
        { kind: 'pessimistic', closingBalance: 1, netFlow: 1, probabilityPercent: 20, deterministicProbabilityPercent: 20, probabilitySource: 'deterministic' },
        { kind: 'realistic', closingBalance: 2, netFlow: 2, probabilityPercent: 50, deterministicProbabilityPercent: 50, probabilitySource: 'deterministic' },
        { kind: 'optimistic', closingBalance: 3, netFlow: 3, probabilityPercent: 30, deterministicProbabilityPercent: 30, probabilitySource: 'deterministic' }
      ]
    });

    expect(orderedScenarios(f).map(s => s.kind)).toEqual(['optimistic', 'realistic', 'pessimistic']);
  });

  it('retourne le scénario réaliste comme référence', () => {
    const f = forecast({
      scenarios: [
        { kind: 'realistic', closingBalance: 42, netFlow: 2, probabilityPercent: 50, deterministicProbabilityPercent: 50, probabilitySource: 'deterministic' }
      ]
    });

    expect(realisticScenario(f)?.closingBalance).toBe(42);
    expect(realisticScenario(null)).toBeNull();
  });

  it('nomme les scénarios en français', () => {
    expect(scenarioLabel('optimistic')).toBe('Scénario optimiste');
    expect(scenarioLabel('realistic')).toBe('Scénario réaliste');
    expect(scenarioLabel('pessimistic')).toBe('Scénario pessimiste');
  });

  it('trie les alertes des plus graves aux moins graves', () => {
    const f = forecast({
      insights: [
        insight({ severity: 'info', title: 'Info' }),
        insight({ severity: 'critical', title: 'Critique' }),
        insight({ severity: 'warning', title: 'Alerte' })
      ]
    });

    expect(orderedAlerts(f).map(a => a.title)).toEqual(['Critique', 'Alerte', 'Info']);
  });

  it('sépare alertes, facteurs et recommandations', () => {
    const f = forecast({
      insights: [
        insight({ kind: 'alert', title: 'A' }),
        insight({ kind: 'driver', title: 'D' }),
        insight({ kind: 'recommendation', title: 'R' })
      ]
    });

    expect(orderedAlerts(f).map(i => i.title)).toEqual(['A']);
    expect(drivers(f).map(i => i.title)).toEqual(['D']);
    expect(recommendations(f).map(i => i.title)).toEqual(['R']);
  });

  it('associe une sévérité à un ton PrimeNG', () => {
    expect(severityTone('critical')).toBe('danger');
    expect(severityTone('warning')).toBe('warn');
    expect(severityTone('info')).toBe('info');
  });

  it('ne casse pas sur une projection absente', () => {
    expect(orderedScenarios(null)).toEqual([]);
    expect(orderedAlerts(null)).toEqual([]);
    expect(drivers(null)).toEqual([]);
  });
});

describe('cash-forecast view-model — tableau mensuel', () => {
  it('additionne les colonnes du tableau', () => {
    const totals = monthlyTotals([
      bucket({ inflows: 100, outflows: 40, netFlow: 60 }),
      bucket({ sequenceIndex: 1, inflows: 200, outflows: 250, netFlow: -50 })
    ]);

    expect(totals.inflows).toBe(300);
    expect(totals.outflows).toBe(290);
    expect(totals.netFlow).toBe(10);
  });

  it('retourne des totaux nuls sans aucun mois', () => {
    expect(monthlyTotals([])).toEqual({ inflows: 0, outflows: 0, netFlow: 0 });
  });

  it('étiquette un mois avec une majuscule initiale', () => {
    const label = bucketLabel(bucket({ periodStart: '2026-06-01' }));
    expect(label.charAt(0)).toBe(label.charAt(0).toUpperCase());
    expect(label).toContain('2026');
  });
});

describe('cash-forecast view-model — libellés de source', () => {
  it('traduit les sources métier', () => {
    expect(sourceLabel('client_invoice')).toBe('Facture client');
    expect(sourceLabel('fiscal_obligation')).toBe('Échéance fiscale');
    expect(sourceLabel('payroll')).toBe('Salaires');
  });
});

describe('cash-forecast view-model — charge utile IA', () => {
  it('décrit ce que montre l’écran', () => {
    const f = forecast({ buckets: [bucket()] });
    const payload = buildAnalyzePayload(f) as Record<string, unknown>;

    expect(payload['screen']).toBe('treasury-cash-forecast');
    expect(payload['soldeCloture']).toBe(112430.78);
    expect(payload['indiceConfiance']).toBe(87);
    expect((payload['buckets'] as unknown[]).length).toBe(1);
  });

  it('reste exploitable sans projection', () => {
    const payload = buildAnalyzePayload(null) as Record<string, unknown>;

    expect(payload['screen']).toBe('treasury-cash-forecast');
    expect(payload['empty']).toBeTrue();
  });
});
