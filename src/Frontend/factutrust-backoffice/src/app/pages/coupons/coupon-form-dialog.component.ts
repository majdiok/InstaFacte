import {
  ChangeDetectionStrategy,
  Component,
  EventEmitter,
  Input,
  OnChanges,
  Output,
  SimpleChanges,
  computed,
  inject,
  signal
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { DialogModule } from 'primeng/dialog';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { InputNumberModule } from 'primeng/inputnumber';
import { CalendarModule } from 'primeng/calendar';
import { DropdownModule } from 'primeng/dropdown';
import { PlatformPlansService } from '@core/services/platform-plans.service';
import {
  CouponType,
  type CouponDto,
  type CouponTypeValue,
  type CreateCouponRequest,
  type PlanDto,
  type UpdateCouponRequest
} from '@core/models/platform.models';
import { COUPONS_FR } from './coupons.i18n.fr';

@Component({
  selector: 'app-coupon-form-dialog',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    FormsModule,
    DialogModule,
    ButtonModule,
    InputTextModule,
    InputNumberModule,
    CalendarModule,
    DropdownModule
  ],
  template: `
    <p-dialog
      [visible]="visible"
      (visibleChange)="onVisibleChange($event)"
      [modal]="true"
      [closable]="!busy"
      [draggable]="false"
      [resizable]="false"
      [style]="{ width: '36rem', maxWidth: '95vw' }"
      [header]="isEdit() ? t('form.title.edit') : t('form.title.create')">

      <div class="grid">
        @if (!isEdit()) {
          <div class="field field--full">
            <label for="cp-code">{{ t('form.field.code') }}</label>
            <input
              id="cp-code"
              type="text"
              pInputText
              [(ngModel)]="code"
              (ngModelChange)="codeSig.set($event)"
              [disabled]="busy"
              autocomplete="off"
              maxlength="40"
              class="w-full upper" />
            <small class="hint">{{ t('form.field.code.hint') }}</small>
          </div>
        }

        <div class="field">
          <label for="cp-type">{{ t('form.field.type') }}</label>
          <p-dropdown
            inputId="cp-type"
            [options]="typeOptions"
            [(ngModel)]="type"
            (ngModelChange)="typeSig.set($event)"
            optionLabel="label"
            optionValue="value"
            [disabled]="busy"
            styleClass="w-full" />
        </div>

        <div class="field">
          <label for="cp-value">
            {{ type === 0 ? t('form.field.value.percent') : t('form.field.value.fixed') }}
          </label>
          <p-inputNumber
            inputId="cp-value"
            [(ngModel)]="value"
            (ngModelChange)="valueSig.set($event)"
            [min]="0.001"
            [max]="type === 0 ? 100 : 1000000"
            [maxFractionDigits]="3"
            [disabled]="busy"
            styleClass="w-full" />
        </div>

        <div class="field">
          <label for="cp-from">{{ t('form.field.validFrom') }}</label>
          <p-calendar
            inputId="cp-from"
            [(ngModel)]="validFrom"
            (ngModelChange)="validFromSig.set($event)"
            dateFormat="dd/mm/yy"
            [showIcon]="true"
            [disabled]="busy"
            styleClass="w-full" />
        </div>

        <div class="field">
          <label for="cp-to">{{ t('form.field.validTo') }}</label>
          <p-calendar
            inputId="cp-to"
            [(ngModel)]="validTo"
            (ngModelChange)="validToSig.set($event)"
            dateFormat="dd/mm/yy"
            [showIcon]="true"
            [disabled]="busy"
            styleClass="w-full" />
        </div>

        <div class="field">
          <label for="cp-max">{{ t('form.field.maxRedemptions') }}</label>
          <p-inputNumber
            inputId="cp-max"
            [(ngModel)]="maxRedemptions"
            [min]="1"
            [showClear]="true"
            [disabled]="busy"
            styleClass="w-full" />
        </div>

        <div class="field">
          <label for="cp-duration">{{ t('form.field.durationMonths') }}</label>
          <p-inputNumber
            inputId="cp-duration"
            [(ngModel)]="durationMonths"
            [min]="1"
            [max]="60"
            [showClear]="true"
            [disabled]="busy"
            styleClass="w-full" />
        </div>

        <div class="field field--full">
          <label for="cp-plan">{{ t('form.field.appliesToPlan') }}</label>
          <p-dropdown
            inputId="cp-plan"
            [options]="planOptions()"
            [(ngModel)]="appliesToPlanId"
            optionLabel="label"
            optionValue="value"
            [showClear]="true"
            [disabled]="busy"
            placeholder="Aucun (tous les plans)"
            styleClass="w-full" />
        </div>

        <div class="field field--full">
          <label for="cp-notes">{{ t('form.field.notes') }}</label>
          <input
            id="cp-notes"
            type="text"
            pInputText
            [(ngModel)]="notes"
            [disabled]="busy"
            maxlength="500"
            class="w-full" />
        </div>
      </div>

      <ng-template pTemplate="footer">
        <p-button label="Annuler" [text]="true" severity="secondary" [disabled]="busy" (onClick)="onCancel()" />
        <p-button
          [label]="isEdit() ? t('form.confirm.update') : t('form.confirm.create')"
          icon="pi pi-check"
          severity="primary"
          [disabled]="busy || !canConfirm()"
          [loading]="busy"
          (onClick)="onConfirm()" />
      </ng-template>
    </p-dialog>
  `,
  styles: [
    `
      .grid {
        display: grid;
        grid-template-columns: 1fr 1fr;
        gap: var(--gap-md);
      }

      .field {
        display: flex;
        flex-direction: column;
        gap: 0.4rem;
      }

      .field--full {
        grid-column: 1 / -1;
      }

      .field label {
        font-size: 0.78rem;
        text-transform: uppercase;
        letter-spacing: 0.05em;
        color: var(--ft-text-muted);
        font-weight: 600;
      }

      :host ::ng-deep .w-full {
        width: 100%;
      }
      :host ::ng-deep .w-full .p-inputtext {
        width: 100%;
      }
      :host ::ng-deep .w-full input {
        width: 100%;
      }

      .upper {
        text-transform: uppercase;
      }

      .hint {
        color: var(--ft-text-subtle);
        font-size: 0.72rem;
      }
    `
  ]
})
export class CouponFormDialogComponent implements OnChanges {
  private readonly plansApi = inject(PlatformPlansService);

