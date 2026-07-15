export const BRAND = {
  name: 'InstaFact',
  shortName: 'IF',
  tagline: 'GESTION COMMERCIALE INTELLIGENTE',
  taglineLong: 'Plateforme de gestion commerciale intelligente',
  streetName: 'Rue InstaFact',
  logoLockup: 'assets/branding/instafact-lockup.png',
  logoIcon: 'assets/branding/instafact-icon.png',
} as const;

export function pageTitle(page: string): string {
  return `${page} \u2014 ${BRAND.name}`;
}

export function brandAlt(suffix = ''): string {
  return suffix ? `${BRAND.name} \u2014 ${suffix}` : BRAND.name;
}
