import { DestroyRef, Injectable, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormBuilder, Validators } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { forkJoin } from 'rxjs';
import {
  FirmActivityCode,
  FirmGovernanceService,
  FirmTimeSheetEntry,
  FirmTimeSheetPeriod,
  FirmTimeSheetYearSettings
} from '@core/services/firm-governance.service';
import { FirmAssignmentService, FirmClientDossier } from '@core/services/firm-assignment.service';
import { FirmCollaboratorsService } from '@core/services/firm-collaborators.service';
import { ToastService } from '@core/services/toast.service';
import { AuthService } from '@core/services/auth.service';
import { ErrorHandlerService } from '@core/services/error-handler.service';
import { isTimesheetRichUiEnabled, setTimesheetRichUiEnabled } from './timesheet-rich-ui.flag';
import { TimeSheetFilters } from './time-sheet-filters-bar.component';
import { CollaboratorOption } from './time-sheet-header-bar.component';
import { activityPastelColor } from './time-sheet-activity-color';

type LoadPeriod = { year: number; month?: number };

export type PeriodScope = 'today' | 'week' | 'month';

type EntryPayload = {
  workDate: string;
  hours: number;
  startTime?: string;
  endTime?: string;
  firmClientAssignmentId?: string;
  activityCode?: string;
  notes?: string;
  isBillable?: boolean;
  workLocation?: string;
  tags?: string;
  targetUserId?: string;
};

