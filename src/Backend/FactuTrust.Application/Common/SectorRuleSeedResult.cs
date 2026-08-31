namespace FactuTrust.Application.Common;

/// <summary>
/// Phase 2 — moteur de règles sectorielles en base (plan §WP-B3/§WP-B5). Result of one
/// catalog→DB seeder run (<c>SectorRuleSeeder.SeedAsync</c> in Infrastructure). Lives in
/// Application (not Infrastructure) so <c>ISectorRuleAdminService</c> can expose
/// <c>SeedFromCatalogAsync</c> without Application depending on Infrastructure.
/// </summary>
public sealed record SectorRuleSeedResult(int Inserted, int Updated, int SkippedExisting, long NewVersion, bool Forced);
