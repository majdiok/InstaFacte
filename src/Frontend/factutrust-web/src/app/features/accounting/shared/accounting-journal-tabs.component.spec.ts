import { Component } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { AccountingJournalTabsComponent } from './accounting-journal-tabs.component';
import { ACCOUNTING_SUB_JOURNALS } from './accounting-journal-tabs.model';

@Component({
  standalone: true,
  imports: [AccountingJournalTabsComponent],
  template: `
    <app-accounting-journal-tabs
      [tabs]="tabs"
      [activeIndex]="activeIndex"
      (tabChange)="onTabChange($event)" />
  `
})
class HostComponent {
  readonly tabs = ACCOUNTING_SUB_JOURNALS;
  activeIndex = 0;
  lastChange: { index: number; code: string } | null = null;

  onTabChange(event: { index: number; code: string }): void {
    this.lastChange = event;
    this.activeIndex = event.index;
  }
}

describe('AccountingJournalTabsComponent', () => {
  let fixture: ComponentFixture<HostComponent>;
  let host: HostComponent;

  beforeEach(async () => {
    await TestBed.configureTestingModule({ imports: [HostComponent] }).compileComponents();
    fixture = TestBed.createComponent(HostComponent);
    host = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('renders six journal tabs', () => {
    const buttons = fixture.nativeElement.querySelectorAll('button[role="tab"]');
    expect(buttons.length).toBe(6);
  });

  it('emits index and code on tab click', () => {
    const buttons = fixture.nativeElement.querySelectorAll('button[role="tab"]');
    buttons[1].click();
    fixture.detectChanges();

    expect(host.lastChange).toEqual({ index: 1, code: 'JA' });
    expect(host.activeIndex).toBe(1);
  });

  it('does not emit when clicking the active tab', () => {
    host.lastChange = null;
    const buttons = fixture.nativeElement.querySelectorAll('button[role="tab"]');
    buttons[0].click();
    fixture.detectChanges();

    expect(host.lastChange).toBeNull();
  });

  it('navigates with ArrowRight', () => {
    const tabList = fixture.nativeElement.querySelector('[role="tablist"]');
    tabList.dispatchEvent(new KeyboardEvent('keydown', { key: 'ArrowRight', bubbles: true }));
    fixture.detectChanges();

    expect(host.lastChange).toEqual({ index: 1, code: 'JA' });
  });

  it('navigates with ArrowLeft from first tab to last', () => {
    const tabList = fixture.nativeElement.querySelector('[role="tablist"]');
    tabList.dispatchEvent(new KeyboardEvent('keydown', { key: 'ArrowLeft', bubbles: true }));
    fixture.detectChanges();

    expect(host.lastChange).toEqual({ index: 5, code: 'JIM' });
  });

  it('navigates with End', () => {
    host.activeIndex = 3;
    fixture.detectChanges();

    const tabList = fixture.nativeElement.querySelector('[role="tablist"]');
    tabList.dispatchEvent(new KeyboardEvent('keydown', { key: 'End', bubbles: true }));
    fixture.detectChanges();

    expect(host.lastChange).toEqual({ index: 5, code: 'JIM' });
  });

  it('navigates with Home', () => {
    host.activeIndex = 3;
    fixture.detectChanges();

    const tabList = fixture.nativeElement.querySelector('[role="tablist"]');
    tabList.dispatchEvent(new KeyboardEvent('keydown', { key: 'Home', bubbles: true }));
    fixture.detectChanges();

    expect(host.lastChange).toEqual({ index: 0, code: 'JV' });
  });

  it('marks active tab with aria-selected', () => {
    host.activeIndex = 2;
    fixture.detectChanges();

    const buttons = fixture.nativeElement.querySelectorAll('button[role="tab"]');
    expect(buttons[2].getAttribute('aria-selected')).toBe('true');
    expect(buttons[0].getAttribute('aria-selected')).toBe('false');
  });
});