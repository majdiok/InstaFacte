import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { StatCardComponent } from './stat-card.component';

describe('StatCardComponent', () => {
  let fixture: ComponentFixture<StatCardComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [StatCardComponent],
      providers: [provideRouter([])]
    }).compileComponents();

    fixture = TestBed.createComponent(StatCardComponent);
  });

  it('renders a plain card without routerLink', () => {
    fixture.componentInstance.label = 'Test KPI';
    fixture.componentInstance.value = 42;
    fixture.detectChanges();

    const link = fixture.nativeElement.querySelector('a.stat-card-link');
    expect(link).toBeNull();
  });

  it('wraps card in a link when routerLink is set', () => {
    fixture.componentInstance.routerLink = '/reports/analytics/revenue';
    fixture.detectChanges();

    const link = fixture.nativeElement.querySelector('a.stat-card-link');
    expect(link).not.toBeNull();
  });
});