import { Component } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { AccountingTableActionsComponent } from './accounting-table-actions.component';

@Component({
  standalone: true,
  imports: [AccountingTableActionsComponent],
  template: `
    <app-accounting-table-actions>
      <button type="button">Edit</button>
      <button type="button">Toggle</button>
    </app-accounting-table-actions>
  `
})
class HostComponent {}

describe('AccountingTableActionsComponent', () => {
  let fixture: ComponentFixture<HostComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({ imports: [HostComponent] }).compileComponents();
    fixture = TestBed.createComponent(HostComponent);
    fixture.detectChanges();
  });

  it('renders action group with projected buttons', () => {
    const group = fixture.nativeElement.querySelector('.accounting-table-actions') as HTMLElement;
    expect(group).toBeTruthy();
    expect(group.getAttribute('role')).toBe('group');
    expect(group.querySelectorAll('button').length).toBe(2);
  });
});