@Injectable()
export class TimeSheetsFacade {
  private readonly api = inject(FirmGovernanceService);
  private readonly assignments = inject(FirmAssignmentService);
  private readonly collaboratorsApi = inject(FirmCollaboratorsService);
  private readonly fb = inject(FormBuilder);
  private readonly toast = inject(ToastService);
  private readonly route = inject(ActivatedRoute);
  private readonly auth = inject(AuthService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly errorHandler = inject(ErrorHandlerService);

  entries = signal<FirmTimeSheetEntry[]>([]);
  clients = signal<FirmClientDossier[]>([]);
  activityCodes = signal<FirmActivityCode[]>([]);
  periods = signal<FirmTimeSheetPeriod[]>([]);
  collaborators = signal<CollaboratorOption[]>([]);
  yearSettings = signal<FirmTimeSheetYearSettings | null>(null);
  anomalies = signal<string[]>([]);
  loading = signal(false);
  bulkValidating = signal(false);
  filterUserId = signal<string | null>(null);
  editingId = signal<string | null>(null);
  selection = signal<FirmTimeSheetEntry[]>([]);
  mode = signal<'list' | 'week' | 'day'>('list');
  periodScope = signal<PeriodScope>('week');
  focusedDate = signal<string>(TimeSheetsFacade.todayLocalIso());
  richUi = signal(isTimesheetRichUiEnabled());
  formVisible = signal(true);
  entryDialogOpen = signal(false);
  pendingRange = signal<{ date: string; startTime: string; endTime: string } | null>(null);
  filters = signal<TimeSheetFilters>({
    clientId: null,
    activityCode: null,
    accountantUserId: null,
    billableOnly: null,
    workLocation: null,
    showValidated: true
  });
  timerTick = signal(0);
  private timerInterval: ReturnType<typeof setInterval> | null = null;
  private inlineTimers = new Map<string, ReturnType<typeof setTimeout>>();

  isManager = computed(() => this.auth.isFirmManager());

  readonly yearOptions = Array.from({ length: 6 }, (_, i) => {
    const y = new Date().getFullYear() - 2 + i;
    return { label: String(y), value: y };
  });

  readonly monthOptions: { label: string; value: number | null }[] = [
    { label: "Toute l'année", value: null },
    ...Array.from({ length: 12 }, (_, i) => ({
      label: new Date(2026, i, 1).toLocaleString('fr-FR', { month: 'long' }),
      value: i + 1 as number | null
    }))
  ];

  selectedYear = signal(new Date().getFullYear());
  selectedMonth = signal<number | null>(new Date().getMonth() + 1);

  accountantOptions = computed(() => {
    const map = new Map<string, string>();
    for (const c of this.clients()) {
      if (c.assignedAccountantUserId && c.assignedAccountantName) {
        map.set(c.assignedAccountantUserId, c.assignedAccountantName);
      }
    }
    return [...map.entries()].map(([id, label]) => ({ id, label }));
  });

  private assignmentAccountantMap = computed(() => {
    const map = new Map<string, string>();
    for (const c of this.clients()) {
      if (c.assignmentId && c.assignedAccountantUserId) {
        map.set(c.assignmentId, c.assignedAccountantUserId);
      }
    }
    return map;
  });

  filteredEntries = computed(() => {
    const f = this.filters();
    const accMap = this.assignmentAccountantMap();
    return this.entries().filter(e => {
      if (f.clientId && e.firmClientAssignmentId !== f.clientId) return false;
      if (f.activityCode && e.activityCode !== f.activityCode) return false;
      if (f.billableOnly !== null && e.isBillable !== f.billableOnly) return false;
      if (f.workLocation && e.workLocation !== f.workLocation) return false;
      if (!f.showValidated && e.isValidated) return false;
      if (f.accountantUserId) {
        const acc = e.firmClientAssignmentId ? accMap.get(e.firmClientAssignmentId) : undefined;
        if (acc !== f.accountantUserId) return false;
      }
      return true;
    });
  });

  totalHours = computed(() => this.filteredEntries().reduce((s, e) => s + e.hours, 0));
  billableHours = computed(() => this.filteredEntries().filter(e => e.isBillable).reduce((s, e) => s + e.hours, 0));
  nonBillableHours = computed(() => this.totalHours() - this.billableHours());
  billableRatio = computed(() => {
    const total = this.totalHours();
    return total > 0 ? (this.billableHours() / total) * 100 : 0;
  });

  /** Totaux visibles sur la grille semaine (entrées filtrées des 7 jours). */
  weekTotalHours = computed(() => {
    const week = new Set(this.weekDays());
    return this.filteredEntries()
      .filter(e => week.has(e.workDate.slice(0, 10)))
      .reduce((s, e) => s + e.hours, 0);
  });

  hoursToValidate = computed(() =>
    this.filteredEntries().filter(e => (e.status ?? 0) === 1).reduce((s, e) => s + e.hours, 0));
  hoursRejected = computed(() => 0);

  statusBadge = computed(() => {
    const rows = this.filteredEntries();
    if (!rows.length) return 'En brouillon';
    const statuses = new Set(rows.map(r => r.isValidated ? 2 : (r.status ?? 0)));
    if (statuses.size > 1) return 'Mixte';
    const only = [...statuses][0];
    if (only === 2) return 'Validé';
    if (only === 1) return 'Soumis';
    return 'En brouillon';
  });

  currentPeriod = computed<FirmTimeSheetPeriod | null>(() => {
    const month = this.selectedMonth();
    if (month === null) return null;
    return this.periods().find(p => p.month === month) ?? null;
  });
  periodLocked = computed(() => this.currentPeriod()?.isLocked === true);

  weekDays = computed(() => {
    const anchor = new Date(this.focusedDate() + 'T12:00:00');
    const day = anchor.getDay();
    const diff = day === 0 ? -6 : 1 - day;
    const monday = new Date(anchor);
    monday.setDate(anchor.getDate() + diff);
    return Array.from({ length: 7 }, (_, i) => {
      const d = new Date(monday);
      d.setDate(monday.getDate() + i);
      return TimeSheetsFacade.toLocalIso(d);
    });
  });

  isoWeekNumber = computed(() => TimeSheetsFacade.isoWeekOf(new Date(this.focusedDate() + 'T12:00:00')));

  weekLabel = computed(() => {
    const days = this.weekDays();
    if (!days.length) return '';
    const a = new Date(days[0] + 'T12:00:00');
    const b = new Date(days[6] + 'T12:00:00');
    return `${a.toLocaleDateString('fr-FR', { day: '2-digit', month: 'short' })} – ${b.toLocaleDateString('fr-FR', { day: '2-digit', month: 'short', year: 'numeric' })}`;
  });

  entriesByDate = computed(() => {
    const map = new Map<string, FirmTimeSheetEntry[]>();
    for (const e of this.filteredEntries()) {
      const key = e.workDate.slice(0, 10);
      const list = map.get(key) ?? [];
      list.push(e);
      map.set(key, list);
    }
    return map;
  });

  activeTimer = computed(() => {
    this.timerTick();
    return this.entries().find(e => !!e.timerStartedAtUtc) ?? null;
  });

  timerContextLabel = computed(() => {
    const t = this.activeTimer();
    if (!t) return '';
    const activity = t.activityCode
      ? (this.activityCodes().find(a => a.code === t.activityCode)?.label ?? t.activityCode)
      : '';
    const client = t.clientCompanyName ?? '';
    return [activity, client].filter(Boolean).join(' · ');
  });

  elapsedLabel = computed(() => {
    this.timerTick();
    const t = this.activeTimer();
    if (!t?.timerStartedAtUtc) return '00:00:00';
    const ms = Date.now() - new Date(t.timerStartedAtUtc).getTime();
    const total = Math.max(0, Math.floor(ms / 1000));
    const h = Math.floor(total / 3600);
    const m = Math.floor((total % 3600) / 60);
    const s = total % 60;
    return `${`${h}`.padStart(2, '0')}:${`${m}`.padStart(2, '0')}:${`${s}`.padStart(2, '0')}`;
  });

  draftCount = computed(() => this.filteredEntries().filter(e => !e.isValidated && (e.status ?? 0) === 0).length);

  /** Heures ouvrées approximatives selon le scope affiché. */
  productiveHoursApprox = computed(() => {
    const settings = this.yearSettings();
    if (!settings?.annualProductiveHours) return 0;
    const scope = this.periodScope();
    if (scope === 'week') return settings.annualProductiveHours / 52;
    if (scope === 'today') return settings.annualProductiveHours / 52 / 5;
    if (this.selectedMonth() !== null) return settings.annualProductiveHours / 12;
    return settings.annualProductiveHours / 12;
  });

  occupationPercent = computed(() => {
    const base = this.productiveHoursApprox();
    if (base <= 0) return 0;
    return Math.min(100, (this.totalHours() / base) * 100);
  });

  form = this.fb.group({
    workDate: [TimeSheetsFacade.todayLocalIso(), Validators.required],
    hours: [1, [Validators.required, Validators.min(0.25)]],
    startTime: ['' as string],
    endTime: ['' as string],
    firmClientAssignmentId: ['' as string],
    activityCode: [''],
    notes: [''],
    isBillable: [true],
    workLocation: ['' as string],
    tags: ['' as string]
  });

  init(): void {
    const qp = this.route.snapshot.queryParamMap;
    this.filterUserId.set(qp.get('userId'));
    const assignmentId = qp.get('assignmentId');
    if (assignmentId) {
      this.form.patchValue({ firmClientAssignmentId: assignmentId });
    }
    if (this.richUi()) {
      this.mode.set('week');
      this.periodScope.set('week');
    }
    this.form.valueChanges
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => this.syncHoursFromSlot());
    this.loadClients();
    this.loadActivityCodes();
    this.loadPeriods();
    this.loadYearSettings();
    if (this.isManager()) this.loadCollaborators();
    this.load();
    this.startTimerTicker();
  }

