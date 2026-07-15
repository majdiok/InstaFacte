import { environment } from '@environments/environment';
import { resolveApiAssetUrl } from './api-asset-url';

describe('resolveApiAssetUrl', () => {
  const originalApiUrl = environment.apiUrl;

  afterEach(() => {
    environment.apiUrl = originalApiUrl;
  });

  it('returns null for empty input', () => {
    expect(resolveApiAssetUrl(null)).toBeNull();
    expect(resolveApiAssetUrl('')).toBeNull();
    expect(resolveApiAssetUrl('   ')).toBeNull();
  });

  it('passes through absolute https and data URLs', () => {
    expect(resolveApiAssetUrl('https://cdn.example.com/a.svg')).toBe('https://cdn.example.com/a.svg');
    expect(resolveApiAssetUrl('data:image/svg+xml;base64,abc')).toBe('data:image/svg+xml;base64,abc');
  });

  it('resolves relative paths using API origin in dev', () => {
    environment.apiUrl = 'https://localhost:7001/api';
    expect(resolveApiAssetUrl('/assets/powerpoint/themes/Vortex/preview-16x9.svg')).toBe(
      'https://localhost:7001/assets/powerpoint/themes/Vortex/preview-16x9.svg'
    );
  });

  it('resolves relative paths without leading slash', () => {
    environment.apiUrl = 'https://localhost:7001/api';
    expect(resolveApiAssetUrl('assets/powerpoint/themes/Pearl/preview-16x9.svg')).toBe(
      'https://localhost:7001/assets/powerpoint/themes/Pearl/preview-16x9.svg'
    );
  });

  it('does not double-prefix already absolute URLs', () => {
    environment.apiUrl = 'https://localhost:7001/api';
    const absolute = 'https://localhost:7001/assets/powerpoint/themes/Vortex/preview-16x9.svg';
    expect(resolveApiAssetUrl(absolute)).toBe(absolute);
  });

  it('uses window.location.origin when apiUrl is relative (prod same-host)', () => {
    environment.apiUrl = '/api';
    expect(resolveApiAssetUrl('/assets/powerpoint/themes/Vortex/preview-16x9.svg')).toBe(
      `${window.location.origin}/assets/powerpoint/themes/Vortex/preview-16x9.svg`
    );
  });
});
