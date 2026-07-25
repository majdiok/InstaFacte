import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { TableModule } from 'primeng/table';
import { ButtonModule } from 'primeng/button';
import { DropdownModule } from 'primeng/dropdown';
import { InputTextModule } from 'primeng/inputtext';
import { CheckboxModule } from 'primeng/checkbox';
import { TagModule } from 'primeng/tag';
import { PageHeaderComponent } from '@shared/components/page-header/page-header.component';
import { EmptyStateComponent } from '@shared/components/empty-state/empty-state.component';
import {
  FirmActivityCode,
  FirmGovernanceService,
  FirmTimeSheetEntry,
  FirmTimeSheetPeriod
} from '@core/services/firm-governance.service';
import { FirmAssignmentService, FirmClientDossier } from '@core/services/firm-assignment.service';
import { ToastService } from '@core/services/toast.service';
import { AuthService } from '@core/services/auth.service';

@Component({
  selector: 'app-firm-time-sheets',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    ReactiveFormsModule,
    RouterLink,
    TableModule,
    ButtonModule,
    DropdownModule,
    InputTextModule,
    CheckboxModule,
    TagModule,
    PageHeaderComponent,
    EmptyStateComponent
  ],
  template: `
    <app-page-header
      title="Feuilles de temps"
      [subtitle]="filterUserId() ? 'Filtrées pour le collaborateur sélectionné' : 'Saisie et validation des temps passés'" />

    <div class="toolbar fc-card">
      <label>Année
        <p-dropdown [options]="yearOptions" [(ngModel)]="selectedYear" (onChange)="onPeriodChange()" [ngModelOptions]="{standalone: true}" />
      </label>
      <label>Mois
        <p-dropdown [options]="monthOptions" [(ngModel)]="selectedMonth" (onChange)="onPeriodChange()" [ngModelOptions]="{standalone: true}" />
      </label>
      <a routerLink="/firm/governance/dossier-time-profitability" class="link-btn">Analyse rentabilité dossiers</a>
    </div>

    @if (currentPeriod(); as period) {
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
        @if (isManager()) {
          @if (period.isLocked) {
            <button type="button" pButton label="Rouvrir" icon="pi pi-lock-open" class="p-button-sm p-button-outlined" (click)="unlockPeriod()"></button>
          } @else {
            <button type="button" pButton label="Clôturer le mois" icon="pi pi-lock" class="p-button-sm p-button-outlined" (click)="lockPeriod()"></button>
          }
        }
      </div>
    }

    @if (anomalies().length > 0) {
      <div class="fc-card anomalies">
        <strong>Dépassements constatés</strong>
        <ul>
          @for (message of anomalies(); track message) {
            <li>{{ message }}</li>
          }
        </ul>
      </div>
    }

    <div class="fc-card entry-card">
      <form [formGroup]="form" (ngSubmit)="submit()" class="entry-form">
        <label>Date
          <input pInputText type="date" formControlName="workDate" />
        </label>
        <label>Heures
          <input pInputText type="number" step="0.25" min="0.25" max="24" formControlName="hours" />
        </label>
        <label>Client
          <p-dropdown
            formControlName="firmClientAssignmentId"
            [options]="clients()"
            optionLabel="companyName"
            optionValue="assignmentId"
            placeholder="Sélectionner (optionnel)"
            [showClear]="true"
            [filter]="true"
            filterBy="companyName"
            appendTo="body" />
        </label>
        <label>Code activité
          @if (activityCodes().length > 0) {
            <p-dropdown
              formControlName="activityCode"
              [options]="activityCodes()"
              optionLabel="label"
              optionValue="code"
              placeholder="Sélectionner"
              [showClear]="true"
              [filter]="true"
              filterBy="label,code"
              appendTo="body"
              (onChange)="onActivityCodeChange($event.value)" />
          } @else {
            <input pInputText formControlName="activityCode" placeholder="COMPTA, REVUE…" />
          }
        </label>
        <label>Notes
          <input pInputText formControlName="notes" />
        </label>
        <label class="checkbox-row">
          <p-checkbox formControlName="isBillable" [binary]="true" inputId="billable" />
          <span for="billable">Facturable</span>
        </label>
        <button type="submit" pButton [label]="editingId() ? 'Enregistrer' : 'Ajouter'" [icon]="editingId() ? 'pi pi-check' : 'pi pi-plus'" [disabled]="periodLocked()"></button>
        @if (editingId()) {
          <button type="button" pButton label="Annuler" class="p-button-text" (click)="cancelEdit()"></button>
        }
      </form>
      <p class="summary">
        Total période : <strong>{{ totalHours() | number:'1.2-2' }}</strong> h
        — facturables {{ billableHours() | number:'1.2-2' }} h,
        non facturables {{ nonBillableHours() | number:'1.2-2' }} h
        (taux de facturabilité {{ billableRatio() | number:'1.0-1' }} %)
      </p>
    </div>

    <div class="fc-card">
      @if (!loading() && entries().length === 0) {
        <app-empty-state
          title="Aucune feuille de temps"
          description="Saisissez vos heures pour la période sélectionnée."
          icon="pi pi-clock" />
      } @else {
        @if (isManager() && selection.length > 0) {
          <div class="bulk-bar">
            <span>{{ selection.length }} sélectionnée(s)</span>
            <button
              type="button"
              pButton
              label="Valider la sélection"
              icon="pi pi-check"
              class="p-button-sm p-button-outlined"
              [loading]="bulkValidating()"
              (click)="validateSelection()"></button>
          </div>
        }
        <p-table
          [value]="entries()"
          [loading]="loading()"
          [(selection)]="selection"
          dataKey="id">
          <ng-template pTemplate="header">
            <tr>
              @if (isManager()) {
                <th style="width: 3rem"><p-tableHeaderCheckbox></p-tableHeaderCheckbox></th>
              }
              <th>Date</th>
              <th>Collaborateur</th>
              <th>Client</th>
              <th>Heures</th>
              <th>Activité</th>
              <th>Facturable</th>
              <th>Statut</th>
              <th>Validée par</th>
              <th style="width: 180px">Actions</th>
            </tr>
          </ng-template>
          <ng-template pTemplate="body" let-e>
            <tr>
              @if (isManager()) {
                <td>
                  @if (!e.isValidated) {
                    <p-tableCheckbox [value]="e"></p-tableCheckbox>
                  }
                </td>
              }
              <td>{{ e.workDate | date:'shortDate' }}</td>
              <td>{{ e.userDisplayName }}</td>
              <td>{{ e.clientCompanyName || '—' }}</td>
              <td>{{ e.hours | number:'1.2-2' }}</td>
              <td>{{ e.activityCode || '—' }}</td>
              <td>{{ e.isBillable ? 'Oui' : 'Non' }}</td>
              <td>
                <p-tag [severity]="e.isValidated ? 'success' : 'warn'" [value]="e.isValidated ? 'Validée' : 'Brouillon'" />
              </td>
              <td class="trace">
                @if (e.isValidated) {
                  {{ e.validatedByDisplayName || '—' }}
                  <small>{{ e.validatedAt | date:'short' }}</small>
                } @else {
                  —
                }
              </td>
              <td class="actions">
                @if (!e.isValidated) {
                  <button type="button" pButton icon="pi pi-pencil" class="p-button-text p-button-sm" [disabled]="periodLocked()" (click)="startEdit(e)" title="Modifier"></button>
                  <button type="button" pButton icon="pi pi-trash" class="p-button-text p-button-danger p-button-sm" [disabled]="periodLocked()" (click)="remove(e)" title="Supprimer"></button>
                }
                @if (isManager() && !e.isValidated) {
                  <button type="button" pButton icon="pi pi-check" class="p-button-text p-button-success p-button-sm" [disabled]="periodLocked()" (click)="validate(e)" title="Valider"></button>
                }
                @if (isManager() && e.isValidated) {
                  <button type="button" pButton icon="pi pi-undo" class="p-button-text p-button-sm" [disabled]="periodLocked()" (click)="unvalidate(e)" title="Repasser en brouillon"></button>
                }
              </td>
            </tr>
          </ng-template>
        </p-table>
      }
    </div>
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
    .toolbar { display: flex; flex-wrap: wrap; gap: .75rem; align-items: flex-end; }
    .toolbar label { display: flex; flex-direction: column; font-size: .875rem; gap: .25rem; min-width: 120px; }
    .link-btn { margin-left: auto; font-size: .875rem; color: var(--color-primary, #0f766e); }
    .entry-form { display: flex; flex-wrap: wrap; gap: .75rem; align-items: flex-end; }
    .entry-form label { display: flex; flex-direction: column; font-size: .875rem; gap: .25rem; min-width: 140px; }
    .checkbox-row { flex-direction: row !important; align-items: center; gap: .5rem; min-width: auto; }
    .summary { margin: .75rem 0 0; font-size: .875rem; color: var(--color-text-muted, #64748b); }
    .actions { display: flex; gap: .15rem; }
    .period-bar { display: flex; align-items: center; gap: .75rem; flex-wrap: wrap; font-size: .875rem; }
    .period-bar.locked { border-color: #f59e0b; background: #fffbeb; }
    .period-state { display: inline-flex; align-items: center; gap: .35rem; font-weight: 600; }
    .period-detail { color: var(--color-text-muted, #64748b); }
    .period-bar button { margin-left: auto; }
    .anomalies { border-color: #f59e0b; background: #fffbeb; font-size: .875rem; }
    .anomalies ul { margin: .5rem 0 0; padding-left: 1.25rem; }
    .bulk-bar { display: flex; align-items: center; gap: .75rem; padding-bottom: .75rem; font-size: .875rem; }
    .trace { font-size: .8125rem; }
    .trace small { display: block; color: var(--color-text-muted, #64748b); }
  `]
})
export class FirmTimeSheetsComponent implements OnInit {
  private readonly api = inject(FirmGovernanceService);
  private readonly assignments = inject(FirmAssignmentService);
  private readonly fb = inject(FormBuilder);
  private readonly toast = inject(ToastService);
  private readonly route = inject(ActivatedRoute);
  private readonly auth = inject(AuthService);