  /** True when both start and end are filled (hours derived from the slot). */
  hasFormTimeSlot(): boolean {
    const start = (this.form.value.startTime ?? '').toString().trim();
    const end = (this.form.value.endTime ?? '').toString().trim();
    return !!start && !!end;
  }

  /**
   * Keep Heures aligned with Début/Fin when both are set (mirrors backend ResolveHours).
   * Does not force 0.25h when the slot is invalid (end <= start).
   */
  syncHoursFromSlot(): void {
    const start = (this.form.value.startTime ?? '').toString().trim();
    const end = (this.form.value.endTime ?? '').toString().trim();
    if (!start || !end) return;
    const derived = this.hoursBetween(start, end);
    if (derived == null) return;
    if (this.form.value.hours !== derived) {
      this.form.patchValue({ hours: derived }, { emitEvent: false });
    }
  }

  toggleRichUi(): void {
    const next = !this.richUi();
    setTimesheetRichUiEnabled(next);
    this.richUi.set(next);
    this.mode.set(next ? 'week' : 'list');
    if (next) {
      this.periodScope.set('week');
      this.entryDialogOpen.set(false);
      this.formVisible.set(false);
    } else {
      this.formVisible.set(true);
      this.entryDialogOpen.set(false);
    }
    this.load();
  }

  activityColor(code?: string): string {
    return activityPastelColor(code);
  }

  onPeriodChange(): void {
    this.selection.set([]);
    this.loadPeriods();
    this.loadYearSettings();
    this.load();
  }

  onModeChange(mode: 'list' | 'week' | 'day'): void {
    this.mode.set(mode);
    if (mode === 'day') this.periodScope.set('today');
    if (mode === 'week') this.periodScope.set('week');
    if (mode !== 'list') {
      this.focusedDate.set(this.form.value.workDate || TimeSheetsFacade.todayLocalIso());
    }
    this.load();
  }

  setPeriodScope(scope: PeriodScope): void {
    this.periodScope.set(scope);
    if (scope === 'today') {
      this.goTodayWeek();
    } else if (scope === 'week') {
      this.mode.set('week');
    } else {
      this.mode.set('list');
      const now = new Date();
      this.selectedYear.set(now.getFullYear());
      this.selectedMonth.set(now.getMonth() + 1);
      this.onPeriodChange();
    }
  }

  /** Focus aujourd'hui en conservant la vue semaine (pas le mode jour). */
  goTodayWeek(): void {
    this.goToday();
    this.periodScope.set('today');
    this.mode.set('week');
    this.load();
  }

  setFocusDate(date: string): void {
    const before = this.weekMonthsKey();
    this.focusedDate.set(date);
    this.form.patchValue({ workDate: date });
    if (this.usesWeekSpanLoad() && this.weekMonthsKey() !== before) {
      this.load();
    }
  }

  shiftWeek(delta: number): void {
    const d = new Date(this.focusedDate() + 'T12:00:00');
    d.setDate(d.getDate() + delta * 7);
    this.setFocusDate(TimeSheetsFacade.toLocalIso(d));
  }

  goToday(): void {
    this.setFocusDate(TimeSheetsFacade.todayLocalIso());
  }

  setFilters(filters: TimeSheetFilters): void {
    this.filters.set(filters);
  }

  setFilterUserId(userId: string | null): void {
    this.filterUserId.set(userId);
    this.load();
  }

  focusAddForm(): void {
    this.editingId.set(null);
    this.pendingRange.set(null);
    this.form.patchValue({
      workDate: this.focusedDate() || TimeSheetsFacade.todayLocalIso(),
      hours: 1,
      startTime: '',
      endTime: ''
    });
    if (this.richUi()) {
      this.entryDialogOpen.set(true);
      this.formVisible.set(false);
    } else {
      this.formVisible.set(true);
      this.entryDialogOpen.set(false);
    }
  }

  load(): void {
    this.loading.set(true);
    this.selection.set([]);
    const userId = this.filterUserId() ?? undefined;
    const periods = this.resolveLoadPeriods();
    const requests = periods.map(p => this.api.listTimeSheets(p.year, p.month, userId));
    (requests.length === 1 ? forkJoin([requests[0]]) : forkJoin(requests)).subscribe({
      next: results => {
        const map = new Map<string, FirmTimeSheetEntry>();
        for (const res of results) {
          for (const e of res.data ?? []) {
            map.set(e.id, e);
          }
        }
        this.entries.set([...map.values()]);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.toast.add({ severity: 'error', summary: 'Erreur', detail: 'Chargement impossible.' });
      }
    });
  }

