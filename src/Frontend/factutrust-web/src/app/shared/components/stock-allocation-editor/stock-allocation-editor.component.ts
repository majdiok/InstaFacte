import {
  Component,
  Input,
  OnChanges,
  SimpleChanges,
  inject,
  signal,
  DestroyRef
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { InputTextModule } from 'primeng/inputtext';
import { InputNumberModule } from 'primeng/inputnumber';
import { SelectModule } from 'primeng/select';
import { DatePickerModule } from 'primeng/datepicker';
import { ButtonComponent } from '@shared/components/button/button.component';
import {
  StockService,
  StockFeatures,
  StockLotBalance,
  ProductSerial
} from '@core/services/stock.service';
import {
  AllocationRow,
  needsManualLotPicker,
  showLotSection,
  showSerialSection,
  allocationSum,
  createDefaultLotRow,
  createDefaultSerialRow,
  TRACKING_MODE_LOT,
  TRACKING_MODE_SERIAL
} from '@shared/utils/stock-traceability.utils';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';

@Component({
  selector: 'app-stock-allocation-editor',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    InputTextModule,
    InputNumberModule,
    SelectModule,
    DatePickerModule,
    ButtonComponent
  ],
  template: `
    @if (visible()) {
      <div class="alloc-editor">
        <span class="alloc-label">{{ labelText() }}</span>

        @if (lotMode()) {
          @for (alloc of allocations; track $index; let ai = $index) {
            <div class="alloc-row">
              @if (mode === 'exit' && availableLots().length > 0) {
                <p-select
                  [options]="lotOptions()"
                  [(ngModel)]="alloc.productLotId"
                  [ngModelOptions]="{ standalone: true }"
                  optionLabel="label"
                  optionValue="value"
                  placeholder="Lot"
                  appendTo="body"
                  (onChange)="onLotPicked(alloc, $event.value)">
                </p-select>
              } @else {
                <input pInputText placeholder="N° lot" [(ngModel)]="alloc.lotNumber" [ngModelOptions]="{ standalone: true }" />
                <p-datepicker
                  [(ngModel)]="alloc.expiryDate"
                  [ngModelOptions]="{ standalone: true }"
                  dateFormat="dd/mm/yy"
                  placeholder="DLUO"
                  [showIcon]="true"
                  appendTo="body">
                </p-datepicker>
              }
              <p-inputNumber
                [(ngModel)]="alloc.quantity"
                [ngModelOptions]="{ standalone: true }"
                [min]="0"
                mode="decimal"
                [minFractionDigits]="0"
                [maxFractionDigits]="3">
              </p-inputNumber>
              <button type="button" class="btn-icon" (click)="removeRow(ai)" aria-label="Retirer">
                <i class="pi pi-times"></i>
              </button>
            </div>
          }
          <app-button type="button" variant="ghost" size="sm" icon="pi-plus" iconPos="left" (clicked)="addLotRow()">
            Ajouter un lot
          </app-button>
        }

        @if (serialMode()) {
          @for (alloc of allocations; track $index; let ai = $index) {
            <div class="alloc-row">
              @if (mode === 'exit' && availableSerials().length > 0) {
                <p-select
                  [options]="serialOptions()"
                  [(ngModel)]="alloc.serialId"
                  [ngModelOptions]="{ standalone: true }"
                  optionLabel="label"
                  optionValue="value"
                  placeholder="N° série"
                  appendTo="body"
                  (onChange)="onSerialPicked(alloc, $event.value)">
                </p-select>
              } @else {
                <input pInputText placeholder="N° série" [(ngModel)]="alloc.serialNumber" [ngModelOptions]="{ standalone: true }" />
              }
              <button type="button" class="btn-icon" (click)="removeRow(ai)" aria-label="Retirer">
                <i class="pi pi-times"></i>
              </button>
            </div>
          }
          @if (mode === 'entry') {
            <app-button type="button" variant="ghost" size="sm" icon="pi-plus" iconPos="left" (clicked)="addSerialRow()">
              Ajouter un n° de série
            </app-button>
          }
        }

        @if (showSumWarning()) {
          <span class="alloc-warning">
            Somme {{ allocationSum(allocations) | number:'1.0-3' }} ≠ quantité ligne {{ lineQuantity | number:'1.0-3' }}
          </span>
        }
      </div>
    }
  `,
  styles: [`
    .alloc-editor {
      display: flex;
      flex-wrap: wrap;
      gap: 0.5rem;
      align-items: center;
      padding: 0.5rem 0;
    }
    .alloc-label {
      font-size: 0.75rem;
      font-weight: 600;
      color: #64748b;
      margin-right: 0.5rem;
    }
    .alloc-row {
      display: flex;
      flex-wrap: wrap;
      gap: 0.5rem;
      align-items: center;
    }
    .btn-icon {
      background: none;
      border: none;
      color: #64748b;
      cursor: pointer;
    }
    .alloc-warning {
      color: #b45309;
      font-size: 0.75rem;
    }
    :host ::ng-deep .p-datepicker { display: flex; }
  `]
})
export class StockAllocationEditorComponent implements OnChanges {
  @Input({ required: true }) mode!: 'entry' | 'exit';
  @Input({ required: true }) productId!: string;
  @Input({ required: true }) warehouseId!: string;
  @Input({ required: true }) lineQuantity!: number;
  @Input() trackingMode = 0;
  @Input() pickingPolicy = 0;
  @Input() features: StockFeatures | null = null;
  @Input() allocations: AllocationRow[] = [];
  @Input() optionalOverride = false;

