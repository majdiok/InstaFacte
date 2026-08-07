import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule, Location } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { TableModule } from 'primeng/table';
import { ButtonModule } from 'primeng/button';
import { SelectModule } from 'primeng/select';
import { InputNumberModule } from 'primeng/inputnumber';
import { PageHeaderComponent } from '@shared/components/page-header/page-header.component';
import {
  FirmCollaboratorRentabilityDetail,
  FirmGovernanceService,
  FirmRentabilityPayrollRow,
  FirmRentabilityPortfolioRow,
  SaveFirmCollaboratorRentability
} from '@core/services/firm-governance.service';
import { FirmCollaboratorsService, FirmUser } from '@core/services/firm-collaborators.service';
import { ToastService } from '@core/services/toast.service';

type FormMode = 'create' | 'edit' | 'view';

@Component({
  selector: 'app-firm-collaborator-rentability-form',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    TableModule,
    ButtonModule,
    SelectModule,
    InputNumberModule,
    PageHeaderComponent
  ],
  template: `
    <app-page-header [title]="title()" [subtitle]="subtitle()">
      <button type="button" pButton label="Retour" icon="pi pi-arrow-left" class="p-button-outlined p-button-sm" (click)="back()"></button>
      @if (mode() === 'view') {
        <button type="button" pButton label="Modifier" icon="pi pi-pencil" class="p-button-sm" (click)="goEdit()"></button>
      }
      @if (mode() !== 'view') {
        <button
          type="button"
          pButton
          label="Enregistrer"
          icon="pi pi-save"
          class="p-button-sm"
          [loading]="saving()"
          [disabled]="form.invalid || saving()"
          (click)="save()"></button>
      }
    </app-page-header>

    @if (loading()) {
      <div class="fc-card muted">Chargement…</div>
    } @else {
      <div class="fc-card">
        <form [formGroup]="form" class="meta-form">
          <label>Collaborateur
            <p-select
              formControlName="collaboratorUserId"
              [options]="collaboratorOptions"
              optionLabel="label"
              optionValue="value"
              [filter]="true"
              filterBy="label"
              placeholder="Sélectionner"
              appendTo="body" />
          </label>
          <label>Année
            <p-select formControlName="year" [options]="yearOptions" />
          </label>
          @if (mode() === 'create') {
            <button type="button" pButton label="Charger le préremplissage" icon="pi pi-refresh" class="p-button-sm p-button-outlined" (click)="loadPrefill()"></button>
          }
        </form>
      </div>

      <div class="fc-card">
        <h3>Chiffre d'affaires produit</h3>
        <p class="muted">
          Les honoraires de chaque dossier sont répartis au prorata des heures réellement saisies.
        </p>
        <p-table [value]="portfolio()" responsiveLayout="scroll">
          <ng-template pTemplate="header">
            <tr>
              <th>Société</th>
              <th>Honoraires HT</th>
              <th>Heures du portefeuille</th>
              <th>Heures totales dossier</th>
              <th>Quote-part</th>
            </tr>
          </ng-template>
          <ng-template pTemplate="body" let-p>
            <tr>
              <td>{{ p.companyName }}</td>
              <td>{{ p.annualFeeHt | number:'1.3-3' }}</td>
              <td>{{ p.portfolioHours | number:'1.2-2' }}</td>
              <td>{{ p.totalDossierHours | number:'1.2-2' }}</td>
              <td><strong>{{ p.revenueShare | number:'1.3-3' }}</strong></td>
            </tr>
          </ng-template>
          <ng-template pTemplate="emptymessage">
            <tr>
              <td colspan="5" class="muted">
                Aucune heure saisie sur un dossier client pour cet exercice — aucun honoraire n'est attribué.
              </td>
            </tr>
          </ng-template>
        </p-table>
      </div>

      <div class="fc-card">
        <h3>Masse salariale</h3>
        <p-table [value]="payrollRows()" responsiveLayout="scroll">
          <ng-template pTemplate="header">
            <tr>
              <th>Collaborateur</th>
              <th>Salaire brut</th>
              <th>Charges patronales</th>
              <th>Extras</th>
              <th>Total annuel</th>
            </tr>
          </ng-template>
          <ng-template pTemplate="body" let-row>
            <tr>
              <td>{{ row.collaboratorName }}</td>
              <td>{{ row.grossSalary | number:'1.3-3' }}</td>
              <td>{{ row.employerContributions | number:'1.3-3' }}</td>
              <td>{{ row.payrollExtras | number:'1.3-3' }}</td>
              <td>{{ payrollTotal(row) | number:'1.3-3' }}</td>
            </tr>
          </ng-template>
          <ng-template pTemplate="emptymessage">
            <tr><td colspan="5" class="muted">Aucune ligne de paie.</td></tr>
          </ng-template>
        </p-table>
      </div>

      <div class="fc-card">
        <h3>Synthèse</h3>
        <div class="summary-grid" [formGroup]="form">
          <label>Chiffre d'affaires
            <div class="field-row">
              <p-inputNumber formControlName="totalRevenue" mode="decimal" [minFractionDigits]="3" [maxFractionDigits]="3" />
              @if (!readOnly()) {
                <button type="button" pButton icon="pi pi-refresh" class="p-button-text p-button-sm" title="Réinitialiser au calculé" (click)="resetToCalculated('totalRevenue')"></button>
              }
            </div>
            <span class="hint">Calculé : {{ (calculated()?.calculatedTotalRevenue ?? 0) | number:'1.3-3' }}</span>
          </label>
          <label>Masse salariale
            <div class="field-row">
              <p-inputNumber formControlName="payrollCost" mode="decimal" [minFractionDigits]="3" [maxFractionDigits]="3" />
              @if (!readOnly()) {
                <button type="button" pButton icon="pi pi-refresh" class="p-button-text p-button-sm" title="Réinitialiser au calculé" (click)="resetToCalculated('payrollCost')"></button>
              }
            </div>
            <span class="hint">Calculé : {{ (calculated()?.calculatedPayrollCost ?? 0) | number:'1.3-3' }}</span>
          </label>
          <label>Quote-part de structure
            <div class="field-row">
              <p-inputNumber formControlName="adminPayrollCharge" mode="decimal" [minFractionDigits]="3" [maxFractionDigits]="3" />
            </div>
            <span class="hint">
              Calculée : masse salariale du personnel support, répartie au prorata du coût employeur
              de chaque collaborateur productif.
            </span>
          </label>
          <label>Solde débiteur clients
            <p-inputNumber formControlName="clientDebitBalance" mode="decimal" [minFractionDigits]="3" [maxFractionDigits]="3" />
          </label>
          <label>Solde créditeur clients
            <p-inputNumber formControlName="clientCreditBalance" mode="decimal" [minFractionDigits]="3" [maxFractionDigits]="3" />
          </label>
          <div class="rentability-box">
            Marge sur coût direct
            <strong [class.neg]="liveRentability() < 0" [class.pos]="liveRentability() > 0">
              {{ liveRentability() | number:'1.3-3' }}
            </strong>
            @if (payrollRows().length > 0) {
              <span class="hint">MS retenue : {{ effectivePayrollCost() | number:'1.3-3' }} (détail de paie)</span>
            }
            <span class="hint">Encaissée : {{ collectedRentability() | number:'1.3-3' }}</span>
          </div>
        </div>
      </div>
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
    .fc-card h3 { margin: 0 0 .75rem; font-size: .95rem; }
    .meta-form { display: flex; flex-wrap: wrap; gap: .75rem; align-items: flex-end; }
    .meta-form label { display: flex; flex-direction: column; font-size: .875rem; gap: .25rem; min-width: 180px; }
    .summary-grid {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(220px, 1fr));
      gap: .75rem;
    }
    .summary-grid label { display: flex; flex-direction: column; font-size: .875rem; gap: .25rem; }
    .field-row { display: flex; align-items: center; gap: .25rem; }
    .hint { font-size: .75rem; color: var(--color-text-muted, #64748b); }
    .muted { color: var(--color-text-muted, #64748b); font-size: .875rem; }
    .rentability-box {
      display: flex; flex-direction: column; justify-content: center; gap: .25rem;
      padding: .75rem; border-radius: 12px; background: var(--color-surface-muted, #f8fafc);
      font-size: .875rem;
    }
    .rentability-box strong { font-size: 1.25rem; }
    .neg { color: #b91c1c; }
    .pos { color: #047857; }
  `]
})
export class FirmCollaboratorRentabilityFormComponent implements OnInit {
  private readonly api = inject(FirmGovernanceService);
  private readonly collaboratorsApi = inject(FirmCollaboratorsService);
  private readonly fb = inject(FormBuilder);
  private readonly toast = inject(ToastService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly location = inject(Location);

  mode = signal<FormMode>('create');
  loading = signal(false);
  saving = signal(false);
  portfolio = signal<FirmRentabilityPortfolioRow[]>([]);
  payrollRows = signal<FirmRentabilityPayrollRow[]>([]);
  calculated = signal<FirmCollaboratorRentabilityDetail | null>(null);
  companiesCount = signal(0);
  attachedCount = signal(0);
  editId = signal<string | null>(null);
  liveRentability = signal(0);

  collaboratorOptions: { label: string; value: string }[] = [];

  readonly yearOptions = Array.from({ length: 8 }, (_, i) => {
    const y = new Date().getFullYear() - 4 + i;
    return { label: String(y), value: y };
  });

  form = this.fb.group({
    collaboratorUserId: ['', Validators.required],
    year: [new Date().getFullYear(), Validators.required],
    totalRevenue: [0, Validators.required],
    payrollCost: [0, Validators.required],
    adminPayrollCharge: [0, Validators.required],
    clientDebitBalance: [0, Validators.required],
    clientCreditBalance: [0, Validators.required]
  });

  readOnly = computed(() => this.mode() === 'view');

  title = computed(() => {
    switch (this.mode()) {
      case 'create': return 'Nouvelle rentabilité';
      case 'edit': return 'Modifier la rentabilité';
      default: return 'Détail rentabilité';
    }
  });

  subtitle = computed(() =>
    this.mode() === 'view'
      ? 'Consultation du snapshot'
      : 'Portefeuille, paie et charges — les boutons rafraîchissent les valeurs calculées'
  );

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    const url = this.router.url;
    if (id && url.endsWith('/edit')) {
      this.mode.set('edit');
      this.editId.set(id);
    } else if (id) {
      this.mode.set('view');
      this.editId.set(id);
    } else {
      this.mode.set('create');
    }

    this.form.valueChanges.subscribe(() => this.recomputeRentability());
    this.recomputeRentability();

    this.collaboratorsApi.list({ isActive: true }).subscribe({
      next: (users: FirmUser[]) => {
        this.collaboratorOptions = users.map(u => ({
          label: `${u.firstName} ${u.lastName}`.trim() || u.email,
          value: u.id
        }));
      }
    });

    if (this.editId()) {
      this.loadExisting(this.editId()!);
    } else {
      this.applyMetaLock();
    }
  }