  /**
   * Liste / scope mois : filtre toolbar.
   * Semaine / jour : tous les mois intersectant weekDays (ex. 27/07–02/08 → juil. + août).
   */
  resolveLoadPeriods(): LoadPeriod[] {
    if (!this.usesWeekSpanLoad()) {
      return [{ year: this.selectedYear(), month: this.selectedMonth() ?? undefined }];
    }
    const keys = new Set<string>();
    const out: LoadPeriod[] = [];
    for (const iso of this.weekDays()) {
      const d = new Date(iso + 'T12:00:00');
      const year = d.getFullYear();
      const month = d.getMonth() + 1;
      const k = `${year}-${month}`;
      if (!keys.has(k)) {
        keys.add(k);
        out.push({ year, month });
      }
    }
    return out.length
      ? out
      : [{ year: this.selectedYear(), month: this.selectedMonth() ?? undefined }];
  }

  private usesWeekSpanLoad(): boolean {
    return this.mode() === 'week' || this.mode() === 'day'
      || this.periodScope() === 'week' || this.periodScope() === 'today';
  }

  private weekMonthsKey(): string {
    return this.resolveLoadPeriods()
      .map(p => `${p.year}-${p.month ?? 'all'}`)
      .sort()
      .join('|');
  }

  private loadPeriods(): void {
    this.api.listTimeSheetPeriods(this.selectedYear()).subscribe({
      next: res => this.periods.set(res.data ?? []),
      error: () => this.periods.set([])
    });
  }

  private loadYearSettings(): void {
    this.api.getTimeSheetYearSettings(this.selectedYear()).subscribe({
      next: res => this.yearSettings.set(res.data ?? null),
      error: () => this.yearSettings.set(null)
    });
  }

  private loadActivityCodes(): void {
    this.api.listActivityCodes().subscribe({
      next: res => this.activityCodes.set(res.data ?? []),
      error: () => this.activityCodes.set([])
    });
  }

  private loadCollaborators(): void {
    this.collaboratorsApi.list({ isActive: true }).subscribe({
      next: users => this.collaborators.set(users.map(u => ({
        id: u.id,
        label: `${u.firstName} ${u.lastName}`.trim() || u.email
      }))),
      error: () => this.collaborators.set([])
    });
  }

  onActivityCodeChange(code: string | null): void {
    const match = this.activityCodes().find(c => c.code === code);
    if (match) this.form.patchValue({ isBillable: match.isBillableByDefault });
  }

  lockPeriod(): void {
    const month = this.selectedMonth();
    if (month === null) return;
    this.api.lockTimeSheetPeriod(this.selectedYear(), month).subscribe({
      next: () => {
        this.loadPeriods();
        this.toast.add({ severity: 'success', summary: 'Période clôturée', detail: 'Plus aucune écriture n\'est acceptée sur ce mois.' });
      },
      error: (err) => this.toast.add({ severity: 'error', summary: 'Erreur', detail: err?.error?.message || 'Clôture impossible.' })
    });
  }

  unlockPeriod(): void {
    const month = this.selectedMonth();
    if (month === null) return;
    const reason = window.prompt('Motif de réouverture de la période (obligatoire) :');
    if (!reason?.trim()) return;
    this.api.unlockTimeSheetPeriod(this.selectedYear(), month, reason.trim()).subscribe({
      next: () => {
        this.loadPeriods();
        this.toast.add({ severity: 'success', summary: 'Période rouverte', detail: 'La réouverture est tracée.' });
      },
      error: (err) => this.toast.add({ severity: 'error', summary: 'Erreur', detail: err?.error?.message || 'Réouverture impossible.' })
    });
  }

  validateSelection(): void {
    const ids = this.selection().filter(e => (e.status ?? 0) === 1 && !e.isValidated).map(e => e.id);
    if (ids.length === 0) {
      this.toast.add({
        severity: 'info',
        summary: 'Validation',
        detail: 'Sélectionnez des feuilles soumises à valider.'
      });
      return;
    }
    this.bulkValidating.set(true);
    this.api.validateTimeSheetsBulkDetailed(ids).subscribe({
      next: res => {
        this.bulkValidating.set(false);
        this.selection.set([]);
        this.load();
        const result = res.data;
        if (result && result.skipped > 0) {
          this.toast.add({
            severity: 'warn',
            summary: 'Validation partielle',
            detail: `${result.validated} validée(s), ${result.skipped} ignorée(s) : ${result.failures[0]?.error ?? ''}`
          });
        } else {
          this.toast.add({ severity: 'success', summary: 'Validées', detail: `${result?.validated ?? ids.length} feuille(s) validée(s).` });
        }
      },
      error: (err) => {
        this.bulkValidating.set(false);
        this.toast.add({ severity: 'error', summary: 'Erreur', detail: err?.error?.message || 'Validation impossible.' });
      }
    });
  }

