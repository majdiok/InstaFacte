import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { TabViewModule } from 'primeng/tabview';
import { InputNumberModule } from 'primeng/inputnumber';
import { InputSwitchModule } from 'primeng/inputswitch';
import { TableModule } from 'primeng/table';
import { ButtonModule } from 'primeng/button';
import { PayrollService, PayrollParameters } from '@core/services/payroll.service';
import { ToastService } from '@core/services/toast.service';
import { PageHeaderComponent } from '@shared/components/page-header/page-header.component';
import { ButtonComponent } from '@shared/components/button/button.component';

@Component({
  selector: 'app-payroll-settings',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    TabViewModule,
    InputNumberModule,
    InputSwitchModule,
    TableModule,
    ButtonModule,
    PageHeaderComponent,
    ButtonComponent
  ],
  template: `
    <app-page-header title="Paramètres de paie" subtitle="Barème IRPP, taux légaux et conformité par exercice." />

    <div class="payroll-toolbar mb-3">
      <label for="fiscalYear">Exercice</label>
      <p-inputNumber id="fiscalYear" [(ngModel)]="fiscalYear" (ngModelChange)="load()" [useGrouping]="false" styleClass="w-8rem" />
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
                <p-inputNumber [(ngModel)]="params()!.cnssEmployeeRate" name="cnssEmployeeRate" [minFractionDigits]="2" [maxFractionDigits]="4" [locale]="'fr-TN'" styleClass="w-full" />
              </div>
              <div class="payroll-form-group">
                <label>CNSS employeur (%)</label>
                <p-inputNumber [(ngModel)]="params()!.cnssEmployerRate" name="cnssEmployerRate" [minFractionDigits]="2" [maxFractionDigits]="4" [locale]="'fr-TN'" styleClass="w-full" />
              </div>
              <div class="payroll-form-group">
                <label>CNSS salarié RSA (%)</label>
                <p-inputNumber [(ngModel)]="params()!.cnssEmployeeRateRsa" name="cnssEmployeeRateRsa" [minFractionDigits]="2" [maxFractionDigits]="4" [locale]="'fr-TN'" styleClass="w-full" />
              </div>
              <div class="payroll-form-group">
                <label>CNSS employeur RSA (%)</label>
                <p-inputNumber [(ngModel)]="params()!.cnssEmployerRateRsa" name="cnssEmployerRateRsa" [minFractionDigits]="2" [maxFractionDigits]="4" [locale]="'fr-TN'" styleClass="w-full" />
              </div>
              <div class="payroll-form-group">
                <label>CSS (%)</label>
                <p-inputNumber [(ngModel)]="params()!.cssRate" name="cssRate" [minFractionDigits]="2" [maxFractionDigits]="4" [locale]="'fr-TN'" styleClass="w-full" />
              </div>
              <div class="payroll-form-group">
                <label>Seuil exonération CSS (annuel)</label>
                <p-inputNumber [(ngModel)]="params()!.cssAnnualExemptionThreshold" name="cssExemption" [minFractionDigits]="3" [locale]="'fr-TN'" styleClass="w-full" />
              </div>
              <div class="payroll-form-group">
                <label>TFP industrie (%)</label>
                <p-inputNumber [(ngModel)]="params()!.tfpRateIndustry" name="tfpIndustry" [minFractionDigits]="2" [maxFractionDigits]="4" [locale]="'fr-TN'" styleClass="w-full" />
              </div>
              <div class="payroll-form-group">
                <label>TFP autres (%)</label>
                <p-inputNumber [(ngModel)]="params()!.tfpRateOther" name="tfpOther" [minFractionDigits]="2" [maxFractionDigits]="4" [locale]="'fr-TN'" styleClass="w-full" />
              </div>
              <div class="payroll-form-group">
                <label>FOPROLOS (%)</label>
                <p-inputNumber [(ngModel)]="params()!.foprolosRate" name="foprolos" [minFractionDigits]="2" [maxFractionDigits]="4" [locale]="'fr-TN'" styleClass="w-full" />
              </div>
              <div class="payroll-form-group">
                <label>SMIG mensuel (TND)</label>
                <p-inputNumber [(ngModel)]="params()!.monthlySmig" name="smig" [minFractionDigits]="3" [locale]="'fr-TN'" styleClass="w-full" />
              </div>
              <div class="payroll-form-group">
                <label>Frais pro. (%)</label>
                <p-inputNumber [(ngModel)]="params()!.professionalExpensesRate" name="proExpRate" [minFractionDigits]="2" [maxFractionDigits]="4" [locale]="'fr-TN'" styleClass="w-full" />
              </div>
              <div class="payroll-form-group">
                <label>Plafond frais pro. (annuel)</label>
                <p-inputNumber [(ngModel)]="params()!.professionalExpensesAnnualCap" name="proExpCap" [minFractionDigits]="3" [locale]="'fr-TN'" styleClass="w-full" />
              </div>
            </div>
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
                    <p-inputNumber [(ngModel)]="bracket.lowerBound" [name]="'lb' + i" [minFractionDigits]="3" [locale]="'fr-TN'" styleClass="w-full" />
                  </td>
                  <td>
                    <p-inputNumber [(ngModel)]="bracket.rate" [name]="'rate' + i" [minFractionDigits]="2" [maxFractionDigits]="4" [locale]="'fr-TN'" styleClass="w-full" />
                  </td>
                  <td>
                    <button type="button" pButton icon="pi pi-trash" class="p-button-text p-button-danger p-button-sm" (click)="removeBracket(i)"></button>
                  </td>
                </tr>
              </ng-template>
            </p-table>
            <app-button type="button" variant="outline" icon="pi-plus" iconPos="left" (click)="addBracket()">Ajouter une tranche</app-button>
          </p-tabPanel>

          <p-tabPanel header="Déductions familiales">
            <ng-template pTemplate="header">
              <i class="pi pi-users mr-2"></i>
              <span>Déductions familiales</span>
            </ng-template>
            <div class="payroll-form-row">
              <div class="payroll-form-group">
                <label>Chef de famille (annuel, TND)</label>
                <p-inputNumber [(ngModel)]="params()!.headOfFamilyAnnualDeduction" name="headDeduction" [minFractionDigits]="3" [locale]="'fr-TN'" styleClass="w-full" />
              </div>
              <div class="payroll-form-group">
                <label>Enfant à charge (annuel, TND)</label>
                <p-inputNumber [(ngModel)]="params()!.childAnnualDeduction" name="childDeduction" [minFractionDigits]="3" [locale]="'fr-TN'" styleClass="w-full" />
              </div>
              <div class="payroll-form-group">
                <label>Enfants déductibles max.</label>
                <p-inputNumber [(ngModel)]="params()!.maxDeductibleChildren" name="maxChildren" [min]="0" styleClass="w-full" />
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
            </div>
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
            irppBrackets: data.irppBrackets.map(b => ({ ...b }))
          });
        } else {
          this.params.set(null);
        }
      },
      error: () => this.toast.add({ severity: 'error', summary: 'Paramètres', detail: 'Paramètres introuvables.' })
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

  save(): void {
    const p = this.params();
    if (!p) return;
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
