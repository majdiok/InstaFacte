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
});
