import { ComponentFixture, TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { DismissReasonModalComponent } from './dismiss-reason-modal.component';

describe('DismissReasonModalComponent', () => {
  let fixture: ComponentFixture<DismissReasonModalComponent>;
  let component: DismissReasonModalComponent;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [DismissReasonModalComponent]
    }).compileComponents();

    fixture = TestBed.createComponent(DismissReasonModalComponent);
    component = fixture.componentInstance;
    fixture.componentRef.setInput('productName', 'Produit Test');
    fixture.detectChanges();
  });

  it('starts with a standard code selected and can confirm immediately', () => {
    expect(component.selectedCode()).toBe('SupplierUnavailable');
    expect(component.canConfirm()).toBe(true);
  });

  it('emits the label for a standard code', () => {
    spyOn(component.confirm, 'emit');
    component.onConfirm();
    expect(component.confirm.emit).toHaveBeenCalledWith('Fournisseur indisponible');
  });

  it('requires a non-empty custom text when "Other" is selected (fix F-M7)', () => {
    spyOn(component.confirm, 'emit');
    component.onSelectCode('Other');
    expect(component.canConfirm()).toBe(false);

    component.onConfirm();
    expect(component.confirm.emit).not.toHaveBeenCalled();
    expect(component.showValidationError()).toBe(true);
  });

  it('accepts a typed custom reason and emits its trimmed value', () => {
    spyOn(component.confirm, 'emit');
    component.onSelectCode('Other');
    component.customText.set('   Stock alternatif chez supplier B  ');
    expect(component.canConfirm()).toBe(true);

    component.onConfirm();
    expect(component.confirm.emit).toHaveBeenCalledWith('Stock alternatif chez supplier B');
  });

  it('clears custom text when switching back from Other', () => {
    component.onSelectCode('Other');
    component.customText.set('something');
    component.onSelectCode('BudgetExhausted');
    expect(component.customText()).toBe('');
  });

  it('emits cancel on Escape', () => {
    spyOn(component.cancel, 'emit');
    component.onEscape();
    expect(component.cancel.emit).toHaveBeenCalled();
  });

  it('emits cancel when backdrop is clicked', () => {
    spyOn(component.cancel, 'emit');
    const backdrop = fixture.debugElement.query(By.css('.modal-backdrop'));
    backdrop.triggerEventHandler('click', null);
    expect(component.cancel.emit).toHaveBeenCalled();
  });

  it('does NOT emit cancel when the panel is clicked (stopPropagation)', () => {
    spyOn(component.cancel, 'emit');
    const panel = fixture.debugElement.query(By.css('.modal-panel'));
    panel.triggerEventHandler('click', { stopPropagation: () => {} });
    expect(component.cancel.emit).not.toHaveBeenCalled();
  });

  it('exposes role="dialog" and aria-modal for accessibility', () => {
    const panel = fixture.debugElement.query(By.css('.modal-panel')).nativeElement as HTMLElement;
    expect(panel.getAttribute('role')).toBe('dialog');
    expect(panel.getAttribute('aria-modal')).toBe('true');
    expect(panel.getAttribute('aria-labelledby')).toBe('dismiss-modal-title');
  });
});