  loadPrefill(): void {
    const { collaboratorUserId, year } = this.form.getRawValue();
    if (!collaboratorUserId || !year) {
      this.toast.add({ severity: 'warn', summary: 'Prérequis', detail: 'Sélectionnez un collaborateur et une année.' });
      return;
    }
    this.loading.set(true);
    this.api.prefillRentability(collaboratorUserId, year).subscribe({
      next: res => {
        if (res.data) this.applyDetail(res.data);
        this.loading.set(false);
      },
      error: (err) => {
        this.loading.set(false);
        this.toast.add({
          severity: 'error',
          summary: 'Erreur',
          detail: err?.error?.message || 'Préremplissage impossible.'
        });
      }
    });
  }

  resetToCalculated(field: 'totalRevenue' | 'payrollCost'): void {
    const c = this.calculated();
    if (!c) return;
    const map: Record<typeof field, number> = {
      totalRevenue: c.calculatedTotalRevenue,
      payrollCost: c.calculatedPayrollCost
    };
    this.form.patchValue({ [field]: map[field] });
  }

  save(): void {
    if (this.form.invalid || this.readOnly()) return;
    const v = this.form.getRawValue();
    const body: SaveFirmCollaboratorRentability = {
      collaboratorUserId: v.collaboratorUserId!,
      year: v.year!,
      totalRevenue: v.totalRevenue ?? 0,
      payrollCost: v.payrollCost ?? 0,
      adminPayrollCharge: v.adminPayrollCharge ?? 0,
      itManagementCharge: 0,
      operatingCharge: 0,
      clientDebitBalance: v.clientDebitBalance ?? 0,
      clientCreditBalance: v.clientCreditBalance ?? 0,
      companiesCount: this.companiesCount(),
      attachedCollaboratorsCount: this.attachedCount(),
      payrollRows: this.payrollRows()
    };

    this.saving.set(true);
    const id = this.editId();
    const req$ = id
      ? this.api.updateRentability(id, body)
      : this.api.createRentability(body);

    req$.subscribe({
      next: res => {
        this.saving.set(false);
        this.toast.add({ severity: 'success', summary: 'Enregistré', detail: 'Marge enregistrée.' });
        const newId = res.data?.id ?? id;
        if (newId) {
          void this.router.navigate(['/firm/governance/collaborator-rentability', newId]);
        } else {
          void this.router.navigate(['/firm/governance/collaborator-rentability']);
        }
      },
      error: (err) => {
        this.saving.set(false);
        this.toast.add({
          severity: 'error',
          summary: 'Erreur',
          detail: err?.error?.message || 'Enregistrement impossible.'
        });
      }
    });
  }

