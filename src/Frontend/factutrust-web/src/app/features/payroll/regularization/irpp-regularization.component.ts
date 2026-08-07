import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { Title } from '@angular/platform-browser';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { SelectModule } from 'primeng/select';
import { InputNumberModule } from 'primeng/inputnumber';
import { Textarea } from 'primeng/textarea';
import { MessageModule } from 'primeng/message';
import { PageHeaderComponent } from '@shared/components/page-header/page-header.component';
import { ButtonComponent } from '@shared/components/button/button.component';
import { FormSectionComponent } from '@shared/components/form-section/form-section.component';
import { EmployeeService, EmployeeListItem } from '@core/services/employee.service';
import { PayrollService, IrppRegularizationPreview } from '@core/services/payroll.service';
import { ToastService } from '@core/services/toast.service';
import { AuthService } from '@core/services/auth.service';
import { canRunPayroll } from '@core/utils/payroll-access';
import { PayrollAmountPipe } from '../shared';
import {
  buildDetailRows,
  buildSummaryRows,
  featureDisabledNotice,
  isWorthSaving,
  monthLabel,
  outcomeHint,
  outcomeLabel,
  outcomeSeverity,
  partialYearNotice,
  resolveOutcome
} from './irpp-regularization.view-model';

@Component({
  selector: 'app-irpp-regularization',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    TableModule,
    TagModule,
    SelectModule,
    InputNumberModule,
    Textarea,
    MessageModule,
    PageHeaderComponent,
    ButtonComponent,
    FormSectionComponent,
    PayrollAmountPipe
  ],
  template: `
    <app-page-header
      title="Régularisation IRPP"
      [subtitle]="'Période : ' + monthLabel(month) + ' ' + year">
      <app-button variant="outline" (click)="reset()">Annuler</app-button>
      <app-button variant="outline" icon="pi-calculator" iconPos="left"
        [disabled]="!employeeId || loading()" (click)="calculate()">Calculer</app-button>
      @if (canManage()) {
        <app-button variant="primary" icon="pi-save" iconPos="left"
          [disabled]="!canSave()" (click)="save()">Enregistrer</app-button>
      }
    </app-page-header>

    @if (notice(); as message) {
      <p-message severity="warn" [text]="message" styleClass="reg-notice" />
    }

    <div class="reg-layout">
      <div class="reg-main">
        <app-form-section title="Informations générales" icon="pi-user" [number]="1">
          <div class="payroll-form-row">
            <div class="payroll-form-group">
              <label for="regEmployee">Salarié</label>
              <p-select inputId="regEmployee" [options]="employees()" optionLabel="fullName" optionValue="id"
                [(ngModel)]="employeeId" (ngModelChange)="onEmployeeChange()" [filter]="true" filterBy="fullName"
                placeholder="Sélectionner un salarié" appendTo="body" styleClass="w-full" />
            </div>
            <div class="payroll-form-group">
              <label for="regYear">Exercice</label>
              <p-inputNumber inputId="regYear" [(ngModel)]="year" [min]="2000" [max]="2100"
                [useGrouping]="false" (ngModelChange)="onPeriodChange()" styleClass="w-full" />
            </div>
            <div class="payroll-form-group">
              <label for="regMonth">Mois de régularisation</label>
              <p-select inputId="regMonth" [options]="monthOptions" optionLabel="label" optionValue="value"
                [(ngModel)]="month" (ngModelChange)="onPeriodChange()" appendTo="body" styleClass="w-full" />
            </div>
          </div>

          @if (preview(); as p) {
            <dl class="payroll-detail-grid">
              <dt>Matricule</dt><dd>{{ p.employeeNumber || '—' }}</dd>
              <dt>Motif</dt><dd>{{ p.reasonLabel }}</dd>
              <dt>Bulletins pris en compte</dt><dd>{{ p.monthsCounted }}</dd>
            </dl>
          }

          <div class="reg-legal">
            <i class="pi pi-info-circle" aria-hidden="true"></i>
            <div>
              <p>
                La retenue mensuelle est calculée en projetant le net imposable du mois sur douze mois.
                Dès que la rémunération varie, la somme des retenues s’écarte de l’impôt réellement dû
                sur le revenu annuel. Cet écran compare les deux et porte la différence sur le bulletin.
              </p>
              <p class="reg-legal-ref">Référence : article 40 et suivants du code de l’IRPP.</p>
            </div>
          </div>
        </app-form-section>

        @if (preview(); as p) {
          <app-form-section title="Détail du calcul" icon="pi-list" [number]="2">
            <div class="payroll-table-scroll">
              <p-table [value]="detailRows()" styleClass="p-datatable-sm">
                <ng-template pTemplate="header">
                  <tr>
                    <th>Mois</th>
                    <th class="text-right">Net imposable</th>
                    <th class="text-right">IRPP retenu</th>
                    <th class="text-right">CSS retenue</th>
                  </tr>
                </ng-template>
                <ng-template pTemplate="body" let-row>
                  <tr [class.reg-total-row]="row.isTotal">
                    <td>
                      {{ row.label }}
                      @if (row.isPending) {
                        <p-tag value="Mois en cours" severity="info" class="ml-2" />
                      }
                    </td>
                    <td class="text-right tabnum">{{ row.netTaxable | payrollAmount:false }}</td>
                    <td class="text-right tabnum">{{ row.irpp | payrollAmount:false }}</td>
                    <td class="text-right tabnum">{{ row.css | payrollAmount:false }}</td>
                  </tr>
                </ng-template>
                <ng-template pTemplate="emptymessage">
                  <tr><td colspan="4">Aucun bulletin arrêté sur cet exercice.</td></tr>
                </ng-template>
              </p-table>
            </div>
          </app-form-section>

          <app-form-section title="Notes" icon="pi-comment" [number]="3">
            <textarea pTextarea [(ngModel)]="notes" rows="3" class="w-full" maxlength="1000"
              placeholder="Saisir une note éventuelle…"></textarea>
          </app-form-section>
        }
      </div>

      <div class="reg-side">
        @if (preview(); as p) {
          <div class="card reg-summary">
            <h3 class="reg-summary-title">Récapitulatif</h3>

            <p-tag [value]="outcomeLabel()" [severity]="outcomeSeverity()" />
            <p class="reg-summary-hint">{{ outcomeHint() }}</p>

            @for (row of summaryRows(); track row.label) {
              <div class="reg-summary-row" [class.reg-summary-sep]="row.separator" [class.reg-summary-strong]="row.highlight">
                <span>{{ row.label }}</span>
                <span class="tabnum">{{ row.value | payrollAmount }}</span>
              </div>
            }

            @if (p.isPartialYear) {
              <p class="reg-summary-note">{{ partialYearNotice() }}</p>
            }
          </div>

          <div class="card reg-actions">
            <h3 class="reg-summary-title">Montant à porter</h3>
            <div class="payroll-form-group">
              <label for="regOverrideIrpp">Écart IRPP (ajustable)</label>
              <p-inputNumber inputId="regOverrideIrpp" [(ngModel)]="overrideIrpp"
                [minFractionDigits]="3" [maxFractionDigits]="3" [locale]="'fr-TN'" styleClass="w-full" />
            </div>
            <div class="payroll-form-group">
              <label for="regOverrideCss">Écart CSS (ajustable)</label>
              <p-inputNumber inputId="regOverrideCss" [(ngModel)]="overrideCss"
                [minFractionDigits]="3" [maxFractionDigits]="3" [locale]="'fr-TN'" styleClass="w-full" />
            </div>
            <p class="reg-summary-note">
              Laissez les montants calculés tels quels, ou ajustez-les : la ligne sera alors signalée
              comme corrigée manuellement et préservée lors des régénérations.
            </p>
          </div>
        }
      </div>
    </div>
  `,
  styles: [`
    .reg-layout { display: grid; grid-template-columns: 1fr; gap: var(--spacing-4); align-items: start; }
    @media (min-width: 64rem) {
      .reg-layout { grid-template-columns: minmax(0, 1.65fr) minmax(18rem, 1fr); }
    }
    .reg-main, .reg-side { min-width: 0; }
    .reg-side { display: flex; flex-direction: column; gap: var(--spacing-4); position: sticky; top: var(--spacing-4); }

    .reg-notice { display: block; margin-bottom: var(--spacing-4); }

    .reg-legal {
      display: flex; gap: var(--spacing-3); margin-top: var(--spacing-4);
      padding: var(--spacing-4); border-radius: var(--radius-md);
      background: var(--color-background-subtle); color: var(--color-text-secondary);
      font-size: var(--font-size-sm);
    }
    .reg-legal i { margin-top: 0.15rem; color: var(--color-success-600); font-size: 1.1rem; }
    .reg-legal p { margin: 0 0 var(--spacing-2); line-height: 1.5; }
    .reg-legal p:last-child { margin-bottom: 0; }
    .reg-legal-ref { font-weight: 600; color: var(--color-text-primary); }

    .reg-summary-title { margin: 0 0 var(--spacing-3); font-size: var(--font-size-md); color: var(--color-success-700); }
    .reg-summary-hint { margin: var(--spacing-2) 0 var(--spacing-4); font-size: var(--font-size-sm); color: var(--color-text-secondary); }
    .reg-summary-row { display: flex; justify-content: space-between; gap: var(--spacing-3); padding: var(--spacing-2) 0; font-size: var(--font-size-sm); }
    .reg-summary-sep { border-top: 1px solid var(--color-border-subtle); margin-top: var(--spacing-2); padding-top: var(--spacing-3); }
    .reg-summary-strong { font-weight: 700; font-size: var(--font-size-md); color: var(--color-success-700); }
    .reg-summary-note { margin: var(--spacing-3) 0 0; font-size: var(--font-size-xs); color: var(--color-text-tertiary); line-height: 1.5; }

    .reg-total-row { font-weight: 700; background: var(--color-background-subtle); }
    .tabnum { font-variant-numeric: tabular-nums; }
    .text-right { text-align: right; }
    .ml-2 { margin-left: var(--spacing-2); }
    .w-full { width: 100%; }
  `]
})
export class IrppRegularizationComponent implements OnInit {
  private readonly payroll = inject(PayrollService);
  private readonly employeesApi = inject(EmployeeService);
  private readonly toast = inject(ToastService);
  private readonly title = inject(Title);
  private readonly route = inject(ActivatedRoute);
  private readonly auth = inject(AuthService);

