import {
  contractBadgeStatus,
  contractStatusLabel,
  invoiceBadgeStatus,
  runBadgeStatus,
  scheduleBadgeStatus,
  formatContractAmount,
  periodAmountMonthlyEstimate,
  fixedLinesMonthlyEstimate,
  toLinePayloads,
  toUpsertPayload,
  parseUsageRecordsCsv
} from './recurring-contracts.ui-utils';
import { RecurringContractDetail } from '@core/services/recurring-contract.service';

describe('recurring-contracts.ui-utils', () => {
  describe('contractBadgeStatus', () => {
    it('mappe les 5 statuts contrat sur les badges existants', () => {
      expect(contractBadgeStatus('Draft')).toBe('draft');
      expect(contractBadgeStatus('Active')).toBe('active');
      expect(contractBadgeStatus('Suspended')).toBe('pending');
      expect(contractBadgeStatus('Cancelled')).toBe('cancelled');
      expect(contractBadgeStatus('Expired')).toBe('overdue');
    });
  });

  describe('contractStatusLabel', () => {
    it('préfère le statusDisplay de l’API quand il est fourni', () => {
      expect(contractStatusLabel('Active', 'Actif')).toBe('Actif');
      expect(contractStatusLabel('Suspended', 'Suspendu (personnalisé)')).toBe('Suspendu (personnalisé)');
    });

    it('retombe sur les libellés FR par défaut sans statusDisplay', () => {
      expect(contractStatusLabel('Draft')).toBe('Brouillon');
      expect(contractStatusLabel('Active')).toBe('Actif');
      expect(contractStatusLabel('Suspended')).toBe('Suspendu');
      expect(contractStatusLabel('Cancelled')).toBe('Résilié');
      expect(contractStatusLabel('Expired')).toBe('Expiré');
    });
  });

  describe('runBadgeStatus', () => {
    it('mappe les 5 statuts de run', () => {
      expect(runBadgeStatus('Pending')).toBe('pending');
      expect(runBadgeStatus('DraftCreated')).toBe('draft');
      expect(runBadgeStatus('Invoiced')).toBe('paid');
      expect(runBadgeStatus('Failed')).toBe('overdue');
      expect(runBadgeStatus('Skipped')).toBe('inactive');
    });
  });

  describe('scheduleBadgeStatus', () => {
    it('mappe les 6 statuts d’échéance', () => {
      expect(scheduleBadgeStatus('Upcoming')).toBe('pending');
      expect(scheduleBadgeStatus('DraftGenerated')).toBe('draft');
      expect(scheduleBadgeStatus('Invoiced')).toBe('paid');
      expect(scheduleBadgeStatus('Overdue')).toBe('overdue');
      expect(scheduleBadgeStatus('Failed')).toBe('overdue');
      expect(scheduleBadgeStatus('Skipped')).toBe('inactive');
    });
  });

  describe('invoiceBadgeStatus', () => {
    it('accepte les statuts facture connus (insensible à la casse)', () => {
      expect(invoiceBadgeStatus('Paid')).toBe('paid');
      expect(invoiceBadgeStatus('Overdue')).toBe('overdue');
      expect(invoiceBadgeStatus('Sent')).toBe('sent');
    });

    it('rabat un statut inconnu sur pending', () => {
      expect(invoiceBadgeStatus('SomethingElse')).toBe('pending');
      expect(invoiceBadgeStatus('')).toBe('pending');
    });
  });

  describe('formatContractAmount', () => {
    it('formate en fr-TN à 3 décimales avec devise', () => {
      expect(formatContractAmount(1234.5, 'TND')).toContain('TND');
      expect(formatContractAmount(1234.5, 'TND')).toContain('234,500');
    });

    it('affiche un tiret cadratin quand la valeur est absente', () => {
      expect(formatContractAmount(null)).toBe('—');
      expect(formatContractAmount(undefined)).toBe('—');
      expect(formatContractAmount(NaN)).toBe('—');
    });
  });

  describe('periodAmountMonthlyEstimate', () => {
    it('laisse le HT mensuel inchangé', () => {
      expect(periodAmountMonthlyEstimate(300, 'Monthly')).toBe(300);
    });

    it('divise par 3 en trimestriel et par 12 en annuel', () => {
      expect(periodAmountMonthlyEstimate(300, 'Quarterly')).toBe(100);
      expect(periodAmountMonthlyEstimate(1200, 'Annual')).toBe(100);
    });
  });

  describe('fixedLinesMonthlyEstimate', () => {
    it('exclut les lignes clôturées par un avenant (isActive === false)', () => {
      // Après un avenant de prix, l'ancienne ligne (clôturée) et la nouvelle coexistent :
      // l'estimation mensuelle ne doit compter que la ligne active.
      expect(
        fixedLinesMonthlyEstimate([
          { lineType: 'FixedRecurring', quantity: 1, unitPriceHT: 350, isActive: false },
          { lineType: 'FixedRecurring', quantity: 1, unitPriceHT: 400, isActive: true }
        ], 'Monthly')
      ).toBe(400);
    });

    it('inclut les lignes sans champ isActive (lignes de formulaire)', () => {
      expect(
        fixedLinesMonthlyEstimate([
          { lineType: 'FixedRecurring', quantity: 2, unitPriceHT: 100 },
          { lineType: 'UsageMetered', quantity: 1, unitPriceHT: 0 }
        ], 'Monthly')
      ).toBe(200);
    });
  });

  describe('toLinePayloads', () => {
    it('préserve tous les champs d’une ligne à la consommation', () => {
      const payloads = toLinePayloads([
        {
          id: 'l1',
          lineType: 'UsageMetered',
          productId: 'p1',
          description: 'Appels API',
          quantity: 1,
          unitPriceHT: 0,
          vatRate: 19,
          usageMetricId: 'm1',
          includedQuantity: 1000,
          overageUnitPriceHT: 0.05
        }
      ]);
      expect(payloads).toEqual([
        {
          id: 'l1',
          lineType: 'UsageMetered',
          productId: 'p1',
          description: 'Appels API',
          quantity: 1,
          unitPriceHT: 0,
          vatRate: 19,
          usageMetricId: 'm1',
          includedQuantity: 1000,
          overageUnitPriceHT: 0.05,
          sortOrder: 0
        }
      ]);
    });

    it('normalise à null les champs usage* des lignes non UsageMetered', () => {
      const payloads = toLinePayloads([
        {
          lineType: 'FixedRecurring',
          description: 'Maintenance',
          quantity: 1,
          unitPriceHT: 200,
          vatRate: 19,
          usageMetricId: 'm1',
          includedQuantity: 5,
          overageUnitPriceHT: 10
        }
      ]);
      expect(payloads[0].usageMetricId).toBeNull();
      expect(payloads[0].includedQuantity).toBeNull();
      expect(payloads[0].overageUnitPriceHT).toBeNull();
      expect(payloads[0].id).toBeNull();
    });

    it('accepte une description vide ou blanche (trim → chaîne vide)', () => {
      const payloads = toLinePayloads([
        { lineType: 'FixedRecurring', description: '   ', quantity: 1, unitPriceHT: 10, vatRate: 19 },
        { lineType: 'FixedRecurring', description: '', quantity: 1, unitPriceHT: 10, vatRate: 19 }
      ]);
      expect(payloads[0].description).toBe('');
      expect(payloads[1].description).toBe('');
    });

    it('réordonne les lignes séquentiellement', () => {
      const payloads = toLinePayloads([
        { lineType: 'FixedRecurring', description: 'A', quantity: 1, unitPriceHT: 1, vatRate: 19 },
        { lineType: 'OneTimeSetup', description: 'B', quantity: 1, unitPriceHT: 2, vatRate: 19 }
      ]);
      expect(payloads.map(p => p.sortOrder)).toEqual([0, 1]);
    });
  });

  describe('toUpsertPayload', () => {
    const detail: RecurringContractDetail = {
      id: 'c1',
      number: 'CTR-1',
      clientId: 'cli-1',
      clientName: 'Client',
      status: 'Active',
      statusDisplay: 'Actif',
      billingFrequency: 'Quarterly',
      billingFrequencyDisplay: 'Trimestriel',
      billingDayOfMonth: 5,
      startDate: '2026-01-01T00:00:00',
      endDate: '2026-12-31T00:00:00',
      autoRenew: false,
      noticePeriodDays: 60,
      paymentTermTemplateId: 'pt-1',
      currency: 'TND',
      sourceQuoteId: null,
      reference: 'REF',
      notes: 'Notes',
      setupFeeBilled: true,
      lines: []
    };

    it('reprend l’en-tête du détail sans écraser autoRenew/noticePeriodDays (B3)', () => {
      const payload = toUpsertPayload(detail, []);
      expect(payload.clientId).toBe('cli-1');
      expect(payload.billingFrequency).toBe('Quarterly');
      expect(payload.autoRenew).toBeFalse();
      expect(payload.noticePeriodDays).toBe(60);
      expect(payload.paymentTermTemplateId).toBe('pt-1');
      expect(payload.startDate).toBe('2026-01-01');
      expect(payload.endDate).toBe('2026-12-31');
    });

    it('gère un contrat sans date de fin', () => {
      const payload = toUpsertPayload({ ...detail, endDate: null }, []);
      expect(payload.endDate).toBeNull();
    });
  });

  describe('parseUsageRecordsCsv', () => {
    it('parse un CSV valide avec en-tête et dates FR', () => {
      const result = parseUsageRecordsCsv(
        'metricCode;du;au;quantité;notes\nAPI;01/08/2026;31/08/2026;1250,5;Pic estival\nGO;2026-08-01;2026-08-31;42'
      );
      expect(result.error).toBeNull();
      expect(result.rows).toEqual([
        { metricCode: 'API', periodFrom: '2026-08-01', periodTo: '2026-08-31', quantity: 1250.5, notes: 'Pic estival' },
        { metricCode: 'GO', periodFrom: '2026-08-01', periodTo: '2026-08-31', quantity: 42, notes: null }
      ]);
    });

    it('rapporte la ligne fautive sans jeter', () => {
      const result = parseUsageRecordsCsv('API;2026-08-01;2026-08-31;abc');
      expect(result.rows).toEqual([]);
      expect(result.error).toContain('Ligne 1');
      expect(result.error).toContain('quantité invalide');
    });

    it('rejette les dates invalides', () => {
      const result = parseUsageRecordsCsv('API;31/08/26;2026-08-31;10');
      expect(result.error).toContain('dates invalides');
    });

    it('rejette un fichier vide de lignes exploitables', () => {
      const result = parseUsageRecordsCsv('metricCode;du;au;quantité');
      expect(result.error).toContain('aucune ligne exploitable');
    });
  });
});
