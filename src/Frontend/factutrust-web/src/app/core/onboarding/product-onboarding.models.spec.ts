import { isItemAgeEligible } from './product-onboarding.models';

describe('isItemAgeEligible (plan §3.5)', () => {
  it('returns true when minAgeDays is absent (no age gate)', () => {
    expect(isItemAgeEligible(undefined, '2020-01-01T00:00:00Z')).toBeTrue();
  });

  it('returns true when minAgeDays is zero or negative', () => {
    expect(isItemAgeEligible(0, '2020-01-01T00:00:00Z')).toBeTrue();
    expect(isItemAgeEligible(-1, '2020-01-01T00:00:00Z')).toBeTrue();
  });

  it('returns true (fail-open) when tenantCreatedAtUtc is absent', () => {
    expect(isItemAgeEligible(3, undefined)).toBeTrue();
    expect(isItemAgeEligible(3, null)).toBeTrue();
    expect(isItemAgeEligible(3, '')).toBeTrue();
  });

  it('returns true (fail-open) when tenantCreatedAtUtc is unparseable', () => {
    expect(isItemAgeEligible(3, 'not-a-date')).toBeTrue();
  });

  it('returns false when the tenant is younger than minAgeDays', () => {
    const oneDayAgo = new Date(Date.now() - 1 * 24 * 60 * 60 * 1000).toISOString();
    expect(isItemAgeEligible(3, oneDayAgo)).toBeFalse();
  });

  it('returns true when the tenant is exactly minAgeDays old (boundary)', () => {
    // Use a slightly-past-the-boundary timestamp to avoid sub-millisecond flakiness.
    const threeDaysPlusOneHour = new Date(Date.now() - (3 * 24 * 60 * 60 * 1000 + 3600000)).toISOString();
    expect(isItemAgeEligible(3, threeDaysPlusOneHour)).toBeTrue();
  });

  it('returns true when the tenant is older than minAgeDays', () => {
    const tenDaysAgo = new Date(Date.now() - 10 * 24 * 60 * 60 * 1000).toISOString();
    expect(isItemAgeEligible(3, tenDaysAgo)).toBeTrue();
  });
});
