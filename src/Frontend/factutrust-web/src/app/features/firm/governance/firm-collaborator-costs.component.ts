import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { TableModule } from 'primeng/table';
import { ButtonModule } from 'primeng/button';
import { SelectModule } from 'primeng/select';
import { InputNumberModule } from 'primeng/inputnumber';
import { DialogModule } from 'primeng/dialog';
import { TagModule } from 'primeng/tag';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { ConfirmationService } from 'primeng/api';
import { PageHeaderComponent } from '@shared/components/page-header/page-header.component';
import { EmptyStateComponent } from '@shared/components/empty-state/empty-state.component';
import {
  FirmCollaboratorCostSyncResult,
  FirmCollaboratorYearCost,
  FirmGovernanceService,
  FirmPayrollCostSnapshot,
  FirmPayrollProvisioningStatus,
  FirmTimeSheetYearSettings
} from '@core/services/firm-governance.service';
import { ToastService } from '@core/services/toast.service';

@Component({
  selector: 'app-firm-collaborator-costs',
  standalone: true,
  providers: [ConfirmationService],
  imports: [
    CommonModule,
    FormsModule,
    ReactiveFormsModule,
    RouterModule,
    TableModule,
    ButtonModule,
    SelectModule,
    InputNumberModule,
    DialogModule,
    TagModule,
    ConfirmDialogModule,
    PageHeaderComponent,
    EmptyStateComponent
  ],
  template: `
    <p-confirmDialog></p-confirmDialog>

    <app-page-header
      title="Coûts collaborateurs"
      subtitle="Coût employeur annuel et taux horaire — alimentation depuis la paie du cabinet">
      <button
        type="button"
        pButton
        label="Synchroniser"
        icon="pi pi-sync"
        class="p-button-sm"
        [loading]="syncing()"
        (click)="sync(false)"></button>
      <button
        type="button"
        pButton
        label="Importer (forcer)"
        icon="pi pi-download"
        class="p-button-sm p-button-outlined"
        [loading]="syncing()"
        (click)="confirmForceImport()"></button>
    </app-page-header>

    <div class="fc-card toolbar">
      <label>Année
        <p-select [options]="yearOptions" [(ngModel)]="selectedYear" (onChange)="load()" />
      </label>
      <button type="button" pButton label="Actualiser" icon="pi pi-refresh" class="p-button-sm p-button-outlined" (click)="load()"></button>

      @if (yearSettings(); as s) {
        <div class="hours-mode">
          <span class="mode-label">Heures productives</span>
          <button
            type="button"
            pButton
            [label]="s.productiveHoursMode === 1 ? 'Congés réels (individualisé)' : 'Forfait de l\\'exercice'"
            [icon]="s.productiveHoursMode === 1 ? 'pi pi-user' : 'pi pi-users'"
            class="p-button-sm p-button-outlined"
            [loading]="savingMode()"
            (click)="confirmToggleHoursMode()"></button>
          <small class="muted">
            {{ s.annualProductiveHours | number:'1.0-2' }} h de référence · cliquez pour changer de mode
          </small>
        </div>
      }
    </div>

    @if (provisioningStatus()?.internalPayrollEnabled && needsProvisioning()) {
      <div class="fc-card alert-banner provision-banner">
        <strong>Salariés paie manquants</strong>
        <p>{{ unprovisionedCount() }} collaborateur(s) actif(s) sans salarié paie. Provisionnez depuis les collaborateurs avant de lancer un cycle.</p>
        <div class="banner-actions">
          <button
            type="button"
            pButton
            label="Provisionner depuis les collaborateurs"
            icon="pi pi-user-plus"
            class="p-button-sm"
            [loading]="provisioning()"
            (click)="provisionAll()"></button>
          <a routerLink="/firm/payroll" class="link">Ouvrir la paie interne</a>
        </div>
      </div>
    }

    @if (incompleteIdentities().length > 0) {
      <div class="fc-card alert-banner">
        <strong>À compléter avant la déclaration sociale</strong>
        <p>Ces salariés sont provisionnés mais il leur manque une mention obligatoire :</p>
        <ul class="identity-list">
          @for (row of incompleteIdentities(); track row.collaboratorUserId) {
            <li>
              <a [routerLink]="['/firm/payroll/employees', row.payrollEmployeeId, 'edit']">{{ row.collaboratorName }}</a>
              — {{ row.missingPayrollIdentifiers?.join(', ') }}
            </li>
          }
        </ul>
        <p class="muted">La paie reste calculable ; seule la déclaration sociale sera bloquée.</p>
      </div>
    }

    @if (payrollUnavailable()) {
      <div class="fc-card alert-banner">
        <strong>Paie du cabinet indisponible</strong>
        <p>{{ payrollUnavailableReason() || 'Aucune paie validée ou clôturée exploitable pour cette année.' }}</p>
        @if (payrollSnapshot()?.activeEmployeeCount != null && payrollSnapshot()!.activeEmployeeCount! > 0) {
          <p class="muted">{{ payrollSnapshot()!.activeEmployeeCount }} salarié(s) actif(s) dans la paie interne.</p>
        }
        <a routerLink="/firm/payroll" class="link">Ouvrir la paie interne du cabinet</a>
      </div>
    }

    @if (lastSync(); as sync) {
      <div class="fc-card sync-summary">
        <strong>Dernière synchronisation</strong>
        <span>{{ sync.syncedAt | date:'short' }}</span>
        @if (sync.linkedByEmail > 0) {
          <span>{{ sync.linkedByEmail }} liaison(s) auto par email</span>
        }
        @if (sync.imported > 0) {
          <span>{{ sync.imported }} import(s)</span>
        }
        @if (sync.skippedManual > 0) {
          <span>{{ sync.skippedManual }} ligne(s) saisie(s) conservée(s)</span>
        }
        @if (sync.skippedUnlinked > 0) {
          <span>{{ sync.skippedUnlinked }} non lié(s)</span>
        }
      </div>
    }

    <div class="fc-card">
      @if (!loading() && rows().length === 0) {
        <app-empty-state
          title="Aucun collaborateur actif"
          description="Synchronisez depuis la paie ou saisissez les coûts manuellement."
          icon="pi pi-users" />
      } @else {
        <p-table [value]="rows()" [loading]="loading()" responsiveLayout="scroll" [paginator]="true" [rows]="20">
          <ng-template pTemplate="header">
            <tr>
              <th>Collaborateur</th>
              <th>Brut annuel</th>
              <th>Charges patronales</th>
              <th>Extras</th>
              <th>Total employeur</th>
              <th>Taux effectif</th>
              <th>Source</th>
              <th>Liaison paie</th>
              <th>Dernière sync</th>
              <th style="width: 6rem"></th>
            </tr>
          </ng-template>
          <ng-template pTemplate="body" let-row>
            <tr>
              <td>{{ row.collaboratorName }}</td>
              <td>{{ row.grossAnnualSalary | number:'1.3-3' }}</td>
              <td>{{ row.employerContributions | number:'1.3-3' }}</td>
              <td>{{ row.payrollExtras | number:'1.3-3' }}</td>
              <td>
                <strong>{{ row.totalEmployerCost | number:'1.3-3' }}</strong>
                @if (row.payslipCount != null && row.payslipCount < 12 && row.totalEmployerCost > 0) {
                  <small class="muted">({{ row.payslipCount }}/12 bulletins)</small>
                }
              </td>
              <td>
                {{ row.effectiveHourlyRate | number:'1.2-2' }}
                <small class="muted" [title]="row.hourlyRateBasis">{{ row.hourlyRateSourceDisplay }}</small>
                @if (hasLeaveGap(row)) {
                  <!-- Écart entre congés réellement pris et jours paramétrés : c'est ce qui
                       permet au cabinet d'ajuster ses paramètres d'exercice en connaissance. -->
                  <small class="muted leave-gap" [title]="row.productiveHoursBasis">
                    {{ row.realAbsenceDays | number:'1.0-2' }} j réels
                    vs {{ row.parametricLeaveDays | number:'1.0-2' }} j paramétrés
                  </small>
                }
              </td>
              <td>
                {{ row.sourceDisplay }}
                @if (row.costDiagnostic !== 0) {
                  <small class="muted diagnostic" [title]="row.costDiagnosticHint || ''">
                    {{ row.costDiagnosticDisplay }}
                  </small>
                }
              </td>
              <td>
                @if (row.payrollEmployeeId) {
                  <!-- Lié : le nom peut manquer si la paie est injoignable — ce n'est pas « non lié ». -->
                  {{ row.payrollEmployeeName || 'Salarié lié' }}
                  @if (row.payrollLinkSourceDisplay) {
                    <p-tag [value]="row.payrollLinkSourceDisplay" [severity]="linkTagSeverity(row.payrollLinkSource)" />
                  }
                  @if (!row.payrollEmployeeName) {
                    <small class="muted">Paie non lisible pour cet exercice</small>
                  }
                } @else {
                  <span class="muted">Non lié</span>
                  <button
                    type="button"
                    pButton
                    label="Lier"
                    icon="pi pi-link"
                    class="p-button-text p-button-sm"
                    [disabled]="payrollEmployeeOptions().length === 0"
                    (click)="openEdit(row)"></button>
                }
              </td>
              <td>{{ row.importedAt ? (row.importedAt | date:'short') : '—' }}</td>
              <td>
                <button
                  type="button"
                  pButton
                  icon="pi pi-pencil"
                  class="p-button-text p-button-sm"
                  title="Modifier"
                  (click)="openEdit(row)"></button>
              </td>
            </tr>
          </ng-template>
        </p-table>
      }
    </div>

    <p-dialog
      header="Modifier le coût employeur"
      [(visible)]="editVisible"
      [modal]="true"
      [style]="{ width: '520px' }"
      (onHide)="closeEdit()">
      @if (editing()) {
        <form [formGroup]="editForm" class="edit-form">
          <p class="muted">{{ editing()!.collaboratorName }} — {{ selectedYear }}</p>
          <label>Salaire brut annuel
            <p-inputNumber formControlName="grossAnnualSalary" mode="decimal" [minFractionDigits]="3" [maxFractionDigits]="3" />
          </label>
          <label>Charges patronales
            <p-inputNumber formControlName="employerContributions" mode="decimal" [minFractionDigits]="3" [maxFractionDigits]="3" />
          </label>
          <label>Extras paie
            <p-inputNumber formControlName="payrollExtras" mode="decimal" [minFractionDigits]="3" [maxFractionDigits]="3" />
          </label>
          <label>Salarié paie lié
            <p-select
              formControlName="payrollEmployeeId"
              [options]="payrollEmployeeOptions()"
              optionLabel="label"
              optionValue="value"
              [showClear]="true"
              placeholder="Aucun"
              appendTo="body" />
          </label>
          <label>Override taux horaire
            <p-inputNumber formControlName="hourlyRateOverride" mode="decimal" [minFractionDigits]="2" [maxFractionDigits]="2" />
          </label>
          <label>Justification override
            <textarea formControlName="overrideJustification" rows="2" class="textarea"></textarea>
          </label>
        </form>
      }
      <ng-template pTemplate="footer">
        <button type="button" pButton label="Annuler" class="p-button-text" (click)="closeEdit()"></button>
        <button
          type="button"
          pButton
          label="Enregistrer"
          [loading]="saving()"
          [disabled]="editForm.invalid || saving()"
          (click)="saveEdit()"></button>
      </ng-template>
    </p-dialog>
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
    .alert-banner { border-color: #f59e0b; background: #fffbeb; font-size: .875rem; }
    .alert-banner p { margin: .35rem 0; }
    .banner-actions { display: flex; flex-wrap: wrap; gap: .75rem; align-items: center; margin-top: .35rem; }
    .link { color: var(--color-primary, #0f766e); font-size: .875rem; }
    .sync-summary {
      display: flex; flex-wrap: wrap; gap: .5rem 1.25rem;
      font-size: .875rem; background: var(--color-surface-muted, #f8fafc);
    }
    .muted { color: var(--color-text-muted, #64748b); font-size: .8rem; display: block; }
    .diagnostic { color: #b45309; cursor: help; }
    .leave-gap { color: #0369a1; cursor: help; }
    .hours-mode { display: flex; flex-direction: column; gap: .25rem; margin-left: auto; }
    .hours-mode .mode-label { font-size: .875rem; }
    .identity-list { margin: .35rem 0; padding-left: 1.25rem; }
    .identity-list li { margin: .15rem 0; }
    .edit-form { display: flex; flex-direction: column; gap: .75rem; }
    .edit-form label { display: flex; flex-direction: column; gap: .25rem; font-size: .875rem; }
    .textarea {
      width: 100%; padding: .5rem; border: 1px solid var(--color-border-subtle, #e2e8f0);
      border-radius: 8px; font-family: inherit;
    }
  `]
})
export class FirmCollaboratorCostsComponent implements OnInit {
  private readonly api = inject(FirmGovernanceService);
  private readonly toast = inject(ToastService);
  private readonly confirm = inject(ConfirmationService);
  private readonly fb = inject(FormBuilder);

