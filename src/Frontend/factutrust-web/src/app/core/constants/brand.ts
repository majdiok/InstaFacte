export const BRAND = {
  name: 'InstaFact',
  shortName: 'IF',
  tagline: 'GESTION COMMERCIALE INTELLIGENTE',
  taglineShort: 'GESTION COMMERCIALE',
  taglineLong: 'Plateforme de gestion commerciale intelligente',
  streetName: 'Rue InstaFact',
  logoLockup: 'assets/branding/instafact-lockup.png',
  logoIcon: 'assets/branding/instafact-icon.png',
  logoLockupOnDark: 'assets/branding/instafact-lockup-on-dark.png',
  logoIconOnDark: 'assets/branding/instafact-icon-on-dark.png',
} as const;

export function pageTitle(page: string): string {
  return `${page} \u2014 ${BRAND.name}`;
}

export function brandAlt(suffix = ''): string {
  return suffix ? `${BRAND.name} \u2014 ${suffix}` : BRAND.name;
}
