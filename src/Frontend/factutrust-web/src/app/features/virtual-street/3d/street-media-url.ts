import { environment } from '../../../../environments/environment';

/**
 * Resolves storefront media URLs the same way as product images:
 * absolute URLs pass through; relative paths are rooted on the API origin.
 */
export function resolveStreetMediaUrl(url: string | null | undefined): string | null {
  if (url == null || url.trim() === '') return null;
  const t = url.trim();
  if (t.startsWith('data:') || t.startsWith('http://') || t.startsWith('https://')) return t;
  try {
    const origin = resolveApiOrigin();
    if (!origin) return null;
    return origin + (t.startsWith('/') ? t : '/' + t);
  } catch {
    return null;
  }
}

/**
 * Only the API origin may load as WebGL textures (TextureLoader + CORS).
 * Blocks arbitrary third-party URLs to reduce tracking / malicious payload risk.
 */
export function isTrustedStreetTextureUrl(absoluteUrl: string): boolean {
  try {
    const u = new URL(absoluteUrl);
    if (u.protocol !== 'https:' && u.protocol !== 'http:') return false;
    const apiOrigin = resolveApiOrigin();
    if (!apiOrigin) return false;
    return u.origin === apiOrigin;
  } catch {
    return false;
  }
}

function resolveApiOrigin(): string | null {
  const base = typeof window !== 'undefined' ? window.location.origin : undefined;
  try {
    return new URL(environment.apiUrl, base).origin;
  } catch {
    return null;
  }
}

export function parseHexColor(hex: string | null | undefined, fallback: number): number {
  if (!hex || typeof hex !== 'string') return fallback;
  const m = /^#?([0-9a-fA-F]{6})$/.exec(hex.trim());
  if (!m) return fallback;
  const n = parseInt(m[1]!, 16);
  return Number.isFinite(n) ? n : fallback;
}