  goEdit(): void {
    const id = this.editId();
    if (id) void this.router.navigate(['/firm/governance/collaborator-rentability', id, 'edit']);
  }

  back(): void {
    this.location.back();
  }

  payrollTotal(row: FirmRentabilityPayrollRow): number {
    return (row.annualTotal ?? (row.grossSalary + row.employerContributions + row.payrollExtras));
  }

  private loadExisting(id: string): void {
    this.loading.set(true);
    this.api.getRentability(id).subscribe({
      next: res => {
        if (res.data) this.applyDetail(res.data);
        this.applyMetaLock();
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.toast.add({ severity: 'error', summary: 'Erreur', detail: 'Chargement impossible.' });
      }
    });
  }

  private applyDetail(d: FirmCollaboratorRentabilityDetail): void {
    this.calculated.set(d);
    this.portfolio.set(d.portfolio ?? []);
    this.payrollRows.set(d.payrollRows ?? []);
    this.companiesCount.set(d.portfolio?.length ?? 0);
    this.attachedCount.set(d.attachedCollaboratorsCount ?? 0);
    this.form.patchValue({
      collaboratorUserId: d.collaboratorUserId,
      year: d.year,
      totalRevenue: d.totalRevenue,
      payrollCost: d.payrollCost,
      adminPayrollCharge: d.adminPayrollCharge,
      clientDebitBalance: d.clientDebitBalance,
      clientCreditBalance: d.clientCreditBalance
    });
    if (d.id) this.editId.set(d.id);
    this.recomputeRentability();
  }

