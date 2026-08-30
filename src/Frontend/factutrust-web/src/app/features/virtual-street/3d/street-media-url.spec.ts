import { environment } from '../../../../environments/environment';
import { isTrustedStreetTextureUrl, parseHexColor, resolveStreetMediaUrl } from './street-media-url';

describe('street-media-url', () => {
  it('resolveStreetMediaUrl returns absolute for relative paths using API origin', () => {
    const origin = new URL(environment.apiUrl, window.location.origin).origin;
    expect(resolveStreetMediaUrl('/media/x.png')).toBe(`${origin}/media/x.png`);
    expect(resolveStreetMediaUrl('media/x.png')).toBe(`${origin}/media/x.png`);
  });

  it('resolveStreetMediaUrl passes through https', () => {
    expect(resolveStreetMediaUrl('https://example.com/a.png')).toBe('https://example.com/a.png');
  });

  it('isTrustedStreetTextureUrl allows only API origin', () => {
    const origin = new URL(environment.apiUrl, window.location.origin).origin;
    expect(isTrustedStreetTextureUrl(`${origin}/files/logo.png`)).toBe(true);
    expect(isTrustedStreetTextureUrl('https://evil.example/logo.png')).toBe(false);
    expect(isTrustedStreetTextureUrl('javascript:alert(1)')).toBe(false);
  });

  it('parseHexColor parses #RRGGBB and falls back', () => {
    expect(parseHexColor('#2563eb', 0)).toBe(0x2563eb);
    expect(parseHexColor('bad', 0x111111)).toBe(0x111111);
  });
});