  entries = signal<FirmTimeSheetEntry[]>([]);
  clients = signal<FirmClientDossier[]>([]);
  activityCodes = signal<FirmActivityCode[]>([]);
  periods = signal<FirmTimeSheetPeriod[]>([]);
  anomalies = signal<string[]>([]);
  loading = signal(false);
  bulkValidating = signal(false);
  filterUserId = signal<string | null>(null);
  editingId = signal<string | null>(null);
  isManager = computed(() => this.auth.isFirmManager());

  selection: FirmTimeSheetEntry[] = [];

  readonly yearOptions = Array.from({ length: 6 }, (_, i) => {
    const y = new Date().getFullYear() - 2 + i;
    return { label: String(y), value: y };
  });

  /** `null` = toute l'année : un mois imposé empêchait toute lecture annuelle. */
  readonly monthOptions: { label: string; value: number | null }[] = [
    { label: "Toute l'année", value: null },
    ...Array.from({ length: 12 }, (_, i) => ({
      label: new Date(2026, i, 1).toLocaleString('fr-FR', { month: 'long' }),
      value: i + 1 as number | null
    }))
  ];

  selectedYear = new Date().getFullYear();
  selectedMonth: number | null = new Date().getMonth() + 1;

  totalHours = computed(() => this.entries().reduce((s, e) => s + e.hours, 0));
  billableHours = computed(() => this.entries().filter(e => e.isBillable).reduce((s, e) => s + e.hours, 0));
  nonBillableHours = computed(() => this.totalHours() - this.billableHours());
  billableRatio = computed(() => {
    const total = this.totalHours();
    return total > 0 ? (this.billableHours() / total) * 100 : 0;
  });