  private applyMetaLock(): void {
    if (this.mode() === 'view') {
      this.form.disable({ emitEvent: false });
      return;
    }
    this.form.enable({ emitEvent: false });
    if (this.mode() !== 'create') {
      this.form.controls.collaboratorUserId.disable({ emitEvent: false });
      this.form.controls.year.disable({ emitEvent: false });
    }
  }

  /**
   * Masse salariale retenue : le détail par collaborateur prime sur le montant agrégé.
   *
   * Même règle que le serveur (`FirmCollaboratorRentability.GetPayrollCost`). Se contenter du
   * champ agrégé afficherait une rentabilité que l'enregistrement contredirait aussitôt.
   */
  /** Rentabilité diminuée des honoraires non recouvrés. Indicateur d'appoint, hors formule. */
  collectedRentability(): number {
    return this.liveRentability() - (this.form.getRawValue().clientDebitBalance ?? 0);
  }

  effectivePayrollCost(): number {
    const rows = this.payrollRows();
    if (rows.length > 0) {
      return rows.reduce((sum, r) => sum + this.payrollTotal(r), 0);
    }
    return this.form.getRawValue().payrollCost ?? 0;
  }

  private recomputeRentability(): void {
    const v = this.form.getRawValue();
    this.liveRentability.set(
      (v.totalRevenue ?? 0)
      - this.effectivePayrollCost()
      - (v.adminPayrollCharge ?? 0)
    );
  }
}
