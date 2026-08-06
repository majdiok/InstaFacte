import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { DropdownModule } from 'primeng/dropdown';
import { InputNumberModule } from 'primeng/inputnumber';
import { InputSwitchModule } from 'primeng/inputswitch';
import { TableModule } from 'primeng/table';
import { ButtonModule } from 'primeng/button';
import { TabViewModule } from 'primeng/tabview';
import { PayrollService, PayrollParameters, PayrollGarnishmentBracket } from '@core/services/payroll.service';
import { ToastService } from '@core/services/toast.service';
import { PageHeaderComponent } from '@shared/components/page-header/page-header.component';
import { ButtonComponent } from '@shared/components/button/button.component';
import { PayrollSocialFundsSettingsComponent } from './payroll-social-funds-settings.component';

@Component({
  selector: 'app-payroll-settings',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    TabViewModule,
    DropdownModule,
    InputNumberModule,
    InputSwitchModule,
    TableModule,
    ButtonModule,
    PageHeaderComponent,
    ButtonComponent,
    PayrollSocialFundsSettingsComponent
  ],
  template: `
    <app-page-header title="Paramètres de paie" subtitle="Barème IRPP, taux légaux et conformité par exercice." />

    <div class="payroll-toolbar mb-3">
      <label for="fiscalYear">Exercice</label>
      <p-inputNumber id="fiscalYear" [(ngModel)]="fiscalYear" (ngModelChange)="load()" [useGrouping]="false" [min]="2000" [max]="2100" styleClass="w-8rem" />
    </div>

    @if (params()) {
      <form (ngSubmit)="save()">
        <p-tabView styleClass="ft-tabs">
          <p-tabPanel header="Taux légaux">
            <ng-template pTemplate="header">
              <i class="pi pi-percentage mr-2"></i>
              <span>Taux légaux</span>
            </ng-template>
            <div class="payroll-form-row">
              <div class="payroll-form-group">
                <label>CNSS salarié (%)</label>
                <p-inputNumber [(ngModel)]="params()!.cnssEmployeeRate" name="cnssEmployeeRate" [minFractionDigits]="2" [maxFractionDigits]="4" [min]="0" [max]="100" [locale]="'fr-TN'" styleClass="w-full" />
              </div>
              <div class="payroll-form-group">
                <label>CNSS employeur (%)</label>
                <p-inputNumber [(ngModel)]="params()!.cnssEmployerRate" name="cnssEmployerRate" [minFractionDigits]="2" [maxFractionDigits]="4" [min]="0" [max]="100" [locale]="'fr-TN'" styleClass="w-full" />
              </div>
              <div class="payroll-form-group">
                <label>CNSS salarié RSA (%)</label>
                <p-inputNumber [(ngModel)]="params()!.cnssEmployeeRateRsa" name="cnssEmployeeRateRsa" [minFractionDigits]="2" [maxFractionDigits]="4" [min]="0" [max]="100" [locale]="'fr-TN'" styleClass="w-full" />
              </div>
              <div class="payroll-form-group">
                <label>CNSS employeur RSA (%)</label>
                <p-inputNumber [(ngModel)]="params()!.cnssEmployerRateRsa" name="cnssEmployerRateRsa" [minFractionDigits]="2" [maxFractionDigits]="4" [min]="0" [max]="100" [locale]="'fr-TN'" styleClass="w-full" />
              </div>
              <div class="payroll-form-group">
                <label>CSS (%)</label>
                <p-inputNumber [(ngModel)]="params()!.cssRate" name="cssRate" [minFractionDigits]="2" [maxFractionDigits]="4" [min]="0" [max]="100" [locale]="'fr-TN'" styleClass="w-full" />
              </div>
              <div class="payroll-form-group">
                <label>Seuil exonération CSS (annuel)</label>
                <p-inputNumber [(ngModel)]="params()!.cssAnnualExemptionThreshold" name="cssExemption" [minFractionDigits]="3" [min]="0" [locale]="'fr-TN'" styleClass="w-full" />
              </div>
              <div class="payroll-form-group">
                <label>CSS patronale (%)</label>
                <p-inputNumber [(ngModel)]="params()!.cssEmployerRate" name="cssEmployerRate" [minFractionDigits]="2" [maxFractionDigits]="4" [min]="0" [max]="100" [locale]="'fr-TN'" styleClass="w-full" />
              </div>
              <div class="payroll-form-group">
                <label>TFP industrie (%)</label>
                <p-inputNumber [(ngModel)]="params()!.tfpRateIndustry" name="tfpIndustry" [minFractionDigits]="2" [maxFractionDigits]="4" [min]="0" [max]="100" [locale]="'fr-TN'" styleClass="w-full" />
              </div>
              <div class="payroll-form-group">
                <label>TFP autres (%)</label>
                <p-inputNumber [(ngModel)]="params()!.tfpRateOther" name="tfpOther" [minFractionDigits]="2" [maxFractionDigits]="4" [min]="0" [max]="100" [locale]="'fr-TN'" styleClass="w-full" />
              </div>
              <div class="payroll-form-group">
                <label>FOPROLOS (%)</label>
                <p-inputNumber [(ngModel)]="params()!.foprolosRate" name="foprolos" [minFractionDigits]="2" [maxFractionDigits]="4" [min]="0" [max]="100" [locale]="'fr-TN'" styleClass="w-full" />
              </div>
              <div class="payroll-form-group">
                <label>SMIG mensuel (TND)</label>
                <p-inputNumber [(ngModel)]="params()!.monthlySmig" name="smig" [minFractionDigits]="3" [min]="0" [locale]="'fr-TN'" styleClass="w-full" />
              </div>
              <div class="payroll-form-group">
                <label>Plafond exonération ticket restaurant / jour (TND)</label>
                <p-inputNumber [(ngModel)]="params()!.mealVoucherDailyExemptionCap" name="mealVoucherCap" [minFractionDigits]="3" [min]="0" [locale]="'fr-TN'" styleClass="w-full" />
              </div>
              <div class="payroll-form-group">
                <label>Frais pro. (%)</label>
                <p-inputNumber [(ngModel)]="params()!.professionalExpensesRate" name="proExpRate" [minFractionDigits]="2" [maxFractionDigits]="4" [min]="0" [max]="100" [locale]="'fr-TN'" styleClass="w-full" />
              </div>
              <div class="payroll-form-group">
                <label>Plafond frais pro. (annuel)</label>
                <p-inputNumber [(ngModel)]="params()!.professionalExpensesAnnualCap" name="proExpCap" [minFractionDigits]="3" [min]="0" [locale]="'fr-TN'" styleClass="w-full" />
              </div>
            </div>
            <p class="payroll-info-text mt-2">Références légales tunisiennes : CNSS 9,18 % / 16,57 % — CSS 0,5 % (seuil 5 000 TND/an) — TFP 1 % industrie, 2 % autres — FOPROLOS 1 % — frais professionnels 10 % plafonnés à 2 000 TND/an.</p>
          </p-tabPanel>

          <p-tabPanel header="Barème IRPP">
            <ng-template pTemplate="header">
              <i class="pi pi-table mr-2"></i>
              <span>Barème IRPP</span>
            </ng-template>
            <p-table [value]="params()!.irppBrackets" styleClass="p-datatable-sm mb-3">
              <ng-template pTemplate="header">
                <tr>
                  <th>Seuil inférieur (TND)</th>
                  <th>Taux (%)</th>
                  <th></th>
                </tr>
              </ng-template>
              <ng-template pTemplate="body" let-bracket let-i="rowIndex">
                <tr>
                  <td>
                    <p-inputNumber [(ngModel)]="bracket.lowerBound" [name]="'lb' + i" [minFractionDigits]="3" [min]="0" [locale]="'fr-TN'" styleClass="w-full" />
                  </td>
                  <td>
                    <p-inputNumber [(ngModel)]="bracket.rate" [name]="'rate' + i" [minFractionDigits]="2" [maxFractionDigits]="4" [min]="0" [max]="100" [locale]="'fr-TN'" styleClass="w-full" />
                  </td>
                  <td>
                    <button type="button" pButton icon="pi pi-trash" class="p-button-text p-button-danger p-button-sm" (click)="removeBracket(i)"></button>
                  </td>
                </tr>
              </ng-template>
            </p-table>
            <app-button type="button" variant="outline" icon="pi-plus" iconPos="left" (click)="addBracket()">Ajouter une tranche</app-button>
            <p class="payroll-info-text mt-2">Barème LF 2025 : 0 % jusqu'à 5 000 — 15 % — 25 % — 30 % — 33 % — 36 % — 38 % — 40 % au-delà de 70 000 TND/an. La première tranche doit démarrer à 0 et les seuils être strictement croissants.</p>
          </p-tabPanel>

          <p-tabPanel header="Déductions familiales">
            <ng-template pTemplate="header">
              <i class="pi pi-users mr-2"></i>
              <span>Déductions familiales</span>
            </ng-template>
            <div class="payroll-form-row">
              <div class="payroll-form-group">
                <label>Chef de famille (annuel, TND)</label>
                <p-inputNumber [(ngModel)]="params()!.headOfFamilyAnnualDeduction" name="headDeduction" [minFractionDigits]="3" [min]="0" [locale]="'fr-TN'" styleClass="w-full" />
              </div>
              <div class="payroll-form-group">
                <label>Enfant à charge (annuel, TND)</label>
                <p-inputNumber [(ngModel)]="params()!.childAnnualDeduction" name="childDeduction" [minFractionDigits]="3" [min]="0" [locale]="'fr-TN'" styleClass="w-full" />
              </div>
              <div class="payroll-form-group">
                <label>Enfants déductibles max.</label>
                <p-inputNumber [(ngModel)]="params()!.maxDeductibleChildren" name="maxChildren" [min]="0" styleClass="w-full" />
              </div>
              <div class="payroll-form-group">
                <label>Enfant étudiant non boursier &lt; 25 ans (annuel, TND)</label>
                <p-inputNumber [(ngModel)]="params()!.studentChildAnnualDeduction" name="studentDeduction" [minFractionDigits]="3" [min]="0" [locale]="'fr-TN'" styleClass="w-full" />
              </div>
              <div class="payroll-form-group">
                <label>Enfant infirme (annuel, TND — hors plafond)</label>
                <p-inputNumber [(ngModel)]="params()!.disabledChildAnnualDeduction" name="disabledDeduction" [minFractionDigits]="3" [min]="0" [locale]="'fr-TN'" styleClass="w-full" />
              </div>
              <div class="payroll-form-group">
                <label>Parent à charge — taux (% du revenu net)</label>
                <p-inputNumber [(ngModel)]="params()!.parentDeductionRatePercent" name="parentRate" [minFractionDigits]="2" [maxFractionDigits]="4" [min]="0" [max]="100" [locale]="'fr-TN'" styleClass="w-full" />
              </div>
              <div class="payroll-form-group">
                <label>Parent à charge — plafond annuel (TND)</label>
                <p-inputNumber [(ngModel)]="params()!.parentAnnualDeductionCap" name="parentCap" [minFractionDigits]="3" [min]="0" [locale]="'fr-TN'" styleClass="w-full" />
              </div>
            </div>
          </p-tabPanel>

          <p-tabPanel header="Conformité">
            <ng-template pTemplate="header">
              <i class="pi pi-shield mr-2"></i>
              <span>Conformité</span>
            </ng-template>
            <div class="payroll-form-row">
              <div class="payroll-form-group switch-row">
                <label for="enforceSmig">Contrôle SMIG sur les contrats</label>
                <p-inputSwitch inputId="enforceSmig" [(ngModel)]="params()!.enforceSmigOnContracts" name="enforceSmig" />
              </div>
              <div class="payroll-form-group switch-row">
                <label for="extendedOvertime">Taux HS étendus (175 %, 200 %)</label>
                <p-inputSwitch inputId="extendedOvertime" [(ngModel)]="params()!.enableExtendedOvertimeRates" name="extendedOvertime" />
              </div>
              <div class="payroll-form-group switch-row">
                <label for="allowanceMatrix">Matrice primes (imposable × CNSS)</label>
                <p-inputSwitch inputId="allowanceMatrix" [(ngModel)]="params()!.enableAllowanceQuadrantMatrix" name="allowanceMatrix" />
              </div>
              <div class="payroll-form-group switch-row">
                <label for="industrialSector">Secteur industriel (TFP 1 % au lieu de 2 %)</label>
                <p-inputSwitch inputId="industrialSector" [(ngModel)]="params()!.isIndustrialSector" name="industrialSector" />
              </div>
              <div class="payroll-form-group">
                <label for="smigExemptionMode">Exonération IRPP SMIG (art. 21)</label>
                <p-dropdown
                  inputId="smigExemptionMode"
                  [(ngModel)]="params()!.smigIrppExemptionMode"
                  name="smigExemptionMode"
                  [options]="smigExemptionModeOptions"
                  optionLabel="label"
                  optionValue="value"
                  styleClass="w-full" />
              </div>
              @if (params()!.smigIrppExemptionMode === 'SmigPortion') {
                <div class="payroll-form-group">
                  <label>Taux applicable à la portion SMIG (%)</label>
                  <p-inputNumber
                    [(ngModel)]="params()!.smigIrppExemptionRateOverride"
                    name="smigExemptionRate"
                    [minFractionDigits]="2"
                    [maxFractionDigits]="2"
                    [min]="0"
                    [max]="100"
                    [locale]="'fr-TN'"
                    placeholder="15 (barème)"
                    styleClass="w-full" />
                  <small class="text-muted">Laisser vide pour utiliser le premier taux non nul du barème IRPP.</small>
                </div>
              }
            </div>
            <p class="payroll-info-text mt-2">
              L'article 21 du code de l'IRPP (LF 2019) exonère l'IRPP sur la part du salaire ne dépassant pas le SMIG.
              Le mode « Portion SMIG exonérée » s'applique à tous les salariés ; « Exonération totale » ne concerne que les salaires de base ≤ SMIG.
              La CSS n'est pas impactée. Voir la documentation paie pour le détail des formules.
            </p>
            <p class="payroll-info-text mt-2">Le taux TFP appliqué aux cycles de paie de cet exercice suit ce paramètre ; recalculez les cycles en brouillon pour l'appliquer.</p>
          </p-tabPanel>

          <p-tabPanel header="Saisies sur salaire">
            <ng-template pTemplate="header">
              <i class="pi pi-exclamation-triangle mr-2"></i>
              <span>Saisies</span>
            </ng-template>
            <p class="payroll-info-text mb-3">Barème de quotité saisissable sur le net mensuel (fraction saisissable par tranche). Si vide, 33 % du net s'applique par défaut.</p>
            <p-table [value]="garnishmentBrackets()" styleClass="p-datatable-sm mb-3">
              <ng-template pTemplate="header">
                <tr>
                  <th>Seuil net mensuel (TND)</th>
                  <th>Fraction saisissable (%)</th>
                  <th></th>
                </tr>
              </ng-template>
              <ng-template pTemplate="body" let-bracket let-i="rowIndex">
                <tr>
                  <td>
                    <p-inputNumber [(ngModel)]="bracket.lowerBoundMonthlyNet" [name]="'gbLb' + i" [minFractionDigits]="3" [min]="0" [locale]="'fr-TN'" styleClass="w-full" />
                  </td>
                  <td>
                    <p-inputNumber [(ngModel)]="bracket.seizableFractionPercent" [name]="'gbFrac' + i" [minFractionDigits]="2" [maxFractionDigits]="4" [min]="0" [max]="100" [locale]="'fr-TN'" styleClass="w-full" />
                  </td>
                  <td>
                    <button type="button" pButton icon="pi pi-trash" class="p-button-text p-button-danger p-button-sm" (click)="removeGarnishmentBracket(i)"></button>
                  </td>
                </tr>
              </ng-template>
            </p-table>
            <app-button type="button" variant="outline" icon="pi-plus" iconPos="left" (click)="addGarnishmentBracket()">Ajouter une tranche</app-button>
            <div class="form-actions mt-4">
              <app-button type="button" variant="primary" icon="pi-check" iconPos="left" (click)="saveGarnishmentBrackets()">Enregistrer le barème saisies</app-button>
            </div>
          </p-tabPanel>

          <p-tabPanel header="Caisses complémentaires">
            <ng-template pTemplate="header">
              <i class="pi pi-heart mr-2"></i>
              <span>Mutuelles</span>
            </ng-template>
            <app-payroll-social-funds-settings [fiscalYear]="fiscalYear" />
          </p-tabPanel>
        </p-tabView>

        <div class="form-actions mt-4">
          <app-button type="submit" variant="primary" icon="pi-check" iconPos="left">Enregistrer</app-button>
        </div>
      </form>
    }
  `,
  styles: [`
    .mb-3 { margin-bottom: var(--spacing-4); }
    .mb-3 { margin-bottom: var(--spacing-4); }
    .mt-2 { margin-top: var(--spacing-2); }
    .mt-4 { margin-top: var(--spacing-6); }
    .mr-2 { margin-right: var(--spacing-2); }
    .w-full { width: 100%; }
    .w-8rem { width: 8rem; }
    .switch-row { flex-direction: row; align-items: center; justify-content: space-between; }
    .form-actions { display: flex; gap: var(--spacing-3); }
  `]
})
export class PayrollSettingsComponent implements OnInit {
  private readonly payroll = inject(PayrollService);
  private readonly toast = inject(ToastService);
  fiscalYear = new Date().getFullYear();
  params = signal<PayrollParameters | null>(null);
  garnishmentBrackets = signal<GarnishmentBracketFormRow[]>([]);
  readonly smigExemptionModeOptions = [
    { label: 'Désactivée', value: 'None' },
    { label: 'Portion SMIG exonérée (art. 21)', value: 'SmigPortion' },
    { label: 'Exonération totale si salaire ≤ SMIG', value: 'FullIfBelow' }
  ];

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.payroll.getParameters(this.fiscalYear).subscribe({
      next: res => {
        const data = res.data;
        if (data) {
          this.params.set({
            ...data,
            smigIrppExemptionMode: data.smigIrppExemptionMode ?? 'None',
            smigIrppExemptionModeDisplay: data.smigIrppExemptionModeDisplay ?? 'Désactivée',
            smigIrppExemptionRateOverride: data.smigIrppExemptionRateOverride ?? null,
            mealVoucherDailyExemptionCap: data.mealVoucherDailyExemptionCap ?? 0,
            irppBrackets: data.irppBrackets.map(b => ({ ...b }))
          });
        } else {
          this.params.set(null);
        }
      },
      error: () => this.toast.add({ severity: 'error', summary: 'Paramètres', detail: 'Paramètres introuvables.' })
    });
    this.loadGarnishmentBrackets();
  }

  private loadGarnishmentBrackets(): void {
    this.payroll.getGarnishmentBrackets(this.fiscalYear).subscribe({
      next: res => {
        const rows = (res.data ?? []).map(b => ({
          lowerBoundMonthlyNet: b.lowerBoundMonthlyNet,
          seizableFractionPercent: b.seizableFraction * 100
        }));
        this.garnishmentBrackets.set(rows);
      },
      error: () => this.garnishmentBrackets.set([])
    });
  }

  addGarnishmentBracket(): void {
    this.garnishmentBrackets.update(rows => [...rows, { lowerBoundMonthlyNet: 0, seizableFractionPercent: 0 }]);
  }

  removeGarnishmentBracket(index: number): void {
    this.garnishmentBrackets.update(rows => rows.filter((_, i) => i !== index));
  }

  saveGarnishmentBrackets(): void {
    const rows = this.garnishmentBrackets();
    const sorted = [...rows].sort((a, b) => a.lowerBoundMonthlyNet - b.lowerBoundMonthlyNet);
    if (sorted.some(b => b.seizableFractionPercent < 0 || b.seizableFractionPercent > 100)) {
      this.toast.add({ severity: 'error', summary: 'Barème saisies', detail: 'Les fractions doivent être entre 0 et 100 %.' });
      return;
    }
    const payload: PayrollGarnishmentBracket[] = sorted.map(b => ({
      lowerBoundMonthlyNet: b.lowerBoundMonthlyNet,
      seizableFraction: b.seizableFractionPercent / 100
    }));
    this.payroll.updateGarnishmentBrackets(this.fiscalYear, payload).subscribe({
      next: () => this.toast.add({ severity: 'success', summary: 'Barème saisies', detail: 'Barème enregistré.' }),
      error: err => this.toast.add({
        severity: 'error',
        summary: 'Barème saisies',
        detail: err?.error?.message ?? 'Enregistrement impossible.'
      })
    });
  }

  addBracket(): void {
    const p = this.params();
    if (!p) return;
    p.irppBrackets.push({ lowerBound: 0, rate: 0 });
    this.params.set({ ...p, irppBrackets: [...p.irppBrackets] });
  }

  removeBracket(index: number): void {
    const p = this.params();
    if (!p) return;
    const brackets = p.irppBrackets.filter((_, i) => i !== index);
    this.params.set({ ...p, irppBrackets: brackets });
  }

  /** Contrôle client du barème IRPP ; renvoie null si valide, sinon le message d'erreur. */
  private validateBrackets(brackets: { lowerBound: number; rate: number }[]): string | null {
    if (!brackets.length) return 'Le barème IRPP doit comporter au moins une tranche.';
    const sorted = [...brackets].sort((a, b) => a.lowerBound - b.lowerBound);
    if (sorted[0].lowerBound !== 0) return 'La première tranche IRPP doit démarrer à 0.';
    for (let i = 1; i < sorted.length; i++) {
      if (sorted[i].lowerBound === sorted[i - 1].lowerBound)
        return 'Deux tranches IRPP ne peuvent pas avoir le même seuil inférieur.';
    }
    if (sorted.some(b => b.rate < 0 || b.rate > 100)) return 'Les taux IRPP doivent être compris entre 0 et 100 %.';
    return null;
  }

  save(): void {
    const p = this.params();
    if (!p) return;
    const bracketError = this.validateBrackets(p.irppBrackets);
    if (bracketError) {
      this.toast.add({ severity: 'error', summary: 'Barème IRPP', detail: bracketError });
      return;
    }
    const { id, fiscalYear, ...body } = p;
    this.payroll.updateParameters(this.fiscalYear, body).subscribe({
      next: () => this.toast.add({ severity: 'success', summary: 'Paramètres', detail: 'Paramètres enregistrés.' }),
      error: err => this.toast.add({
        severity: 'error',
        summary: 'Paramètres',
        detail: err?.error?.message ?? 'Enregistrement impossible.'
      })
    });
  }
}

interface GarnishmentBracketFormRow {
  lowerBoundMonthlyNet: number;
  seizableFractionPercent: number;
}
