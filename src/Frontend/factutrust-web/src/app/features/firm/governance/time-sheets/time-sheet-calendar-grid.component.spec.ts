import { ComponentFixture, TestBed } from '@angular/core/testing';
import { TimeSheetCalendarGridComponent } from './time-sheet-calendar-grid.component';
import { FirmTimeSheetEntry } from '@core/services/firm-governance.service';
import { activityPastelColor } from './time-sheet-activity-color';

describe('TimeSheetCalendarGridComponent', () => {
  let fixture: ComponentFixture<TimeSheetCalendarGridComponent>;
  let component: TimeSheetCalendarGridComponent;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [TimeSheetCalendarGridComponent]
    }).compileComponents();
    fixture = TestBed.createComponent(TimeSheetCalendarGridComponent);
    component = fixture.componentInstance;
    component.mode = 'week';
    component.weekDays = ['2026-07-20', '2026-07-21', '2026-07-22', '2026-07-23', '2026-07-24', '2026-07-25', '2026-07-26'];
    component.focusedDate = '2026-07-20';
    component.activityCodes = [{ id: '1', code: 'COMPTA', label: 'Compta', isBillableByDefault: true } as never];
    fixture.detectChanges();
  });

  it('place un bloc absolu top/height selon le créneau', () => {
    const entry = {
      id: 'e1',
      workDate: '2026-07-20',
      startTime: '09:00',
      endTime: '11:00',
      hours: 2,
      activityCode: 'COMPTA',
      status: 0,
      isValidated: false
    } as FirmTimeSheetEntry;
    component.entries = [entry];
    const blocks = component.blocksFor('2026-07-20');
    expect(blocks.length).toBe(1);
    expect(blocks[0].top).toBe(48); // 09:00 → 1h * 48px
    expect(blocks[0].height).toBe(96); // 2h
    expect(blocks[0].color).toBe(activityPastelColor('COMPTA'));
  });

  it('marque les weekends', () => {
    expect(component.isWeekend('2026-07-25')).toBeTrue(); // samedi
    expect(component.isWeekend('2026-07-26')).toBeTrue(); // dimanche
    expect(component.isWeekend('2026-07-20')).toBeFalse();
  });

  it('émet createRange sans créer côté parent (pas d’API)', () => {
    const spy = jasmine.createSpy('createRange');
    component.createRange.subscribe(spy);
    component.createRange.emit({ date: '2026-07-20', startTime: '10:00', endTime: '10:30' });
    expect(spy).toHaveBeenCalledWith({ date: '2026-07-20', startTime: '10:00', endTime: '10:30' });
  });

  it('rend les colonnes jour et la colonne Total', () => {
    fixture.detectChanges();
    const el: HTMLElement = fixture.nativeElement;
    expect(el.querySelectorAll('.day-column').length).toBe(7);
    expect(el.querySelector('.total-column')).toBeTruthy();
    expect(el.querySelector('.total-head')).toBeTruthy();
  });
});
