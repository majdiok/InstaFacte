/**
 * Extrait le suffixe Font Awesome (ex. fa-bag-shopping) pour le styling
 * des pastilles icon-box (sidebar / secondary-nav).
 */
export function getNavIconKey(icon?: string | null): string | null {
  if (!icon) {
    return null;
  }
  const parts = icon.split(/\s+/);
  const faClass = parts.find(
    p => p.startsWith('fa-') && p !== 'fa-solid' && p !== 'fa-regular' && p !== 'fa-brands'
  );
  return faClass ?? null;
}
