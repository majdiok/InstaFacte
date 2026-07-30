import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-time-sheet-kpi-panel',
  standalone: true,
  imports: [CommonModule],
  template: `
    <aside class="kpi">
      <h3>Répartition de la période</h3>
      <div class="donut-wrap">
        <svg viewBox="0 0 36 36" class="donut">
          <circle cx="18" cy="18" r="15.9" fill="transparent" stroke="#e2e8f0" stroke-width="3.2" />
          <circle
            cx="18" cy="18" r="15.9" fill="transparent"
            stroke="var(--color-primary, #0f766e)"
            stroke-width="3.2"
            [attr.stroke-dasharray]="billableRatio + ' ' + (100 - billableRatio)"
            stroke-dashoffset="25" />
          <circle
            cx="18" cy="18" r="15.9" fill="transparent"
            stroke="#94a3b8"
            stroke-width="3.2"
            [attr.stroke-dasharray]="(100 - billableRatio) + ' ' + billableRatio"
            [attr.stroke-dashoffset]="25 - billableRatio" />
        </svg>
        <div class="legend">
          <div><span class="dot billable"></span> Facturable {{ billableHours | number:'1.2-2' }} h ({{ billableRatio | number:'1.0-0' }} %)</div>
          <div><span class="dot non"></span> Non facturable {{ nonBillableHours | number:'1.2-2' }} h ({{ 100 - billableRatio | number:'1.0-0' }} %)</div>
        </div>
      </div>

      <h3>Informations de la période</h3>
      <ul class="metrics">
        <li><span>Heures ouvrées</span><strong>{{ productiveHours | number:'1.2-2' }} h</strong></li>
        <li><span>Heures saisies</span><strong>{{ totalHours | number:'1.2-2' }} h</strong></li>
        <li>
          <span>Taux d'occupation</span>
          <strong>{{ occupationPercent | number:'1.0-0' }} %</strong>
        </li>
        <li class="bar-row">
          <div class="bar"><div class="fill" [style.width.%]="occupationPercent"></div></div>
        </li>
        <li><span>Heures à valider</span><strong>{{ hoursToValidate | number:'1.2-2' }} h</strong></li>
        <li><span>Heures refusées</span><strong>{{ hoursRejected | number:'1.2-2' }} h</strong></li>
      </ul>
      <p class="hint">Heures ouvrées : approximation settings année (productives / période).</p>
    </aside>
  `,
  styles: [`
    .kpi {
      background: var(--color-surface, #fff);
      border: 1px solid var(--color-border-subtle, #e2e8f0);
      border-radius: var(--radius-xl, 16px);
      padding: .85rem;
    }
    h3 { margin: 0 0 .6rem; font-size: .95rem; }
    h3 + .metrics { margin-top: .25rem; }
    .donut-wrap { display: flex; gap: .75rem; align-items: center; margin-bottom: 1rem; }
    .donut { width: 72px; height: 72px; flex-shrink: 0; }
    .legend { font-size: .8rem; display: flex; flex-direction: column; gap: .35rem; }
    .dot { display: inline-block; width: 8px; height: 8px; border-radius: 50%; margin-right: .35rem; }
    .dot.billable { background: var(--color-primary, #0f766e); }
    .dot.non { background: #94a3b8; }
    .metrics { list-style: none; margin: 0; padding: 0; }
    .metrics li { display: flex; justify-content: space-between; gap: .5rem; font-size: .85rem; margin-bottom: .4rem; }
    .bar-row { display: block !important; }
    .bar { height: 6px; background: #e2e8f0; border-radius: 999px; overflow: hidden; width: 100%; }
    .fill { height: 100%; background: var(--color-primary, #0f766e); }
    .hint { color: #94a3b8; font-size: .72rem; margin: .5rem 0 0; }
  `]
})
export class TimeSheetKpiPanelComponent {
  @Input() totalHours = 0;
  @Input() billableHours = 0;
  @Input() nonBillableHours = 0;
  @Input() billableRatio = 0;
  @Input() productiveHours = 0;
  @Input() occupationPercent = 0;
  @Input() hoursToValidate = 0;
  @Input() hoursRejected = 0;
}