  /** Verrou du mois affiché. Sur la vue annuelle, aucun verrou ne s'applique à la saisie. */
  currentPeriod = computed<FirmTimeSheetPeriod | null>(() => {
    const month = this.selectedMonth;
    if (month === null) return null;
    return this.periods().find(p => p.month === month) ?? null;
  });

  periodLocked = computed(() => this.currentPeriod()?.isLocked === true);

  form = this.fb.group({
    workDate: [FirmTimeSheetsComponent.todayLocalIso(), Validators.required],
    hours: [1, [Validators.required, Validators.min(0.25)]],
    firmClientAssignmentId: ['' as string],
    activityCode: [''],
    notes: [''],
    isBillable: [true]
  });

  /**
   * Date du jour au format `YYYY-MM-DD` en heure locale.
   *
   * `toISOString()` convertit en UTC : à Tunis (UTC+1), entre minuit et 1 h, il renvoyait la
   * veille et pré-remplissait la saisie avec la mauvaise journée.
   */
  private static todayLocalIso(): string {
    const now = new Date();
    const month = `${now.getMonth() + 1}`.padStart(2, '0');
    const day = `${now.getDate()}`.padStart(2, '0');
    return `${now.getFullYear()}-${month}-${day}`;
  }

  ngOnInit(): void {
    const qp = this.route.snapshot.queryParamMap;
    this.filterUserId.set(qp.get('userId'));
    const assignmentId = qp.get('assignmentId');
    if (assignmentId) {
      this.form.patchValue({ firmClientAssignmentId: assignmentId });
    }
    this.loadClients();
    this.loadActivityCodes();
    this.loadPeriods();
    this.load();
  }

