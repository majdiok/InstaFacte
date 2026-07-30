import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FirmTimeSheetEntry } from '@core/services/firm-governance.service';
import { activityPastelColor } from './time-sheet-activity-color';

@Component({
  selector: 'app-time-sheet-activity-legend',
  standalone: true,
  imports: [CommonModule],
  template: `
    <aside class="legend">
      <h3>Activités</h3>
      @for (row of rows; track row.code) {
        <div class="row">
          <span class="swatch" [style.background]="row.color"></span>
          <span class="code" [title]="row.code">{{ row.label }}</span>
          <strong>{{ row.hours | number:'1.2-2' }} h</strong>
        </div>
      }
      @if (rows.length) {
        <div class="total">Total : <strong>{{ totalHours | number:'1.2-2' }} h</strong></div>
      }
      @if (!rows.length) {
        <p class="empty">Aucune saisie filtrée.</p>
      }
    </aside>
  `,
  styles: [`
    .legend {
      background: var(--color-surface, #fff);
      border: 1px solid var(--color-border-subtle, #e2e8f0);
      border-radius: var(--radius-xl, 16px);
      padding: .85rem;
    }
    h3 { margin: 0 0 .6rem; font-size: .95rem; }
    .row { display: grid; grid-template-columns: 12px 1fr auto; gap: .45rem; align-items: center; margin-bottom: .4rem; font-size: .85rem; }
    .swatch { width: 12px; height: 12px; border-radius: 3px; }
    .code { color: #475569; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
    .total { margin-top: .5rem; padding-top: .5rem; border-top: 1px solid #e2e8f0; font-size: .85rem; }
    .empty { color: #94a3b8; font-size: .85rem; margin: 0; }
  `]
})
export class TimeSheetActivityLegendComponent {
  @Input() entries: FirmTimeSheetEntry[] = [];
  @Input() activityCodes: { code: string; label: string }[] = [];

  get totalHours(): number {
    return this.entries.reduce((s, e) => s + e.hours, 0);
  }

  get rows(): { code: string; label: string; hours: number; color: string }[] {
    const map = new Map<string, number>();
    for (const e of this.entries) {
      const code = e.activityCode || 'Sans code';
      map.set(code, (map.get(code) ?? 0) + e.hours);
    }
    return [...map.entries()]
      .map(([code, hours]) => ({
        code,
        label: this.activityCodes.find(a => a.code === code)?.label ?? code,
        hours,
        color: this.colorFor(code)
      }))
      .sort((a, b) => b.hours - a.hours);
  }

  private colorFor(code: string): string {
    return activityPastelColor(code === 'Sans code' ? undefined : code);
  }
}
