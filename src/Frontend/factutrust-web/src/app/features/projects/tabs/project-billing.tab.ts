import { Component, EventEmitter, Input, OnChanges, Output, SimpleChanges } from '@angular/core';

import { CommonModule } from '@angular/common';

import { FormsModule } from '@angular/forms';

import { RouterLink } from '@angular/router';

import { TableModule } from 'primeng/table';

import { InputTextModule } from 'primeng/inputtext';

import { InputNumberModule } from 'primeng/inputnumber';

import { SelectModule } from 'primeng/select';

import { DatePickerModule } from 'primeng/datepicker';

import { MessageModule } from 'primeng/message';

import { DialogModule } from 'primeng/dialog';

import { ProgressBarModule } from 'primeng/progressbar';

import { ButtonComponent } from '@shared/components/button/button.component';

import { StatusBadgeComponent } from '@shared/components/status-badge/status-badge.component';

import {

  ProjectBillingReadiness,

  ProjectDetail,

  ProjectMilestone,

  ProjectSituation,

  ProjectSubcontractor

} from '../project-api.service';

import {

  TUNISIAN_VAT_OPTIONS,

  canActivate,

  canBeBilled,

  isBtp,

  isFixedPriceBilling,

  parseProjectSituationStatus,

  showMilestones,

  situationStatusBadge,

  toIsoDate

} from '../project-enums';



export interface SupplierOption {

  id: string;

  name: string;

}



const TIME_GROUP_OPTIONS = [

  { label: 'Par membre', value: 'member' },

  { label: 'Par tâche', value: 'task' }

];



