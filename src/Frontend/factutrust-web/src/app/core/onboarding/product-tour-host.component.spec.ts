import { ComponentFixture, TestBed, fakeAsync, tick, flushMicrotasks, discardPeriodicTasks } from '@angular/core/testing';
import { NavigationEnd, Router } from '@angular/router';
import { Subject, of } from 'rxjs';
import { signal } from '@angular/core';
import { AuthService } from '@core/services/auth.service';
import { LayoutRouteService } from '@core/layout/layout-route.service';
import { ProductTourHostComponent } from './product-tour-host.component';
import { ProductTourLayoutBridge } from './product-tour-layout.bridge';
import { ProductOnboardingApiService } from './product-onboarding.service';
import { ProductTourService } from './product-tour.service';
import { ProductOnboardingStatus } from './product-onboarding.models';

describe('ProductTourHostComponent', () => {
  let fixture: ComponentFixture<ProductTourHostComponent> | null = null;
  let events: Subject<NavigationEnd>;
  let tourStart: jasmine.Spy;
  let tourDestroy: jasmine.Spy;
  let patchSpy: jasmine.Spy;

  function setup(opts: {
    status?: ProductOnboardingStatus;
    url?: string;
    delegated?: boolean;
    hideLayout?: boolean;
    firm?: boolean;
  }): void {
    events = new Subject<NavigationEnd>();
    tourStart = jasmine.createSpy('start');
    tourDestroy = jasmine.createSpy('destroy');
    patchSpy = jasmine.createSpy('patch').and.returnValue(of(null));
    const replayTick = signal(0);
    const isTourRunning = signal(false);

    TestBed.configureTestingModule({
      imports: [ProductTourHostComponent],
      providers: [
        {
          provide: Router,
          useValue: {
            events: events.asObservable(),
            url: opts.url ?? '/dashboard'
          }
        },
        {
          provide: AuthService,
          useValue: {
            user: () => ({
              companyName: 'Ste Test',
              productOnboardingStatus: opts.status ?? 'NotStarted'
            }),
            isDelegatedMode: () => !!opts.delegated,
            isAccountingFirm: () => !!opts.firm
          }
        },
        {
          provide: LayoutRouteService,
          useValue: {
            flags: () => ({ hideLayout: !!opts.hideLayout, fullWidth: false })
          }
        },
        {
          provide: ProductTourLayoutBridge,
          useValue: {
            expandSidebar: () => undefined,
            closeAiPanel: () => undefined,
            expandNavSection: () => null,
            restoreNavSection: () => undefined,
            hasNavItems: () => true
          }
        },
        {
          provide: ProductOnboardingApiService,
          useValue: {
            get: () => of(null),
            patch: patchSpy,
            replayTick,
            isTourRunning,
            requestReplay: () => replayTick.update(n => n + 1)
          }
        },
        {
          provide: ProductTourService,
          useValue: {
            get isActive() {
              return false;
            },
            start: tourStart,
            destroy: tourDestroy
          }
        }
      ]
    });

    fixture = TestBed.createComponent(ProductTourHostComponent);
  }

  afterEach(() => {
    fixture?.destroy();
    fixture = null;
    TestBed.resetTestingModule();
  });

  it('auto-starts on the company dashboard', fakeAsync(() => {
    setup({ status: 'NotStarted', url: '/dashboard' });
    flushMicrotasks();
    tick(400);
    expect(tourStart).toHaveBeenCalled();
    expect(patchSpy).toHaveBeenCalledWith({ status: 'InProgress' });
    discardPeriodicTasks();
  }));

  it('does not auto-start in delegated mode', fakeAsync(() => {
    setup({ status: 'NotStarted', url: '/dashboard', delegated: true });
    flushMicrotasks();
    tick(400);
    expect(tourStart).not.toHaveBeenCalled();
    discardPeriodicTasks();
  }));

  it('does not auto-start when the layout is hidden (POS)', fakeAsync(() => {
    setup({ status: 'NotStarted', url: '/dashboard', hideLayout: true });
    flushMicrotasks();
    tick(400);
    expect(tourStart).not.toHaveBeenCalled();
    discardPeriodicTasks();
  }));

  it('does not auto-start for Completed users', fakeAsync(() => {
    setup({ status: 'Completed', url: '/dashboard' });
    flushMicrotasks();
    tick(400);
    expect(tourStart).not.toHaveBeenCalled();
    discardPeriodicTasks();
  }));

  it('uses the firm catalogue without ventes steps', fakeAsync(() => {
    setup({ status: 'NotStarted', url: '/firm/dashboard', firm: true });
    flushMicrotasks();
    tick(400);
    expect(tourStart).toHaveBeenCalled();
    const steps = tourStart.calls.mostRecent().args[0].steps as { id: string }[];
    expect(steps.some(s => s.id.includes('ventes'))).toBeFalse();
    expect(steps.some(s => s.id === 'welcome')).toBeTrue();
    discardPeriodicTasks();
  }));

  it('destroys the overlay when leaving the dashboard', fakeAsync(() => {
    const router = { events: new Subject<NavigationEnd>(), url: '/dashboard' };
    events = router.events;
    tourStart = jasmine.createSpy('start');
    tourDestroy = jasmine.createSpy('destroy');
    let active = true;
    TestBed.configureTestingModule({
      imports: [ProductTourHostComponent],
      providers: [
        { provide: Router, useValue: router },
        {
          provide: AuthService,
          useValue: {
            user: () => ({ productOnboardingStatus: 'InProgress', companyName: 'Ste' }),
            isDelegatedMode: () => false,
            isAccountingFirm: () => false
          }
        },
        { provide: LayoutRouteService, useValue: { flags: () => ({ hideLayout: false, fullWidth: false }) } },
        {
          provide: ProductTourLayoutBridge,
          useValue: {
            expandSidebar: () => undefined,
            closeAiPanel: () => undefined,
            expandNavSection: () => null,
            restoreNavSection: () => undefined,
            hasNavItems: () => true
          }
        },
        {
          provide: ProductOnboardingApiService,
          useValue: {
            patch: () => of(null),
            replayTick: signal(0),
            isTourRunning: signal(true)
          }
        },
        {
          provide: ProductTourService,
          useValue: {
            get isActive() {
              return active;
            },
            start: tourStart,
            destroy: () => {
              active = false;
              tourDestroy();
            }
          }
        }
      ]
    });
    fixture = TestBed.createComponent(ProductTourHostComponent);
    router.url = '/invoices';
    events.next(new NavigationEnd(1, '/invoices', '/invoices'));
    expect(tourDestroy).toHaveBeenCalled();
    discardPeriodicTasks();
  }));
});