  onPeriodChange(): void {
    this.selection = [];
    this.loadPeriods();
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.selection = [];
    const userId = this.filterUserId() ?? undefined;
    this.api.listTimeSheets(this.selectedYear, this.selectedMonth ?? undefined, userId).subscribe({
      next: res => {
        this.entries.set(res.data ?? []);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.toast.add({ severity: 'error', summary: 'Erreur', detail: 'Chargement impossible.' });
      }
    });
  }

  private loadPeriods(): void {
    this.api.listTimeSheetPeriods(this.selectedYear).subscribe({
      next: res => this.periods.set(res.data ?? []),
      error: () => this.periods.set([])
    });
  }

  private loadActivityCodes(): void {
    this.api.listActivityCodes().subscribe({
      next: res => this.activityCodes.set(res.data ?? []),
      error: () => this.activityCodes.set([])
    });
  }

  /** Le code choisi propose sa facturabilité par défaut, sans la figer. */
  onActivityCodeChange(code: string | null): void {
    const match = this.activityCodes().find(c => c.code === code);
    if (match) {
      this.form.patchValue({ isBillable: match.isBillableByDefault });
    }
  }

  lockPeriod(): void {
    const month = this.selectedMonth;
    if (month === null) return;
    this.api.lockTimeSheetPeriod(this.selectedYear, month).subscribe({
      next: () => {
        this.loadPeriods();
        this.toast.add({ severity: 'success', summary: 'Période clôturée', detail: 'Plus aucune écriture n\'est acceptée sur ce mois.' });
      },
      error: (err) => this.toast.add({
        severity: 'error',
        summary: 'Erreur',
        detail: err?.error?.message || 'Clôture impossible.'
      })
    });
  }

  unlockPeriod(): void {
    const month = this.selectedMonth;
    if (month === null) return;
    // Le motif est la pièce justificative de la réouverture : le serveur le refuse s'il est vide.
    const reason = window.prompt('Motif de réouverture de la période (obligatoire) :');
    if (!reason?.trim()) return;

    this.api.unlockTimeSheetPeriod(this.selectedYear, month, reason.trim()).subscribe({
      next: () => {
        this.loadPeriods();
        this.toast.add({ severity: 'success', summary: 'Période rouverte', detail: 'La réouverture est tracée.' });
      },
      error: (err) => this.toast.add({
        severity: 'error',
        summary: 'Erreur',
        detail: err?.error?.message || 'Réouverture impossible.'
      })
    });
  }

