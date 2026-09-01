import {
  COMPANY_TOUR_STEPS,
  FIRM_TOUR_STEPS,
  COMPANY_CHECKLIST_ITEMS,
  FIRM_CHECKLIST_ITEMS,
  filterTourSteps,
  mergeChecklistDone
} from './product-onboarding.catalog';
import { AppModule } from '@core/models/app-module';
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

describe('checklist catalog module/segment gating metadata (plan §4.4 / WP-F4 + plan v1 §2.6)', () => {
  it('exactly the four additive items (Phase 2 + plan v1 §2.6) carry modules/segments gating; ' +
    'the 7 original/progressive items remain ungated by modules/segments', () => {
    const gated = COMPANY_CHECKLIST_ITEMS.filter(item => item.modules || item.segments);
    expect(gated.map(item => item.id).sort()).toEqual(
      ['btp-first-project', 'check-default-warehouse', 'commerce-stock-receipt', 'recurring-contract-setup'].sort()
    );

    const ungated = COMPANY_CHECKLIST_ITEMS.filter(item => !item.modules && !item.segments);
    expect(ungated.length).toBe(7);

    expect(FIRM_CHECKLIST_ITEMS.every(item => !item.modules && !item.segments)).toBeTrue();
  });

  it('"check-default-warehouse" requires the Stock module and no segment restriction', () => {
    const item = COMPANY_CHECKLIST_ITEMS.find(i => i.id === 'check-default-warehouse');
    expect(item?.modules).toEqual([AppModule.Stock]);
    expect(item?.segments).toBeUndefined();
    expect(item?.route).toBe('/settings/warehouses');
  });

  it('"commerce-stock-receipt" requires the Stock module and is restricted to the commerce segment', () => {
    const item = COMPANY_CHECKLIST_ITEMS.find(i => i.id === 'commerce-stock-receipt');
    expect(item?.modules).toEqual([AppModule.Stock]);
    expect(item?.segments).toEqual(['commerce']);
    expect(item?.route).toBe('/stock/entries/new');
  });

  it('"btp-first-project" (plan v1 §2.6) requires the Projects module and the btp-construction segment', () => {
    const item = COMPANY_CHECKLIST_ITEMS.find(i => i.id === 'btp-first-project');
    expect(item?.modules).toEqual([AppModule.Projects]);
    expect(item?.segments).toEqual(['btp-construction']);
    expect(item?.route).toBe('/projects');
  });

  it('"recurring-contract-setup" (plan v1 §2.6) requires the RecurringContracts module and Services/Éducatif segments', () => {
    const item = COMPANY_CHECKLIST_ITEMS.find(i => i.id === 'recurring-contract-setup');
    expect(item?.modules).toEqual([AppModule.RecurringContracts]);
    expect(item?.segments).toEqual(['services', 'etablissement-educatif']);
    expect(item?.route).toBe('/recurring-contracts/new');
  });

  it('"complete-company-profile" (plan §3.5) carries minAgeDays gating but no modules/segments', () => {
    const item = COMPANY_CHECKLIST_ITEMS.find(i => i.id === 'complete-company-profile');
    expect(item).toBeDefined();
    expect(item?.minAgeDays).toBe(3);
    expect(item?.modules).toBeUndefined();
    expect(item?.segments).toBeUndefined();
    expect(item?.route).toBe('/settings/company');
  });

  it('un tenant BTP ne voit pas de widget/gating croisé avec le segment commerce (isolation des items sectoriels)', () => {
    const btpItem = COMPANY_CHECKLIST_ITEMS.find(i => i.id === 'btp-first-project');
    const commerceItem = COMPANY_CHECKLIST_ITEMS.find(i => i.id === 'commerce-stock-receipt');
    expect(btpItem?.segments).not.toContain('commerce');
    expect(commerceItem?.segments).not.toContain('btp-construction');
  });
});
