import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { of } from 'rxjs';
import { GuidedEntryComponent } from './guided-entry.component';
import { EntryFormStore } from '../../services/entry-form.store';
import { EntryReferenceStore } from '../../services/entry-reference.store';
import { AccountingService } from '../../../services/accounting.service';
import { getScenarioById } from '../../models/guided-scenarios.catalog';

describe('GuidedEntryComponent', () => {
  let fixture: ComponentFixture<GuidedEntryComponent>;
  let component: GuidedEntryComponent;
  let store: EntryFormStore;

  const refsMock = {
    openPeriods: signal([]),
    bankAccounts: signal([]),
    thirdPartySuggestions: signal([]),
    searchThirdParties: jasmine.createSpy('searchThirdParties'),
    resolveAccount: jasmine.createSpy('resolveAccount').and.returnValue(null),
    accounts: signal([]),
    vatRates: signal([])
  };

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [GuidedEntryComponent],
      providers: [
        provideNoopAnimations(),
        EntryFormStore,
        { provide: EntryReferenceStore, useValue: refsMock },
        {
          provide: AccountingService,
          useValue: {
            getJournalTemplates: () => of({ success: true, data: [] })
          }
        }
      ]
    }).compileComponents();

    store = TestBed.inject(EntryFormStore);
    fixture = TestBed.createComponent(GuidedEntryComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('renders 8 scenario cards at step 1', () => {
    const cards = fixture.nativeElement.querySelectorAll('.scenario-card');
    expect(cards.length).toBe(8);
  });

  it('renders a PrimeIcon on each scenario card', () => {
    const icons = fixture.nativeElement.querySelectorAll('i.scenario-card__icon') as NodeListOf<HTMLElement>;
    expect(icons.length).toBe(8);
    icons.forEach(icon => {
      expect(icon.classList.contains('pi')).toBe(true);
      const hasPiIcon = Array.from(icon.classList).some(c => c.startsWith('pi-'));
      expect(hasPiIcon).toBe(true);
      expect(icon.getAttribute('aria-hidden')).toBe('true');
    });
  });

  it('disables Suivant until a scenario is selected', () => {
    const nextBtn = Array.from(
      fixture.nativeElement.querySelectorAll('button') as NodeListOf<HTMLButtonElement>
    ).find(b => b.textContent?.trim() === 'Suivant');
    expect(nextBtn).toBeTruthy();
    expect(nextBtn!.disabled).toBe(true);

    const ventes = getScenarioById('ventes')!;
    component.selectScenario(ventes);
    fixture.detectChanges();

    expect(nextBtn!.disabled).toBe(false);
  });

  it('selectScenario updates selectedScenario and default journal', () => {
    const ventes = getScenarioById('ventes')!;
    component.selectScenario(ventes);

    expect(component.selectedScenario()?.id).toBe('ventes');
    expect(store.journalCode()).toBe('JV');
  });
});