  private stockService = inject(StockService);
  private destroyRef = inject(DestroyRef);

  visible = signal(false);
  lotMode = signal(false);
  serialMode = signal(false);
  availableLots = signal<StockLotBalance[]>([]);
  availableSerials = signal<ProductSerial[]>([]);

  lotOptions = signal<{ label: string; value: string }[]>([]);
  serialOptions = signal<{ label: string; value: string }[]>([]);

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['trackingMode'] || changes['features'] || changes['productId'] || changes['warehouseId']) {
      this.refreshVisibility();
      this.loadAvailability();
    }
  }

  labelText(): string {
    if (this.serialMode()) return this.mode === 'entry' ? 'N° de série' : 'N° de série';
    if (this.mode === 'entry') return 'Lots';
    if (this.optionalOverride || !needsManualLotPicker(this.pickingPolicy)) {
      return 'Lots (optionnel — sinon FEFO)';
    }
    return 'Lots (obligatoire)';
  }

  showSumWarning(): boolean {
    if (!this.visible()) return false;
    if (this.serialMode()) {
      return this.allocations.length !== Math.round(this.lineQuantity);
    }
    return Math.abs(allocationSum(this.allocations) - this.lineQuantity) > 0.0001
      && allocationSum(this.allocations) > 0;
  }

  allocationSum(rows: AllocationRow[]): number {
    return allocationSum(rows);
  }

  addLotRow(): void {
    this.allocations.push(createDefaultLotRow(0));
  }

  addSerialRow(): void {
    this.allocations.push(createDefaultSerialRow());
  }

  removeRow(index: number): void {
    if (this.allocations.length <= 1) return;
    this.allocations.splice(index, 1);
  }

  onLotPicked(alloc: AllocationRow, lotId: string | null): void {
    if (!lotId) return;
    const lot = this.availableLots().find(l => l.productLotId === lotId);
    if (lot) {
      alloc.productLotId = lot.productLotId;
      alloc.lotNumber = lot.lotNumber;
      if (lot.expiryDate) alloc.expiryDate = new Date(lot.expiryDate);
    }
  }

  onSerialPicked(alloc: AllocationRow, serialId: string | null): void {
    if (!serialId) return;
    const serial = this.availableSerials().find(s => s.id === serialId);
    if (serial) {
      alloc.serialId = serial.id;
      alloc.serialNumber = serial.serialNumber;
      alloc.quantity = 1;
    }
  }

  private refreshVisibility(): void {
    const lot = showLotSection(this.trackingMode, this.features);
    const serial = showSerialSection(this.trackingMode, this.features);
    this.lotMode.set(lot);
    this.serialMode.set(serial);
    this.visible.set(lot || serial);
  }

  private loadAvailability(): void {
    if (!this.productId || !this.warehouseId || !this.visible()) return;

    if (this.trackingMode === TRACKING_MODE_LOT && this.mode === 'exit') {
      this.stockService.getLotsByProduct(this.productId, this.warehouseId)
        .pipe(takeUntilDestroyed(this.destroyRef))
        .subscribe(res => {
          if (res.success && res.data) {
            this.availableLots.set(res.data);
            this.lotOptions.set(
              res.data
                .filter(l => l.quantityAvailable > 0)
                .map(l => ({
                  value: l.productLotId,
                  label: `${l.lotNumber} (${l.quantityAvailable})${l.expiryDate ? ' · ' + l.expiryDate.slice(0, 10) : ''}`
                }))
            );
          }
        });
    }

    if (this.trackingMode === TRACKING_MODE_SERIAL && this.mode === 'exit') {
      this.stockService.getSerialsByProduct(this.productId, this.warehouseId)
        .pipe(takeUntilDestroyed(this.destroyRef))
        .subscribe(res => {
          if (res.success && res.data) {
            this.availableSerials.set(res.data);
            this.serialOptions.set(
              res.data.map(s => ({
                value: s.id,
                label: s.lotNumber ? `${s.serialNumber} (${s.lotNumber})` : s.serialNumber
              }))
            );
          }
        });
    }
  }
}