  readonly canManage = computed(() => canRunPayroll(this.auth));

  readonly employees = signal<EmployeeListItem[]>([]);
  readonly preview = signal<IrppRegularizationPreview | null>(null);
  readonly loading = signal(false);

  employeeId: string | null = null;
  year = new Date().getFullYear();
  month = 12;
  notes = '';
  overrideIrpp: number | null = null;
  overrideCss: number | null = null;

  readonly monthOptions = Array.from({ length: 12 }, (_, i) => ({ value: i + 1, label: monthLabel(i + 1) }));

  readonly detailRows = computed(() => buildDetailRows(this.preview()?.months ?? []));
  readonly summaryRows = computed(() => {
    const p = this.preview();
    return p ? buildSummaryRows(p) : [];
  });

  readonly notice = computed(() => {
    const p = this.preview();
    return p ? featureDisabledNotice(p) : null;
  });

  ngOnInit(): void {
    this.title.setTitle('Régularisation IRPP - InstaFact');

    // Deep-link depuis le cycle de paie : ?year=2026&month=12
    const params = this.route.snapshot.queryParamMap;
    const year = Number(params.get('year'));
    const month = Number(params.get('month'));
    if (Number.isInteger(year) && year >= 2000 && year <= 2100) this.year = year;
    if (Number.isInteger(month) && month >= 1 && month <= 12) this.month = month;

    this.loadEmployees();
  }