@Component({

  selector: 'app-project-billing-tab',

  standalone: true,

  imports: [

    CommonModule,

    FormsModule,

    RouterLink,

    TableModule,

    InputTextModule,

    InputNumberModule,

    SelectModule,

    DatePickerModule,

    MessageModule,

    DialogModule,

    ProgressBarModule,

    ButtonComponent,

    StatusBadgeComponent

  ],

  template: `

    @if (project && !canBeBilled(project.status)) {

      @if (readiness?.blockers?.length) {

        <ul class="blockers mb-3">

          @for (b of readiness!.blockers; track b) {

            <li>{{ b }}</li>

          }

        </ul>

      } @else {

        <p-message severity="warn" styleClass="w-full mb-3"

          text="Activez le projet (statut Brouillon) avant de facturer. La facturation est réservée aux projets Actif ou Terminé." />

      }

      @if (canActivate(project.status) && canUpdate) {

        <app-button class="mb-3" variant="primary" (click)="activate.emit()">Activer le projet</app-button>

      }

    }



    @if (readiness) {

      <div class="card p-3 mb-3">

        <h3 class="mt-0">Prérequis de facturation</h3>

        @if (readiness.blockers.length) {

          <ul class="m-0 pl-3 mb-2">

            @for (b of readiness.blockers; track b) {

              <li>{{ b }}</li>

            }

          </ul>

        }

        <ul class="m-0 pl-3">

          <li>Statut : {{ readiness.statusDisplay }} {{ readiness.canBill ? '✓' : '✗' }}</li>

          <li>Temps validés non facturés : {{ readiness.validatedUninvoicedHours | number:'1.0-1' }} h</li>

          <li>Équipe sans TJM / coût h :

            @if (readiness.membersWithoutRate.length) {

              {{ readiness.membersWithoutRate.join(', ') }}

            } @else { aucun }

          </li>

        </ul>

      </div>

    }



    @if (project && canBeBilled(project.status) && canBill) {

      <div class="card p-3 mb-3">

        <h3 class="mt-0">Facturation régie</h3>

        <div class="proj-field-row">

          <label>Grouper par

            <p-select [options]="groupOptions" [(ngModel)]="timeGroupBy" optionLabel="label" optionValue="value" />

          </label>

          <label>Notes

            <input pInputText [(ngModel)]="timeNotes" placeholder="Notes facture" />

          </label>

          <app-button variant="primary" [disabled]="!canInvoiceTime" (click)="emitInvoiceTime()">Facturer les temps validés</app-button>

        </div>

      </div>

    }



    @if (project && isFixedPriceBilling(project.billingMode) && canBeBilled(project.status) && canBill) {

      <div class="card p-3 mb-3">

        <h3 class="mt-0">Forfait</h3>

        <div class="proj-field-row">

          <label>Montant HT

            <p-inputNumber [(ngModel)]="fixedPriceAmount" mode="decimal" [minFractionDigits]="3" />

          </label>

          <label>Notes

            <input pInputText [(ngModel)]="fixedPriceNotes" />

          </label>

          <app-button variant="primary" [disabled]="fixedPriceInvoiced" (click)="emitFixedPrice()">Facturer le forfait</app-button>

        </div>

      </div>

    }



    @if (project && showMilestones(project.kind, project.billingMode)) {

      <h3>Jalons</h3>

      @if (canBill) {

        <div class="proj-field-row mb-2">

          <label>Nom <input pInputText [(ngModel)]="milestoneName" /></label>

          <label>% <p-inputNumber [(ngModel)]="milestonePercent" [min]="0" [max]="100" /></label>

          <label>Montant HT <p-inputNumber [(ngModel)]="milestoneAmount" /></label>

          <label>Échéance <p-datepicker [(ngModel)]="milestoneDue" dateFormat="dd/mm/yy" [showClear]="true" /></label>

          <app-button (click)="addMs()">Ajouter</app-button>

        </div>

      }

      <p-table [value]="milestones" styleClass="p-datatable-sm">

        <ng-template pTemplate="header"><tr><th>Jalon</th><th>%</th><th>Montant</th><th>Échéance</th><th></th></tr></ng-template>

        <ng-template pTemplate="body" let-m>

          <tr>

            <td>{{ m.name }}</td>

            <td>{{ m.percent }}</td>

            <td>{{ m.amountHt | number:'1.3-3' }}</td>

            <td>{{ m.dueDate ? (m.dueDate | date:'shortDate') : '—' }}</td>

            <td>

              @if (!m.invoicedInvoiceId && canBill && project && canBeBilled(project.status)) {

                <app-button size="sm" (click)="invoiceMilestone.emit(m.id)">Facturer</app-button>

              } @else if (m.invoicedInvoiceId) {

                <a [routerLink]="['/invoices', m.invoicedInvoiceId]">Facture</a>

              }

            </td>

          </tr>

        </ng-template>

      </p-table>

    }



    @if (project && isBtp(project.kind)) {

      @if (latestSituationPercent != null) {

        <div class="card p-3 mb-3">

          <h3 class="mt-0">Avancement cumulé chantier</h3>

          <p-progressBar [value]="latestSituationPercent" [showValue]="true" />

        </div>

      }

      <h3>Situations de travaux</h3>

      @if (canBill) {

        <div class="proj-field-row mb-2">

          <label>Début <p-datepicker [(ngModel)]="sitStart" dateFormat="dd/mm/yy" /></label>

          <label>Fin <p-datepicker [(ngModel)]="sitEnd" dateFormat="dd/mm/yy" /></label>

          <label>% cumulé <p-inputNumber [(ngModel)]="sitPercent" /></label>

          <label>Brut HT <p-inputNumber [(ngModel)]="sitGross" /></label>

          <label>Retenue <p-inputNumber [(ngModel)]="sitRetainage" /></label>

          <label>TVA

            <p-select [options]="vatOptions" [(ngModel)]="sitVat" optionLabel="label" optionValue="value" />

          </label>

          <app-button (click)="addSit()">Nouvelle situation</app-button>

        </div>

      }

      <p-table [value]="situations" styleClass="p-datatable-sm">

        <ng-template pTemplate="header"><tr><th>N°</th><th>%</th><th>Net HT</th><th>Retenue</th><th>TTC</th><th>Statut</th><th></th></tr></ng-template>

        <ng-template pTemplate="body" let-s>

          <tr>

            <td>{{ s.number }}</td>

            <td>{{ s.cumulativePercent }}</td>

            <td>{{ s.netAmountHt | number:'1.3-3' }}</td>

            <td>{{ s.retainageAmountHt | number:'1.3-3' }}</td>

            <td>{{ s.totalTtc | number:'1.3-3' }}</td>

            <td><app-status-badge [status]="situationStatusBadge(s.status)" [label]="s.statusDisplay" /></td>

            <td>

              @if (parseProjectSituationStatus(s.status) === 'Draft' && canBill) {

                <app-button size="sm" variant="outline" (click)="openEditSituation(s)">Modifier</app-button>

                <app-button size="sm" (click)="validateSituation.emit(s.id)">Valider</app-button>

              }

              @if (parseProjectSituationStatus(s.status) === 'Validated' && !s.invoiceId && canBill && project && canBeBilled(project.status)) {

                <app-button size="sm" (click)="invoiceSituation.emit(s.id)">Facturer</app-button>

              }

              @if (s.invoiceId) {

                <a [routerLink]="['/invoices', s.invoiceId]">Facture</a>

              }

            </td>

          </tr>

        </ng-template>

      </p-table>

      <h3>Sous-traitants</h3>

      @if (canUpdate) {

        <div class="proj-field-row mb-2">

          <label>Fournisseur <p-select [options]="suppliers" [(ngModel)]="subSupplierId" optionLabel="name" optionValue="id" placeholder="Fournisseur" /></label>

          <label>Marché <input pInputText [(ngModel)]="subRef" /></label>

          <label>Montant HT <p-inputNumber [(ngModel)]="subAmount" /></label>

          <label>Retenue % <p-inputNumber [(ngModel)]="subRetainage" /></label>

          <app-button (click)="addSub()">Ajouter</app-button>

        </div>

      }

      <p-table [value]="subs" styleClass="p-datatable-sm">

        <ng-template pTemplate="header"><tr><th>Fournisseur</th><th>Marché</th><th>Montant</th><th>Retenue %</th>@if (canUpdate) { <th></th> }</tr></ng-template>

        <ng-template pTemplate="body" let-s>

          <tr>

            <td>{{ s.supplierName }}</td>

            <td>{{ s.contractReference }}</td>

            <td>{{ s.amountHt | number:'1.3-3' }}</td>

            <td>{{ s.retainagePercent }}</td>

            @if (canUpdate) {

              <td><app-button size="sm" variant="outline" (click)="openEditSub(s)">Modifier</app-button></td>

            }

          </tr>

        </ng-template>

      </p-table>

    }



    <p-dialog [(visible)]="sitEditVisible" header="Modifier la situation" [modal]="true" [style]="{ width: '32rem' }">

      <div class="proj-field-row flex-column">

        <label>Début <p-datepicker [(ngModel)]="editSitStart" dateFormat="dd/mm/yy" /></label>

        <label>Fin <p-datepicker [(ngModel)]="editSitEnd" dateFormat="dd/mm/yy" /></label>

        <label>% cumulé <p-inputNumber [(ngModel)]="editSitPercent" /></label>

        <label>Brut HT <p-inputNumber [(ngModel)]="editSitGross" /></label>

        <label>Retenue <p-inputNumber [(ngModel)]="editSitRetainage" /></label>

        <label>TVA <p-select [options]="vatOptions" [(ngModel)]="editSitVat" optionLabel="label" optionValue="value" /></label>

      </div>

      <ng-template pTemplate="footer">

        <app-button variant="secondary" (click)="sitEditVisible = false">Annuler</app-button>

        <app-button variant="primary" (click)="saveSituation()">Enregistrer</app-button>

      </ng-template>

    </p-dialog>



    <p-dialog [(visible)]="subEditVisible" header="Modifier le sous-traitant" [modal]="true" [style]="{ width: '28rem' }">

      <div class="proj-field-row flex-column">

        <label>Marché <input pInputText class="w-full" [(ngModel)]="editSubRef" /></label>

        <label>Montant HT <p-inputNumber [(ngModel)]="editSubAmount" /></label>

        <label>Retenue % <p-inputNumber [(ngModel)]="editSubRetainage" /></label>

      </div>

      <ng-template pTemplate="footer">

        <app-button variant="secondary" (click)="subEditVisible = false">Annuler</app-button>

        <app-button variant="primary" (click)="saveSub()">Enregistrer</app-button>

      </ng-template>

    </p-dialog>

  `,

  styles: [`

    .blockers { color: var(--red-500, #dc2626); padding-left: 1.25rem; }

  `],

})

