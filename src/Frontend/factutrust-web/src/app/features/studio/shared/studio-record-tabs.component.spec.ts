import { ComponentFixture, TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { StudioRecordTabsComponent, StudioRecordTab } from './studio-record-tabs.component';

const tabs: StudioRecordTab[] = [
  { key: 'form', label: 'Fiche' },
  { key: 'linked:j1', label: 'Liés — Techniciens', badge: 3 },
  { key: 'workflows', label: 'Workflows', badge: null }
];

describe('StudioRecordTabsComponent', () => {
  let fixture: ComponentFixture<StudioRecordTabsComponent>;
  let component: StudioRecordTabsComponent;

  beforeEach(async () => {
    await TestBed.configureTestingModule({ imports: [StudioRecordTabsComponent] }).compileComponents();
    fixture = TestBed.createComponent(StudioRecordTabsComponent);
    component = fixture.componentInstance;
    fixture.componentRef.setInput('tabs', tabs);
    fixture.detectChanges();
  });

  it('rend un onglet par entrée avec role=tab et aria-selected', () => {
    const buttons = fixture.debugElement.queryAll(By.css('[role="tab"]'));
    expect(buttons.length).toBe(3);
    expect(buttons[0].attributes['aria-selected']).toBe('true');
    expect(buttons[0].attributes['tabindex']).toBe('0');
    expect(buttons[1].attributes['aria-selected']).toBe('false');
    expect(buttons[1].attributes['tabindex']).toBe('-1');
    expect(fixture.debugElement.query(By.css('[role="tablist"]'))).not.toBeNull();
  });

  it('affiche le badge quand fourni', () => {
    const badge = fixture.debugElement.query(By.css('[data-testid="studio-tab-linked:j1"] .studio-tab__badge'));
    expect(badge.nativeElement.textContent.trim()).toBe('3');
    expect(fixture.debugElement.query(By.css('[data-testid="studio-tab-form"] .studio-tab__badge'))).toBeNull();
    expect(fixture.debugElement.query(By.css('[data-testid="studio-tab-workflows"] .studio-tab__badge'))).toBeNull();
  });

  it('émet active au clic et au clavier', () => {
    const buttons = fixture.debugElement.queryAll(By.css('[role="tab"]'));
    buttons[1].triggerEventHandler('click', null);
    expect(component.active()).toBe('linked:j1');
    fixture.detectChanges();
    expect(buttons[1].attributes['aria-selected']).toBe('true');

    component.onKeydown(new KeyboardEvent('keydown', { key: 'ArrowLeft' }), 1);
    expect(component.active()).toBe('form');
    component.onKeydown(new KeyboardEvent('keydown', { key: 'ArrowLeft' }), 0);
    expect(component.active()).toBe('workflows');
    component.onKeydown(new KeyboardEvent('keydown', { key: 'ArrowRight' }), 2);
    expect(component.active()).toBe('form');
  });
});
