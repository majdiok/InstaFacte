import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivityCodePickerComponent } from './activity-code-picker.component';
import { FirmActivityCode } from '@core/services/firm-governance.service';
import { provideRouter } from '@angular/router';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';

describe('ActivityCodePickerComponent', () => {
  let fixture: ComponentFixture<ActivityCodePickerComponent>;
  let component: ActivityCodePickerComponent;

  const codes: FirmActivityCode[] = [
    { id: '1', code: 'TENUE', label: 'Tenue comptable / saisie', category: 1, categoryDisplay: 'Comptabilité', isBillableByDefault: true, defaultUnitPrice: 150, isActive: true, sortOrder: 10 },
    { id: '2', code: 'FISC-M', label: 'Déclarations mensuelles', category: 2, categoryDisplay: 'Fiscal', isBillableByDefault: true, defaultUnitPrice: null, isActive: true, sortOrder: 20 }
  ];

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ActivityCodePickerComponent, NoopAnimationsModule],
      providers: [provideRouter([])]
    }).compileComponents();

    fixture = TestBed.createComponent(ActivityCodePickerComponent);
    component = fixture.componentInstance;
    component.codes = codes;
    fixture.detectChanges();
  });

  it('groups codes by categoryDisplay', () => {
    expect(component.groups.length).toBe(2);
    expect(component.groups.map(g => g.label)).toContain('Comptabilité');
    expect(component.groups.flatMap(g => g.items.map(i => i.code))).toContain('TENUE');
  });

  it('emits code, label and defaultUnitPrice on select', () => {
    const spy = jasmine.createSpy('codeChange');
    component.codeChange.subscribe(spy);
    component.onSelect('TENUE');
    expect(spy).toHaveBeenCalledWith({
      code: 'TENUE',
      label: 'Tenue comptable / saisie',
      defaultUnitPrice: 150
    });
  });

  it('emits null price when code has no tariff', () => {
    const spy = jasmine.createSpy('codeChange');
    component.codeChange.subscribe(spy);
    component.onSelect('FISC-M');
    expect(spy).toHaveBeenCalledWith({
      code: 'FISC-M',
      label: 'Déclarations mensuelles',
      defaultUnitPrice: null
    });
  });

  it('emits null when cleared', () => {
    const spy = jasmine.createSpy('codeChange');
    component.codeChange.subscribe(spy);
    component.onSelect(null);
    expect(spy).toHaveBeenCalledWith({ code: null, label: '', defaultUnitPrice: null });
  });
});
