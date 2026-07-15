import {
  parseStorefrontWorkflowStatus,
  STOREFRONT_WORKFLOW_STATUS_VALUES,
  workflowStatusLabel,
  workflowStatusTagLabel,
  type StorefrontWorkflowStatus
} from './storefront-workflow-status';

describe('storefront-workflow-status', () => {
  describe('parseStorefrontWorkflowStatus', () => {
    const validCases: ReadonlyArray<{ raw: unknown; expected: StorefrontWorkflowStatus }> = [
      { raw: 'draft', expected: 'draft' },
      { raw: 'pendingReview', expected: 'pendingReview' },
      { raw: 'published', expected: 'published' },
      { raw: 'suspended', expected: 'suspended' },
      { raw: 'Draft', expected: 'draft' },
      { raw: 'PendingReview', expected: 'pendingReview' },
      { raw: 'Published', expected: 'published' },
      { raw: 'Suspended', expected: 'suspended' },
      { raw: '  published  ', expected: 'published' },
      { raw: 0, expected: 'draft' },
      { raw: 1, expected: 'pendingReview' },
      { raw: 2, expected: 'published' },
      { raw: 3, expected: 'suspended' }
    ];

    for (const { raw, expected } of validCases) {
      it(`accepts ${JSON.stringify(raw)} → ${expected}`, () => {
        const r = parseStorefrontWorkflowStatus(raw);
        expect(r.ok).toBe(true);
        if (r.ok) expect(r.value).toBe(expected);
      });
    }

    const invalidRaws: unknown[] = [
      'PENDINGREVIEW',
      '',
      '99',
      99,
      3.5,
      -1,
      null,
      undefined,
      {},
      [],
      true
    ];

    for (const raw of invalidRaws) {
      it(`rejects invalid raw ${JSON.stringify(raw)}`, () => {
        expect(parseStorefrontWorkflowStatus(raw).ok).toBe(false);
      });
    }
  });

  it('exposes four canonical workflow values', () => {
    expect(STOREFRONT_WORKFLOW_STATUS_VALUES).toEqual([
      'draft',
      'pendingReview',
      'published',
      'suspended'
    ]);
  });

  it('workflowStatusLabel uses French tenant labels', () => {
    expect(workflowStatusLabel('draft')).toBe('Brouillon');
    expect(workflowStatusLabel('pendingReview')).toBe('En attente de validation');
    expect(workflowStatusLabel('published')).toBe('Publiée');
    expect(workflowStatusLabel('suspended')).toBe('Suspendue');
  });

  it('workflowStatusTagLabel shortens pending for table tags', () => {
    expect(workflowStatusTagLabel('pendingReview')).toBe('En attente');
    expect(workflowStatusTagLabel('draft')).toBe('Brouillon');
  });
});
