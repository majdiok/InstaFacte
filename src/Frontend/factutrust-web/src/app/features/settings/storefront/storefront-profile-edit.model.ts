/**
 * Client-side limits aligned with domain `StorefrontProfile` / FluentValidation on `UpdateStorefrontProfileRequest`.
 * Server remains authoritative.
 */
export const STOREFRONT_DESCRIPTION_MAX_LENGTH = 4000;
export const STOREFRONT_TAGLINE_MAX_LENGTH = 120;
export const STOREFRONT_PUBLIC_URL_MAX_LENGTH = 500;

const HEX_COLOR = /^#[0-9A-Fa-f]{6}$/;

export function isValidStorefrontHexColor(value: string): boolean {
  return HEX_COLOR.test((value ?? '').trim());
}

export function isModerationReadyForSubmit(descriptionMarkdown: string | null | undefined, publicLogoUrl: string | null | undefined): boolean {
  const desc = (descriptionMarkdown ?? '').trim();
  const logo = (publicLogoUrl ?? '').trim();
  return desc.length > 0 && logo.length > 0;
}
