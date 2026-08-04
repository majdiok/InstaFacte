import { Component, EventEmitter, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { DropdownModule } from 'primeng/dropdown';
import { FirmActivityCode } from '@core/services/firm-governance.service';
import { activityPastelBorder, activityPastelColor } from '../governance/time-sheets/time-sheet-activity-color';

export interface ActivityCodeGroup {
  label: string;
  items: FirmActivityCode[];
}

export interface ActivityCodeChangeEvent {
  code: string | null;
  label: string;
  defaultUnitPrice: number | null;
}

@Component({
  selector: 'app-activity-code-picker',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, DropdownModule],
  template: `
    @if (codes.length === 0) {
      <div class="empty-hint">
        <span>Aucun type d’activité configuré.</span>
        <a routerLink="/firm/settings/activity-codes">Configurer</a>
      </div>
    } @else {
      <p-dropdown
        [options]="groups"
        [ngModel]="value"
        (ngModelChange)="onSelect($event)"
        [group]="true"
        optionLabel="label"
        optionValue="code"
        optionGroupLabel="label"
        optionGroupChildren="items"
        [filter]="true"
        filterBy="label,code"
        [showClear]="true"
        [disabled]="disabled"
        placeholder="Sélectionner le service"
        appendTo="body"
        styleClass="w-full activity-picker">
        <ng-template let-group pTemplate="group">
          <div class="group-label">{{ group.label }}</div>
        </ng-template>
        <ng-template let-item pTemplate="item">
          <div class="item" [style.--pill]="pastel(item.code)" [style.--pill-border]="pastelBorder(item.code)">
            <span class="code">{{ item.code }}</span>
            <span class="label">{{ item.label }}</span>
            <span class="price">{{ formatPrice(item.defaultUnitPrice) }}</span>
          </div>
        </ng-template>
        <ng-template let-item pTemplate="selectedItem">
          @if (item) {
            <div class="selected" [attr.title]="item.label">
              <span class="code">{{ item.code }}</span>
              <span class="label">{{ item.label }}</span>
            </div>
          }
        </ng-template>
      </p-dropdown>
    }
  `,
  styles: [`
    :host { display: block; width: 100%; min-width: 0; }
    .w-full { width: 100%; }
    .empty-hint {
      display: flex; flex-wrap: wrap; gap: 0.35rem; align-items: center;
      font-size: 0.85rem; color: #64748b;
      padding: 0.5rem; border: 1px dashed #93c5fd; border-radius: 8px; background: #f8fafc;
    }
    .empty-hint a { color: #2563eb; text-decoration: none; font-weight: 600; }
    .group-label { font-weight: 700; font-size: 0.75rem; text-transform: uppercase; letter-spacing: 0.04em; color: #475569; }
    .item {
      display: flex; align-items: center; gap: 0.5rem; flex-wrap: wrap;
    }
    .selected {
      display: flex; align-items: center; gap: 0.5rem; flex-wrap: nowrap;
      overflow: hidden; width: 100%; min-width: 0;
    }
    .code {
      font-weight: 700; font-size: 0.75rem; padding: 0.1rem 0.4rem; border-radius: 999px;
      background: var(--pill, #e2e8f0); border: 1px solid var(--pill-border, #94a3b8); color: #0f172a;
      flex-shrink: 0;
    }
    .label { font-size: 0.9rem; color: #1e293b; }
    .selected .label {
      overflow: hidden; text-overflow: ellipsis; white-space: nowrap; min-width: 0;
    }
    .price { font-size: 0.75rem; color: #475569; margin-left: auto; font-variant-numeric: tabular-nums; white-space: nowrap; }
    .price.muted { color: #94a3b8; }
  `]
})
export class ActivityCodePickerComponent {
  @Input() codes: FirmActivityCode[] = [];
  @Input() value: string | null = null;
  @Input() disabled = false;
  @Output() codeChange = new EventEmitter<ActivityCodeChangeEvent>();

  get groups(): ActivityCodeGroup[] {
    return this.buildGroups(this.codes);
  }

  onSelect(code: string | null): void {
    const match = code ? this.codes.find(c => c.code === code) : null;
    this.codeChange.emit({
      code: match?.code ?? null,
      label: match?.label ?? '',
      defaultUnitPrice: match?.defaultUnitPrice ?? null
    });
  }

  pastel(code: string): string {
    return activityPastelColor(code);
  }

  pastelBorder(code: string): string {
    return activityPastelBorder(code);
  }

  formatPrice(value: number | null | undefined): string {
    if (value == null || value <= 0) return '—';
    return `${value.toLocaleString('fr-TN', { minimumFractionDigits: 3, maximumFractionDigits: 3 })} TND`;
  }

  private buildGroups(codes: FirmActivityCode[]): ActivityCodeGroup[] {
    const map = new Map<string, FirmActivityCode[]>();
    for (const c of codes) {
      const key = c.categoryDisplay || 'Autres';
      if (!map.has(key)) map.set(key, []);
      map.get(key)!.push(c);
    }
    return Array.from(map.entries()).map(([label, items]) => ({ label, items }));
  }
}
