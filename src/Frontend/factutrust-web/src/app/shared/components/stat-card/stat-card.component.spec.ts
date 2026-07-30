import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { StatCardComponent } from './stat-card.component';

describe('StatCardComponent', () => {
  let fixture: ComponentFixture<StatCardComponent>;
  let component: StatCardComponent;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [StatCardComponent],
      providers: [provideRouter([])]
    }).compileComponents();

    fixture = TestBed.createComponent(StatCardComponent);
    component = fixture.componentInstance;
    component.label = 'CA';
    component.value = '1000';
    fixture.detectChanges();
  });

  it('should create with default appearance', () => {
    expect(component).toBeTruthy();
    expect(component.appearance).toBe('default');
    expect(fixture.nativeElement.querySelector('.stat-card--solid')).toBeNull();
  });

  it('should apply solid appearance class', () => {
    component.appearance = 'solid';
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('.stat-card--solid')).toBeTruthy();
    expect(fixture.nativeElement.querySelector('.stat-sparkline')).toBeTruthy();
  });

  it('should use black label color in solid appearance', () => {
    component.appearance = 'solid';
    fixture.detectChanges();
    const label = fixture.nativeElement.querySelector('.stat-label') as HTMLElement;
    expect(getComputedStyle(label).color).toBe('rgb(0, 0, 0)');
  });

  it('should not force black label color in default appearance', () => {
    component.appearance = 'default';
    fixture.detectChanges();
    const label = fixture.nativeElement.querySelector('.stat-label') as HTMLElement;
    expect(getComputedStyle(label).color).not.toBe('rgb(0, 0, 0)');
  });
});
