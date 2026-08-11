import { buildFallbackCorrectionLink } from './audit-correction.navigation';

describe('audit-correction.navigation', () => {
  it('builds drafts link with status filter', () => {
    const link = buildFallbackCorrectionLink(
      {
        id: '1',
        ruleCode: 'drafts',
        deepLinkRoute: '/accounting/journal',
        periodFrom: '2026-01-01',
        periodTo: '2026-12-31'
      },
      2026
    );
    expect(link?.route).toBe('/accounting/entry-search');
    expect(link?.queryParams['status']).toBe('0');
    expect(link?.queryParams['autoSearch']).toBe('1');
  });

  it('builds unlettered link with account and autoLoad', () => {
    const link = buildFallbackCorrectionLink(
      {
        id: '2',
        ruleCode: 'unlettered',
        deepLinkRoute: '/accounting/lettering',
        accountRef: '4111/4011'
      },
      2026
    );
    expect(link?.route).toBe('/accounting/lettering');
    expect(link?.queryParams['account']).toBe('4111');
    expect(link?.queryParams['autoLoad']).toBe('1');
  });
});
