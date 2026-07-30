import { Component, OnInit, computed, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { PageHeaderComponent } from '@shared/components/page-header/page-header.component';
import { ButtonModule } from 'primeng/button';
import { TimeSheetsFacade } from './time-sheets.facade';
import { TimeSheetsToolbarComponent } from './time-sheets-toolbar.component';
import { TimeSheetEntryFormComponent } from './time-sheet-entry-form.component';
import { TimeSheetEntryDialogComponent } from './time-sheet-entry-dialog.component';
import { TimeSheetWeekViewComponent } from './time-sheet-week-view.component';
import { TimeSheetListComponent } from './time-sheet-list.component';
import { TimeSheetHeaderBarComponent } from './time-sheet-header-bar.component';
import { TimeSheetFiltersBarComponent } from './time-sheet-filters-bar.component';
import { TimeSheetCalendarGridComponent } from './time-sheet-calendar-grid.component';
import { TimeSheetActivityLegendComponent } from './time-sheet-activity-legend.component';
import { TimeSheetKpiPanelComponent } from './time-sheet-kpi-panel.component';
import { TimeSheetDetailTableComponent } from './time-sheet-detail-table.component';
import { TimeSheetQuickActionsComponent } from './time-sheet-quick-actions.component';

@Component({
  selector: 'app-time-sheets-page',
  standalone: true,
  imports: [
    CommonModule,
    ButtonModule,
    PageHeaderComponent,
    TimeSheetsToolbarComponent,
    TimeSheetEntryFormComponent,
    TimeSheetEntryDialogComponent,
    TimeSheetWeekViewComponent,
    TimeSheetListComponent,
    TimeSheetHeaderBarComponent,
    TimeSheetFiltersBarComponent,
    TimeSheetCalendarGridComponent,
    TimeSheetActivityLegendComponent,
    TimeSheetKpiPanelComponent,
    TimeSheetDetailTableComponent,
    TimeSheetQuickActionsComponent
  ],
  providers: [TimeSheetsFacade],
  template: `
    <app-page-header
      title="Feuilles de temps"
      [subtitle]="facade.filterUserId() ? 'Filtrées pour le collaborateur sélectionné' : 'Saisie et validation des temps passés'">
      <button type="button" pButton class="p-button-text p-button-sm"
        [label]="facade.richUi() ? 'Mode classique' : 'Mode enrichi'"
        (click)="facade.toggleRichUi()"></button>
    </app-page-header>

    @if (facade.richUi()) {
      <app-time-sheet-header-bar
        [isManager]="facade.isManager()"
        [collaborators]="facade.collaborators()"
        [filterUserId]="facade.filterUserId()"
        [weekLabel]="facade.weekLabel()"
        [isoWeek]="facade.isoWeekNumber()"
        [statusBadge]="facade.statusBadge()"
        [mode]="facade.mode()"
        [periodScope]="facade.periodScope()"
        [focusedDate]="facade.focusedDate()"
        [totalHours]="facade.totalHours()"
        [billableHours]="facade.billableHours()"
        [billableRatio]="facade.billableRatio()"
        [locked]="facade.periodLocked()"
        [activeTimer]="facade.activeTimer()"
        [elapsedLabel]="facade.elapsedLabel()"
        [timerContext]="facade.timerContextLabel()"
        (filterUserIdChange)="facade.setFilterUserId($event)"
        (shiftWeek)="facade.shiftWeek($event)"
        (modeChange)="facade.onModeChange($event)"
        (periodScopeChange)="facade.setPeriodScope($event)"
        (goTodayWeek)="facade.goTodayWeek()"
        (focusDatePick)="facade.setFocusDate($event)"
        (startTimer)="facade.startTimer()"
        (stopTimer)="facade.stopTimer()"
        (addTime)="facade.focusAddForm()" />

      <app-time-sheets-toolbar
        [yearOptions]="facade.yearOptions"
        [monthOptions]="facade.monthOptions"
        [selectedYear]="facade.selectedYear()"
        [selectedMonth]="facade.selectedMonth()"
        [mode]="facade.mode()"
        [hideModes]="true"
        (yearChange)="facade.selectedYear.set($event); facade.onPeriodChange()"
        (monthChange)="facade.selectedMonth.set($event); facade.onPeriodChange()"
        (modeChange)="facade.onModeChange($event)" />

      <app-time-sheet-filters-bar
        [filters]="facade.filters()"
        [clients]="facade.clients()"
        [activityCodes]="facade.activityCodes()"
        [accountants]="facade.accountantOptions()"
        (filtersChange)="facade.setFilters($event)" />

      <app-time-sheet-quick-actions
        [locked]="facade.periodLocked()"
        [isManager]="facade.isManager()"
        [bulkValidating]="facade.bulkValidating()"
        [draftCount]="facade.draftCount()"
        [selectionCount]="facade.selection().length"
        (duplicateWeek)="facade.duplicateWeek()"
        (exportCsv)="facade.exportCsv()"
        (submitDrafts)="facade.submitDrafts()"
        (validateSelection)="facade.validateSelection()" />
    } @else {
      <app-time-sheets-toolbar
        [yearOptions]="facade.yearOptions"
        [monthOptions]="facade.monthOptions"
        [selectedYear]="facade.selectedYear()"
        [selectedMonth]="facade.selectedMonth()"
        [mode]="facade.mode()"
        (yearChange)="facade.selectedYear.set($event); facade.onPeriodChange()"
        (monthChange)="facade.selectedMonth.set($event); facade.onPeriodChange()"
        (modeChange)="facade.onModeChange($event)" />
    }

    @if (facade.currentPeriod(); as period) {
      <div class="fc-card period-bar" [class.locked]="period.isLocked">
        <span class="period-state">
          <i class="pi" [class.pi-lock]="period.isLocked" [class.pi-lock-open]="!period.isLocked"></i>
          {{ period.isLocked ? 'Période clôturée' : 'Période ouverte' }}
        </span>
        @if (period.isLocked) {
          <span class="period-detail">
            Clôturée le {{ period.lockedAt | date:'shortDate' }} par {{ period.lockedByDisplayName }}
            @if (period.lockReason) { — {{ period.lockReason }} }
          </span>
        }
        @if (facade.isManager()) {
          @if (period.isLocked) {
            <button type="button" pButton label="Rouvrir" icon="pi pi-lock-open" class="p-button-sm p-button-outlined" (click)="facade.unlockPeriod()"></button>
          } @else {
            <button type="button" pButton label="Clôturer le mois" icon="pi pi-lock" class="p-button-sm p-button-outlined" (click)="facade.lockPeriod()"></button>
          }
        }
      </div>
    }

    @if (facade.anomalies().length > 0) {
      <div class="fc-card anomalies">
        <strong>Dépassements constatés</strong>
        <ul>
          @for (message of facade.anomalies(); track message) {
            <li>{{ message }}</li>
          }
        </ul>
      </div>
    }

    @if (!facade.richUi()) {
      <app-time-sheet-entry-form
        [form]="facade.form"
        [clients]="facade.clients()"
        [activityCodes]="facade.activityCodes()"
        [editing]="editing()"
        [locked]="facade.periodLocked()"
        [totalHours]="facade.totalHours()"
        [billableHours]="facade.billableHours()"
        [nonBillableHours]="facade.nonBillableHours()"
        [billableRatio]="facade.billableRatio()"
        [rich]="false"
        (submit)="facade.submit()"
        (cancel)="facade.cancelEdit()"
        (activityCodeChange)="facade.onActivityCodeChange($event)" />
    }

    @if (facade.richUi()) {
      <app-time-sheet-entry-dialog
        [visible]="facade.entryDialogOpen()"
        [form]="facade.form"
        [clients]="facade.clients()"
        [activityCodes]="facade.activityCodes()"
        [editing]="editing()"
        [locked]="facade.periodLocked()"
        (visibleChange)="facade.onEntryDialogVisible($event)"
        (submit)="facade.submit()"
        (cancel)="facade.closeEntryDialog()"
        (activityCodeChange)="facade.onActivityCodeChange($event)" />

      <div class="rich-layout">
        <div class="main">
          @if (facade.mode() !== 'list') {
            <app-time-sheet-calendar-grid
              [mode]="facade.mode() === 'day' ? 'day' : 'week'"
              [focusedDate]="facade.focusedDate()"
              [weekDays]="facade.weekDays()"
              [entries]="facade.filteredEntries()"
              [activityCodes]="facade.activityCodes()"
              [locked]="facade.periodLocked()"
              [weekTotalHours]="facade.weekTotalHours()"
              (createRange)="facade.requestCreateFromRange($event)"
              (moveEntry)="facade.moveEntry($event.entry, $event)"
              (edit)="facade.startEdit($event)"
              (focusDate)="facade.setFocusDate($event)" />
          }
          <app-time-sheet-detail-table
            [entries]="visibleEntries()"
            [clients]="facade.clients()"
            [activityCodes]="facade.activityCodes()"
            [loading]="facade.loading()"
            [isManager]="facade.isManager()"
            [periodLocked]="facade.periodLocked()"
            [selection]="facade.selection()"
            (selectionChange)="facade.selection.set($event)"
            (edit)="facade.startEdit($event)"
            (remove)="facade.remove($event)"
            (duplicate)="facade.duplicateToNextDay($event)"
            (validate)="facade.validate($event)"
            (unvalidate)="facade.unvalidate($event)"
            (submitOne)="facade.submitOne($event)"
            (startTimer)="facade.startTimer($event)"
            (patch)="onInlinePatch($event)" />
        </div>
        <div class="side">
          <app-time-sheet-activity-legend [entries]="facade.filteredEntries()" [activityCodes]="facade.activityCodes()" />
          <app-time-sheet-kpi-panel
            [totalHours]="facade.totalHours()"
            [billableHours]="facade.billableHours()"
            [nonBillableHours]="facade.nonBillableHours()"
            [billableRatio]="facade.billableRatio()"
            [productiveHours]="facade.productiveHoursApprox()"
            [occupationPercent]="facade.occupationPercent()"
            [hoursToValidate]="facade.hoursToValidate()"
            [hoursRejected]="facade.hoursRejected()" />
        </div>
      </div>
    } @else {
      @if (facade.mode() !== 'list') {
        <app-time-sheet-week-view
          [mode]="facade.mode() === 'day' ? 'day' : 'week'"
          [focusedDate]="facade.focusedDate()"
          [weekDays]="facade.weekDays()"
          [entriesByDate]="facade.entriesByDate()"
          (quickAdd)="facade.addFast($event.date, $event.hours)"
          (focusDate)="facade.setFocusDate($event)" />
      }

      <app-time-sheet-list
        [entries]="visibleEntries()"
        [loading]="facade.loading()"
        [isManager]="facade.isManager()"
        [bulkValidating]="facade.bulkValidating()"
        [periodLocked]="facade.periodLocked()"
        [selection]="facade.selection()"
        (selectionChange)="facade.selection.set($event)"
        (validateSelection)="facade.validateSelection()"
        (duplicate)="facade.duplicateToNextDay($event)"
        (edit)="facade.startEdit($event)"
        (remove)="facade.remove($event)"
        (validate)="facade.validate($event)"
        (unvalidate)="facade.unvalidate($event)" />
    }
  `,
  styles: [`
    :host { display: block; }
    .fc-card {
      background: var(--color-surface, #fff);
      border: 1px solid var(--color-border-subtle, #e2e8f0);
      border-radius: var(--radius-xl, 16px);
      box-shadow: var(--shadow-soft-sm, 0 1px 2px rgba(15, 23, 42, 0.05));
      padding: var(--spacing-3, 12px);
      margin-bottom: 1rem;
    }
    .period-bar { display: flex; align-items: center; gap: .75rem; flex-wrap: wrap; font-size: .875rem; }
    .period-bar.locked { border-color: #f59e0b; background: #fffbeb; }
    .period-state { display: inline-flex; align-items: center; gap: .35rem; font-weight: 600; }
    .period-detail { color: var(--color-text-muted, #64748b); }
    .period-bar button { margin-left: auto; }
    .anomalies { border-color: #f59e0b; background: #fffbeb; font-size: .875rem; }
    .anomalies ul { margin: .5rem 0 0; padding-left: 1.25rem; }
    .rich-layout {
      display: grid;
      grid-template-columns: minmax(0, 1fr) 260px;
      gap: 1rem;
      align-items: start;
    }
    .side { display: flex; flex-direction: column; gap: .75rem; position: sticky; top: 1rem; }
    @media (max-width: 960px) {
      .rich-layout { grid-template-columns: 1fr; }
      .side { position: static; }
    }
  `]
})
export class TimeSheetsPageComponent implements OnInit {
  readonly facade = inject(TimeSheetsFacade);
  readonly editing = computed(() => this.facade.editingId() !== null);

  readonly visibleEntries = computed(() => {
    const mode = this.facade.mode();
    const entries = this.facade.richUi() ? this.facade.filteredEntries() : this.facade.entries();
    if (mode === 'list') return entries;
    if (mode === 'day') {
      const day = this.facade.focusedDate();
      return entries.filter(e => e.workDate.slice(0, 10) === day);
    }
    const week = new Set(this.facade.weekDays());
    return entries.filter(e => week.has(e.workDate.slice(0, 10)));
  });

  ngOnInit(): void {
    this.facade.init();
  }

  onInlinePatch(event: { entry: import('@core/services/firm-governance.service').FirmTimeSheetEntry; changes: Record<string, unknown> }): void {
    this.facade.patchInline(event.entry, event.changes as Parameters<TimeSheetsFacade['patchInline']>[1]);
  }
}
