import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormsModule, ReactiveFormsModule } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { InputNumberModule } from 'primeng/inputnumber';
import { CheckboxModule } from 'primeng/checkbox';
import { SelectModule } from 'primeng/select';
import { ToastService } from '@core/services/toast.service';
import { FirmLeavesService } from './data-access/firm-leaves.service';

@Component({
  selector: 'app-firm-leaves-settings',
  standalone: true,
  imports: [CommonModule, FormsModule, ReactiveFormsModule, ButtonModule, InputNumberModule, CheckboxModule, SelectModule],
  template: `
    <div class="fc-card">
      <div class="year-row">
        <label>Année
          <p-select [options]="yearOptions" [ngModel]="year" (ngModelChange)="onYear($event)" [ngModelOptions]="{standalone:true}" />
        </label>
      </div>
      <form [formGroup]="form" class="form">
        <label>Jours de congés payés par défaut
          <p-inputNumber formControlName="defaultAnnualPaidDays" [minFractionDigits]="0" [maxFractionDigits]="3" />
        </label>
        <label>Préavis minimum (jours)
          <p-inputNumber formControlName="minNoticeDays" [min]="0" />
        </label>
        <label>Report max (jours)
          <p-inputNumber formControlName="maxCarryOverDays" [minFractionDigits]="0" [maxFractionDigits]="3" />
        </label>
        <label class="chk"><p-checkbox formControlName="allowHalfDays" [binary]="true" /><span>Autoriser les demi-journées</span></label>
        <label class="chk"><p-checkbox formControlName="blockOverlap" [binary]="true" /><span>Bloquer les chevauchements</span></label>
        <label class="chk"><p-checkbox formControlName="carryOverEnabled" [binary]="true" /><span>Activer le report N+1</span></label>
        <button type="button" pButton label="Enregistrer" [loading]="saving()" (click)="save()"></button>
      </form>
    </div>
  `,
  styles: [`
    .fc-card { background:#fff; border:1px solid #e2e8f0; border-radius:16px; padding:1.25rem; max-width:520px; }
    .form { display:grid; gap:.85rem; margin-top:1rem; }
    .form label { display:grid; gap:.35rem; font-size:.875rem; }
    .chk { display:flex !important; flex-direction:row !important; align-items:center; gap:.5rem; }
    .year-row label { display:grid; gap:.35rem; max-width:160px; }
  `]
})
export class FirmLeavesSettingsComponent implements OnInit {
  private readonly api = inject(FirmLeavesService);
  private readonly toast = inject(ToastService);
  private readonly fb = inject(FormBuilder);

  year = new Date().getFullYear();
  readonly yearOptions = [2025, 2026, 2027, 2028].map(y => ({ label: String(y), value: y }));
  readonly saving = signal(false);

  readonly form = this.fb.nonNullable.group({
    defaultAnnualPaidDays: [30],
    allowHalfDays: [true],
    minNoticeDays: [0],
    blockOverlap: [true],
    carryOverEnabled: [false],
    maxCarryOverDays: [0]
  });

  ngOnInit(): void { this.load(); }

  onYear(y: number): void {
    this.year = y;
    this.load();
  }

  load(): void {
    this.api.getSettings(this.year).subscribe({
      next: r => {
        if (r.data) this.form.patchValue(r.data);
      }
    });
  }

  save(): void {
    this.saving.set(true);
    this.api.updateSettings(this.year, this.form.getRawValue()).subscribe({
      next: r => {
        this.saving.set(false);
        if (r.success) this.toast.add({ severity: 'success', summary: 'Paramètres', detail: 'Enregistrés' });
        else this.toast.add({ severity: 'error', summary: 'Erreur', detail: r.message ?? '' });
      },
      error: () => this.saving.set(false)
    });
  }
}