  selectedYear = new Date().getFullYear();
  yearOptions = Array.from({ length: 6 }, (_, i) => new Date().getFullYear() - i);

  rows = signal<FirmCollaboratorYearCost[]>([]);
  loading = signal(false);
  syncing = signal(false);
  saving = signal(false);
  lastSync = signal<FirmCollaboratorCostSyncResult | null>(null);
  payrollSnapshot = signal<FirmPayrollCostSnapshot | null>(null);
  provisioningStatus = signal<FirmPayrollProvisioningStatus | null>(null);
  provisioning = signal(false);
  yearSettings = signal<FirmTimeSheetYearSettings | null>(null);
  savingMode = signal(false);

  editVisible = false;
  editing = signal<FirmCollaboratorYearCost | null>(null);

  editForm = this.fb.group({
    grossAnnualSalary: [0, Validators.required],
    employerContributions: [0 as number | null],
    payrollExtras: [0, Validators.required],
    payrollEmployeeId: [null as string | null],
    hourlyRateOverride: [null as number | null],
    overrideJustification: ['']
  });

  payrollEmployeeOptions = computed(() => {
    const employees = this.payrollSnapshot()?.employees ?? [];
    return employees.map(e => ({
      label: e.employeeName,
      value: e.payrollEmployeeId
    }));
  });