  submit(): void {
    if (this.form.invalid) return;
    this.syncHoursFromSlot();
    const v = this.form.getRawValue();
    const startTime = (v.startTime || '').toString().trim() || undefined;
    const endTime = (v.endTime || '').toString().trim() || undefined;
    let hours = v.hours!;
    if (startTime && endTime) {
      const derived = this.hoursBetween(startTime, endTime);
      if (derived == null) {
        this.toast.add({
          severity: 'error',
          summary: 'Créneau invalide',
          detail: 'L\'heure de fin doit être postérieure au début.'
        });
        return;
      }
      hours = derived;
    } else if (startTime || endTime) {
      this.toast.add({
        severity: 'error',
        summary: 'Créneau incomplet',
        detail: 'Début et fin de créneau sont requis ensemble.'
      });
      return;
    }
    const workDate = v.workDate!;
    const editId = this.editingId();
    const limitMessages = this.evaluateHardLimitMessages(workDate, hours, editId);
    if (limitMessages.length) {
      this.reportHardLimitRefusal(limitMessages);
      return;
    }
    const body: EntryPayload = {
      workDate,
      hours,
      startTime,
      endTime,
      firmClientAssignmentId: v.firmClientAssignmentId || undefined,
      activityCode: v.activityCode || undefined,
      notes: v.notes || undefined,
      isBillable: v.isBillable ?? true,
      workLocation: v.workLocation || undefined,
      tags: v.tags || undefined,
      targetUserId: this.filterUserId() ?? undefined
    };
    const req$ = editId ? this.api.updateTimeSheet(editId, body) : this.api.createTimeSheet(body);
    req$.subscribe({
      next: (res) => {
        this.cancelEdit(true);
        this.pendingRange.set(null);
        this.load();
        this.anomalies.set(res.data?.warnings ?? []);
        this.toast.add({
          severity: res.data?.warnings?.length ? 'warn' : 'success',
          summary: 'Feuille de temps',
          detail: editId ? 'Modification enregistrée.' : 'Saisie enregistrée.'
        });
      },
      error: (err) => {
        const message = this.apiErrorMessage(err, 'Saisie impossible.');
        this.anomalies.set([message]);
        this.toast.add({
          severity: 'error',
          summary: 'Plafond / saisie refusée',
          detail: message,
          life: 8000
        });
      }
    });
  }

  /** Drag create: ouvre le dialog prérempli (pas d'auto-submit). */
  requestCreateFromRange(range: { date: string; startTime: string; endTime: string }): void {
    this.editingId.set(null);
    this.pendingRange.set(range);
    this.form.patchValue({
      workDate: range.date,
      startTime: range.startTime,
      endTime: range.endTime,
      hours: this.hoursBetween(range.startTime, range.endTime) ?? 0.25
    });
    if (this.richUi()) {
      this.entryDialogOpen.set(true);
      this.formVisible.set(false);
    } else {
      this.formVisible.set(true);
    }
  }

  confirmPendingRange(): void {
    if (!this.pendingRange()) return;
    this.submit();
  }

  cancelPendingRange(): void {
    this.pendingRange.set(null);
    if (this.richUi()) this.closeEntryDialog();
  }

  closeEntryDialog(): void {
    if (!this.entryDialogOpen() && !this.editingId() && !this.pendingRange()) return;
    this.cancelEdit();
  }

  onEntryDialogVisible(open: boolean): void {
    if (open) {
      this.entryDialogOpen.set(true);
      return;
    }
    if (!this.entryDialogOpen()) return;
    this.cancelEdit();
  }

  /** @deprecated use requestCreateFromRange — kept for classic quick paths if needed */
  createFromRange(range: { date: string; startTime: string; endTime: string }): void {
    this.requestCreateFromRange(range);
  }

  moveEntry(entry: FirmTimeSheetEntry, target: { date: string; startTime: string; endTime: string }): void {
    if (entry.isValidated || (entry.status ?? 0) !== 0 || this.periodLocked()) {
      this.toast.add({ severity: 'warn', summary: 'Non modifiable', detail: 'Seuls les brouillons en période ouverte peuvent être déplacés.' });
      return;
    }
    const hours = this.hoursBetween(target.startTime, target.endTime);
    if (hours == null) {
      this.toast.add({ severity: 'error', summary: 'Créneau invalide', detail: 'L\'heure de fin doit être postérieure au début.' });
      return;
    }
    const body: EntryPayload = {
      workDate: target.date,
      hours,
      startTime: target.startTime,
      endTime: target.endTime,
      firmClientAssignmentId: entry.firmClientAssignmentId,
      activityCode: entry.activityCode,
      notes: entry.notes,
      isBillable: entry.isBillable,
      workLocation: entry.workLocation,
      tags: entry.tags
    };
    this.api.updateTimeSheet(entry.id, body).subscribe({
      next: (res) => {
        this.anomalies.set(res.data?.warnings ?? []);
        this.load();
        this.toast.add({
          severity: res.data?.warnings?.length ? 'warn' : 'success',
          summary: 'Déplacé',
          detail: 'Créneau mis à jour.'
        });
      },
      error: (err) => {
        const message = this.apiErrorMessage(err, 'Déplacement impossible.');
        this.anomalies.set([message]);
        this.toast.add({
          severity: 'error',
          summary: 'Plafond / saisie refusée',
          detail: message,
          life: 8000
        });
      }
    });
  }

