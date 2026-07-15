import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { ToastService } from '@core/services/toast.service';
import { ForecastingService } from '../../services/forecasting.service';
import { formatLocalDate } from '@core/utils/date.util';
import { CalendarEvent } from '../../models/forecasting.models';

/**
 * Calendrier commercial tunisien : fêtes civiles fixes, religieuses lunaires (Ramadan, Aïd, Mouled,
 * Ras El Am Hijri), périodes commerciales (Soldes hiver/été, Rentrée, Black Friday) et saisons.
 * Source : table embarquée 2024-2030 + règles civiles encodées côté backend.
 */
@Component({
  selector: 'app-calendar-impact',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <section class="calendar-impact">
      <header class="filters">
        <label>
          Du
          <input type="date" [(ngModel)]="from" (change)="load()" />
        </label>
        <label>
          Au
          <input type="date" [(ngModel)]="to" (change)="load()" />
        </label>
      </header>

      @if (loading()) {
        <div class="state">Chargement…</div>
      } @else if (events().length === 0) {
        <div class="state empty">Aucun événement sur la période.</div>
      } @else {
        <ul class="event-list">
          @for (e of events(); track e.code + e.startDate) {
            <li class="event {{ e.category }}" [class.holiday]="e.isHoliday" [class.commercial]="e.isCommercialWindow">
              <div class="dates">
                <span class="from">{{ e.startDate | date:'dd MMM' }}</span>
                @if (e.startDate !== e.endDate) {
                  <span class="sep">→</span>
                  <span class="to">{{ e.endDate | date:'dd MMM yyyy' }}</span>
                } @else {
                  <span class="year">{{ e.startDate | date:'yyyy' }}</span>
                }
              </div>
              <div class="info">
                <h4>{{ e.displayName }}</h4>
                <p class="tags">
                  <span class="tag">{{ e.category }}</span>
                  @if (e.isHoliday) { <span class="tag holiday">Férié</span> }
                  @if (e.isCommercialWindow) { <span class="tag commercial">Fenêtre commerciale</span> }
                </p>
              </div>
            </li>
          }
        </ul>
      }
    </section>
  `,
  styles: [`
    .calendar-impact { display: block; }
    .filters { display: flex; gap: 1rem; margin-bottom: 1rem; flex-wrap: wrap; }
    .filters label { display: flex; flex-direction: column; gap: .25rem; font-size: .85rem; color: var(--color-neutral-600, #6b7280); }
    .filters input { padding: .45rem .6rem; border-radius: 6px; border: 1px solid var(--color-neutral-300, #d1d5db); }
    .state { padding: 1.25rem; color: var(--color-neutral-600, #6b7280); font-style: italic; }
    .state.empty { background: var(--color-neutral-50, #f9fafb); border: 1px dashed var(--color-neutral-300, #d1d5db); border-radius: 8px; }
    .event-list { list-style: none; padding: 0; margin: 0; display: flex; flex-direction: column; gap: .5rem; }
    .event { display: flex; gap: 1.5rem; padding: 1rem 1.25rem; background: white; border: 1px solid var(--color-neutral-200, #e5e7eb); border-left: 4px solid var(--color-neutral-300, #d1d5db); border-radius: 6px; }
    .event.holiday { border-left-color: var(--color-danger-600, #dc2626); }
    .event.commercial { border-left-color: var(--color-primary-600, #2563eb); }
    .dates { min-width: 130px; display: flex; flex-direction: column; gap: .15rem; }
    .dates .from, .dates .to { font-weight: 600; }
    .dates .year, .dates .sep { color: var(--color-neutral-500, #9ca3af); font-size: .85rem; }
    .info { flex: 1; }
    .info h4 { margin: 0 0 .35rem; font-size: 1.05rem; }
    .info .tags { margin: 0; display: flex; gap: .35rem; flex-wrap: wrap; }
    .tag { padding: .15rem .55rem; border-radius: 4px; font-size: .75rem; background: var(--color-neutral-100, #f3f4f6); color: var(--color-neutral-700, #374151); }
    .tag.holiday { background: #fee2e2; color: #b91c1c; }
    .tag.commercial { background: #dbeafe; color: #1e40af; }
  `]
})
export class CalendarImpactComponent implements OnInit {
  private readonly forecastingService = inject(ForecastingService);
  private readonly toast = inject(ToastService);

  from = signal(this.todayIso());
  to = signal(this.dateOffsetIso(90));
  events = signal<CalendarEvent[]>([]);
  loading = signal(false);

  ngOnInit(): void { this.load(); }

  async load() {
    this.loading.set(true);
    try {
      const list = await firstValueFrom(this.forecastingService.getCalendar(this.from(), this.to()));
      this.events.set(list);
    } catch (err: any) {
      this.toast.add({ severity: 'error', summary: 'Erreur', detail: err?.error?.message ?? 'Chargement impossible.' });
      this.events.set([]);
    } finally {
      this.loading.set(false);
    }
  }

  private todayIso(): string {
    return formatLocalDate(new Date());
  }

  private dateOffsetIso(days: number): string {
    const d = new Date();
    d.setDate(d.getDate() + days);
    return formatLocalDate(d);
  }
}
