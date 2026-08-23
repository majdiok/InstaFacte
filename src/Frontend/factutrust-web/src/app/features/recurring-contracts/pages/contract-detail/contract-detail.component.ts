import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterModule } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { PageHeaderComponent } from '@shared/components/page-header/page-header.component';
import { ButtonComponent } from '@shared/components/button/button.component';
import {
  RecurringContractDetail,
  RecurringContractService,
  UsageMetric,
  UsageRecord
} from '@core/services/recurring-contract.service';
import { AuthService } from '@core/services/auth.service';
import { PERMISSIONS } from '@core/config/permission-keys';

@Component({
  selector: 'app-contract-detail',
  standalone: true,
  imports: [CommonModule, RouterModule, FormsModule, PageHeaderComponent, ButtonComponent],
  template: `
    @if (contract(); as c) {
      <app-page-header
        [title]="c.number || 'Contrat récurrent'"
        [subtitle]="c.clientName + ' — ' + c.statusDisplay">
        <div class="actions">
          @if (c.status === 0) {
            <app-button variant="primary" (clicked)="activate()">Activer</app-button>
          }
          @if (c.status === 1) {
            <app-button variant="outline" (clicked)="suspend()">Suspendre</app-button>
            @if (canTriggerBilling()) {
              <app-button variant="outline" (clicked)="triggerBilling()">Générer brouillon</app-button>
            }
          }
          @if (c.status === 2) {
            <app-button variant="primary" (clicked)="resume()">Reprendre</app-button>
          }
          @if (c.status === 1 || c.status === 2) {
            <app-button variant="outline" (clicked)="cancel()">Résilier</app-button>
          }
          <app-button variant="outline" [routerLink]="['edit']">Modifier</app-button>
        </div>
      </app-page-header>

      <div class="ft-card info">
        <p><strong>Périodicité :</strong> {{ c.billingFrequencyDisplay }} (jour {{ c.billingDayOfMonth }})</p>
        <p><strong>Début :</strong> {{ c.startDate | date:'dd/MM/yyyy' }}</p>
        <p><strong>Prochaine facturation :</strong> {{ c.nextBillingDate ? (c.nextBillingDate | date:'dd/MM/yyyy') : '—' }}</p>
        @if (c.notes) { <p><strong>Notes :</strong> {{ c.notes }}</p> }
      </div>

      <div class="ft-card">
        <h3>Lignes</h3>
        <table class="ft-table">
          <thead><tr><th>Type</th><th>Description</th><th>Qté</th><th>Prix HT</th><th>TVA</th></tr></thead>
          <tbody>
            @for (line of c.lines; track line.id) {
              <tr>
                <td>{{ line.lineTypeDisplay || line.lineType }}</td>
                <td>{{ line.description }}</td>
                <td>{{ line.quantity }}</td>
                <td>{{ line.unitPriceHT | number:'1.3-3' }}</td>
                <td>{{ line.vatRate }}%</td>
              </tr>
            }
          </tbody>
        </table>
      </div>

      <div class="ft-card">
        <h3>Consommation</h3>
        @if (canRecordUsage()) {
          <form class="usage-form" (ngSubmit)="submitUsage()">
            <select class="ft-input" [(ngModel)]="usageMetricId" name="metric" required>
              <option value="">— Métrique —</option>
              @for (m of usageMetrics(); track m.id) {
                <option [value]="m.id">{{ m.name }} ({{ m.unit }})</option>
              }
            </select>
            <input class="ft-input" type="date" [(ngModel)]="usagePeriodFrom" name="from" required />
            <input class="ft-input" type="date" [(ngModel)]="usagePeriodTo" name="to" required />
            <input class="ft-input" type="number" [(ngModel)]="usageQuantity" name="qty" placeholder="Quantité" required />
            <app-button type="submit" variant="primary">Enregistrer</app-button>
          </form>
        }
        @if (usageRecords().length === 0) {
          <p>Aucune saisie de consommation.</p>
        } @else {
          <table class="ft-table">
            <thead><tr><th>Métrique</th><th>Période</th><th>Quantité</th><th>Source</th></tr></thead>
            <tbody>
              @for (r of usageRecords(); track r.id) {
                <tr>
                  <td>{{ r.usageMetricName }}</td>
                  <td>{{ r.periodFrom | date:'dd/MM/yyyy' }} — {{ r.periodTo | date:'dd/MM/yyyy' }}</td>
                  <td>{{ r.quantity }}</td>
                  <td>{{ r.sourceDisplay }}</td>
                </tr>
              }
            </tbody>
          </table>
        }
      </div>
    }
  `,
  styles: [`
    .actions { display: flex; gap: .5rem; flex-wrap: wrap; }
    .info p { margin: .35rem 0; }
    .ft-card { margin-bottom: 1rem; padding: 1rem; }
    .usage-form { display: grid; grid-template-columns: repeat(auto-fill, minmax(160px, 1fr)); gap: .75rem; margin-bottom: 1rem; align-items: end; }
  `]
})
export class ContractDetailComponent implements OnInit {
  private readonly service = inject(RecurringContractService);
  private readonly auth = inject(AuthService);
  private readonly route = inject(ActivatedRoute);

  readonly contract = signal<RecurringContractDetail | null>(null);
  readonly usageRecords = signal<UsageRecord[]>([]);
  readonly usageMetrics = signal<UsageMetric[]>([]);
  private contractId = '';

  usageMetricId = '';
  usagePeriodFrom = '';
  usagePeriodTo = '';
  usageQuantity = 0;

  ngOnInit(): void {
    this.contractId = this.route.snapshot.paramMap.get('id')!;
    this.service.listUsageMetrics().subscribe(m => this.usageMetrics.set(m));
    this.reload();
  }

  canRecordUsage(): boolean {
    return this.auth.hasPermission(PERMISSIONS.recurringContracts.recordUsage);
  }

  canTriggerBilling(): boolean {
    return this.auth.hasPermission(PERMISSIONS.recurringContracts.triggerBilling);
  }

  activate(): void { this.service.activate(this.contractId).subscribe(() => this.reload()); }
  suspend(): void { this.service.suspend(this.contractId).subscribe(() => this.reload()); }
  resume(): void { this.service.resume(this.contractId).subscribe(() => this.reload()); }
  cancel(): void { this.service.cancel(this.contractId).subscribe(() => this.reload()); }
  triggerBilling(): void { this.service.triggerBilling(this.contractId).subscribe(() => this.reload()); }

  submitUsage(): void {
    if (!this.usageMetricId) return;
    this.service.recordUsage(this.contractId, {
      usageMetricId: this.usageMetricId,
      periodFrom: this.usagePeriodFrom,
      periodTo: this.usagePeriodTo,
      quantity: this.usageQuantity
    }).subscribe(() => {
      this.usageQuantity = 0;
      this.reload();
    });
  }

  private reload(): void {
    this.service.get(this.contractId).subscribe(c => this.contract.set(c));
    this.service.listUsageRecords(this.contractId).subscribe(r => this.usageRecords.set(r));
  }
}
