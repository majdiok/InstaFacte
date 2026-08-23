import { Component } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import {
  AgentSuggestionCategory,
  FIRM_MISSION_SUGGESTION_CATEGORIES
} from '../../config/agent-scopes.config';
import { SuggestionCatalogComponent } from './suggestion-catalog.component';

@Component({
  standalone: true,
  imports: [SuggestionCatalogComponent],
  template: `
    <app-suggestion-catalog
      [categories]="categories"
      (questionSelected)="onQuestion($event)" />
  `
})
class HostComponent {
  categories: readonly AgentSuggestionCategory[] = FIRM_MISSION_SUGGESTION_CATEGORIES;
  lastQuestion: string | null = null;
  questionEmitCount = 0;

  onQuestion(question: string): void {
    this.lastQuestion = question;
    this.questionEmitCount += 1;
  }
}

describe('SuggestionCatalogComponent', () => {
  let fixture: ComponentFixture<HostComponent>;
  let host: HostComponent;

  const overview = FIRM_MISSION_SUGGESTION_CATEGORIES[0];
  const deadlines = FIRM_MISSION_SUGGESTION_CATEGORIES[1];
  const review = FIRM_MISSION_SUGGESTION_CATEGORIES[4];
  const risk = FIRM_MISSION_SUGGESTION_CATEGORIES[2];

  beforeEach(async () => {
    await TestBed.configureTestingModule({ imports: [HostComponent] }).compileComponents();
    fixture = TestBed.createComponent(HostComponent);
    host = fixture.componentInstance;
    fixture.detectChanges();
  });

  function tabButtons(): HTMLButtonElement[] {
    return Array.from(fixture.nativeElement.querySelectorAll('button[role="tab"]'));
  }

  function chipTexts(): string[] {
    return Array.from(fixture.nativeElement.querySelectorAll('.suggestion-chip')).map(el =>
      (el as HTMLElement).textContent!.trim()
    );
  }

  function tabList(): HTMLElement {
    return fixture.nativeElement.querySelector('[role="tablist"]');
  }

  function press(key: string): void {
    tabList().dispatchEvent(new KeyboardEvent('keydown', { key, bubbles: true }));
    fixture.detectChanges();
  }

  it('renders five tabs in catalogue label order', () => {
    expect(tabButtons().map(b => b.textContent!.trim())).toEqual(
      FIRM_MISSION_SUGGESTION_CATEGORIES.map(c => c.label)
    );
  });

  it('selects the first tab and leaves the others unselected', () => {
    const buttons = tabButtons();
    expect(buttons[0].getAttribute('aria-selected')).toBe('true');
    expect(buttons[0].tabIndex).toBe(0);
    for (const button of buttons.slice(1)) {
      expect(button.getAttribute('aria-selected')).toBe('false');
      expect(button.tabIndex).toBe(-1);
    }
  });

  it('shows only overview questions in the DOM', () => {
    expect(chipTexts()).toEqual([...overview.questions]);
  });

  it('filters to deadlines on tab click without emitting a question', () => {
    tabButtons()[1].click();
    fixture.detectChanges();

    expect(chipTexts()).toEqual([...deadlines.questions]);
    expect(host.questionEmitCount).toBe(0);
    expect(tabButtons()[1].getAttribute('aria-selected')).toBe('true');
    expect(tabButtons()[0].getAttribute('aria-selected')).toBe('false');
  });

  it('emits the exact chip text on question click', () => {
    const chips = fixture.nativeElement.querySelectorAll('.suggestion-chip');
    chips[1].click();
    fixture.detectChanges();

    expect(host.lastQuestion).toBe(overview.questions[1]);
    expect(host.questionEmitCount).toBe(1);
  });

  it('does not change state when the active tab is clicked again', () => {
    tabButtons()[0].click();
    fixture.detectChanges();

    expect(chipTexts()).toEqual([...overview.questions]);
    expect(host.questionEmitCount).toBe(0);
    expect(tabButtons()[0].getAttribute('aria-selected')).toBe('true');
  });

  it('navigates with ArrowRight', () => {
    press('ArrowRight');
    expect(chipTexts()).toEqual([...deadlines.questions]);
    expect(tabButtons()[1].getAttribute('aria-selected')).toBe('true');
  });

  it('wraps with ArrowLeft from the first tab to the last', () => {
    press('ArrowLeft');
    expect(chipTexts()).toEqual([...review.questions]);
    expect(tabButtons()[4].getAttribute('aria-selected')).toBe('true');
  });

  it('jumps to the last tab with End', () => {
    press('End');
    expect(chipTexts()).toEqual([...review.questions]);
  });

  it('jumps to the first tab with Home', () => {
    tabButtons()[2].click();
    fixture.detectChanges();
    press('Home');
    expect(chipTexts()).toEqual([...overview.questions]);
    expect(tabButtons()[0].getAttribute('aria-selected')).toBe('true');
  });

  it('falls back to the first remaining category when the selected id disappears', () => {
    tabButtons()[1].click();
    fixture.detectChanges();
    expect(chipTexts()).toEqual([...deadlines.questions]);

    host.categories = FIRM_MISSION_SUGGESTION_CATEGORIES.filter(
      c => c.id === 'risk' || c.id === 'review'
    );
    fixture.detectChanges();

    expect(chipTexts()).toEqual([...risk.questions]);
    expect(tabButtons()[0].getAttribute('aria-selected')).toBe('true');
    expect(tabButtons()[0].textContent!.trim()).toBe(risk.label);
  });

  it('wires tablist and tabpanel aria attributes', () => {
    const list = tabList();
    expect(list.getAttribute('aria-label')).toBe('Thèmes de questions');

    const activeTab = tabButtons()[0];
    const panel: HTMLElement = fixture.nativeElement.querySelector('[role="tabpanel"]');
    expect(activeTab.id).toBe('sug-tab-overview');
    expect(activeTab.getAttribute('aria-controls')).toBe('sug-panel-overview');
    expect(panel.id).toBe('sug-panel-overview');
    expect(panel.getAttribute('aria-labelledby')).toBe('sug-tab-overview');
  });
});