  patchInline(entry: FirmTimeSheetEntry, patch: Partial<EntryPayload>): void {
    if (entry.isValidated || (entry.status ?? 0) !== 0 || this.periodLocked()) return;
    const existing = this.inlineTimers.get(entry.id);
    if (existing) clearTimeout(existing);
    this.inlineTimers.set(entry.id, setTimeout(() => {
      this.inlineTimers.delete(entry.id);
      const startTime = patch.startTime ?? entry.startTime;
      const endTime = patch.endTime ?? entry.endTime;
      const derived = startTime && endTime ? this.hoursBetween(startTime, endTime) : null;
      const hours = derived ?? (patch.hours ?? entry.hours);
      const body: EntryPayload = {
        workDate: (patch.workDate ?? entry.workDate).slice(0, 10),
        hours,
        startTime,
        endTime,
        firmClientAssignmentId: patch.firmClientAssignmentId ?? entry.firmClientAssignmentId,
        activityCode: patch.activityCode ?? entry.activityCode,
        notes: patch.notes ?? entry.notes,
        isBillable: patch.isBillable ?? entry.isBillable,
        workLocation: patch.workLocation ?? entry.workLocation,
        tags: patch.tags ?? entry.tags
      };
      const snapshot = [...this.entries()];
      this.entries.update(list => list.map(e => e.id === entry.id ? {
        ...e,
        workDate: body.workDate,
        hours: body.hours,
        startTime: body.startTime,
        endTime: body.endTime,
        firmClientAssignmentId: body.firmClientAssignmentId,
        activityCode: body.activityCode,
        notes: body.notes,
        isBillable: body.isBillable ?? e.isBillable,
        workLocation: body.workLocation,
        tags: body.tags
      } : e));
      this.api.updateTimeSheet(entry.id, body).subscribe({
        next: (res) => {
          this.anomalies.set(res.data?.warnings ?? []);
          if (res.data) {
            this.entries.update(list => list.map(e => e.id === entry.id ? { ...e, ...res.data! } : e));
          }
        },
        error: (err) => {
          this.entries.set(snapshot);
          const message = this.apiErrorMessage(err, 'Enregistrement impossible.');
          this.anomalies.set([message]);
          this.toast.add({
            severity: 'error',
            summary: 'Plafond / saisie refusée',
            detail: message,
            life: 8000
          });
        }
      });
    }, 500));
  }

  addFast(date: string, hours: number): void {
    this.form.patchValue({ workDate: date, hours, startTime: '', endTime: '' });
    this.submit();
  }

  duplicateToNextDay(entry: FirmTimeSheetEntry): void {
    const source = new Date(entry.workDate.slice(0, 10) + 'T12:00:00');
    source.setDate(source.getDate() + 1);
    const nextDate = TimeSheetsFacade.toLocalIso(source);
    const payload: EntryPayload = {
      workDate: nextDate,
      hours: entry.hours,
      startTime: entry.startTime,
      endTime: entry.endTime,
      firmClientAssignmentId: entry.firmClientAssignmentId,
      activityCode: entry.activityCode,
      notes: entry.notes,
      isBillable: entry.isBillable,
      workLocation: entry.workLocation,
      tags: entry.tags
    };
    this.api.createTimeSheet(payload).subscribe({
      next: (res) => {
        this.anomalies.set(res.data?.warnings ?? []);
        this.load();
        this.toast.add({ severity: 'success', summary: 'Dupliquée', detail: `Ligne dupliquée au ${new Date(nextDate).toLocaleDateString('fr-FR')}.` });
      },
      error: (err) => this.toast.add({ severity: 'error', summary: 'Erreur', detail: err?.error?.message || 'Duplication impossible.' })
    });
  }

  duplicateWeek(): void {
    const source = this.weekDays()[0];
    if (!source) return;
    const targetDate = new Date(source + 'T12:00:00');
    targetDate.setDate(targetDate.getDate() + 7);
    const target = TimeSheetsFacade.toLocalIso(targetDate);
    this.api.duplicateTimeSheetWeek({
      sourceWeekStart: source,
      targetWeekStart: target,
      userId: this.filterUserId() ?? undefined
    }).subscribe({
      next: res => {
        this.load();
        this.toast.add({
          severity: 'success',
          summary: 'Semaine dupliquée',
          detail: `${res.data?.length ?? 0} ligne(s) créée(s) sur la semaine suivante.`
        });
      },
      error: (err) => this.toast.add({ severity: 'error', summary: 'Erreur', detail: err?.error?.message || 'Duplication impossible.' })
    });
  }