export class ProjectBillingTabComponent implements OnChanges {



  @Input() project: ProjectDetail | null = null;

  @Input() readiness: ProjectBillingReadiness | null = null;

  @Input() milestones: ProjectMilestone[] = [];

  @Input() situations: ProjectSituation[] = [];

  @Input() subs: ProjectSubcontractor[] = [];

  @Input() suppliers: SupplierOption[] = [];

  @Input() canBill = false;

  @Input() canUpdate = false;

  @Output() activate = new EventEmitter<void>();

  @Output() invoiceTime = new EventEmitter<{ groupBy: string; notes?: string }>();

  @Output() invoiceFixedPrice = new EventEmitter<{ amountHt: number; notes?: string }>();

  @Output() addMilestone = new EventEmitter<{ name: string; percent: number; amountHt: number; dueDate?: string | null }>();

  @Output() invoiceMilestone = new EventEmitter<string>();

  @Output() addSituation = new EventEmitter<{

    periodStart: string;

    periodEnd: string;

    cumulativePercent: number;

    grossAmountHt: number;

    retainageAmountHt: number;

    vatRatePercent: number;

  }>();

  @Output() updateSituation = new EventEmitter<{

    id: string;

    payload: {

      periodStart: string;

      periodEnd: string;

      cumulativePercent: number;

      grossAmountHt: number;

      retainageAmountHt: number;

      vatRatePercent: number;

    };

  }>();

