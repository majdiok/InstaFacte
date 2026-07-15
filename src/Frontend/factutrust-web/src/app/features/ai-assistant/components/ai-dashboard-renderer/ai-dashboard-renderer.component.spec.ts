import { Router } from '@angular/router';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { AiDashboardRendererComponent } from './ai-dashboard-renderer.component';
import { DashboardConfig } from '../../models/ai-chat.models';

/**
 * Non-régression : un KPI sans `data` ou sans `trend` (sortie variable de generate_dashboard_config)
 * ne doit pas lever « Cannot read properties of undefined (reading 'trend') ». La tendance ne
 * s'affiche que pour un KPI dont le trend est un nombre.
 */
describe('AiDashboardRendererComponent (KPI trend null-safety)', () => {
  let fixture: ComponentFixture<AiDashboardRendererComponent>;
  let component: AiDashboardRendererComponent;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [AiDashboardRendererComponent],
      providers: [{ provide: Router, useValue: {} }]
    }).compileComponents();

    fixture = TestBed.createComponent(AiDashboardRendererComponent);
    component = fixture.componentInstance;
  });

  it('renders KPI cards without throwing when data/trend are missing', () => {
    const config: DashboardConfig = {
      title: 'Tableau de bord des ventes',
      sections: [
        { type: 'kpi_card', title: 'Avec tendance', data: { value: 100, unit: 'TND', trend: 12.5 } },
        { type: 'kpi_card', title: 'Sans tendance', data: { value: 50, unit: 'TND' } },
        // KPI sans `data` du tout — c'est ce cas qui plantait avant le correctif.
        { type: 'kpi_card', title: 'Sans data' } as unknown as DashboardConfig['sections'][number]
      ]
    };
    component.config = config;

    expect(() => fixture.detectChanges()).not.toThrow();

    const host = fixture.nativeElement as HTMLElement;
    expect(host.querySelectorAll('.kpi-card').length).toBe(3);
    // Seul le KPI au trend numérique affiche le bloc tendance.
    expect(host.querySelectorAll('.kpi-trend').length).toBe(1);
  });

  it('does not render a trend block when trend is null', () => {
    component.config = {
      title: 'T',
      sections: [{ type: 'kpi_card', title: 'X', data: { value: 1, trend: null } }]
    } as unknown as DashboardConfig;

    expect(() => fixture.detectChanges()).not.toThrow();
    expect((fixture.nativeElement as HTMLElement).querySelectorAll('.kpi-trend').length).toBe(0);
  });
});