  validateSelection(): void {
    const ids = this.selection.filter(e => !e.isValidated).map(e => e.id);
    if (ids.length === 0) return;

    this.bulkValidating.set(true);
    this.api.validateTimeSheetsBulkDetailed(ids).subscribe({
      next: res => {
        this.bulkValidating.set(false);
        this.selection = [];
        this.load();
        const result = res.data;
        if (result && result.skipped > 0) {
          this.toast.add({
            severity: 'warn',
            summary: 'Validation partielle',
            detail: `${result.validated} validée(s), ${result.skipped} ignorée(s) : ${result.failures[0]?.error ?? ''}`
          });
        } else {
          this.toast.add({
            severity: 'success',
            summary: 'Validées',
            detail: `${result?.validated ?? ids.length} feuille(s) validée(s).`
          });
        }
      },
      error: (err) => {
        this.bulkValidating.set(false);
        this.toast.add({
          severity: 'error',
          summary: 'Erreur',
          detail: err?.error?.message || 'Validation impossible.'
        });
      }
    });
  }

  unvalidate(e: FirmTimeSheetEntry): void {
    this.api.unvalidateTimeSheet(e.id).subscribe({
      next: () => {
        this.load();
        this.toast.add({ severity: 'success', summary: 'Brouillon', detail: 'Feuille de temps repassée en brouillon.' });
      },
      error: (err) => this.toast.add({
        severity: 'error',
        summary: 'Erreur',
        detail: err?.error?.message || 'Dévalidation impossible.'
      })
    });
  }

  submit(): void {
    if (this.form.invalid) return;
    const v = this.form.getRawValue();
    const body = {
      workDate: v.workDate!,
      hours: v.hours!,
      firmClientAssignmentId: v.firmClientAssignmentId || undefined,
      activityCode: v.activityCode || undefined,
      notes: v.notes || undefined,
      isBillable: v.isBillable ?? true
    };
    const editId = this.editingId();
    const req$ = editId
      ? this.api.updateTimeSheet(editId, body)
      : this.api.createTimeSheet(body);

    req$.subscribe({
      next: (res) => {
        this.cancelEdit();
        this.load();
        // Sur un exercice dont les plafonds ne sont pas opposables, la saisie passe mais le
        // dépassement doit rester visible plutôt que d'être avalé en silence.
        this.anomalies.set(res.data?.warnings ?? []);
        this.toast.add({
          severity: res.data?.warnings?.length ? 'warn' : 'success',
          summary: 'Feuille de temps',
          detail: editId ? 'Modification enregistrée.' : 'Saisie enregistrée.'
        });
      },
      error: (err) => {
        this.anomalies.set([]);
        this.toast.add({
          severity: 'error',
          summary: 'Erreur',
          detail: err?.error?.message || 'Saisie impossible.'
        });
      }
    });
  }

  startEdit(e: FirmTimeSheetEntry): void {
    this.editingId.set(e.id);
    this.form.patchValue({
      workDate: e.workDate.slice(0, 10),
      hours: e.hours,
      firmClientAssignmentId: e.firmClientAssignmentId ?? '',
      activityCode: e.activityCode ?? '',
      notes: e.notes ?? '',
      isBillable: e.isBillable
    });
  }

  cancelEdit(): void {
    this.editingId.set(null);
    this.form.reset({
      workDate: FirmTimeSheetsComponent.todayLocalIso(),
      hours: 1,
      firmClientAssignmentId: this.route.snapshot.queryParamMap.get('assignmentId') ?? '',
      activityCode: '',
      notes: '',
      isBillable: true
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

  private loadClients(): void {
    this.assignments.getActiveClients().subscribe({
      next: r => { if (r.success) this.clients.set(r.data ?? []); }
    });
  }
}
