import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, Output } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TableModule } from 'primeng/table';
import { FiscalScheduleEntryDto, FiscalScheduleListDto } from '../services/fiscal-schedule.service';
import { formatFiscalAmount, formatFiscalDate, statusClass, statusIcon } from './fiscal-schedule.view-model';
import { FISCAL_SCHEDULE_SHARED_STYLES } from './fiscal-schedule-shared.styles';

@Component({
  selector: 'app-fiscal-schedule-table',
  standalone: true,
  imports: [CommonModule, FormsModule, TableModule],
  template: `
    <div class="table-panel">
      <div class="loading-row" *ngIf="loading">Chargement de l'echeancier fiscal...</div>
      <p-table
        *ngIf="!loading"
        [value]="list?.items ?? []"
        styleClass="p-datatable-sm accounting-datatable fiscal-schedule-table"
        [rowHover]="true"
        selectionMode="single"
        [(selection)]="selection"
        (onRowSelect)="onSelect($event.data)"
        (onRowUnselect)="onSelect(null)"
        dataKey="id">
        <ng-template pTemplate="header">
          <tr>
            <th class="marker-col"></th>
            <th>Date d'echeance</th>
            <th>Type d'obligation</th>
            <th>Periode / Exercice</th>
            <th>Societe</th>
            <th class="amount-col">Montant estime (TND)</th>
            <th>Statut</th>
            <th>Date depot</th>
            <th>Date paiement</th>
            <th>Responsable</th>
            <th>Observations</th>
          </tr>
        </ng-template>
        <ng-template pTemplate="body" let-row>
          <tr
            [pSelectableRow]="row"
            [class.selected]="selectedId === row.id"
            (dblclick)="rowDblClick.emit(row)">
            <td class="marker-col"><i [class]="icon(row.status)" [ngClass]="statusClassName(row.status)"></i></td>
            <td>{{ date(row.dueDate) }}</td>
            <td>{{ row.obligationTypeDisplay }}</td>
            <td>{{ row.periodDisplay }}</td>
            <td>{{ row.companyName || companyLabel }}</td>
            <td class="amount-col">{{ amount(row.estimatedAmount, row.currency) }}</td>
            <td><span class="status-pill" [ngClass]="statusClassName(row.status)">{{ row.statusDisplay }}</span></td>
            <td>{{ date(row.depositDate) }}</td>
            <td>{{ date(row.paymentDate) }}</td>
            <td>{{ row.responsibleName || '-' }}</td>
            <td>{{ row.observations || '-' }}</td>
          </tr>
        </ng-template>
        <ng-template pTemplate="emptymessage">
          <tr>
            <td colspan="11" class="empty-cell">
              <p>Aucune echeance fiscale ne correspond aux filtres.</p>
              <p class="empty-hint" *ngIf="showEmptyActions">Generez l'echeancier standard de l'exercice ou creez une echeance manuellement.</p>
              <div class="empty-actions" *ngIf="showEmptyActions">
                <button class="icon-button primary" type="button" (click)="generate.emit()" [disabled]="generating">
                  <i class="fa-solid fa-wand-magic-sparkles"></i><span>Generer l'echeancier</span>
                </button>
                <button class="icon-button" type="button" (click)="create.emit()">
                  <i class="fa-solid fa-plus"></i><span>Creer manuellement</span>
                </button>
              </div>
            </td>
          </tr>
        </ng-template>
        <ng-template pTemplate="footer" *ngIf="list">
          <tr>
            <td colspan="5">Nombre d'echeances : {{ list.totalCount }}</td>
            <td class="amount-col">{{ amount(list.summary.totalAmount) }}</td>
            <td colspan="5"></td>
          </tr>
        </ng-template>
      </p-table>

      <div class="pagination-row" *ngIf="list && list.totalCount > list.pageSize">
        <button class="square-button" type="button" title="Premiere page" (click)="pageChange.emit(1)" [disabled]="page === 1">
          <i class="fa-solid fa-angles-left"></i>
        </button>
        <button class="square-button" type="button" title="Page precedente" (click)="pageChange.emit(page - 1)" [disabled]="page === 1">
          <i class="fa-solid fa-angle-left"></i>
        </button>
        <button
          class="page-num"
          type="button"
          *ngFor="let p of pageNumbers"
          [class.active]="p === page"
          (click)="pageChange.emit(p)">{{ p }}</button>
        <button class="square-button" type="button" title="Page suivante" (click)="pageChange.emit(page + 1)" [disabled]="page === totalPages">
          <i class="fa-solid fa-angle-right"></i>
        </button>
        <button class="square-button" type="button" title="Derniere page" (click)="pageChange.emit(totalPages)" [disabled]="page === totalPages">
          <i class="fa-solid fa-angles-right"></i>
        </button>
        <label class="page-size-label">
          Afficher
          <select [ngModel]="pageSize" (ngModelChange)="pageSizeChange.emit($event)">
            <option [ngValue]="25">25</option>
            <option [ngValue]="50">50</option>
            <option [ngValue]="100">100</option>
          </select>
          elements
        </label>
      </div>
    </div>
  `,
  styles: [FISCAL_SCHEDULE_SHARED_STYLES, `
    .table-panel {
      border: 1px solid #d8dee8;
      border-radius: 6px;
      background: #fff;
      overflow-x: auto;
    }
    .loading-row, .empty-cell {
      padding: 22px;
      text-align: center;
      color: #64748b;
      font-weight: 600;
    }
    .empty-hint { margin: 8px 0 14px; font-size: 12px; font-weight: 500; }
    .empty-actions { display: flex; justify-content: center; gap: 8px; flex-wrap: wrap; }
    .marker-col { width: 34px; text-align: center; }
    :host ::ng-deep .fiscal-schedule-table .p-datatable-tbody > tr { cursor: pointer; font-size: 12px; }
    :host ::ng-deep .fiscal-schedule-table .p-datatable-tbody > tr.selected,
    :host ::ng-deep .fiscal-schedule-table .p-datatable-tbody > tr.p-highlight {
      background: #f2f7fb !important;
    }
    .pagination-row {
      display: flex;
      align-items: center;
      justify-content: flex-end;
      gap: 8px;
      flex-wrap: wrap;
      padding: 10px 12px;
      font-size: 12px;
      color: #475569;
    }
    .page-num {
      min-width: 30px;
      height: 30px;
      border: 1px solid #d8dee8;
      border-radius: 5px;
      background: #fff;
      cursor: pointer;
      font-weight: 700;
    }
    .page-num.active { background: #0f8f4d; color: #fff; border-color: #0f8f4d; }
    .page-size-label {
      display: inline-flex;
      align-items: center;
      gap: 6px;
      font-weight: 600;
    }
    .page-size-label select { width: auto; min-width: 64px; }
  `]
})
export class FiscalScheduleTableComponent {
  @Input() list: FiscalScheduleListDto | null = null;
  @Input() loading = false;
  @Input() selected: FiscalScheduleEntryDto | null = null;
  @Input() companyLabel = '-';
  @Input() showEmptyActions = false;
  @Input() generating = false;
  @Input() page = 1;
  @Input() pageSize = 25;

