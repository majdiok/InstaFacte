import { ComponentFixture, TestBed } from '@angular/core/testing';
import { BalanceIndicatorComponent } from './balance-indicator.component';

describe('BalanceIndicatorComponent', () => {
  let fixture: ComponentFixture<BalanceIndicatorComponent>;

  function create(debit: number, credit: number, canAutoBalance = false): void {
    fixture = TestBed.createComponent(BalanceIndicatorComponent);
    fixture.componentRef.setInput('totalDebit', debit);
    fixture.componentRef.setInput('totalCredit', credit);
    fixture.componentRef.setInput('canAutoBalance', canAutoBalance);
    fixture.detectChanges();
  }

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [BalanceIndicatorComponent]
    }).compileComponents();
  });

  it('shows neutral empty state when totals are zero', () => {
    create(0, 0);
    const el = fixture.nativeElement as HTMLElement;
    expect(el.textContent).toContain('Saisissez vos lignes');
    expect(el.querySelector('.me-empty')).toBeTruthy();
    expect(el.querySelector('.me-unbalanced')).toBeFalsy();
  });

  it('shows unbalanced state with gap', () => {
    create(100, 50);
    const el = fixture.nativeElement as HTMLElement;
    expect(el.textContent).toContain('Écart');
    expect(el.querySelector('.me-unbalanced')).toBeTruthy();
  });

  it('shows balanced state when debit equals credit and positive', () => {
    create(100, 100);
    const el = fixture.nativeElement as HTMLElement;
    expect(el.textContent).toContain('Écriture équilibrée');
    expect(el.querySelector('.me-balanced')).toBeTruthy();
  });

  it('shows auto-balance button when enabled', () => {
    create(100, 50, true);
    const btn = fixture.nativeElement.querySelector('.me-auto-balance-btn');
    expect(btn).toBeTruthy();
  });
});
