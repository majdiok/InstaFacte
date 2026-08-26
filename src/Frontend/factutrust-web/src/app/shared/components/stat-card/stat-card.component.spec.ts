import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { DECORATIVE_SPARKLINE_POINTS, StatCardComponent } from './stat-card.component';

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

  it('should not render a sparkline in default appearance', () => {
    component.appearance = 'default';
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('.stat-sparkline')).toBeNull();
  });

  it('should keep the decorative polyline when solid and sparkline is omitted', () => {
    component.appearance = 'solid';
    fixture.detectChanges();
    const polyline = fixture.nativeElement.querySelector('.stat-sparkline polyline') as SVGPolylineElement | null;
    expect(polyline).toBeTruthy();
    expect(polyline!.getAttribute('points')).toBe(DECORATIVE_SPARKLINE_POINTS);
  });

  it('should render a data-driven path when a series is provided', () => {
    component.appearance = 'solid';
    component.sparkline = [1, 3, 2];
    fixture.detectChanges();
    const svg = fixture.nativeElement.querySelector('.stat-sparkline') as SVGElement | null;
    const paths = fixture.nativeElement.querySelectorAll('.stat-sparkline path');
    expect(svg).toBeTruthy();
    expect(paths.length).toBe(2);
    expect(paths[1].getAttribute('d')).toContain('M ');
    expect(paths[1].getAttribute('d')).toContain(' L ');
    expect(fixture.nativeElement.querySelector('.stat-sparkline polyline')).toBeNull();
    expect(paths[1].getAttribute('d')).not.toContain(DECORATIVE_SPARKLINE_POINTS);
  });

  it('should hide the sparkline when sparkline is explicitly null', () => {
    component.appearance = 'solid';
    component.sparkline = null;
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('.stat-sparkline')).toBeNull();
  });

  it('should hide the sparkline when the series is too short', () => {
    component.appearance = 'solid';
    component.sparkline = [42];
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('.stat-sparkline')).toBeNull();
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

  it('should render a long monetary value in stat-value', () => {
    component.value = '12 345 678,999';
    fixture.detectChanges();
    const valueEl = fixture.nativeElement.querySelector('.stat-value') as HTMLElement;
    expect(valueEl.textContent?.trim()).toBe('12 345 678,999');
  });

  it('should apply featured class and clamp-friendly value styling', () => {
    component.featured = true;
    component.value = '1 010,497';
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('.stat-card--featured')).toBeTruthy();
    const valueEl = fixture.nativeElement.querySelector('.stat-value') as HTMLElement;
    expect(valueEl.textContent?.trim()).toBe('1 010,497');
  });

  it('should set title attribute when valueTitle is provided', () => {
    component.value = '1 010,497';
    component.valueTitle = '1 010,497 TND';
    fixture.detectChanges();
    const valueEl = fixture.nativeElement.querySelector('.stat-value') as HTMLElement;
    expect(valueEl.getAttribute('title')).toBe('1 010,497 TND');
  });
});
