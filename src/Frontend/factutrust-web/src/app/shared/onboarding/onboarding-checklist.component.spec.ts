import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { signal } from '@angular/core';
import { AuthService } from '@core/services/auth.service';
import { AppModule } from '@core/models/app-module';
import { ProductOnboardingApiService } from '@core/onboarding/product-onboarding.service';
import { OnboardingChecklistComponent } from './onboarding-checklist.component';

describe('OnboardingChecklistComponent', () => {
  let fixture: ComponentFixture<OnboardingChecklistComponent>;
  let patchSpy: jasmine.Spy;

  function setup(opts: {
    dismissed?: boolean;
    autoIds?: string[];
    doneIds?: string[];
    delegated?: boolean;
    firm?: boolean;
    admin?: boolean;
    companySegment?: string | null;
    enabledModuleIds?: number[];
    tenantCreatedAtUtc?: string | null;
  }): void {
    patchSpy = jasmine.createSpy('patch').and.returnValue(of({
      enabled: true,
      status: 'InProgress',
      version: 1,
      checklist: { dismissed: !!opts.dismissed, doneIds: opts.doneIds ?? [] },
      autoCompletedIds: opts.autoIds ?? []
    }));

    TestBed.configureTestingModule({
      imports: [OnboardingChecklistComponent],
      providers: [
        provideRouter([]),
        {
          provide: AuthService,
          useValue: {
            user: () => ({
              companySegment: opts.companySegment ?? null,
              tenantCreatedAtUtc: opts.tenantCreatedAtUtc ?? undefined,
              productOnboardingChecklist: {
                dismissed: false,
                doneIds: opts.doneIds ?? []
              }
            }),
            isDelegatedMode: () => !!opts.delegated,
            isAccountingFirm: () => !!opts.firm,
            isAdmin: () => !!opts.admin,
            isFirmManager: () => !!opts.firm,
            hasPermission: () => true,
            hasAllModules: (modules: readonly number[]) => {
              const enabled = new Set(opts.enabledModuleIds ?? []);
              return modules.every(m => enabled.has(m));
            }
          }
        },
        {
          provide: ProductOnboardingApiService,
          useValue: {
            get: () => of({
              enabled: true,
              status: 'InProgress',
              version: 1,
              checklist: { dismissed: !!opts.dismissed, doneIds: opts.doneIds ?? [] },
              autoCompletedIds: opts.autoIds ?? []
            }),
            patch: patchSpy,
            replayTick: signal(0),
            isTourRunning: signal(false)
          }
        }
      ]
    });

    fixture = TestBed.createComponent(OnboardingChecklistComponent);
    fixture.detectChanges();
  }

  it('renders remaining company steps and hides when dismissed', () => {
    setup({ admin: true });
    expect(fixture.nativeElement.textContent).toContain('Premiers pas');
    expect(fixture.nativeElement.textContent).toContain('Créer un client');

    fixture.componentInstance.dismiss();
    fixture.detectChanges();
    expect(patchSpy).toHaveBeenCalledWith({ checklistDismissed: true });
    expect(fixture.nativeElement.textContent).not.toContain('Premiers pas');
  });

  it('marks auto-completed items as done', () => {
    setup({ autoIds: ['create-client'], admin: true });
    const item = fixture.nativeElement.querySelector('[aria-label="Créer un client — fait"]');
    expect(item).toBeTruthy();
  });

  it('persists a manual done click', () => {
    setup({ admin: true });
    fixture.componentInstance.markDone('create-client');
    expect(patchSpy).toHaveBeenCalledWith({ checklistDoneId: 'create-client' });
  });

  it('does not render in delegated mode', () => {
    setup({ delegated: true, admin: true });
    expect(fixture.nativeElement.textContent).not.toContain('Premiers pas');
  });

  describe('module/segment gating (plan §4.4 / WP-F4)', () => {
    // `canSee` is a private helper; exercised directly here with synthetic items to
    // cover the gating rules in isolation. The two real gated catalog items
    // ('check-default-warehouse', 'commerce-stock-receipt') are covered end-to-end
    // below, through the actual rendered component.
    function canSee(item: object): boolean {
      // eslint-disable-next-line @typescript-eslint/no-explicit-any
      return (fixture.componentInstance as any).canSee(item);
    }

    it('hides an item whose required modules are not all enabled', () => {
      setup({ admin: true, enabledModuleIds: [] });
      expect(canSee({ modules: [7] })).toBeFalse();
    });

    it('shows an item whose required modules are all enabled', () => {
      setup({ admin: true, enabledModuleIds: [7, 9] });
      expect(canSee({ modules: [7, 9] })).toBeTrue();
    });

    it('hides a segment-gated item when the user has no companySegment', () => {
      setup({ admin: true, companySegment: null });
      expect(canSee({ segments: ['commerce'] })).toBeFalse();
    });

    it('hides a segment-gated item when companySegment does not match', () => {
      setup({ admin: true, companySegment: 'association' });
      expect(canSee({ segments: ['commerce'] })).toBeFalse();
    });

    it('shows a segment-gated item when companySegment matches', () => {
      setup({ admin: true, companySegment: 'commerce' });
      expect(canSee({ segments: ['commerce', 'services'] })).toBeTrue();
    });

    it('items with neither modules nor segments are unaffected', () => {
      setup({ admin: true });
      expect(canSee({})).toBeTrue();
    });
  });

  describe('real catalog items — Phase 2 additive gating end-to-end', () => {
    it('hides "check-default-warehouse" and "commerce-stock-receipt" when Stock is disabled', () => {
      setup({ admin: true, enabledModuleIds: [], companySegment: 'commerce' });
      const text: string = fixture.nativeElement.textContent;
      expect(text).not.toContain('Vérifier votre entrepôt par défaut');
      expect(text).not.toContain('Réceptionner votre premier stock');
    });

    it('shows "check-default-warehouse" once Stock is enabled, regardless of segment', () => {
      setup({ admin: true, enabledModuleIds: [AppModule.Stock], companySegment: 'services' });
      expect(fixture.nativeElement.textContent).toContain('Vérifier votre entrepôt par défaut');
      expect(fixture.nativeElement.textContent).not.toContain('Réceptionner votre premier stock');
    });

    it('shows "commerce-stock-receipt" only when Stock is enabled AND segment is commerce', () => {
      setup({ admin: true, enabledModuleIds: [AppModule.Stock], companySegment: 'commerce' });
      expect(fixture.nativeElement.textContent).toContain('Réceptionner votre premier stock');
    });

    it('hides "commerce-stock-receipt" for a non-commerce segment even with Stock enabled', () => {
      setup({ admin: true, enabledModuleIds: [AppModule.Stock], companySegment: 'entreprise' });
      expect(fixture.nativeElement.textContent).not.toContain('Réceptionner votre premier stock');
    });
  });

  describe('plan v1 §2.6 — items sectoriels additionnels (BTP / Services & Éducatif)', () => {
    it('tenant BTP avec module Projets actif voit « Créer votre premier projet », pas le tenant Commerce', () => {
      setup({ admin: true, enabledModuleIds: [AppModule.Projects], companySegment: 'btp-construction' });
      expect(fixture.nativeElement.textContent).toContain('Créer votre premier projet');
    });

    it('tenant Commerce ne voit pas « Créer votre premier projet » même avec le module Projets actif', () => {
      setup({ admin: true, enabledModuleIds: [AppModule.Projects], companySegment: 'commerce' });
      expect(fixture.nativeElement.textContent).not.toContain('Créer votre premier projet');
    });

    it('masque « Créer votre premier projet » pour un tenant BTP sans le module Projets', () => {
      setup({ admin: true, enabledModuleIds: [], companySegment: 'btp-construction' });
      expect(fixture.nativeElement.textContent).not.toContain('Créer votre premier projet');
    });

    it('tenant Services avec module RecurringContracts actif voit « Configurer un contrat récurrent »', () => {
      setup({ admin: true, enabledModuleIds: [AppModule.RecurringContracts], companySegment: 'services' });
      expect(fixture.nativeElement.textContent).toContain('Configurer un contrat récurrent');
    });

    it('tenant Établissement éducatif avec module RecurringContracts actif voit aussi cet item', () => {
      setup({ admin: true, enabledModuleIds: [AppModule.RecurringContracts], companySegment: 'etablissement-educatif' });
      expect(fixture.nativeElement.textContent).toContain('Configurer un contrat récurrent');
    });

    it('tenant BTP ne voit pas « Configurer un contrat récurrent » (réservé Services/Éducatif)', () => {
      setup({ admin: true, enabledModuleIds: [AppModule.RecurringContracts], companySegment: 'btp-construction' });
      expect(fixture.nativeElement.textContent).not.toContain('Configurer un contrat récurrent');
    });
  });

  describe('progress counts the filtered list (plan WP-F5)', () => {
    // Two shipped catalog items ARE module/segment-gated now ('check-default-warehouse',
    // 'commerce-stock-receipt' — see the 'real catalog items' describe block above), but this
    // test still sets `items` directly to a small, fixed synthetic list so the assertion is
    // about `progressLabel`/`visibleItems` deriving their denominator from the already-filtered
    // list (populated by `ngOnInit` via `catalog.filter(item => this.canSee(item))`), not from
    // the full unfiltered catalog — independent of how many/which real items happen to be gated.
    it('progressLabel and visibleItems reflect only the items that passed canSee, not the full catalog', () => {
      setup({ admin: true, doneIds: ['create-client'] });
      const filtered = [
        { id: 'create-client', label: 'Créer un client', description: '', route: '/clients' },
        { id: 'create-invoice', label: 'Créer une facture', description: '', route: '/invoices' }
      ];
      // eslint-disable-next-line @typescript-eslint/no-explicit-any
      (fixture.componentInstance as any).items.set(filtered);
      fixture.detectChanges();

      expect(fixture.componentInstance.visibleItems().length).toBe(2);
      expect(fixture.componentInstance.progressLabel()).toBe('1 / 2 étapes terminées');
    });
  });

  describe('plan §3.5 — minAgeDays gating (progressive profiling)', () => {
    it('hides the "Complétez votre profil entreprise" item when the tenant is younger than minAgeDays', () => {
      const oneDayAgo = new Date(Date.now() - 1 * 24 * 60 * 60 * 1000).toISOString();
      setup({ admin: true, tenantCreatedAtUtc: oneDayAgo });
      expect(fixture.nativeElement.textContent).not.toContain('Complétez votre profil entreprise');
    });

    it('shows the "Complétez votre profil entreprise" item once the tenant is old enough', () => {
      const tenDaysAgo = new Date(Date.now() - 10 * 24 * 60 * 60 * 1000).toISOString();
      setup({ admin: true, tenantCreatedAtUtc: tenDaysAgo });
      expect(fixture.nativeElement.textContent).toContain('Complétez votre profil entreprise');
    });

    it('shows the item when tenantCreatedAtUtc is absent (fail-open = pre-Phase-3 behavior)', () => {
      setup({ admin: true, tenantCreatedAtUtc: undefined });
      expect(fixture.nativeElement.textContent).toContain('Complétez votre profil entreprise');
    });
  });
});
