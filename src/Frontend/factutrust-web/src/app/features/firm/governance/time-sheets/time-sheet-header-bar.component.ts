import { Component, EventEmitter, HostListener, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { SelectModule } from 'primeng/select';
import { TagModule } from 'primeng/tag';
import { DatePickerModule } from 'primeng/datepicker';
import { FirmTimeSheetEntry } from '@core/services/firm-governance.service';
import { PeriodScope } from './time-sheets.facade';

export interface CollaboratorOption {
  id: string;
  label: string;
}

@Component({
  selector: 'app-time-sheet-header-bar',
  standalone: true,
  imports: [CommonModule, FormsModule, ButtonModule, SelectModule, TagModule, DatePickerModule],
  template: `
    <div class="header-bar">
      <div class="left">
        @if (isManager) {
          <p-select
            [options]="collaborators"
            optionLabel="label"
            optionValue="id"
            [ngModel]="filterUserId"
            (ngModelChange)="filterUserIdChange.emit($event)"
            placeholder="Collaborateur"
            [showClear]="true"
            styleClass="collab-select" />
        }
        <div class="week-nav">
          <button type="button" pButton icon="pi pi-chevron-left" class="p-button-text p-button-sm" (click)="shiftWeek.emit(-1)"></button>
          <div class="week-meta">
            <button type="button" class="week-label-btn" (click)="toggleMiniMonth($event)" [attr.aria-expanded]="miniOpen">
              <strong>{{ weekLabel }}</strong>
              <span class="iso">S{{ isoWeek }}</span>
            </button>
            @if (miniOpen) {
              <div class="mini-month" (click)="$event.stopPropagation()">
                <p-datepicker
                  [inline]="true"
                  [ngModel]="focusDateValue"
                  (ngModelChange)="onMiniDate($event)"
                  [showWeek]="true"
                  dateFormat="dd/mm/yy" />
              </div>
            }
          </div>
          <button type="button" pButton icon="pi pi-chevron-right" class="p-button-text p-button-sm" (click)="shiftWeek.emit(1)"></button>
        </div>
        <p-tag [value]="statusBadge" [severity]="badgeSeverity" />
        <div class="scopes">
          <button type="button" pButton label="Aujourd'hui" class="p-button-sm" [class.p-button-outlined]="periodScope !== 'today'" (click)="goTodayWeek.emit()"></button>
          <button type="button" pButton label="Semaine" class="p-button-sm" [class.p-button-outlined]="periodScope !== 'week'" (click)="periodScopeChange.emit('week')"></button>
          <button type="button" pButton label="Mois" class="p-button-sm" [class.p-button-outlined]="periodScope !== 'month'" (click)="periodScopeChange.emit('month')"></button>
        </div>
        <div class="modes">
          <button type="button" pButton label="Vue Semaine" class="p-button-sm" [class.p-button-outlined]="mode !== 'week'" (click)="modeChange.emit('week')"></button>
          <button type="button" pButton label="Vue Jour" class="p-button-sm" [class.p-button-outlined]="mode !== 'day'" (click)="modeChange.emit('day')"></button>
          <button type="button" pButton label="Liste des temps" class="p-button-sm" [class.p-button-outlined]="mode !== 'list'" (click)="modeChange.emit('list')"></button>
        </div>
        <button type="button" pButton label="+ Ajouter du temps" icon="pi pi-plus" class="p-button-sm" [disabled]="locked" (click)="addTime.emit()"></button>
      </div>
      <div class="right">
        <div class="totals">
          <span class="total">{{ totalHours | number:'1.2-2' }} h</span>
          <small>Facturables : {{ billableHours | number:'1.2-2' }} h ({{ billableRatio | number:'1.0-0' }} %)</small>
        </div>
        @if (activeTimer) {
          <div class="timer-box">
            <span class="timer-live">{{ elapsedLabel }}</span>
            @if (timerContext) { <small>{{ timerContext }}</small> }
            <button type="button" pButton label="Arrêter" icon="pi pi-stop" class="p-button-danger p-button-sm" [disabled]="locked" (click)="stopTimer.emit()"></button>
          </div>
        } @else {
          <button type="button" pButton label="Chronomètre" icon="pi pi-play" class="p-button-sm" [disabled]="locked" (click)="startTimer.emit()"></button>
        }
      </div>
    </div>
  `,
  styles: [`
    .header-bar {
      display: flex; flex-wrap: wrap; gap: .75rem; justify-content: space-between; align-items: center;
      background: var(--color-surface, #fff);
      border: 1px solid var(--color-border-subtle, #e2e8f0);
      border-radius: var(--radius-xl, 16px);
      padding: .75rem 1rem; margin-bottom: .75rem;
    }
    .left, .right, .week-nav, .modes, .scopes { display: flex; flex-wrap: wrap; gap: .45rem; align-items: center; }
    .week-meta { position: relative; display: flex; flex-direction: column; line-height: 1.2; }
    .week-label-btn {
      border: 0; background: transparent; cursor: pointer; text-align: left; padding: .15rem .25rem; border-radius: 6px;
    }
    .week-label-btn:hover { background: #f1f5f9; }
    .iso { display: block; font-size: .75rem; color: var(--color-text-muted, #64748b); }
    .mini-month {
      position: absolute; top: calc(100% + 6px); left: 0; z-index: 30;
      background: #fff; border: 1px solid #e2e8f0; border-radius: 12px;
      box-shadow: 0 8px 24px rgba(15, 23, 42, .12); padding: .35rem;
    }
    .totals { text-align: right; }
    .total { font-weight: 700; font-size: 1.25rem; color: var(--color-primary, #0f766e); display: block; }
    .totals small { color: var(--color-text-muted, #64748b); }
    .timer-box { display: flex; flex-direction: column; align-items: flex-end; gap: .2rem; }
    .timer-live { font-variant-numeric: tabular-nums; color: var(--color-danger, #b91c1c); font-weight: 700; }
    .timer-box small { color: #64748b; max-width: 180px; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
    :host ::ng-deep .collab-select { min-width: 200px; }
  `]
})
export class TimeSheetHeaderBarComponent {
  @Input() isManager = false;
  @Input() collaborators: CollaboratorOption[] = [];
  @Input() filterUserId: string | null = null;
  @Input() weekLabel = '';
  @Input() isoWeek = 1;
  @Input() statusBadge = 'En brouillon';
  @Input() mode: 'list' | 'week' | 'day' = 'week';
  @Input() periodScope: PeriodScope = 'week';
  @Input() focusedDate = '';
  @Input() totalHours = 0;
  @Input() billableHours = 0;
  @Input() billableRatio = 0;
  @Input() locked = false;
  @Input() activeTimer: FirmTimeSheetEntry | null = null;
  @Input() elapsedLabel = '00:00:00';
  @Input() timerContext = '';
  @Output() filterUserIdChange = new EventEmitter<string | null>();
  @Output() shiftWeek = new EventEmitter<number>();
  @Output() modeChange = new EventEmitter<'list' | 'week' | 'day'>();
  @Output() periodScopeChange = new EventEmitter<PeriodScope>();
  @Output() goTodayWeek = new EventEmitter<void>();
  @Output() focusDatePick = new EventEmitter<string>();
  @Output() startTimer = new EventEmitter<void>();
  @Output() stopTimer = new EventEmitter<void>();
  @Output() addTime = new EventEmitter<void>();

  miniOpen = false;

  get focusDateValue(): Date | null {
    if (!this.focusedDate) return null;
    return new Date(this.focusedDate + 'T12:00:00');
  }

  get badgeSeverity(): 'success' | 'warn' | 'info' | 'secondary' {
    if (this.statusBadge === 'Validé') return 'success';
    if (this.statusBadge === 'Soumis') return 'warn';
    if (this.statusBadge === 'Mixte') return 'info';
    return 'secondary';
  }

  toggleMiniMonth(ev: MouseEvent): void {
    ev.stopPropagation();
    this.miniOpen = !this.miniOpen;
  }

  onMiniDate(value: Date | null): void {
    if (!value) return;
    const y = value.getFullYear();
    const m = `${value.getMonth() + 1}`.padStart(2, '0');
    const d = `${value.getDate()}`.padStart(2, '0');
    this.focusDatePick.emit(`${y}-${m}-${d}`);
    this.miniOpen = false;
  }

  @HostListener('document:click')
  onDocumentClick(): void {
    if (this.miniOpen) this.miniOpen = false;
  }
}
