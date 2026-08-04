import { ComponentFixture, TestBed } from '@angular/core/testing';
import { FormsModule } from '@angular/forms';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { AccountingAmountInputComponent } from './accounting-amount-input.component';

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

  it('emits amountChange on model change', () => {
    const spy = jasmine.createSpy('amountChange');
    component.amountChange.subscribe(spy);
    component.onModelChange(100.5);
    expect(spy).toHaveBeenCalledWith(100.5);
    expect(component.value).toBe(100.5);
  });

  it('normalizes zero to null on model change', () => {
    component.onModelChange(0);
    expect(component.value).toBeNull();
  });

  it('writeValue sets normalized value', () => {
    component.writeValue(70.002);
    expect(component.value).toBe(70.002);
  });

  it('emits enterPressed on Enter key', () => {
    const spy = jasmine.createSpy('enterPressed');
    component.enterPressed.subscribe(spy);
    component.onKeyDown(new KeyboardEvent('keydown', { key: 'Enter' }));
    expect(spy).toHaveBeenCalled();
  });

  it('emits tabFromAmount on Tab key', () => {
    const spy = jasmine.createSpy('tabFromAmount');
    component.tabFromAmount.subscribe(spy);
    component.onKeyDown(new KeyboardEvent('keydown', { key: 'Tab', shiftKey: true }));
    expect(spy).toHaveBeenCalledWith({ shiftKey: true });
  });

  it('uses compact mode without grouping', () => {
    component.compact = true;
    expect(component.useGrouping).toBe(false);
  });

  it('applies side-specific style class', () => {
    component.side = 'credit';
    expect(component.styleClass).toContain('accounting-amount-input--credit');
  });
});
