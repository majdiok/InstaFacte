import { ComponentFixture, TestBed } from '@angular/core/testing';
import { FormsModule } from '@angular/forms';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { AccountingAmountInputComponent } from './accounting-amount-input.component';
import { ACCOUNTING_AMOUNT_FRACTION_DIGITS } from './accounting-amount.utils';

describe('AccountingAmountInputComponent', () => {
  let fixture: ComponentFixture<AccountingAmountInputComponent>;
  let component: AccountingAmountInputComponent;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [AccountingAmountInputComponent, FormsModule],
      providers: [provideNoopAnimations()]
    }).compileComponents();

    fixture = TestBed.createComponent(AccountingAmountInputComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('creates component with p-inputNumber', () => {
    expect(fixture.nativeElement.querySelector('p-inputnumber, p-inputNumber')).toBeTruthy();
  });

  it('emits amountChange on model change while focused without forcing 3 decimals', () => {
    const spy = jasmine.createSpy('amountChange');
    component.amountChange.subscribe(spy);
    component.onFocus();
    expect(component.minFractionDigits).toBe(0);
    expect(component.maxFractionDigits).toBe(ACCOUNTING_AMOUNT_FRACTION_DIGITS);

    component.onModelChange(4);
    expect(spy).toHaveBeenCalledWith(4);
    expect(component.value).toBe(4);

    component.onModelChange(40);
    expect(spy).toHaveBeenCalledWith(40);
    expect(component.value).toBe(40);
    expect(component.minFractionDigits).toBe(0);
  });

  it('does not normalize zero to null while focused', () => {
    component.onFocus();
    component.onModelChange(0);
    expect(component.value).toBe(0);
  });

  it('normalizes and emits amountCommitted on blur', () => {
    const committed = jasmine.createSpy('amountCommitted');
    component.amountCommitted.subscribe(committed);
    component.onFocus();
    component.onModelChange(10.0004);
    component.onBlur();
    expect(component.isFocused).toBeFalse();
    expect(component.minFractionDigits).toBe(ACCOUNTING_AMOUNT_FRACTION_DIGITS);
    expect(component.value).toBe(10);
    expect(committed).toHaveBeenCalledWith(10);
  });

  it('normalizes zero to null on blur', () => {
    component.onFocus();
    component.onModelChange(0);
    component.onBlur();
    expect(component.value).toBeNull();
  });

  it('writeValue sets normalized value', () => {
    component.writeValue(70.002);
    expect(component.value).toBe(70.002);
  });

  it('emits enterPressed and commits on Enter key', () => {
    const enterSpy = jasmine.createSpy('enterPressed');
    const committed = jasmine.createSpy('amountCommitted');
    component.enterPressed.subscribe(enterSpy);
    component.amountCommitted.subscribe(committed);
    component.onFocus();
    component.onModelChange(100.5);
    component.onKeyDown(new KeyboardEvent('keydown', { key: 'Enter' }));
    expect(enterSpy).toHaveBeenCalled();
    expect(committed).toHaveBeenCalledWith(100.5);
    expect(component.isFocused).toBeFalse();
  });

  it('emits tabFromAmount and commits on Tab key', () => {
    const spy = jasmine.createSpy('tabFromAmount');
    const committed = jasmine.createSpy('amountCommitted');
    component.tabFromAmount.subscribe(spy);
    component.amountCommitted.subscribe(committed);
    component.onFocus();
    component.onModelChange(12.5);
    component.onKeyDown(new KeyboardEvent('keydown', { key: 'Tab', shiftKey: true }));
    expect(spy).toHaveBeenCalledWith({ shiftKey: true });
    expect(committed).toHaveBeenCalledWith(12.5);
  });

  it('uses compact mode without grouping', () => {
    component.compact = true;
    expect(component.useGrouping).toBe(false);
  });

  it('applies side-specific style class', () => {
    component.side = 'credit';
    expect(component.styleClass).toContain('accounting-amount-input--credit');
  });

  describe('décimales de la devise', () => {
    it('reste au millime sans entrée explicite', () => {
      // Non-régression des écrans en devise de tenue, qui ne passent rien.
      expect(component.fractionDigits).toBe(ACCOUNTING_AMOUNT_FRACTION_DIGITS);
      expect(component.maxFractionDigits).toBe(ACCOUNTING_AMOUNT_FRACTION_DIGITS);
      expect(component.minFractionDigits).toBe(ACCOUNTING_AMOUNT_FRACTION_DIGITS);
    });

    it('borne à deux décimales quand la devise en compte deux', () => {
      component.fractionDigits = 2;

      expect(component.maxFractionDigits).toBe(2);
      expect(component.minFractionDigits).toBe(2);
    });

    it('laisse la frappe libre pendant la saisie, quelle que soit la devise', () => {
      component.fractionDigits = 2;
      component.onFocus();

      expect(component.minFractionDigits).toBe(0);
      expect(component.maxFractionDigits).toBe(2);
    });
  });
});
