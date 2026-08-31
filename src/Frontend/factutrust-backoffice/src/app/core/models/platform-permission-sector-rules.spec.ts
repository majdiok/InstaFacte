import { PlatformPermission } from './platform.models';

/**
 * Phase 2 (WP-F9) — Épingle les valeurs des permissions ajoutées en WP-F6 pour garantir la
 * parité de chaîne avec `PlatformPermissions.cs` côté backend (D7). Toute dérive ici doit être
 * corrigée dans les deux projets simultanément.
 */
describe('PlatformPermission — Phase 2 sector-rules keys', () => {
  it('pins SectorRulesRead', () => {
    expect(PlatformPermission.SectorRulesRead).toBe('platform.sector-rules:read');
  });

  it('pins SectorRulesManage', () => {
    expect(PlatformPermission.SectorRulesManage).toBe('platform.sector-rules:manage');
  });

  it('pins SectorRulesApply', () => {
    expect(PlatformPermission.SectorRulesApply).toBe('platform.sector-rules:apply');
  });
});
