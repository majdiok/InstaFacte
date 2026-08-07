import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { TableModule } from 'primeng/table';
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { InputNumberModule } from 'primeng/inputnumber';
import { InputTextModule } from 'primeng/inputtext';
import { ProgressBarModule } from 'primeng/progressbar';
import { SelectModule } from 'primeng/select';
import { AuthService } from '@core/services/auth.service';
import { ToastService } from '@core/services/toast.service';
import { FirmLeavesService } from './data-access/firm-leaves.service';
import { FirmLeaveBalance } from './data-access/firm-leaves.models';

@Component({
  selector: 'app-firm-leaves-balances',
  standalone: true,
  imports: [
    CommonModule, FormsModule, TableModule, ButtonModule, DialogModule,
    InputNumberModule, InputTextModule, ProgressBarModule, SelectModule
  ],
  template: `
    <div class="filters">
      <p-select [(ngModel)]="year" [options]="yearOptions" (onChange)="reload()" />
    </div>
    <div class="fc-card">
      <p-table [value]="rows()" [loading]="loading()">
        <ng-template pTemplate="header">
          <tr>
            <th>Collaborateur</th>
            <th>Ouverture</th>
            <th>Ajustement</th>
            <th>Consommé</th>
            <th>Restant</th>
            <th>Utilisation</th>
            @if (isManager()) { <th></th> }
          </tr>
        </ng-template>
        <ng-template pTemplate="body" let-b>
          <tr>
            <td>{{ b.collaboratorName }}</td>
            <td>{{ b.openingBalanceDays }}</td>
            <td>{{ b.adjustmentDays }}</td>
            <td>{{ b.consumedDays }}</td>
            <td><strong>{{ b.remainingDays }}</strong></td>
            <td>
              <p-progressBar [value]="usage(b)" [showValue]="false" styleClass="h-progress" />
              <small>{{ usage(b) | number:'1.0-0' }} %</small>
            </td>
            @if (isManager()) {
              <td><button type="button" pButton icon="pi pi-pencil" class="p-button-text p-button-sm" (click)="openEdit(b)"></button></td>
            }
          </tr>
        </ng-template>
      </p-table>
    </div>

    <p-dialog header="Ajuster le solde" [(visible)]="editVisible" [modal]="true" [style]="{width:'420px'}" appendTo="body">
      <div class="dlg">
        <label>Solde d'ouverture
          <p-inputNumber [(ngModel)]="editOpening" [minFractionDigits]="0" [maxFractionDigits]="3" />
        </label>
        <label>Ajustement
          <p-inputNumber [(ngModel)]="editAdjustment" [minFractionDigits]="0" [maxFractionDigits]="3" />
        </label>
        <label>Notes
          <input pInputText [(ngModel)]="editNotes" />
        </label>
      </div>
      <ng-template pTemplate="footer">
        <button type="button" pButton label="Annuler" class="p-button-text" (click)="editVisible=false"></button>
        <button type="button" pButton label="Enregistrer" (click)="save()"></button>
      </ng-template>
    </p-dialog>
  `,
  styles: [`
    .filters { margin-bottom: .75rem; }
    .fc-card { background:#fff; border:1px solid #e2e8f0; border-radius:16px; padding:8px; }
    .dlg { display:grid; gap:.75rem; }
    .dlg label { display:grid; gap:.35rem; }
    :host ::ng-deep .h-progress { height: .5rem; }
  `]
})
export class FirmLeavesBalancesComponent implements OnInit {
  private readonly api = inject(FirmLeavesService);
  private readonly auth = inject(AuthService);
  private readonly toast = inject(ToastService);

  readonly isManager = this.auth.isFirmManager;
  readonly loading = signal(false);
  readonly rows = signal<FirmLeaveBalance[]>([]);
  year = new Date().getFullYear();
  readonly yearOptions = [2025, 2026, 2027, 2028].map(y => ({ label: String(y), value: y }));
  editVisible = false;
  editOpening = 0;
  editAdjustment = 0;
  editNotes = '';
  private editUserId = '';

  ngOnInit(): void { this.reload(); }

  reload(): void {
    this.loading.set(true);
    this.api.listBalances(this.year).subscribe({
      next: r => { this.rows.set(r.data ?? []); this.loading.set(false); },
      error: () => this.loading.set(false)
    });
  }

  usage(b: FirmLeaveBalance): number {
    const total = b.openingBalanceDays + b.adjustmentDays;
    if (total <= 0) return 0;
    return Math.min(100, Math.round((b.consumedDays / total) * 1000) / 10);
  }

  openEdit(b: FirmLeaveBalance): void {
    this.editUserId = b.userId;
    this.editOpening = b.openingBalanceDays;
    this.editAdjustment = b.adjustmentDays;
    this.editNotes = b.notes ?? '';
    this.editVisible = true;
  }

  save(): void {
    this.api.setBalance(this.editUserId, this.year, {
      openingBalanceDays: this.editOpening,
      adjustmentDays: this.editAdjustment,
      notes: this.editNotes || undefined
    }).subscribe({
      next: r => {
        if (r.success) {
          this.editVisible = false;
          this.toast.add({ severity: 'success', summary: 'Solde', detail: 'Mis à jour' });
          this.reload();
        } else this.toast.add({ severity: 'error', summary: 'Erreur', detail: r.message ?? '' });
      }
    });
  }
}