  payrollUnavailable = computed(() => {
    const snap = this.payrollSnapshot();
    return snap !== null && !snap.isAvailable;
  });

  payrollUnavailableReason = computed(() => this.payrollSnapshot()?.unavailableReason ?? null);

  needsProvisioning = computed(() => {
    const status = this.provisioningStatus();
    if (!status?.internalPayrollEnabled) return false;
    return status.collaborators.some(c => !c.hasPayrollEmployee);
  });

  unprovisionedCount = computed(() => {
    const status = this.provisioningStatus();
    if (!status) return 0;
    return status.collaborators.filter(c => !c.hasPayrollEmployee).length;
  });

  incompleteIdentities = computed(() => {
    const status = this.provisioningStatus();
    if (!status?.internalPayrollEnabled) return [];
    return status.collaborators.filter(c => (c.missingPayrollIdentifiers?.length ?? 0) > 0);
  });

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.api.listCollaboratorCosts(this.selectedYear).subscribe({
      next: res => {
        this.rows.set(res.data ?? []);
        this.loading.set(false);
      },
      error: err => {
        this.loading.set(false);
        this.toast.add({
          severity: 'error',
          summary: 'Erreur',
          detail: err?.error?.message || 'Chargement impossible.'
        });
      }
    });
    this.api.listPayrollEmployees(this.selectedYear).subscribe({
      next: res => this.payrollSnapshot.set(res.data ?? null),
      error: () => this.payrollSnapshot.set({ isAvailable: false, employees: [] })
    });
    this.api.getPayrollProvisioningStatus().subscribe({
      next: res => this.provisioningStatus.set(res.data ?? null),
      error: () => this.provisioningStatus.set(null)
    });
    this.api.getTimeSheetYearSettings(this.selectedYear).subscribe({
      next: res => this.yearSettings.set(res.data ?? null),
      error: () => this.yearSettings.set(null)
    });
  }

  confirmToggleHoursMode(): void {
    const current = this.yearSettings();
    if (!current) return;

    const toIndividual = (current.productiveHoursMode ?? 0) === 0;
    this.confirm.confirm({
      header: 'Mode de calcul des heures productives',
      message: toIndividual
        ? `Les heures productives seront calculées à partir des congés réellement approuvés de chaque collaborateur, et de sa présence sur ${this.selectedYear}. Les taux horaires — donc les marges — vont changer. Les rentabilités déjà enregistrées ne bougeront qu'au prochain « Recalculer ».`
        : `Retour au forfait commun de l'exercice ${this.selectedYear} pour tous les collaborateurs. Les taux horaires vont changer.`,
      icon: 'pi pi-exclamation-triangle',
      accept: () => this.saveHoursMode(current, toIndividual ? 1 : 0)
    });
  }

  private saveHoursMode(current: FirmTimeSheetYearSettings, mode: number): void {
    this.savingMode.set(true);
    // Le PUT attend l'exercice complet : on repart des valeurs en place et on ne change que le mode.
    this.api.saveTimeSheetYearSettings(this.selectedYear, {
      weeklyRegime: current.weeklyRegime,
      maxDailyHours: current.maxDailyHours,
      maxWeeklyHours: current.maxWeeklyHours,
      allowFutureEntryDays: current.allowFutureEntryDays,
      maxBackdatingDays: current.maxBackdatingDays,
      enforceHardLimits: current.enforceHardLimits,
      paidLeaveDaysPerYear: current.paidLeaveDaysPerYear,
      publicHolidayDaysPerYear: current.publicHolidayDaysPerYear,
      productivityRatePercent: current.productivityRatePercent,
      cnssEmployerRate: current.cnssEmployerRate,
      tfpRate: current.tfpRate,
      foprolosRate: current.foprolosRate,
      workAccidentRate: current.workAccidentRate,
      cssEmployerRate: current.cssEmployerRate,
      productiveHoursMode: mode
    }).subscribe({
      next: res => {
        this.savingMode.set(false);
        if (res.success) {
          this.toast.add({
            severity: 'success',
            summary: 'Heures productives',
            detail: mode === 1 ? 'Mode individualisé activé.' : 'Forfait de l\'exercice rétabli.'
          });
          this.load();
        } else {
          this.toast.add({ severity: 'error', summary: 'Erreur', detail: res.message ?? '' });
        }
      },
      error: err => {
        this.savingMode.set(false);
        this.toast.add({
          severity: 'error',
          summary: 'Erreur',
          detail: err?.error?.message || 'Changement de mode impossible.'
        });
      }
    });
  }

  provisionAll(): void {
    this.provisioning.set(true);
    this.api.provisionPayrollFromCollaborators().subscribe({
      next: res => {
        this.provisioning.set(false);
        const data = res.data;
        if (data) {
          this.toast.add({
            severity: 'success',
            summary: 'Provision paie',
            detail: `${data.created} créé(s), ${data.linked} lié(s)${data.skipped ? `, ${data.skipped} ignoré(s)` : ''}.`
          });
        }
        this.load();
      },
      error: err => {
        this.provisioning.set(false);
        this.toast.add({
          severity: 'error',
          summary: 'Provision paie',
          detail: err?.error?.message || 'Provision impossible.'
        });
      }
    });
  }

  sync(force: boolean): void {
    this.syncing.set(true);
    this.api.syncCollaboratorCosts(this.selectedYear, force).subscribe({
      next: res => {
        this.syncing.set(false);
        if (res.data) {
          this.lastSync.set(res.data);
          const parts = [
            res.data.linkedByEmail > 0 ? `${res.data.linkedByEmail} lié(s) par email` : null,
            res.data.imported > 0 ? `${res.data.imported} importé(s)` : null,
            res.data.skippedManual > 0 ? `${res.data.skippedManual} saisie(s) conservée(s)` : null,
            res.data.skippedUnlinked > 0 ? `${res.data.skippedUnlinked} sans liaison` : null
          ].filter(Boolean);
          // Paie indisponible n'est pas un échec : les liaisons ont pu être établies malgré tout.
          const fallback = res.data.payrollAvailable
            ? 'Aucune mise à jour nécessaire.'
            : res.data.unavailableReason || 'Paie du cabinet indisponible.';
          this.toast.add({
            severity: res.data.payrollAvailable ? 'success' : 'warn',
            summary: 'Synchronisation',
            detail: parts.length ? parts.join(' · ') : fallback
          });
        }
        this.load();
      },
      error: err => {
        this.syncing.set(false);
        this.toast.add({
          severity: 'error',
          summary: 'Erreur',
          detail: err?.error?.message || 'Synchronisation impossible.'
        });
      }
    });
  }

  confirmForceImport(): void {
    const manualCount = this.rows().filter(r => r.source === 1).length;
    if (manualCount === 0) {
      this.sync(true);
      return;
    }
    this.confirm.confirm({
      message: `${manualCount} ligne(s) saisie(s) manuellement seront écrasées par les bulletins de paie. Continuer ?`,
      header: 'Import forcé',
      icon: 'pi pi-exclamation-triangle',
      accept: () => this.sync(true)
    });
  }

  /**
   * Vrai quand les congés réellement pris s'écartent du forfait de l'exercice.
   * Affiché quel que soit le mode : en paramétrique c'est un signal d'ajustement,
   * en individualisé c'est l'explication du taux.
   */
  hasLeaveGap(row: FirmCollaboratorYearCost): boolean {
    const real = row.realAbsenceDays ?? 0;
    const parametric = row.parametricLeaveDays ?? 0;
    return Math.abs(real - parametric) >= 0.5;
  }

  linkTagSeverity(source?: number): 'success' | 'info' | 'secondary' {
    if (source === 2) return 'success';
    if (source === 1) return 'info';
    return 'secondary';
  }

  openEdit(row: FirmCollaboratorYearCost): void {
    this.editing.set(row);
    this.editForm.reset({
      grossAnnualSalary: row.grossAnnualSalary,
      employerContributions: row.employerContributions,
      payrollExtras: row.payrollExtras,
      payrollEmployeeId: row.payrollEmployeeId ?? null,
      hourlyRateOverride: row.hourlyRateOverride ?? null,
      overrideJustification: row.overrideJustification ?? ''
    });
    this.editVisible = true;
  }

  closeEdit(): void {
    this.editVisible = false;
    this.editing.set(null);
  }

  saveEdit(): void {
    const row = this.editing();
    if (!row || this.editForm.invalid) return;

    const v = this.editForm.getRawValue();
    if (v.hourlyRateOverride != null && !v.overrideJustification?.trim()) {
      this.toast.add({
        severity: 'warn',
        summary: 'Validation',
        detail: 'La justification est obligatoire pour un override de taux.'
      });
      return;
    }

    this.saving.set(true);
    const linkId = v.payrollEmployeeId ?? null;
    const previousLink = row.payrollEmployeeId ?? null;

    const saveCost = () => {
      this.api.saveCollaboratorCost(this.selectedYear, row.collaboratorUserId, {
        grossAnnualSalary: v.grossAnnualSalary!,
        employerContributions: v.employerContributions,
        payrollExtras: v.payrollExtras!,
        hourlyRateOverride: v.hourlyRateOverride,
        overrideJustification: v.overrideJustification || null
      }).subscribe({
        next: () => {
          this.saving.set(false);
          this.toast.add({ severity: 'success', summary: 'Enregistré', detail: 'Coût mis à jour.' });
          this.closeEdit();
          this.load();
        },
        error: err => {
          this.saving.set(false);
          this.toast.add({
            severity: 'error',
            summary: 'Erreur',
            detail: err?.error?.message || 'Enregistrement impossible.'
          });
        }
      });
    };

    if (linkId !== previousLink) {
      this.api.linkPayrollEmployee(row.collaboratorUserId, linkId).subscribe({
        next: () => saveCost(),
        error: err => {
          this.saving.set(false);
          this.toast.add({
            severity: 'error',
            summary: 'Erreur liaison paie',
            detail: err?.error?.message || 'Liaison impossible.'
          });
        }
      });
    } else {
      saveCost();
    }
  }
}
