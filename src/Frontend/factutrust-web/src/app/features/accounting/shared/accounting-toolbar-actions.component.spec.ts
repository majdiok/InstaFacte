import { Component } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { AccountingToolbarActionsComponent } from './accounting-toolbar-actions.component';

@Component({
  standalone: true,
  imports: [AccountingToolbarActionsComponent],
  template: `
    <app-accounting-toolbar-actions>
      <button type="button">Primary</button>
      <button type="button">IA</button>
    </app-accounting-toolbar-actions>
  `
})
class HostComponent {}

describe('AccountingToolbarActionsComponent', () => {
  let fixture: ComponentFixture<HostComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({ imports: [HostComponent] }).compileComponents();
    fixture = TestBed.createComponent(HostComponent);
    fixture.detectChanges();
  });

  it('renders action group with projected buttons', () => {
    const group = fixture.nativeElement.querySelector('.accounting-toolbar-actions') as HTMLElement;
    expect(group).toBeTruthy();
    expect(group.getAttribute('role')).toBe('group');
    expect(group.querySelectorAll('button').length).toBe(2);
  });
});
