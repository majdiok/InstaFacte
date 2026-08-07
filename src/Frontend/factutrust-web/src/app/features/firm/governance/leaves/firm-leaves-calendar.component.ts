import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { SelectModule } from 'primeng/select';
import { FirmLeavesService } from './data-access/firm-leaves.service';
import { FirmLeaveCalendarEntry } from './data-access/firm-leaves.models';

@Component({
  selector: 'app-firm-leaves-calendar',
  standalone: true,
  imports: [CommonModule, FormsModule, ButtonModule, SelectModule],
  template: `
    <div class="controls">
      <button type="button" pButton icon="pi pi-chevron-left" class="p-button-text" (click)="shift(-1)"></button>
      <button type="button" pButton label="Aujourd'hui" class="p-button-outlined p-button-sm" (click)="goToday()"></button>
      <button type="button" pButton icon="pi pi-chevron-right" class="p-button-text" (click)="shift(1)"></button>
      <strong>{{ rangeLabel() }}</strong>
      <p-select [(ngModel)]="mode" [options]="modes" (onChange)="reload()" />
    </div>

    <div class="fc-card cal" [style.--cols]="days().length">
      <div class="head-row">
        <div class="name-col">Collaborateur</div>
        @for (d of days(); track d.toISOString()) {
          <div class="day-col" [class.weekend]="isWeekend(d)">
            <span>{{ d | date:'EEE' }}</span>
            <strong>{{ d | date:'d' }}</strong>
          </div>
        }
      </div>
      @for (row of rows(); track row.userId) {
        <div class="body-row">
          <div class="name-col">
            <strong>{{ row.name }}</strong>
            <small>{{ row.qualification || '—' }}</small>
          </div>
          @for (d of days(); track d.toISOString()) {
            <div class="day-col" [class.weekend]="isWeekend(d)">
              @for (e of cells(row.userId, d); track e.requestId) {
                <div class="bar" [style.background]="e.colorHex" [title]="e.leaveTypeLabel + ' · ' + e.days + ' j.'"></div>
              }
            </div>
          }
        </div>
      }
      @if (rows().length === 0) {
        <p class="empty">Aucune absence sur cette période.</p>
      }
    </div>
  `,
  styles: [`
    .controls { display:flex; flex-wrap:wrap; gap:.5rem; align-items:center; margin-bottom:.75rem; }
    .fc-card { background:#fff; border:1px solid #e2e8f0; border-radius:16px; overflow:auto; }
    .head-row, .body-row { display:grid; grid-template-columns: 180px repeat(var(--cols), minmax(48px,1fr)); min-width:720px; }
    .head-row { border-bottom:1px solid #e2e8f0; background:#f8fafc; }
    .body-row { border-bottom:1px solid #f1f5f9; }
    .name-col { padding:.65rem .75rem; display:flex; flex-direction:column; gap:.1rem; position:sticky; left:0; background:inherit; z-index:1; }
    .name-col small { color:#94a3b8; font-size:.75rem; }
    .day-col { border-left:1px solid #f1f5f9; min-height:48px; padding:4px; display:flex; flex-direction:column; align-items:center; gap:2px; }
    .day-col.weekend { background:#fafafa; }
    .head-row .day-col { font-size:.7rem; color:#64748b; padding:.5rem 2px; }
    .bar { width:100%; height:14px; border-radius:4px; opacity:.9; }
    .empty { padding:1.5rem; color:#64748b; }
  `]
})
export class FirmLeavesCalendarComponent implements OnInit {
  private readonly api = inject(FirmLeavesService);

  mode: 'week' | 'month' = 'week';
  readonly modes = [
    { label: 'Semaine', value: 'week' },
    { label: 'Mois', value: 'month' }
  ];
  private anchor = this.startOfWeek(new Date());
  readonly entries = signal<FirmLeaveCalendarEntry[]>([]);
  readonly days = signal<Date[]>([]);

  readonly rows = computed(() => {
    const map = new Map<string, { userId: string; name: string; qualification?: string }>();
    for (const e of this.entries()) {
      if (!map.has(e.userId)) map.set(e.userId, { userId: e.userId, name: e.collaboratorName, qualification: e.qualification });
    }
    return [...map.values()].sort((a, b) => a.name.localeCompare(b.name));
  });

  readonly rangeLabel = computed(() => {
    const ds = this.days();
    if (!ds.length) return '';
    const a = ds[0];
    const b = ds[ds.length - 1];
    return `${a.toLocaleDateString('fr-FR')} – ${b.toLocaleDateString('fr-FR')}`;
  });

  ngOnInit(): void { this.reload(); }

  goToday(): void {
    this.anchor = this.startOfWeek(new Date());
    this.reload();
  }

  shift(dir: number): void {
    const d = new Date(this.anchor);
    if (this.mode === 'week') d.setDate(d.getDate() + dir * 7);
    else d.setMonth(d.getMonth() + dir);
    this.anchor = this.mode === 'week' ? this.startOfWeek(d) : new Date(d.getFullYear(), d.getMonth(), 1);
    this.reload();
  }

  reload(): void {
    const days = this.buildDays();
    this.days.set(days);
    const from = this.toIso(days[0]);
    const to = this.toIso(days[days.length - 1]);
    this.api.getCalendar(from, to).subscribe({
      next: r => this.entries.set(r.data ?? [])
    });
  }

  cells(userId: string, day: Date): FirmLeaveCalendarEntry[] {
    const iso = this.toIso(day);
    return this.entries().filter(e =>
      e.userId === userId &&
      this.toIso(new Date(e.startDate)) <= iso &&
      this.toIso(new Date(e.endDate)) >= iso
    );
  }

  isWeekend(d: Date): boolean {
    const w = d.getDay();
    return w === 0 || w === 6;
  }

  private buildDays(): Date[] {
    if (this.mode === 'week') {
      return Array.from({ length: 7 }, (_, i) => {
        const d = new Date(this.anchor);
        d.setDate(d.getDate() + i);
        return d;
      });
    }
    const y = this.anchor.getFullYear();
    const m = this.anchor.getMonth();
    const count = new Date(y, m + 1, 0).getDate();
    return Array.from({ length: count }, (_, i) => new Date(y, m, i + 1));
  }

  private startOfWeek(d: Date): Date {
    const x = new Date(d);
    const day = (x.getDay() + 6) % 7; // Monday=0
    x.setDate(x.getDate() - day);
    x.setHours(0, 0, 0, 0);
    return x;
  }

  private toIso(d: Date): string {
    const y = d.getFullYear();
    const m = String(d.getMonth() + 1).padStart(2, '0');
    const day = String(d.getDate()).padStart(2, '0');
    return `${y}-${m}-${day}`;
  }
}
