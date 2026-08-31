import {
  COMPANY_TOUR_STEPS,
  FIRM_TOUR_STEPS,
  COMPANY_CHECKLIST_ITEMS,
  FIRM_CHECKLIST_ITEMS,
  filterTourSteps,
  mergeChecklistDone
} from './product-onboarding.catalog';
import { canStartProductTour, normalizeOnboardingStatus, shouldAutoStartTour } from './product-onboarding.models';

describe('filterTourSteps', () => {
  it('keeps welcome steps without a selector', () => {
    const steps = filterTourSteps(COMPANY_TOUR_STEPS, {
      compact: false,
      elementExists: () => false
    });
    expect(steps.some(s => s.id === 'welcome')).toBeTrue();
    expect(steps.some(s => s.selector)).toBeFalse();
  });

  it('skips missing selectors and keeps existing ones', () => {
    const steps = filterTourSteps(COMPANY_TOUR_STEPS, {
      compact: false,
      elementExists: selector => selector === '[data-tour="nav-ventes"]'
    });
    expect(steps.map(s => s.id)).toEqual(['welcome', 'nav-ventes']);
  });

  it('compact catalogue only includes compact steps', () => {
    const steps = filterTourSteps(COMPANY_TOUR_STEPS, {
      compact: true,
      elementExists: () => true
    });
    expect(steps.every(s => s.compact)).toBeTrue();
    expect(steps.some(s => s.id === 'nav-ventes')).toBeTrue();
    expect(steps.some(s => s.id === 'nav-achats')).toBeFalse();
  });

  it('firm catalogue has no ventes step', () => {
    expect(FIRM_TOUR_STEPS.some(s => s.id.includes('ventes'))).toBeFalse();
    expect(FIRM_TOUR_STEPS.some(s => s.selector === '[data-tour="nav-clients"]')).toBeTrue();
  });
});

describe('mergeChecklistDone', () => {
  it('unions manual and auto ids', () => {
    const done = mergeChecklistDone(['a'], ['a', 'b']);
    expect(done.has('a')).toBeTrue();
    expect(done.has('b')).toBeTrue();
  });
});

describe('shouldAutoStartTour', () => {
  it('starts only for NotStarted and InProgress', () => {
    expect(shouldAutoStartTour('NotStarted')).toBeTrue();
    expect(shouldAutoStartTour('InProgress')).toBeTrue();
    expect(shouldAutoStartTour('Completed')).toBeFalse();
    expect(shouldAutoStartTour('Dismissed')).toBeFalse();
    expect(shouldAutoStartTour(undefined)).toBeFalse();
  });
});

describe('normalizeOnboardingStatus', () => {
  it('maps numeric enum and unknown values fail-closed to Completed', () => {
    expect(normalizeOnboardingStatus(0)).toBe('NotStarted');
    expect(normalizeOnboardingStatus(1)).toBe('InProgress');
    expect(normalizeOnboardingStatus('notstarted')).toBe('NotStarted');
    expect(normalizeOnboardingStatus('Completed')).toBe('Completed');
    expect(normalizeOnboardingStatus(undefined)).toBe('Completed');
  });
});

describe('canStartProductTour', () => {
  const ready = {
    uiEnabled: true,
    isReplay: false,
    status: 'NotStarted' as const,
    hideLayout: false,
    isDelegated: false,
    isOnboardingRoute: true,
    hasBlockingModal: false,
    hasNavItems: true
  };

  it('auto-starts for a native dashboard user', () => {
    expect(canStartProductTour(ready)).toBeTrue();
  });

  it('does not auto-start in delegated mode, POS, or off the dashboard', () => {
    expect(canStartProductTour({ ...ready, isDelegated: true })).toBeFalse();
    expect(canStartProductTour({ ...ready, hideLayout: true })).toBeFalse();
    expect(canStartProductTour({ ...ready, isOnboardingRoute: false })).toBeFalse();
    expect(canStartProductTour({ ...ready, status: 'Completed' })).toBeFalse();
  });

  it('allows replay even when status is Completed', () => {
    expect(canStartProductTour({ ...ready, isReplay: true, status: 'Completed' })).toBeTrue();
  });
});

describe('checklist catalog module/segment gating metadata (plan WP-F4)', () => {
  it('no shipped company checklist item is module/segment-gated yet (TODO, additive only)', () => {
    // Pins the current state: OnboardingChecklistItemDef.modules/segments are wired
    // end-to-end (see onboarding-checklist.component.ts canSee()) but not yet used by
    // any real catalog entry — exact list is a product decision (see the TODO comment
    // above COMPANY_CHECKLIST_ITEMS). Update this pin deliberately once items are added.
    expect(COMPANY_CHECKLIST_ITEMS.every(item => !item.modules && !item.segments)).toBeTrue();
    expect(FIRM_CHECKLIST_ITEMS.every(item => !item.modules && !item.segments)).toBeTrue();
  });
});
