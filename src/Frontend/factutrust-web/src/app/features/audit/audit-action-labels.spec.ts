import {
  auditActionLabel,
  auditEntityTypeLabel,
  auditActionSelectOptions,
  auditEntityTypeSelectOptions
} from './audit-action-labels';

describe('audit-action-labels', () => {
  it('maps known invoice action to French', () => {
    expect(auditActionLabel('Invoice.Paid')).toBe('Facture payée');
  });

  it('falls back to raw action when unknown', () => {
    expect(auditActionLabel('Custom.Unknown')).toBe('Custom.Unknown');
  });

  it('maps entity type to French', () => {
    expect(auditEntityTypeLabel('Invoice')).toBe('Facture');
  });

  it('exposes non-empty select options', () => {
    expect(auditActionSelectOptions().length).toBeGreaterThan(10);
    expect(auditEntityTypeSelectOptions().length).toBeGreaterThan(5);
  });

  it('action filter options use French label only; value stays technical for API', () => {
    const paid = auditActionSelectOptions().find(o => o.value === 'Invoice.Paid');
    expect(paid).toBeDefined();
    expect(paid!.label).toBe('Facture payée');
    expect(paid!.label).not.toContain('Invoice.Paid');
  });
});
