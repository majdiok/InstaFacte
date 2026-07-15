brand_ts = r"""export const BRAND = {
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
"""
brand_cs = r"""namespace FactuTrust.Domain.Constants;

public static class BrandConstants
{
    public const string Name = "InstaFact";
    public const string Tagline = "Gestion commerciale intelligente";
    public const string StreetName = "Rue InstaFact";
}
"""
open(r"c:\Solution\FactuTrust - Copy\src\Frontend\factutrust-web\src\app\core\constants\brand.ts", "w", encoding="utf-8", newline="\n").write(brand_ts)
open(r"c:\Solution\FactuTrust - Copy\src\Backend\FactuTrust.Domain\Constants\BrandConstants.cs", "w", encoding="utf-8", newline="\n").write(brand_cs)
print("ok")
