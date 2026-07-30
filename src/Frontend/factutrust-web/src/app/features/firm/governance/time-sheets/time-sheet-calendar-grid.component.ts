import { Component, EventEmitter, HostListener, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FirmActivityCode, FirmTimeSheetEntry } from '@core/services/firm-governance.service';
import { activityPastelBorder, activityPastelColor } from './time-sheet-activity-color';

const GRID_START = 8;
const GRID_END = 18;
const HOURS = Array.from({ length: GRID_END - GRID_START + 1 }, (_, i) => GRID_START + i);
const SLOT_PX = 48;
const GRID_HEIGHT = (GRID_END - GRID_START) * SLOT_PX;

interface TimedBlock {
  entry: FirmTimeSheetEntry;
  top: number;
  height: number;
  activityLabel: string;
  client: string;
  title: string;
  color: string;
  border: string;
  draft: boolean;
}

@Component({
  selector: 'app-time-sheet-calendar-grid',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="grid-wrap">
      <div class="grid" [style.--cols]="displayDays.length">
        <div class="corner"></div>
        @for (date of displayDays; track date) {
          <div
            class="day-head"
            [class.today]="date === todayIso"
            (click)="focusDate.emit(date)">
            <span>{{ date | date:'EEE dd/MM' }}</span>
            <small>{{ dayHours(date) | number:'1.2-2' }} h</small>
          </div>
        }
        <div class="day-head total-head">
          <span>Total</span>
          <small>{{ weekTotalHours | number:'1.2-2' }} h</small>
        </div>

        <div class="time-axis" [style.height.px]="gridHeight">
          @for (hour of hours; track hour) {
            @if (hour < gridEnd) {
              <div class="hour-label" [style.height.px]="slotPx">{{ pad(hour) }}:00</div>
            } @else {
              <div class="hour-label end">{{ pad(hour) }}:00</div>
            }
          }
        </div>

        @for (date of displayDays; track date) {
          <div
            class="day-column"
            [class.weekend]="isWeekend(date)"
            [class.today]="date === todayIso"
            [style.height.px]="gridHeight"
            [attr.data-date]="date"
            (pointerdown)="onColumnPointerDown($event, date)">
            @for (hour of bodyHours; track hour) {
              <div class="hour-line" [style.top.px]="(hour - gridStart) * slotPx"></div>
              <div class="half-line" [style.top.px]="(hour - gridStart) * slotPx + slotPx / 2"></div>
            }
            @if (nowLineTop(date) !== null) {
              <div class="now-line" [style.top.px]="nowLineTop(date)!"></div>
            }
            @if (preview && preview.date === date) {
              <div
                class="ghost"
                [style.top.px]="hourToPx(preview.startHour)"
                [style.height.px]="Math.max(8, hourToPx(preview.endHour) - hourToPx(preview.startHour))"></div>
            }
            @for (block of blocksFor(date); track block.entry.id) {
              <button
                type="button"
                class="block"
                [class.draft]="block.draft"
                [class.locked]="!block.draft || locked"
                [style.top.px]="block.top"
                [style.height.px]="block.height"
                [style.background]="block.color"
                [style.borderColor]="block.border"
                [title]="block.title"
                (pointerdown)="onBlockPointerDown($event, block.entry)"
                (click)="onBlockClick($event, block.entry)">
                <strong>{{ block.activityLabel }}</strong>
                <span>{{ block.client }}</span>
                <small>{{ block.entry.startTime }}–{{ block.entry.endTime }} · {{ block.entry.hours | number:'1.2-2' }} h</small>
              </button>
            }
          </div>
        }

        <div class="total-column" [style.height.px]="gridHeight">
          <div class="total-stack">
            <span class="total-value">{{ weekTotalHours | number:'1.2-2' }}</span>
            <span class="total-unit">h</span>
            <small>semaine filtrée</small>
          </div>
        </div>
      </div>
      <p class="hint">Glissez pour créer un créneau — déplacez un brouillon pour le repositionner (snap 15 min).</p>
      @if (legacyBands.length) {
        <div class="legacy">
          <strong>Sans créneau (durée seule)</strong>
          <div class="legacy-row">
            @for (item of legacyBands; track item.entry.id) {
              <button
                type="button"
                class="legacy-chip"
                [style.background]="colorFor(item.entry.activityCode)"
                [style.borderColor]="borderFor(item.entry.activityCode)"
                (click)="edit.emit(item.entry)">
                {{ item.date | date:'dd/MM' }} · {{ item.entry.hours }} h · {{ activityLabel(item.entry.activityCode) }}
              </button>
            }
          </div>
        </div>
      }
    </div>
  `,
  styles: [`
    .grid-wrap {
      background: var(--color-surface, #fff);
      border: 1px solid var(--color-border-subtle, #e2e8f0);
      border-radius: var(--radius-xl, 16px);
      padding: .75rem; margin-bottom: 1rem; overflow-x: auto;
    }
    .grid {
      display: grid;
      grid-template-columns: 54px repeat(var(--cols), minmax(110px, 1fr)) 72px;
      min-width: 780px;
      align-items: start;
    }
    .corner { border-bottom: 1px solid #e2e8f0; min-height: 44px; }
    .day-head, .total-head {
      text-align: center; font-weight: 600; padding: .35rem;
      border-bottom: 1px solid #e2e8f0; cursor: pointer; font-size: .85rem;
      min-height: 44px; display: flex; flex-direction: column; justify-content: center;
    }
    .day-head.today { color: var(--color-primary, #0f766e); background: #f0fdfa; border-radius: 8px 8px 0 0; }
    .day-head small, .total-head small { display: block; font-weight: 400; color: #64748b; }
    .total-head { cursor: default; background: #f8fafc; }
    .time-axis { position: relative; border-right: 1px solid #e2e8f0; }
    .hour-label {
      font-size: .72rem; color: #64748b; padding: 0 .3rem;
      transform: translateY(-0.55em); line-height: 1;
    }
    .hour-label.end { height: 0; }
    .day-column {
      position: relative;
      border-right: 1px solid #f1f5f9;
      background: #fff;
      touch-action: none;
      user-select: none;
    }
    .day-column.today { background: #f8fffd; }
    .day-column.weekend {
      background:
        repeating-linear-gradient(-45deg, transparent, transparent 6px, rgba(148, 163, 184, .12) 6px, rgba(148, 163, 184, .12) 7px),
        #f8fafc;
    }
    .day-column.weekend.today {
      background:
        repeating-linear-gradient(-45deg, transparent, transparent 6px, rgba(15, 118, 110, .1) 6px, rgba(15, 118, 110, .1) 7px),
        #f0fdfa;
    }
    .hour-line {
      position: absolute; left: 0; right: 0; height: 0;
      border-top: 1px solid #e2e8f0; pointer-events: none;
    }
    .half-line {
      position: absolute; left: 0; right: 0; height: 0;
      border-top: 1px dashed #e2e8f0; pointer-events: none;
    }
    .now-line {
      position: absolute; left: 0; right: 0; height: 2px; z-index: 4;
      background: #ef4444; pointer-events: none;
    }
    .now-line::before {
      content: ''; position: absolute; left: -4px; top: -3px;
      width: 8px; height: 8px; border-radius: 50%; background: #ef4444;
    }
    .ghost {
      position: absolute; left: 3px; right: 3px; z-index: 3;
      background: rgba(15, 118, 110, .18); border: 1px dashed var(--color-primary, #0f766e);
      border-radius: 8px; pointer-events: none;
    }
    .block {
      position: absolute; left: 3px; right: 3px; z-index: 2;
      border: 1px solid; border-radius: 8px; color: #1e293b; font-size: .68rem;
      padding: .25rem .4rem; text-align: left; overflow: hidden; cursor: pointer;
      box-shadow: 0 1px 2px rgba(15,23,42,.08);
      display: flex; flex-direction: column; gap: 1px; line-height: 1.15;
    }
    .block.draft:not(.locked) { cursor: grab; }
    .block.draft:not(.locked):active { cursor: grabbing; }
    .block.locked { cursor: pointer; opacity: .92; }
    .block strong { font-size: .72rem; color: #0f172a; }
    .block span, .block small { color: #334155; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
    .total-column {
      background: #f8fafc; border-left: 1px solid #e2e8f0;
      display: flex; align-items: center; justify-content: center;
    }
    .total-stack { text-align: center; color: #334155; }
    .total-value { font-size: 1.35rem; font-weight: 700; color: var(--color-primary, #0f766e); }
    .total-unit { font-weight: 600; margin-left: .15rem; }
    .total-stack small { display: block; color: #94a3b8; font-size: .7rem; margin-top: .25rem; }
    .hint { margin: .5rem 0 0; font-size: .8rem; color: #64748b; }
    .legacy { margin-top: .75rem; }
    .legacy-row { display: flex; flex-wrap: wrap; gap: .4rem; margin-top: .35rem; }
    .legacy-chip {
      border: 1px solid; border-radius: 999px; color: #1e293b;
      padding: .25rem .6rem; font-size: .75rem; cursor: pointer;
    }
  `]
})
export class TimeSheetCalendarGridComponent {
  @Input() mode: 'week' | 'day' = 'week';
  @Input() focusedDate = '';
  @Input() weekDays: string[] = [];
  @Input() entries: FirmTimeSheetEntry[] = [];
  @Input() activityCodes: FirmActivityCode[] = [];
  @Input() locked = false;
  @Input() weekTotalHours = 0;
  @Output() createRange = new EventEmitter<{ date: string; startTime: string; endTime: string }>();
  @Output() moveEntry = new EventEmitter<{ entry: FirmTimeSheetEntry; date: string; startTime: string; endTime: string }>();
  @Output() edit = new EventEmitter<FirmTimeSheetEntry>();
  @Output() focusDate = new EventEmitter<string>();

  readonly hours = HOURS;
  readonly bodyHours = HOURS.slice(0, -1);
  readonly gridStart = GRID_START;
  readonly gridEnd = GRID_END;
  readonly slotPx = SLOT_PX;
  readonly gridHeight = GRID_HEIGHT;
  readonly Math = Math;
  readonly todayIso = TimeSheetCalendarGridComponent.localTodayIso();

  preview: { date: string; startHour: number; endHour: number } | null = null;

  private createDrag: { date: string; startHour: number; pointerId: number } | null = null;
  private moveDrag: {
    entry: FirmTimeSheetEntry;
    durationHours: number;
    pointerId: number;
    originDate: string;
    moved: boolean;
  } | null = null;
  private suppressClick = false;

  get displayDays(): string[] {
    if (this.mode === 'day') return this.focusedDate ? [this.focusedDate] : [];
    return this.weekDays;
  }

  get legacyBands(): { date: string; entry: FirmTimeSheetEntry }[] {
    return this.entries
      .filter(e => !e.startTime || !e.endTime)
      .filter(e => this.displayDays.includes(e.workDate.slice(0, 10)))
      .map(e => ({ date: e.workDate.slice(0, 10), entry: e }));
  }

  pad(h: number): string {
    return `${h}`.padStart(2, '0');
  }

  dayHours(date: string): number {
    return this.entries.filter(e => e.workDate.slice(0, 10) === date).reduce((s, e) => s + e.hours, 0);
  }

  isWeekend(date: string): boolean {
    const d = new Date(date + 'T12:00:00').getDay();
    return d === 0 || d === 6;
  }

  activityLabel(code?: string): string {
    if (!code) return 'Sans code';
    return this.activityCodes.find(a => a.code === code)?.label ?? code;
  }

  colorFor(code?: string): string {
    return activityPastelColor(code);
  }

  borderFor(code?: string): string {
    return activityPastelBorder(code);
  }

  hourToPx(hour: number): number {
    return (hour - GRID_START) * SLOT_PX;
  }

  blocksFor(date: string): TimedBlock[] {
    const result: TimedBlock[] = [];
    for (const entry of this.entries) {
      if (entry.workDate.slice(0, 10) !== date || !entry.startTime || !entry.endTime) continue;
      const start = this.clampHour(this.parseHour(entry.startTime));
      const end = this.clampHour(this.parseHour(entry.endTime));
      if (end <= GRID_START || start >= GRID_END) continue;
      const top = this.hourToPx(Math.max(start, GRID_START));
      const height = Math.max(18, this.hourToPx(Math.min(end, GRID_END)) - top);
      const label = this.activityLabel(entry.activityCode);
      const draft = !entry.isValidated && (entry.status ?? 0) === 0;
      result.push({
        entry,
        top,
        height,
        activityLabel: label,
        client: entry.clientCompanyName || '—',
        title: `${label} · ${entry.clientCompanyName || '—'} · ${entry.startTime}-${entry.endTime} · ${entry.hours} h`,
        color: this.colorFor(entry.activityCode),
        border: this.borderFor(entry.activityCode),
        draft
      });
    }
    return result;
  }

  nowLineTop(date: string): number | null {
    if (date !== this.todayIso) return null;
    const now = new Date();
    const hour = now.getHours() + now.getMinutes() / 60;
    if (hour < GRID_START || hour > GRID_END) return null;
    return this.hourToPx(hour);
  }

  onColumnPointerDown(ev: PointerEvent, date: string): void {
    if (this.locked || this.moveDrag) return;
    if ((ev.target as HTMLElement).closest('.block')) return;
    if (ev.button !== 0) return;
    const hour = this.snapHour(this.yToHour(ev, date));
    this.createDrag = { date, startHour: hour, pointerId: ev.pointerId };
    this.preview = { date, startHour: hour, endHour: Math.min(GRID_END, hour + 0.25) };
    (ev.currentTarget as HTMLElement).setPointerCapture?.(ev.pointerId);
    ev.preventDefault();
  }

  onBlockPointerDown(ev: PointerEvent, entry: FirmTimeSheetEntry): void {
    if (this.locked || ev.button !== 0) return;
    const draft = !entry.isValidated && (entry.status ?? 0) === 0;
    if (!draft || !entry.startTime || !entry.endTime) return;
    const duration = Math.max(0.25, this.parseHour(entry.endTime) - this.parseHour(entry.startTime));
    this.moveDrag = {
      entry,
      durationHours: duration,
      pointerId: ev.pointerId,
      originDate: entry.workDate.slice(0, 10),
      moved: false
    };
    (ev.currentTarget as HTMLElement).setPointerCapture?.(ev.pointerId);
    ev.stopPropagation();
    ev.preventDefault();
  }

  onBlockClick(ev: MouseEvent, entry: FirmTimeSheetEntry): void {
    ev.stopPropagation();
    if (this.suppressClick) {
      this.suppressClick = false;
      return;
    }
    this.edit.emit(entry);
  }

  @HostListener('document:pointermove', ['$event'])
  onDocumentPointerMove(ev: PointerEvent): void {
    if (this.createDrag && this.createDrag.pointerId === ev.pointerId) {
      const hour = this.snapHour(this.yToHour(ev, this.createDrag.date));
      const start = Math.min(this.createDrag.startHour, hour);
      const end = Math.max(this.createDrag.startHour, hour);
      this.preview = {
        date: this.createDrag.date,
        startHour: start,
        endHour: Math.max(start + 0.25, end === start ? start + 0.25 : end)
      };
      return;
    }
    if (this.moveDrag && this.moveDrag.pointerId === ev.pointerId) {
      const hit = this.hitTestColumn(ev);
      if (!hit) return;
      this.moveDrag.moved = true;
      const start = this.snapHour(hit.hour);
      const end = Math.min(GRID_END, start + this.moveDrag.durationHours);
      this.preview = { date: hit.date, startHour: start, endHour: Math.max(start + 0.25, end) };
    }
  }

  @HostListener('document:pointerup', ['$event'])
  onDocumentPointerUp(ev: PointerEvent): void {
    if (this.createDrag && this.createDrag.pointerId === ev.pointerId) {
      const range = this.preview;
      this.createDrag = null;
      this.preview = null;
      if (!range || range.endHour <= range.startHour) return;
      this.createRange.emit({
        date: range.date,
        startTime: this.formatTime(range.startHour),
        endTime: this.formatTime(Math.min(GRID_END, range.endHour))
      });
      return;
    }

    if (this.moveDrag && this.moveDrag.pointerId === ev.pointerId) {
      const drag = this.moveDrag;
      const range = this.preview;
      this.moveDrag = null;
      this.preview = null;
      if (!drag.moved || !range) return;
      this.suppressClick = true;
      this.moveEntry.emit({
        entry: drag.entry,
        date: range.date,
        startTime: this.formatTime(range.startHour),
        endTime: this.formatTime(Math.min(GRID_END, range.endHour))
      });
    }
  }

  private hitTestColumn(ev: PointerEvent): { date: string; hour: number } | null {
    const el = document.elementFromPoint(ev.clientX, ev.clientY)?.closest('.day-column') as HTMLElement | null;
    if (!el) return null;
    const date = el.getAttribute('data-date');
    if (!date) return null;
    const rect = el.getBoundingClientRect();
    const y = Math.min(Math.max(0, ev.clientY - rect.top), rect.height);
    const hour = GRID_START + (y / SLOT_PX);
    return { date, hour };
  }

  private yToHour(ev: PointerEvent, date: string): number {
    const col = document.querySelector(`.day-column[data-date="${date}"]`) as HTMLElement | null;
    if (!col) return GRID_START;
    const rect = col.getBoundingClientRect();
    const y = Math.min(Math.max(0, ev.clientY - rect.top), rect.height);
    return GRID_START + y / SLOT_PX;
  }

  private snapHour(hour: number): number {
    const snapped = Math.round(hour * 4) / 4;
    return Math.min(GRID_END - 0.25, Math.max(GRID_START, snapped));
  }

  private clampHour(hour: number): number {
    return Math.min(GRID_END, Math.max(GRID_START, hour));
  }

  private formatTime(hour: number): string {
    const h = Math.floor(hour);
    const m = Math.round((hour - h) * 60);
    return `${this.pad(h)}:${String(m).padStart(2, '0')}`;
  }

  private parseHour(value: string): number {
    const [h, m] = value.split(':').map(Number);
    return (h || 0) + (m || 0) / 60;
  }

  private static localTodayIso(): string {
    const d = new Date();
    const y = d.getFullYear();
    const m = `${d.getMonth() + 1}`.padStart(2, '0');
    const day = `${d.getDate()}`.padStart(2, '0');
    return `${y}-${m}-${day}`;
  }
}
