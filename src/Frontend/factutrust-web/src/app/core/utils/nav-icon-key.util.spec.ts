import { getNavIconKey } from './nav-icon-key.util';

describe('getNavIconKey', () => {
  it('extracts the FA glyph class from a solid icon string', () => {
    expect(getNavIconKey('fa-solid fa-sliders')).toBe('fa-sliders');
    expect(getNavIconKey('fa-solid fa-bag-shopping')).toBe('fa-bag-shopping');
  });

  it('returns null for missing or empty input', () => {
    expect(getNavIconKey(undefined)).toBeNull();
    expect(getNavIconKey(null)).toBeNull();
    expect(getNavIconKey('')).toBeNull();
  });

  it('ignores style prefixes fa-regular and fa-brands', () => {
    expect(getNavIconKey('fa-regular fa-circle-question')).toBe('fa-circle-question');
  });
});