  @Output() validateSituation = new EventEmitter<string>();

  @Output() invoiceSituation = new EventEmitter<string>();

  @Output() addSubcontractor = new EventEmitter<{

    supplierId: string;

    contractReference?: string;

    amountHt: number;

    retainagePercent: number;

  }>();

  @Output() updateSubcontractor = new EventEmitter<{

    id: string;

    payload: { contractReference?: string; amountHt: number; retainagePercent: number };

  }>();



  milestoneName = '';

  milestoneAmount = 0;

  milestonePercent = 0;

  milestoneDue: Date | null = null;

  sitPercent = 0;

  sitGross = 0;

  sitRetainage = 0;

  sitVat = 19;

  sitStart = new Date();

  sitEnd = new Date();

  subSupplierId = '';

  subRef = '';

  subAmount = 0;

  subRetainage = 0;

  timeGroupBy = 'member';

  timeNotes = '';

  fixedPriceAmount = 0;

  fixedPriceNotes = '';

  fixedPriceInvoiced = false;



  sitEditVisible = false;

  private editSitId = '';

  editSitStart = new Date();

  editSitEnd = new Date();

  editSitPercent = 0;

  editSitGross = 0;

  editSitRetainage = 0;

  editSitVat = 19;



  subEditVisible = false;

  private editSubId = '';

  editSubRef = '';

  editSubAmount = 0;

  editSubRetainage = 0;



  readonly groupOptions = TIME_GROUP_OPTIONS;

  readonly vatOptions = TUNISIAN_VAT_OPTIONS;

  readonly canBeBilled = canBeBilled;

  readonly canActivate = canActivate;

  readonly isBtp = isBtp;

  readonly isFixedPriceBilling = isFixedPriceBilling;

  readonly showMilestones = showMilestones;

  readonly parseProjectSituationStatus = parseProjectSituationStatus;

