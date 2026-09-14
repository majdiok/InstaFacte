import { ComponentFixture, TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { StudioViewSwitcherComponent } from './studio-view-switcher.component';
import { CustomRecordViewDto, RecordViewDefinition } from './studio-record-views.models';

const definition: RecordViewDefinition = {
  columns: [],
  filters: [],
  sort: [],
  searchEnabled: true,
  pageSize: 25
};

function view(id: string, displayName: string, isDefault = false): CustomRecordViewDto {
  return {
    id,
    key: id,
    displayName,
    mode: 'List',
    definition,
    isDefault,
    isActive: true,
    rowVersion: 'AAA',
    updatedAt: '2026-01-01T00:00:00Z'
  };
}

describe('StudioViewSwitcherComponent', () => {
  let fixture: ComponentFixture<StudioViewSwitcherComponent>;
  let component: StudioViewSwitcherComponent;

  beforeEach(async () => {
    await TestBed.configureTestingModule({ imports: [StudioViewSwitcherComponent] }).compileComponents();
    fixture = TestBed.createComponent(StudioViewSwitcherComponent);
    component = fixture.componentInstance;
  });

  it('affiche l’onglet « Liste » plus un onglet par vue enregistrée (≤ 6 vues)', () => {
    fixture.componentRef.setInput('views', [view('v1', 'Actifs'), view('v2', 'Archivés', true)]);
    fixture.detectChanges();

    const tabs = fixture.debugElement.queryAll(By.css('.view-switcher__tab'));
    expect(tabs.length).toBe(3);
    expect(tabs[0].nativeElement.textContent).toContain('Liste');
    expect(tabs[1].nativeElement.textContent).toContain('Actifs');
    expect(tabs[2].nativeElement.textContent).toContain('Archivés');
  });

  it('affiche une étoile sur la vue par défaut', () => {
    fixture.componentRef.setInput('views', [view('v1', 'Actifs', true)]);
    fixture.detectChanges();

    const stars = fixture.debugElement.queryAll(By.css('.view-switcher__star'));
    expect(stars.length).toBe(1);
  });

  it('marque l’onglet actif via aria-selected', () => {
    fixture.componentRef.setInput('views', [view('v1', 'Actifs')]);
    fixture.componentRef.setInput('activeId', 'v1');
    fixture.detectChanges();

    const tabs = fixture.debugElement.queryAll(By.css('.view-switcher__tab'));
    expect(tabs[0].attributes['aria-selected']).toBe('false');
    expect(tabs[1].attributes['aria-selected']).toBe('true');
  });

  it('émet activeIdChange au clic sur un onglet, sans réémettre si déjà actif', () => {
    fixture.componentRef.setInput('views', [view('v1', 'Actifs')]);
    fixture.componentRef.setInput('activeId', null);
    fixture.detectChanges();

    const emitted: (string | null)[] = [];
    component.activeIdChange.subscribe(id => emitted.push(id));

    let tabs = fixture.debugElement.queryAll(By.css('.view-switcher__tab'));
    tabs[1].nativeElement.click(); // v1 ≠ activeId (null) → émission
    fixture.detectChanges();

    fixture.componentRef.setInput('activeId', 'v1'); // le parent répercute le changement
    fixture.detectChanges();
    tabs = fixture.debugElement.queryAll(By.css('.view-switcher__tab'));
    tabs[1].nativeElement.click(); // déjà actif → pas de réémission
    tabs[0].nativeElement.click(); // Liste ≠ activeId ('v1') → émission

    expect(emitted).toEqual(['v1', null]);
  });

  it('bascule sur un p-select au-delà de 6 vues', () => {
    const many = Array.from({ length: 7 }, (_, i) => view(`v${i}`, `Vue ${i}`));
    fixture.componentRef.setInput('views', many);
    fixture.detectChanges();

    expect(fixture.debugElement.query(By.css('.view-switcher'))).toBeNull();
    expect(fixture.debugElement.query(By.css('p-select'))).not.toBeNull();
  });
});