  monthLabel(month: number): string {
    return monthLabel(month);
  }

  outcomeLabel(): string {
    return outcomeLabel(resolveOutcome(this.preview()?.totalDelta ?? 0));
  }

  outcomeSeverity(): 'warn' | 'success' | 'info' {
    return outcomeSeverity(resolveOutcome(this.preview()?.totalDelta ?? 0));
  }

  outcomeHint(): string {
    return outcomeHint(resolveOutcome(this.preview()?.totalDelta ?? 0));
  }

  partialYearNotice(): string | null {
    const p = this.preview();
    return p ? partialYearNotice(p) : null;
  }

  canSave(): boolean {
    return this.canManage() && !this.loading() && isWorthSaving(this.preview());
  }

  onEmployeeChange(): void {
    this.preview.set(null);
  }

  onPeriodChange(): void {
    this.preview.set(null);
  }

  reset(): void {
    this.preview.set(null);
    this.notes = '';
    this.overrideIrpp = null;
    this.overrideCss = null;
  }

  calculate(): void {
    if (!this.employeeId) return;

    this.loading.set(true);
    this.payroll.previewIrppRegularization(this.employeeId, this.year, this.month).subscribe({
      next: res => {
        this.loading.set(false);
        const data = res.data ?? null;
        this.preview.set(data);
        if (data) {
          this.overrideIrpp = data.irppDelta;
          this.overrideCss = data.cssDelta;
        }
      },
      error: err => {
        this.loading.set(false);
        this.preview.set(null);
        this.toast.add({
          severity: 'error',
          summary: 'Régularisation IRPP',
          detail: err?.error?.message ?? 'Calcul impossible.'
        });
      }
    });
  }

  save(): void {
    // Défense en profondeur : le bouton est masqué, la méthode refuse aussi.
    if (!this.canManage() || !this.employeeId) return;

    const p = this.preview();
    if (!p) return;

    // Un montant inchangé n'est pas un override : la ligne reste « calculée ».
    const irppOverride = this.overrideIrpp !== null && this.overrideIrpp !== p.irppDelta ? this.overrideIrpp : null;
    const cssOverride = this.overrideCss !== null && this.overrideCss !== p.cssDelta ? this.overrideCss : null;

    this.loading.set(true);
    this.payroll.upsertIrppRegularization({
      employeeId: this.employeeId,
      year: this.year,
      month: this.month,
      overrideIrppDelta: irppOverride,
      overrideCssDelta: cssOverride,
      notes: this.notes.trim() || null
    }).subscribe({
      next: () => {
        this.loading.set(false);
        this.toast.add({
          severity: 'success',
          summary: 'Régularisation IRPP',
          detail: 'Enregistrée. Recalculez le cycle pour la porter sur le bulletin.'
        });
      },
      error: err => {
        this.loading.set(false);
        this.toast.add({
          severity: 'error',
          summary: 'Régularisation IRPP',
          detail: err?.error?.message ?? 'Enregistrement impossible.'
        });
      }
    });
  }

  private loadEmployees(): void {
    this.employeesApi.list(undefined, true, 1, 500).subscribe({
      next: res => this.employees.set(res.data?.items ?? []),
      error: () => {}
    });
  }
}
