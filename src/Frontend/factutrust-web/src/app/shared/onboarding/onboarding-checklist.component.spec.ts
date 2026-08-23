import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { signal } from '@angular/core';
import { AuthService } from '@core/services/auth.service';
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
              productOnboardingChecklist: {
                dismissed: false,
                doneIds: opts.doneIds ?? []
              }
            }),
            isDelegatedMode: () => !!opts.delegated,
            isAccountingFirm: () => !!opts.firm,
            isAdmin: () => !!opts.admin,
            isFirmManager: () => !!opts.firm,
            hasPermission: () => true
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
});