  @Input() visible = false;
  @Input() editing: CouponDto | null = null;
  @Input() busy = false;

  @Output() visibleChange = new EventEmitter<boolean>();
  @Output() confirmed = new EventEmitter<{
    isEdit: boolean;
    id?: string;
    create?: CreateCouponRequest;
    update?: UpdateCouponRequest;
  }>();
  @Output() cancelled = new EventEmitter<void>();

  protected code = '';
  protected type: CouponTypeValue = CouponType.Percent;
  protected value = 10;
  protected validFrom: Date = new Date();
  protected validTo: Date = new Date(Date.now() + 30 * 86400_000);
  protected maxRedemptions: number | null = null;
  protected durationMonths: number | null = null;
  protected appliesToPlanId: string | null = null;
  protected notes = '';

  protected codeSig = signal('');
  protected typeSig = signal<CouponTypeValue>(CouponType.Percent);
  protected valueSig = signal(10);
  protected validFromSig = signal<Date>(new Date());
  protected validToSig = signal<Date>(new Date(Date.now() + 30 * 86400_000));

  readonly plans = signal<PlanDto[]>([]);
  readonly planOptions = computed(() =>
    this.plans().map((p) => ({ label: `${p.code} — ${p.name}`, value: p.id }))
  );

  protected readonly typeOptions = [
    { label: COUPONS_FR['form.field.value.percent'], value: CouponType.Percent },
    { label: COUPONS_FR['form.field.value.fixed'], value: CouponType.FixedAmount }
  ];

  protected readonly canConfirm = computed(() => {
    const codeOk = this.isEdit() || this.codeSig().trim().length >= 3;
    return (
      codeOk
      && this.valueSig() > 0
      && this.validFromSig() < this.validToSig()
    );
  });

  protected isEdit = signal(false);

  protected t(key: keyof typeof COUPONS_FR): string {
    return COUPONS_FR[key];
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['visible'] && this.visible) {
      this.loadPlans();
      if (this.editing) {
        this.isEdit.set(true);
        this.applyEditing(this.editing);
      } else {
        this.isEdit.set(false);
        this.resetState();
      }
    }
  }

  private loadPlans(): void {
    if (this.plans().length > 0) return;
    this.plansApi.list(false).subscribe({
      next: (res) => {
        if (res.success && res.data) this.plans.set(res.data);
      }
    });
  }

  private resetState(): void {
    this.code = '';
    this.type = CouponType.Percent;
    this.value = 10;
    this.validFrom = new Date();
    this.validTo = new Date(Date.now() + 30 * 86400_000);
    this.maxRedemptions = null;
    this.durationMonths = null;
    this.appliesToPlanId = null;
    this.notes = '';
    this.codeSig.set('');
    this.typeSig.set(CouponType.Percent);
    this.valueSig.set(10);
    this.validFromSig.set(this.validFrom);
    this.validToSig.set(this.validTo);
  }

  private applyEditing(c: CouponDto): void {
    this.code = c.code;
    this.type = c.type;
    this.value = c.value;
    this.validFrom = new Date(c.validFrom);
    this.validTo = new Date(c.validTo);
    this.maxRedemptions = c.maxRedemptions;
    this.durationMonths = c.durationMonths;
    this.appliesToPlanId = c.appliesToPlanId;
    this.notes = c.notes ?? '';
    this.codeSig.set(c.code);
    this.typeSig.set(c.type);
    this.valueSig.set(c.value);
    this.validFromSig.set(this.validFrom);
    this.validToSig.set(this.validTo);
  }

  onVisibleChange(value: boolean): void {
    this.visible = value;
    this.visibleChange.emit(value);
  }

  onCancel(): void {
    this.cancelled.emit();
    this.onVisibleChange(false);
  }

  onConfirm(): void {
    if (!this.canConfirm() || this.busy) return;

    const validFromIso = this.validFrom.toISOString();
    const validToIso = this.validTo.toISOString();
    const notes = this.notes.trim() || undefined;

    if (this.isEdit() && this.editing) {
      const request: UpdateCouponRequest = {
        type: this.type,
        value: this.value,
        durationMonths: this.durationMonths ?? undefined,
        maxRedemptions: this.maxRedemptions ?? undefined,
        validFrom: validFromIso,
        validTo: validToIso,
        appliesToPlanId: this.appliesToPlanId ?? undefined,
        notes
      };
      this.confirmed.emit({ isEdit: true, id: this.editing.id, update: request });
    } else {
      const request: CreateCouponRequest = {
        code: this.code.trim().toUpperCase(),
        type: this.type,
        value: this.value,
        durationMonths: this.durationMonths ?? undefined,
        maxRedemptions: this.maxRedemptions ?? undefined,
        validFrom: validFromIso,
        validTo: validToIso,
        appliesToPlanId: this.appliesToPlanId ?? undefined,
        notes
      };
      this.confirmed.emit({ isEdit: false, create: request });
    }
  }
}