  exportCsv(): void {
    const rows = this.filteredEntries();
    const header = ['Date', 'Client', 'Activite', 'Notes', 'Debut', 'Fin', 'Heures', 'Facturable', 'Tags', 'Lieu', 'Statut'];
    const lines = rows.map(e => [
      e.workDate.slice(0, 10),
      e.clientCompanyName ?? '',
      e.activityCode ?? '',
      (e.notes ?? '').replaceAll('"', '""'),
      e.startTime ?? '',
      e.endTime ?? '',
      String(e.hours),
      e.isBillable ? '1' : '0',
      e.tags ?? '',
      e.workLocation ?? '',
      e.statusDisplay ?? ''
    ].map(v => `"${v}"`).join(';'));
    const csv = [header.join(';'), ...lines].join('\n');
    const blob = new Blob([csv], { type: 'text/csv;charset=utf-8;' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `feuilles-temps-${this.selectedYear()}-${this.selectedMonth() ?? 'all'}.csv`;
    a.click();
    URL.revokeObjectURL(url);
  }

  submitDrafts(): void {
    const drafts = this.filteredEntries().filter(e => !e.isValidated && (e.status ?? 0) === 0 && !e.timerStartedAtUtc);
    if (!drafts.length) return;
    let done = 0;
    let failed = 0;
    drafts.forEach(d => {
      this.api.submitTimeSheet(d.id).subscribe({
        next: () => {
          done++;
          if (done + failed === drafts.length) {
            this.load();
            this.toast.add({
              severity: failed ? 'warn' : 'success',
              summary: 'Soumission',
              detail: `${done} soumise(s)${failed ? `, ${failed} échec(s)` : ''}.`
            });
          }
        },
        error: () => {
          failed++;
          if (done + failed === drafts.length) {
            this.load();
            this.toast.add({ severity: 'warn', summary: 'Soumission partielle', detail: `${done} ok, ${failed} échec(s).` });
          }
        }
      });
    });
  }

  submitOne(entry: FirmTimeSheetEntry): void {
    this.api.submitTimeSheet(entry.id).subscribe({
      next: () => {
        this.load();
        this.toast.add({ severity: 'success', summary: 'Soumise', detail: 'Feuille de temps soumise.' });
      },
      error: (err) => this.toast.add({ severity: 'error', summary: 'Erreur', detail: err?.error?.message || 'Soumission impossible.' })
    });
  }

  startTimer(fromEntry?: FirmTimeSheetEntry): void {
    const v = this.form.getRawValue();
    this.api.startTimeSheetTimer({
      workDate: fromEntry?.workDate.slice(0, 10) || v.workDate || TimeSheetsFacade.todayLocalIso(),
      firmClientAssignmentId: fromEntry?.firmClientAssignmentId || v.firmClientAssignmentId || undefined,
      activityCode: fromEntry?.activityCode || v.activityCode || undefined,
      isBillable: fromEntry?.isBillable ?? v.isBillable ?? true,
      targetUserId: this.filterUserId() ?? undefined
    }).subscribe({
      next: () => {
        this.load();
        this.toast.add({ severity: 'success', summary: 'Timer', detail: 'Chronomètre démarré.' });
      },
      error: (err) => this.toast.add({ severity: 'error', summary: 'Erreur', detail: err?.error?.message || 'Démarrage impossible.' })
    });
  }

  stopTimer(): void {
    this.api.stopTimeSheetTimer({
      entryId: this.activeTimer()?.id,
      targetUserId: this.filterUserId() ?? undefined
    }).subscribe({
      next: (res) => {
        this.anomalies.set(res.data?.warnings ?? []);
        this.load();
        this.toast.add({ severity: 'success', summary: 'Timer', detail: 'Chronomètre arrêté — saisie créée.' });
      },
      error: (err) => this.toast.add({ severity: 'error', summary: 'Erreur', detail: err?.error?.message || 'Arrêt impossible.' })
    });
  }

  startEdit(e: FirmTimeSheetEntry): void {
    this.editingId.set(e.id);
    this.pendingRange.set(null);
    this.form.patchValue({
      workDate: e.workDate.slice(0, 10),
      hours: e.hours,
      startTime: e.startTime ?? '',
      endTime: e.endTime ?? '',
      firmClientAssignmentId: e.firmClientAssignmentId ?? '',
      activityCode: e.activityCode ?? '',
      notes: e.notes ?? '',
      isBillable: e.isBillable,
      workLocation: e.workLocation ?? '',
      tags: e.tags ?? ''
    });
    if (this.richUi()) {
      this.entryDialogOpen.set(true);
      this.formVisible.set(false);
    } else {
      this.formVisible.set(true);
    }
  }

  cancelEdit(keepContext = false): void {
    const assignmentId = keepContext
      ? this.form.value.firmClientAssignmentId ?? ''
      : this.route.snapshot.queryParamMap.get('assignmentId') ?? '';
    const activityCode = keepContext ? this.form.value.activityCode ?? '' : '';
    const isBillable = keepContext ? this.form.value.isBillable ?? true : true;
    this.editingId.set(null);
    this.pendingRange.set(null);
    this.entryDialogOpen.set(false);
    this.form.reset({
      workDate: TimeSheetsFacade.todayLocalIso(),
      hours: 1,
      startTime: '',
      endTime: '',
      firmClientAssignmentId: assignmentId,
      activityCode,
      notes: '',
      isBillable,
      workLocation: '',
      tags: ''
    });
  }

  remove(e: FirmTimeSheetEntry): void {
    this.api.deleteTimeSheet(e.id).subscribe({
      next: () => {
        this.load();
        this.toast.add({ severity: 'success', summary: 'Supprimé', detail: 'Feuille de temps supprimée.' });
      },
      error: () => this.toast.add({ severity: 'error', summary: 'Erreur', detail: 'Suppression impossible.' })
    });
  }

  validate(e: FirmTimeSheetEntry): void {
    this.api.validateTimeSheet(e.id).subscribe({
      next: () => {
        this.load();
        this.toast.add({ severity: 'success', summary: 'Validée', detail: 'Feuille de temps validée.' });
      },
      error: () => this.toast.add({ severity: 'error', summary: 'Erreur', detail: 'Validation impossible.' })
    });
  }

  unvalidate(e: FirmTimeSheetEntry): void {
    this.api.unvalidateTimeSheet(e.id).subscribe({
      next: () => {
        this.load();
        this.toast.add({ severity: 'success', summary: 'Brouillon', detail: 'Feuille de temps repassée en brouillon.' });
      },
      error: (err) => this.toast.add({ severity: 'error', summary: 'Erreur', detail: err?.error?.message || 'Dévalidation impossible.' })
    });
  }

  activityLabel(code?: string): string {
    if (!code) return 'Sans code';
    return this.activityCodes().find(a => a.code === code)?.label ?? code;
  }

  private loadClients(): void {
    this.assignments.getActiveClients().subscribe({
      next: r => { if (r.success) this.clients.set(r.data ?? []); }
    });
  }

  private startTimerTicker(): void {
    if (this.timerInterval) return;
    this.timerInterval = setInterval(() => this.timerTick.update(v => v + 1), 1000);
  }

  /** Messages plafond journalier / hebdo alignés sur TimeSheetLegalValidator (hard only). */
  evaluateHardLimitMessages(workDate: string, hours: number, excludeEntryId?: string | null): string[] {
    const settings = this.yearSettings();
    if (!settings?.enforceHardLimits) return [];

    const day = workDate.slice(0, 10);
    const dayDate = new Date(day + 'T12:00:00');
    const editId = excludeEntryId ?? null;
    const others = this.entries().filter(e => e.id !== editId);

    const otherHoursSameDay = others
      .filter(e => e.workDate.slice(0, 10) === day)
      .reduce((s, e) => s + e.hours, 0);
    const dailyTotal = otherHoursSameDay + hours;
    const messages: string[] = [];
    if (dailyTotal > settings.maxDailyHours) {
      messages.push(
        `Le total du ${TimeSheetsFacade.formatFrDate(dayDate)} atteindrait ${TimeSheetsFacade.formatHoursFr(dailyTotal)} h, `
        + `au-delà du plafond journalier de ${TimeSheetsFacade.formatHoursFr(settings.maxDailyHours)} h.`
      );
    }

    const weekSet = new Set(this.isoWeekDaysFor(day));
    const otherHoursSameWeek = others
      .filter(e => weekSet.has(e.workDate.slice(0, 10)))
      .reduce((s, e) => s + e.hours, 0);
    const weeklyTotal = otherHoursSameWeek + hours;
    if (weeklyTotal > settings.maxWeeklyHours) {
      const weekNo = TimeSheetsFacade.isoWeekOf(dayDate);
      messages.push(
        `Le total de la semaine ${weekNo} atteindrait ${TimeSheetsFacade.formatHoursFr(weeklyTotal)} h, `
        + `au-delà de la durée hebdomadaire de ${TimeSheetsFacade.formatHoursFr(settings.maxWeeklyHours)} h.`
      );
    }
    return messages;
  }

  private reportHardLimitRefusal(messages: string[]): void {
    this.anomalies.set(messages);
    this.toast.add({
      severity: 'error',
      summary: 'Plafond / saisie refusée',
      detail: messages[0],
      life: 8000
    });
  }

  private apiErrorMessage(err: unknown, fallback: string): string {
    const extracted = this.errorHandler.extractErrorMessage(err);
    return extracted || fallback;
  }

  /** Jours lun–dim de la semaine ISO contenant `isoDate`. */
  private isoWeekDaysFor(isoDate: string): string[] {
    const anchor = new Date(isoDate + 'T12:00:00');
    const day = anchor.getDay();
    const diff = day === 0 ? -6 : 1 - day;
    const monday = new Date(anchor);
    monday.setDate(anchor.getDate() + diff);
    return Array.from({ length: 7 }, (_, i) => {
      const d = new Date(monday);
      d.setDate(monday.getDate() + i);
      return TimeSheetsFacade.toLocalIso(d);
    });
  }

  /**
   * Duration in hours from HH:mm (or HH:mm:ss) strings.
   * Returns null when either side is missing/invalid or end <= start (no silent 0.25 floor).
   */
  hoursBetween(start: string, end: string): number | null {
    const startMin = TimeSheetsFacade.parseTimeToMinutes(start);
    const endMin = TimeSheetsFacade.parseTimeToMinutes(end);
    if (startMin == null || endMin == null) return null;
    const derived = (endMin - startMin) / 60;
    if (derived <= 0 || derived > 24) return null;
    return Math.round(derived * 1000) / 1000;
  }

  private static parseTimeToMinutes(value: string): number | null {
    const parts = (value ?? '').trim().split(':').map(Number);
    if (parts.length < 2 || parts.some(n => Number.isNaN(n))) return null;
    const [h, m, s = 0] = parts;
    if (h < 0 || h > 23 || m < 0 || m > 59 || s < 0 || s > 59) return null;
    return h * 60 + m + s / 60;
  }

  static todayLocalIso(): string {
    return TimeSheetsFacade.toLocalIso(new Date());
  }

  static toLocalIso(d: Date): string {
    const month = `${d.getMonth() + 1}`.padStart(2, '0');
    const day = `${d.getDate()}`.padStart(2, '0');
    return `${d.getFullYear()}-${month}-${day}`;
  }

  /** Aligné sur TimeSheetLegalValidator.Fmt (fr-FR, 0.##). */
  static formatHoursFr(value: number): string {
    return value.toLocaleString('fr-FR', { maximumFractionDigits: 2 });
  }

  static formatFrDate(d: Date): string {
    const dd = `${d.getDate()}`.padStart(2, '0');
    const mm = `${d.getMonth() + 1}`.padStart(2, '0');
    return `${dd}/${mm}/${d.getFullYear()}`;
  }

  /** Semaine ISO 8601 (alignée backend TimeSheetLegalValidator.IsoWeekOf). */
  static isoWeekOf(date: Date): number {
    const d = new Date(Date.UTC(date.getFullYear(), date.getMonth(), date.getDate()));
    const dayNum = d.getUTCDay() || 7;
    d.setUTCDate(d.getUTCDate() + 4 - dayNum);
    const yearStart = new Date(Date.UTC(d.getUTCFullYear(), 0, 1));
    return Math.ceil((((d.getTime() - yearStart.getTime()) / 86400000) + 1) / 7);
  }
}
