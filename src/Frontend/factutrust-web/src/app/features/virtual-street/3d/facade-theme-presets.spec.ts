import { FacadeTheme, getFacadePreset } from './facade-theme-presets';

describe('facade-theme-presets', () => {
  it('getFacadePreset returns distinct heights per theme', () => {
    const classic = getFacadePreset(FacadeTheme.Classic);
    const minimal = getFacadePreset(FacadeTheme.Minimal);
    expect(classic.bodyHeight).not.toBe(minimal.bodyHeight);
  });

  it('getFacadePreset defaults for unknown enum value', () => {
    const u = getFacadePreset(999);
    expect(u.bodyHeight).toBeGreaterThan(0);
    expect(u.glassTransmission).toBeGreaterThan(0);
  });

  it('uses enlarged retail footprint (scaled defaults)', () => {
    const u = getFacadePreset(999);
    expect(u.bodyWidth).toBeGreaterThan(3.0);
    expect(u.bodyHeight).toBeGreaterThan(4.0);
  });
});
