import { FirmFeatureFlagsService } from './firm-feature-flags.service';

describe('FirmFeatureFlagsService', () => {
  let service: FirmFeatureFlagsService;

  beforeEach(() => {
    localStorage.removeItem('ft.firm.featureFlags');
    service = new FirmFeatureFlagsService();
  });

  it('defaults fiscalOpsV2 and firmGovernance to true', () => {
    expect(service.isEnabled('fiscalOpsV2')).toBeTrue();
    expect(service.isEnabled('firmGovernance')).toBeTrue();
    expect(service.isEnabled('consoleFirmErrors')).toBeFalse();
  });

  it('persists flag changes', () => {
    service.setFlag('fiscalOpsV2', false);
    const reloaded = new FirmFeatureFlagsService();
    expect(reloaded.isEnabled('fiscalOpsV2')).toBeFalse();
  });

  it('reset restores defaults', () => {
    service.setFlag('firmGovernance', false);
    service.reset();
    expect(service.isEnabled('firmGovernance')).toBeTrue();
  });
});
