import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { TableModule } from 'primeng/table';
import { InputNumberModule } from 'primeng/inputnumber';
import { InputTextModule } from 'primeng/inputtext';
import { InputSwitchModule } from 'primeng/inputswitch';
import { SelectModule } from 'primeng/select';
import { PageHeaderComponent } from '@shared/components/page-header/page-header.component';
import { ButtonComponent } from '@shared/components/button/button.component';
import { PayrollService, AnnualBonusRule, UpsertAnnualBonusRuleRequest } from '@core/services/payroll.service';
import { ToastService } from '@core/services/toast.service';

@Component({
  selector: 'app-annual-bonuses-settings',
  standalone: true,
  imports: [
    CommonModule, FormsModule, TableModule, InputNumberModule, InputTextModule,
    InputSwitchModule, SelectModule, PageHeaderComponent, ButtonComponent
  ],
  template: `
    <app-page-header title="Primes annuelles" subtitle="Règles de 13e mois, ancienneté et vacances." />

    <div class="payroll-toolbar mb-3">
      <label>Exercice</label>
      <p-inputNumber [(ngModel)]="fiscalYear" (ngModelChange)="load()" [useGrouping]="false" />
      <app-button class="ml-3" variant="primary" (click)="startCreate()">Nouvelle règle</app-button>
    </div>

    @if (editing()) {
      <div class="payroll-form-row mb-4">
        <div class="payroll-form-group"><label>Code</label><input pInputText [(ngModel)]="form.code" class="w-full" /></div>
        <div class="payroll-form-group"><label>Libellé</label><input pInputText [(ngModel)]="form.label" class="w-full" /></div>
        <div class="payroll-form-group"><label>Mois de versement</label><p-inputNumber [(ngModel)]="form.paymentMonth" [min]="1" [max]="12" /></div>
        <div class="payroll-form-group"><label>Mois de base</label><p-inputNumber [(ngModel)]="form.monthsOfBase" [minFractionDigits]="2" /></div>
        <div class="payroll-form-group"><label>Actif</label><p-inputSwitch [(ngModel)]="form.isActive" /></div>
        <div class="payroll-form-group flex-end">
          <app-button variant="primary" (click)="saveRule()">Enregistrer</app-button>
        </div>
      </div>
    }

    <p-table [value]="rules()" styleClass="p-datatable-sm">
      <ng-template pTemplate="header">
        <tr><th>Code</th><th>Libellé</th><th>Type</th><th>Mois</th><th>Formule</th><th>Actif</th><th></th></tr>
      </ng-template>
      <ng-template pTemplate="body" let-row>
        <tr>
          <td>{{ row.code }}</td>
          <td>{{ row.label }}</td>
          <td>{{ row.kindDisplay }}</td>
          <td>{{ row.paymentMonth }}</td>
          <td>{{ row.formulaDisplay }}</td>
          <td>{{ row.isActive ? 'Oui' : 'Non' }}</td>
          <td><button type="button" class="p-button p-button-text" (click)="edit(row)">Modifier</button></td>
        </tr>
      </ng-template>
    </p-table>
  `
})
export class AnnualBonusesSettingsComponent implements OnInit {
  private readonly payroll = inject(PayrollService);
  private readonly toast = inject(ToastService);

  fiscalYear = new Date().getFullYear();
  readonly rules = signal<AnnualBonusRule[]>([]);
  readonly editing = signal(false);
  editingId: string | null = null;
  form: UpsertAnnualBonusRuleRequest = this.emptyForm();

  ngOnInit(): void { this.load(); }

  load(): void {
    this.payroll.listAnnualBonusRules(this.fiscalYear).subscribe({
      next: res => this.rules.set(res.data ?? []),
      error: () => this.toast.add({ severity: 'error', summary: 'Primes annuelles', detail: 'Chargement impossible.' })
    });
  }

  startCreate(): void {
    this.editingId = null;
    this.form = this.emptyForm();
    this.editing.set(true);
  }

  edit(row: AnnualBonusRule): void {
    this.editingId = row.id;
    this.form = {
      code: row.code,
      label: row.label,
      kind: row.kind,
      formula: row.formula,
      paymentMonth: row.paymentMonth,
      fixedAmount: row.fixedAmount,
      ratePercent: row.ratePercent,
      monthsOfBase: row.monthsOfBase,
      taxable: row.taxable,
      subjectToCnss: row.subjectToCnss,
      isActive: row.isActive,
      fiscalYear: row.fiscalYear
    };
    this.editing.set(true);
  }

  saveRule(): void {
    const req = { ...this.form, fiscalYear: this.fiscalYear };
    const obs = this.editingId
      ? this.payroll.updateAnnualBonusRule(this.editingId, req)
      : this.payroll.createAnnualBonusRule(req);
    obs.subscribe({
      next: () => { this.toast.add({ severity: 'success', summary: 'Primes annuelles', detail: 'Règle enregistrée.' }); this.editing.set(false); this.load(); },
      error: err => this.toast.add({ severity: 'error', summary: 'Primes annuelles', detail: err?.error?.message ?? 'Erreur.' })
    });
  }

  private emptyForm(): UpsertAnnualBonusRuleRequest {
    return {
      code: '', label: '', kind: 'ThirteenthMonth', formula: 'MonthsOfBase',
      paymentMonth: 12, fixedAmount: 0, ratePercent: 0, monthsOfBase: 1,
      taxable: true, subjectToCnss: true, isActive: true
    };
  }
}