  readonly situationStatusBadge = situationStatusBadge;



  ngOnChanges(changes: SimpleChanges): void {
    if (changes['project']?.currentValue && this.fixedPriceAmount === 0) {
      this.fixedPriceAmount = (changes['project'].currentValue as ProjectDetail).budgetHt;
    }
  }



  get canInvoiceTime(): boolean {

    if (this.readiness?.canInvoiceTime !== undefined) {

      return this.readiness.canInvoiceTime;

    }

    return !!this.readiness && this.readiness.canBill && this.readiness.validatedUninvoicedHours > 0

      && this.readiness.membersWithoutRate.length === 0;

  }



  get latestSituationPercent(): number | null {

    if (!this.situations.length) return null;

    const sorted = [...this.situations].sort((a, b) => b.number - a.number);

    return sorted[0]?.cumulativePercent ?? null;

  }



  emitInvoiceTime(): void {

    this.invoiceTime.emit({ groupBy: this.timeGroupBy, notes: this.timeNotes || undefined });

  }



  emitFixedPrice(): void {

    this.invoiceFixedPrice.emit({ amountHt: this.fixedPriceAmount, notes: this.fixedPriceNotes || undefined });

    this.fixedPriceInvoiced = true;

  }



  addMs(): void {

    if (!this.milestoneName.trim()) return;

    this.addMilestone.emit({

      name: this.milestoneName,

      percent: this.milestonePercent,

      amountHt: this.milestoneAmount,

      dueDate: toIsoDate(this.milestoneDue)?.slice(0, 10) ?? null

    });

    this.milestoneName = '';

    this.milestonePercent = 0;

    this.milestoneDue = null;

  }



  addSit(): void {

    const start = toIsoDate(this.sitStart);

    const end = toIsoDate(this.sitEnd);

    if (!start || !end) return;

    this.addSituation.emit({

      periodStart: start,

      periodEnd: end,

      cumulativePercent: this.sitPercent,

      grossAmountHt: this.sitGross,

      retainageAmountHt: this.sitRetainage,

      vatRatePercent: this.sitVat

    });

  }



  openEditSituation(s: ProjectSituation): void {

    this.editSitId = s.id;

    this.editSitStart = new Date(s.periodStart);

    this.editSitEnd = new Date(s.periodEnd);

    this.editSitPercent = s.cumulativePercent;

    this.editSitGross = s.grossAmountHt;

    this.editSitRetainage = s.retainageAmountHt;

    this.editSitVat = s.vatRatePercent;

    this.sitEditVisible = true;

  }



  saveSituation(): void {

    const start = toIsoDate(this.editSitStart);

    const end = toIsoDate(this.editSitEnd);

    if (!start || !end) return;

    this.updateSituation.emit({

      id: this.editSitId,

      payload: {

        periodStart: start,

        periodEnd: end,

        cumulativePercent: this.editSitPercent,

        grossAmountHt: this.editSitGross,

        retainageAmountHt: this.editSitRetainage,

        vatRatePercent: this.editSitVat

      }

    });

    this.sitEditVisible = false;

  }



  addSub(): void {

    if (!this.subSupplierId) return;

    this.addSubcontractor.emit({

      supplierId: this.subSupplierId,

      contractReference: this.subRef || undefined,

      amountHt: this.subAmount,

      retainagePercent: this.subRetainage

    });

  }



  openEditSub(s: ProjectSubcontractor): void {

    this.editSubId = s.id;

    this.editSubRef = s.contractReference ?? '';

    this.editSubAmount = s.amountHt;

    this.editSubRetainage = s.retainagePercent;

    this.subEditVisible = true;

  }



  saveSub(): void {

    this.updateSubcontractor.emit({

      id: this.editSubId,

      payload: {

        contractReference: this.editSubRef || undefined,

        amountHt: this.editSubAmount,

        retainagePercent: this.editSubRetainage

      }

    });

    this.subEditVisible = false;

  }

}