  @Output() selectRow = new EventEmitter<FiscalScheduleEntryDto | null>();
  @Output() rowDblClick = new EventEmitter<FiscalScheduleEntryDto>();
  @Output() pageChange = new EventEmitter<number>();
  @Output() pageSizeChange = new EventEmitter<number>();
  @Output() generate = new EventEmitter<void>();
  @Output() create = new EventEmitter<void>();

  selection: FiscalScheduleEntryDto | null = null;

  get selectedId(): string | null {
    return this.selected?.id ?? null;
  }

  get totalPages(): number {
    if (!this.list) return 1;
    return Math.max(1, Math.ceil(this.list.totalCount / this.list.pageSize));
  }

  get pageNumbers(): number[] {
    const total = this.totalPages;
    const current = this.page;
    const start = Math.max(1, current - 2);
    const end = Math.min(total, start + 4);
    const adjustedStart = Math.max(1, end - 4);
    const pages: number[] = [];
    for (let i = adjustedStart; i <= end; i++) pages.push(i);
    return pages;
  }

  onSelect(row: FiscalScheduleEntryDto | null): void {
    this.selection = row;
    this.selectRow.emit(row);
  }

  amount(value: number, currency = 'TND'): string {
    return formatFiscalAmount(value, currency);
  }

  date(value?: string | null): string {
    return formatFiscalDate(value);
  }

  icon(status: number): string {
    return statusIcon(status);
  }

  statusClassName(status: number): string {
    return statusClass(status);
  }
}
