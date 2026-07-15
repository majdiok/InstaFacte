import { Component } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { AccountingFilterBarComponent } from './accounting-filter-bar.component';

@Component({
  standalone: true,
  imports: [AccountingFilterBarComponent],
  template: `
    <app-accounting-filter-bar ariaLabel="Test">
      <div accountingFilterFields>Fields</div>
      <div accountingFilterActions>Actions</div>
    </app-accounting-filter-bar>
  `
})
class HostComponent {}

describe('AccountingFilterBarComponent', () => {
  let fixture: ComponentFixture<HostComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({ imports: [HostComponent] }).compileComponents();
    fixture = TestBed.createComponent(HostComponent);
    fixture.detectChanges();
  });

  it('projects fields and actions slots', () => {
    const el = fixture.nativeElement as HTMLElement;
    expect(el.textContent).toContain('Fields');
    expect(el.textContent).toContain('Actions');
    expect(el.querySelector('.accounting-filter-bar__fields')).toBeTruthy();
    expect(el.querySelector('.accounting-filter-bar__actions')).toBeTruthy();
  });
});
