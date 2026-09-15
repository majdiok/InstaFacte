import { ComponentFixture, TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { provideRouter, Router } from '@angular/router';
import { StudioCalendarComponent } from './studio-calendar.component';
import { RecordViewCalendar, RecordViewCalendarEventDto } from './studio-record-views.models';
import { CustomField } from '../studio.models';

const calendar: RecordViewCalendar = {
  startFieldKey: 'debut',
  endFieldKey: 'fin',
  titleFieldKey: 'nom',
  colorFieldKey: 'statut'
};

const fields: CustomField[] = [
  { id: 'f1', key: 'statut', label: 'Statut', fieldType: 7, isRequired: false, isUnique: false, sortOrder: 0, rules: null, options: [{ value: 'a', label: 'À planifier' }], relation: null, isActive: true }
];

function events(): RecordViewCalendarEventDto[] {
  const today = new Date();
  const iso = today.toISOString().slice(0, 10);
  return [
    { recordId: 'r1', title: 'Réunion', start: iso, end: null, colorValue: 'a' },
    { recordId: 'r2', title: 'Chantier', start: iso, end: iso, colorValue: 'a' }
  ];
}

describe('StudioCalendarComponent', () => {
  let fixture: ComponentFixture<StudioCalendarComponent>;
  let component: StudioCalendarComponent;
  let router: Router;

  function setup(autoDetect = true): void {
    TestBed.configureTestingModule({
      imports: [StudioCalendarComponent],
      providers: [provideRouter([]), provideNoopAnimations()]
    });
    router = TestBed.inject(Router);
    spyOn(router, 'navigate').and.resolveTo(true);
    fixture = TestBed.createComponent(StudioCalendarComponent);
    component = fixture.componentInstance;
    fixture.componentRef.setInput('entityKey', 'interventions');
    fixture.componentRef.setInput('calendar', calendar);
    fixture.componentRef.setInput('events', events());
    fixture.componentRef.setInput('allFields', fields);
    fixture.componentRef.setInput('truncated', false);
    if (autoDetect) fixture.detectChanges();
  }

  it('affiche les événements du jour courant', () => {
    setup();
    const evButtons = fixture.debugElement.queryAll(By.css('.cal-ev'));
    expect(evButtons.length).toBeGreaterThan(0);
    expect(evButtons.some(e => e.nativeElement.textContent.includes('Réunion'))).toBe(true);
  });

  it("émet rangeChange avec des bornes ISO à l'initialisation", () => {
    setup(false);
    let emitted: { rangeStart: string; rangeEnd: string } | undefined;
    component.rangeChange.subscribe(v => emitted = v);
    fixture.detectChanges();
    expect(emitted?.rangeStart).toMatch(/^\d{4}-\d{2}-\d{2}$/);
    expect(emitted?.rangeEnd).toMatch(/^\d{4}-\d{2}-\d{2}$/);
  });

  it('bascule entre mois et semaine', () => {
    setup();
    expect(component['effectiveMode']()).toBe('month');
    component.setMode('week');
    fixture.detectChanges();
    expect(component['effectiveMode']()).toBe('week');
  });

  it('force la semaine sous 768px même si le mode choisi est « mois »', () => {
    setup();
    component['narrow'].set(true);
    fixture.detectChanges();
    expect(component['effectiveMode']()).toBe('week');
  });

  it("ouvre le popover au clic sur un événement puis navigue via « Ouvrir »", () => {
    setup();
    const evButton = fixture.debugElement.queryAll(By.css('.cal-ev'))[0];
    evButton.nativeElement.click();
    fixture.detectChanges();
    const selected = component['selectedEvent']();
    expect(selected?.recordId).toBe('r1');
    component.openRecord();
    expect(router.navigate).toHaveBeenCalledWith(['/studio/d', 'interventions', 'r1', 'edit']);
  });

  it('affiche la bannière de troncature quand truncated=true', () => {
    setup();
    fixture.componentRef.setInput('truncated', true);
    fixture.detectChanges();
    expect(fixture.debugElement.query(By.css('.runner-banner'))).toBeTruthy();
  });

  it('affiche la légende avec les libellés résolus via les options du champ', () => {
    setup();
    const legendItems = fixture.debugElement.queryAll(By.css('.cal__legend-item'));
    expect(legendItems.length).toBe(1);
    expect(legendItems[0].nativeElement.textContent).toContain('À planifier');
  });
});
