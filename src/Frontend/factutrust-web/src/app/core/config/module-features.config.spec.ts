import { AppModule } from '@core/models/app-module';
import type { UserModuleAccessItem } from '@core/services/tenant-users.service';
import { withSubFeatureToggled } from './module-features.config';

describe('withSubFeatureToggled', () => {
  const visible = ['read', 'manage', 'forecast_read'];

  function treasury(keys: string[] | null): UserModuleAccessItem {
    return { module: AppModule.Treasury, enabled: true, enabledFeatureKeys: keys };
  }

  it('null + décocher une clé visible n’inclut pas une clé masquée du catalogue statique', () => {
    const next = withSubFeatureToggled(treasury(null), 'manage', false, visible);
    expect(next.enabledFeatureKeys).toEqual(['read', 'forecast_read']);
    expect(next.enabledFeatureKeys).not.toContain('forecast_manage');
  });

  it('liste explicite contenant une clé masquée → la clé est retirée au toggle d’une clé visible', () => {
    const next = withSubFeatureToggled(
      treasury(['read', 'forecast_manage']),
      'forecast_read',
      true,
      visible
    );
    expect(next.enabledFeatureKeys).toEqual(['read', 'forecast_read']);
    expect(next.enabledFeatureKeys).not.toContain('forecast_manage');
  });
});
