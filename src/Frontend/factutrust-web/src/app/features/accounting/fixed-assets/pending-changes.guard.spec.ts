import { pendingChangesGuard } from './pending-changes.guard';
import type { FixedAssetDetailComponent } from './fixed-asset-detail.component';

describe('pendingChangesGuard (T15 / C9)', () => {
  it('allows deactivation when the component reports no pending changes', () => {
    const component = { canDeactivate: () => true } as unknown as FixedAssetDetailComponent;

    expect(pendingChangesGuard(component, {} as never, {} as never, {} as never)).toBeTrue();
  });

  it('blocks deactivation when the component reports pending changes (user cancels)', () => {
    const component = { canDeactivate: () => false } as unknown as FixedAssetDetailComponent;

    expect(pendingChangesGuard(component, {} as never, {} as never, {} as never)).toBeFalse();
  });

  it('allows deactivation when there is no component instance', () => {
    expect(pendingChangesGuard(null as unknown as FixedAssetDetailComponent, {} as never, {} as never, {} as never)).toBeTrue();
  });
});
