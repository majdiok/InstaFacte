import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { PageHeaderComponent } from '@shared/components/page-header/page-header.component';
import { ButtonComponent } from '@shared/components/button/button.component';
import {
  RecurringContractDetail,
  RecurringContractService,
  UpsertRecurringContractPayload
} from '@core/services/recurring-contract.service';
import { ClientService, ClientListItem } from '@core/services/client.service';

@Component({
  selector: 'app-contract-form',
  standalone: true,
  imports: [CommonModule, RouterModule, FormsModule, PageHeaderComponent, ButtonComponent],
  template: `
    <app-page-header
      [title]="isEdit() ? 'Modifier le contrat' : 'Nouveau contrat récurrent'"
      subtitle="Définissez la périodicité, les lignes et les conditions de facturation">
    </app-page-header>

    <form class="ft-card form" (ngSubmit)="save()">
      <div class="grid">
        <label>Client
          <select class="ft-input" [(ngModel)]="clientId" name="clientId" required>
            <option value="">— Sélectionner —</option>
            @for (c of clients(); track c.id) {
              <option [value]="c.id">{{ c.name }}</option>
            }
          </select>
        </label>
        <label>Périodicité
          <select class="ft-input" [(ngModel)]="billingFrequency" name="billingFrequency">
            <option [ngValue]="0">Mensuel</option>
            <option [ngValue]="1">Trimestriel</option>
            <option [ngValue]="2">Annuel</option>
          </select>
        </label>
        <label>Jour de facturation
          <input class="ft-input" type="number" min="1" max="31" [(ngModel)]="billingDayOfMonth" name="billingDay" />
        </label>
        <label>Date de début
          <input class="ft-input" type="date" [(ngModel)]="startDate" name="startDate" required />
        </label>
        <label>Date de fin
          <input class="ft-input" type="date" [(ngModel)]="endDate" name="endDate" />
        </label>
        <label>Référence
          <input class="ft-input" [(ngModel)]="reference" name="reference" />
        </label>
      </div>

      <h3>Lignes du contrat</h3>
      @for (line of lines; track $index; let i = $index) {
        <div class="line-row">
          <select class="ft-input" [(ngModel)]="line.lineType" [name]="'lineType' + i">
            <option [ngValue]="0">Récurrent fixe</option>
            <option [ngValue]="1">À la consommation</option>
            <option [ngValue]="2">Frais d'installation</option>
          </select>
          <input class="ft-input" placeholder="Description" [(ngModel)]="line.description" [name]="'desc' + i" required />
          <input class="ft-input" type="number" placeholder="Qté" [(ngModel)]="line.quantity" [name]="'qty' + i" />
          <input class="ft-input" type="number" placeholder="Prix HT" [(ngModel)]="line.unitPriceHT" [name]="'price' + i" />
          <input class="ft-input" type="number" placeholder="TVA %" [(ngModel)]="line.vatRate" [name]="'vat' + i" />
          <button type="button" class="link-btn" (click)="removeLine(i)">Supprimer</button>
        </div>
      }
      <app-button type="button" variant="outline" (clicked)="addLine()">Ajouter une ligne</app-button>

      <label>Notes
        <textarea class="ft-input" rows="3" [(ngModel)]="notes" name="notes"></textarea>
      </label>

      <div class="actions">
        <app-button type="button" variant="outline" routerLink="/recurring-contracts">Annuler</app-button>
        <app-button type="submit" variant="primary" [disabled]="saving()">Enregistrer</app-button>
      </div>
    </form>
  `,
  styles: [`
    .form { padding: 1.5rem; display: flex; flex-direction: column; gap: 1rem; }
    .grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(220px, 1fr)); gap: 1rem; }
    label { display: flex; flex-direction: column; gap: .35rem; font-size: .9rem; }
    .line-row { display: grid; grid-template-columns: 140px 1fr 80px 100px 80px auto; gap: .5rem; align-items: center; }
    .actions { display: flex; gap: .75rem; justify-content: flex-end; margin-top: 1rem; }
    .link-btn { background: none; border: none; color: var(--primary-color); cursor: pointer; }
  `]
})
export class ContractFormComponent implements OnInit {
  private readonly service = inject(RecurringContractService);
  private readonly clientService = inject(ClientService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  readonly clients = signal<ClientListItem[]>([]);
  readonly isEdit = signal(false);
  readonly saving = signal(false);
  contractId: string | null = null;

  clientId = '';
  billingFrequency = 0;
  billingDayOfMonth = 1;
  startDate = new Date().toISOString().slice(0, 10);
  endDate = '';
  reference = '';
  notes = '';
  lines: Array<{
    lineType: number;
    description: string;
    quantity: number;
    unitPriceHT: number;
    vatRate: number;
    sortOrder: number;
  }> = [{ lineType: 0, description: '', quantity: 1, unitPriceHT: 0, vatRate: 19, sortOrder: 0 }];

  ngOnInit(): void {
    this.clientService.getClients({ page: 1, pageSize: 200 }).subscribe({
      next: (res) => {
        if (res.success) this.clients.set(res.data.items);
      }
    });
    const id = this.route.snapshot.paramMap.get('id');
    if (id && this.route.snapshot.url.some(s => s.path === 'edit')) {
      this.isEdit.set(true);
      this.contractId = id;
      this.service.get(id).subscribe(c => this.patchForm(c));
    }
  }

  addLine(): void {
    this.lines.push({ lineType: 0, description: '', quantity: 1, unitPriceHT: 0, vatRate: 19, sortOrder: this.lines.length });
  }

  removeLine(index: number): void {
    this.lines.splice(index, 1);
  }

  save(): void {
    const payload: UpsertRecurringContractPayload = {
      clientId: this.clientId,
      billingFrequency: this.billingFrequency,
      billingDayOfMonth: this.billingDayOfMonth,
      startDate: this.startDate,
      endDate: this.endDate || null,
      autoRenew: true,
      noticePeriodDays: 30,
      reference: this.reference || null,
      notes: this.notes || null,
      lines: this.lines.map((l, i) => ({ ...l, sortOrder: i }))
    };
    this.saving.set(true);
    if (this.isEdit() && this.contractId) {
      this.service.update(this.contractId, payload).subscribe({
        next: () => {
          this.saving.set(false);
          void this.router.navigate(['/recurring-contracts', this.contractId!]);
        },
        error: () => this.saving.set(false)
      });
      return;
    }
    this.service.create(payload).subscribe({
      next: (id) => {
        this.saving.set(false);
        void this.router.navigate(['/recurring-contracts', id]);
      },
      error: () => this.saving.set(false)
    });
  }

  private patchForm(c: RecurringContractDetail): void {
    this.clientId = c.clientId;
    this.billingFrequency = c.billingFrequency;
    this.billingDayOfMonth = c.billingDayOfMonth;
    this.startDate = c.startDate.slice(0, 10);
    this.endDate = c.endDate?.slice(0, 10) ?? '';
    this.reference = c.reference ?? '';
    this.notes = c.notes ?? '';
    this.lines = c.lines.map((l, i) => ({
      lineType: l.lineType,
      description: l.description,
      quantity: l.quantity,
      unitPriceHT: l.unitPriceHT,
      vatRate: l.vatRate,
      sortOrder: i
    }));
  }
}
