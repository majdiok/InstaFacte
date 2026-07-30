import { Component, EventEmitter, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ButtonModule } from 'primeng/button';
import { FirmTimeSheetEntry } from '@core/services/firm-governance.service';

@Component({
  selector: 'app-time-sheet-week-view',
  standalone: true,
  imports: [CommonModule, ButtonModule],
  template: `
    <div class="fc-card">
      <div class="week-head">
        <strong>{{ mode === 'week' ? 'Vue semaine' : 'Vue jour' }}</strong>
      </div>
      @if (mode === 'day') {
        @if (focusedDate; as date) {
          <div class="day-card">
            <div class="line">
              <strong>{{ date | date:'EEEE dd/MM' }}</strong>
              <span>{{ sumHours(date) | number:'1.2-2' }} h</span>
            </div>
            <button type="button" pButton class="p-button-sm p-button-outlined" label="+ 1h rapide" (click)="quickAdd.emit({ date, hours: 1 })"></button>
          </div>
        }
      } @else {
        <div class="week-grid">
          @for (date of weekDays; track date) {
            <div class="day-card" (click)="focusDate.emit(date)">
              <div class="line">
                <strong>{{ date | date:'EEE dd/MM' }}</strong>
                <span>{{ sumHours(date) | number:'1.2-2' }} h</span>
              </div>
              <small>{{ countEntries(date) }} ligne(s)</small>
              <button type="button" pButton class="p-button-sm p-button-text" label="+ 1h" (click)="quickAdd.emit({ date, hours: 1 }); $event.stopPropagation()"></button>
            </div>
          }
        </div>
      }
    </div>
  `,
  styles: [`
    .fc-card {
      background: var(--color-surface, #fff);
      border: 1px solid var(--color-border-subtle, #e2e8f0);
      border-radius: var(--radius-xl, 16px);
      box-shadow: var(--shadow-soft-sm, 0 1px 2px rgba(15, 23, 42, 0.05));
      padding: var(--spacing-3, 12px);
      margin-bottom: 1rem;
    }
    .week-head { margin-bottom: .75rem; }
    .week-grid { display: grid; gap: .5rem; grid-template-columns: repeat(auto-fit, minmax(160px, 1fr)); }
    .day-card { border: 1px solid var(--color-border-subtle, #e2e8f0); border-radius: 10px; padding: .6rem; background: #fafafa; cursor: pointer; }
    .day-card .line { display: flex; justify-content: space-between; align-items: center; margin-bottom: .35rem; }
    .day-card small { color: var(--color-text-muted, #64748b); }
  `]
})
export class TimeSheetWeekViewComponent {
  @Input() mode: 'week' | 'day' = 'week';
  @Input() focusedDate = '';
  @Input() weekDays: string[] = [];
  @Input() entriesByDate = new Map<string, FirmTimeSheetEntry[]>();
  @Output() quickAdd = new EventEmitter<{ date: string; hours: number }>();
  @Output() focusDate = new EventEmitter<string>();

  sumHours(date: string): number {
    return (this.entriesByDate.get(date) ?? []).reduce((sum, row) => sum + row.hours, 0);
  }

  countEntries(date: string): number {
    return this.entriesByDate.get(date)?.length ?? 0;
  }
}
