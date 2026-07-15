import { environment } from '@environments/environment';

/**
 * Resolves relative API static asset paths to absolute URLs.
 * Dev: prefixes API origin (e.g. https://localhost:7001).
 * Prod: same-host reverse proxy via window.location.origin when apiUrl is relative.
 */
export function resolveApiAssetUrl(url: string | null | undefined): string | null {
  if (url == null || url.trim() === '') {
    return null;
  }

  const trimmed = url.trim();
  if (trimmed.startsWith('data:') || /^https?:\/\//i.test(trimmed)) {
    return trimmed;
  }

  const path = trimmed.startsWith('/') ? trimmed : `/${trimmed}`;
  const origin = resolveApiOrigin();
  if (!origin) {
    return path;
  }

  return `${origin}${path}`;
}

function resolveApiOrigin(): string | null {
  const apiUrl = environment.apiUrl?.trim() ?? '';
  if (!apiUrl) {
    return typeof window !== 'undefined' ? window.location.origin : null;
  }

  if (/^https?:\/\//i.test(apiUrl)) {
    try {
      return new URL(apiUrl).origin;
    } catch {
      return null;
    }
  }

  if (typeof window !== 'undefined' && window.location?.origin) {
    return window.location.origin;
  }

  return null;
}
