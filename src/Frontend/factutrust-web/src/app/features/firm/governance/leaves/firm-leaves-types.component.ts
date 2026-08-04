import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { TableModule } from 'primeng/table';
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { InputTextModule } from 'primeng/inputtext';
import { InputNumberModule } from 'primeng/inputnumber';
import { CheckboxModule } from 'primeng/checkbox';
import { TagModule } from 'primeng/tag';
import { ToastService } from '@core/services/toast.service';
import { FirmLeavesService } from './data-access/firm-leaves.service';
import { FirmLeaveType } from './data-access/firm-leaves.models';

@Component({
  selector: 'app-firm-leaves-types',
  standalone: true,
  imports: [
    CommonModule, ReactiveFormsModule, TableModule, ButtonModule, DialogModule,
    InputTextModule, InputNumberModule, CheckboxModule, TagModule
  ],
  template: `
    <div class="toolbar">
      <button type="button" pButton label="Nouveau type" icon="pi pi-plus" class="p-button-sm" (click)="openCreate()"></button>
    </div>
    <div class="fc-card">
      <p-table [value]="rows()" [loading]="loading()">
        <ng-template pTemplate="header">
          <tr>
            <th>Couleur</th><th>Code</th><th>Libellé</th><th>Déduit solde</th><th>Approbation</th><th>Actif</th><th></th>
          </tr>
        </ng-template>
        <ng-template pTemplate="body" let-t>
          <tr>
            <td><span class="swatch" [style.background]="t.colorHex"></span></td>
            <td>{{ t.code }}</td>
            <td>{{ t.label }}</td>
            <td>{{ t.deductsBalance ? 'Oui' : 'Non' }}</td>
            <td>{{ t.requiresApproval ? 'Oui' : 'Non' }}</td>
            <td><p-tag [value]="t.isActive ? 'Actif' : 'Inactif'" [severity]="t.isActive ? 'success' : 'secondary'" /></td>
            <td><button type="button" pButton icon="pi pi-pencil" class="p-button-text p-button-sm" (click)="openEdit(t)"></button></td>
          </tr>
        </ng-template>
      </p-table>
    </div>

    <p-dialog [header]="editingId ? 'Modifier le type' : 'Nouveau type'" [(visible)]="visible" [modal]="true"
      [style]="{width:'460px'}" appendTo="body">
      <form [formGroup]="form" class="dlg">
        @if (!editingId) {
          <label>Code * <input pInputText formControlName="code" /></label>
        }
        <label>Libellé * <input pInputText formControlName="label" /></label>
        <label>Couleur <input pInputText formControlName="colorHex" /></label>
        <label>Ordre <p-inputNumber formControlName="sortOrder" /></label>
        <label class="chk"><p-checkbox formControlName="deductsBalance" [binary]="true" /><span>Déduit du solde</span></label>
        <label class="chk"><p-checkbox formControlName="requiresApproval" [binary]="true" /><span>Nécessite approbation</span></label>
        <label class="chk"><p-checkbox formControlName="isActive" [binary]="true" /><span>Actif</span></label>
      </form>
      <ng-template pTemplate="footer">
        <button type="button" pButton label="Annuler" class="p-button-text" (click)="visible=false"></button>
        <button type="button" pButton label="Enregistrer" (click)="save()"></button>
      </ng-template>
    </p-dialog>
  `,
  styles: [`
    .toolbar { margin-bottom: .75rem; }
    .fc-card { background:#fff; border:1px solid #e2e8f0; border-radius:16px; padding:8px; }
    .swatch { display:inline-block; width:16px; height:16px; border-radius:4px; }
    .dlg { display:grid; gap:.65rem; }
    .dlg label { display:grid; gap:.3rem; font-size:.875rem; }
    .chk { display:flex !important; flex-direction:row !important; align-items:center; gap:.5rem; }
  `]
})
export class FirmLeavesTypesComponent implements OnInit {
  private readonly api = inject(FirmLeavesService);
  private readonly toast = inject(ToastService);
  private readonly fb = inject(FormBuilder);

  readonly loading = signal(false);
  readonly rows = signal<FirmLeaveType[]>([]);
  visible = false;
  editingId: string | null = null;

  readonly form = this.fb.nonNullable.group({
    code: ['', Validators.required],
    label: ['', Validators.required],
    colorHex: ['#64748b'],
    sortOrder: [0],
    deductsBalance: [false],
    requiresApproval: [true],
    isActive: [true]
  });

  ngOnInit(): void { this.reload(); }

  reload(): void {
    this.loading.set(true);
    this.api.listTypes(false).subscribe({
      next: r => { this.rows.set(r.data ?? []); this.loading.set(false); },
      error: () => this.loading.set(false)
    });
  }

  openCreate(): void {
    this.editingId = null;
    this.form.reset({
      code: '', label: '', colorHex: '#64748b', sortOrder: 0,
      deductsBalance: false, requiresApproval: true, isActive: true
    });
    this.visible = true;
  }

  openEdit(t: FirmLeaveType): void {
    this.editingId = t.id;
    this.form.patchValue(t);
    this.visible = true;
  }

  save(): void {
    if (this.form.invalid) { this.form.markAllAsTouched(); return; }
    const v = this.form.getRawValue();
    this.api.upsertType({
      id: this.editingId ?? undefined,
      code: v.code,
      label: v.label,
      colorHex: v.colorHex,
      sortOrder: v.sortOrder,
      deductsBalance: v.deductsBalance,
      requiresApproval: v.requiresApproval,
      isActive: v.isActive
    }).subscribe({
      next: r => {
        if (r.success) {
          this.visible = false;
          this.toast.add({ severity: 'success', summary: 'Types', detail: 'Enregistré' });
          this.reload();
        } else this.toast.add({ severity: 'error', summary: 'Erreur', detail: r.message ?? '' });
      }
    });
  }
}
