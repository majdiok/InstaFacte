import {
  ChangeDetectionStrategy,
  Component,
  EventEmitter,
  Input,
  OnChanges,
  Output,
  SimpleChanges,
  computed,
  signal
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { DialogModule } from 'primeng/dialog';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { InputTextarea } from 'primeng/inputtextarea';
import { InputNumberModule } from 'primeng/inputnumber';
import { DropdownModule } from 'primeng/dropdown';
import { CheckboxModule } from 'primeng/checkbox';
import { TableModule } from 'primeng/table';
import { TooltipModule } from 'primeng/tooltip';

import {
  DunningStepActionValue,
  type CreateDunningCampaignRequest,
  type DunningCampaignDto,
  type DunningStepInput,
  type UpdateDunningCampaignRequest
} from '@core/models/platform.models';

import { DUNNING_FR } from './dunning.i18n.fr';

interface StepRow {
  daysAfterDueDate: number;
  action: number;
  emailTemplateCode: string;
  label: string;
}

/**
 * Lot C6 — Dialog création/édition d'une campagne dunning.
 *
 * Permet à l'admin BillingAdmin de définir une suite ordonnée d'étapes
 * (rappel e-mail / marquer impayé / suspendre). Une seule campagne peut
 * être active à la fois — l'activation se fait depuis la liste (table parent).
 */
@Component({
  selector: 'app-dunning-campaign-form-dialog',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    FormsModule,
    DialogModule,
    ButtonModule,
    InputTextModule,
    InputTextarea,
    InputNumberModule,
    DropdownModule,
    CheckboxModule,
    TableModule,
    TooltipModule
  ],
  template: `
    <p-dialog
      [visible]="visible"
      (visibleChange)="onVisibleChange($event)"
      [modal]="true"
      [closable]="!busy"
      [draggable]="false"
      [resizable]="false"
      [style]="{ width: '46rem', maxWidth: '95vw' }"
      [header]="isEdit() ? t('form.title.edit') : t('form.title.create')">

      <div class="grid">
        <div class="field field--full">
          <label for="dcf-name">{{ t('form.field.name') }}</label>
          <input
            id="dcf-name"
            type="text"
            pInputText
            [(ngModel)]="name"
            [disabled]="busy"
            maxlength="120"
            class="w-full" />
        </div>
        <div class="field field--full">
          <label for="dcf-desc">{{ t('form.field.description') }}</label>
          <textarea
            id="dcf-desc"
            pInputTextarea
            rows="2"
            [(ngModel)]="description"
            [disabled]="busy"
            maxlength="500"
            class="w-full"></textarea>
        </div>
        @if (!isEdit()) {
          <div class="field field--full">
            <label class="checkbox">
              <p-checkbox [(ngModel)]="activateImmediately" [binary]="true" [disabled]="busy" />
              <span>{{ t('form.field.activate') }}</span>
            </label>
          </div>
        }
      </div>

      <h4 class="section-title">{{ t('form.section.steps') }}</h4>
      <div class="row-actions">
        <p-button
          [label]="t('form.steps.add')"
          icon="pi pi-plus"
          size="small"
          [outlined]="true"
          [disabled]="busy"
          (onClick)="addStep()" />
      </div>

      <p-table [value]="steps()" styleClass="ft-table">
        <ng-template pTemplate="header">
          <tr>
            <th class="col-day">{{ t('form.steps.day') }}</th>
            <th class="col-action">{{ t('form.steps.action') }}</th>
            <th>{{ t('form.steps.template') }}</th>
            <th>{{ t('form.steps.label') }}</th>
            <th></th>
          </tr>
        </ng-template>
        <ng-template pTemplate="body" let-row let-i="rowIndex">
          <tr>
            <td>
              <p-inputNumber
                [(ngModel)]="row.daysAfterDueDate"
                [min]="0"
                [max]="365"
                [disabled]="busy"
                [showButtons]="true"
                buttonLayout="stacked"
                styleClass="day-input" />
            </td>
            <td>
              <p-dropdown
                [options]="actionOptions"
                [(ngModel)]="row.action"
                optionLabel="label"
                optionValue="value"
                [disabled]="busy"
                styleClass="w-full" />
            </td>
            <td>
              <input
                type="text"
                pInputText
                [(ngModel)]="row.emailTemplateCode"
                [disabled]="busy || row.action !== 0"
                placeholder="dunning-step-N"
                maxlength="80"
                class="w-full mono" />
            </td>
            <td>
              <input
                type="text"
                pInputText
                [(ngModel)]="row.label"
                [disabled]="busy"
                maxlength="200"
                class="w-full" />
            </td>
            <td class="cell-action">
              <p-button
                icon="pi pi-trash"
                severity="danger"
                [text]="true"
                [disabled]="busy || steps().length === 1"
                pTooltip="{{ t('form.steps.remove') }}"
                tooltipPosition="left"
                (onClick)="removeStep(i)" />
            </td>
          </tr>
        </ng-template>
        <ng-template pTemplate="emptymessage">
          <tr>
            <td colspan="5" class="empty-row">Aucune étape — cliquez « {{ t('form.steps.add') }} »</td>
          </tr>
        </ng-template>
      </p-table>

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
        gap: var(--gap-md, 1rem);
        padding-top: 0.6rem;
      }
      @media (max-width: 540px) { .grid { grid-template-columns: 1fr; } }
      .field { display: flex; flex-direction: column; gap: 0.35rem; }
      .field--full { grid-column: 1 / -1; }
      .field label {
        font-size: 0.78rem;
        text-transform: uppercase;
        letter-spacing: 0.05em;
        color: var(--ft-text-muted, #8b949e);
        font-weight: 600;
      }
      .checkbox { flex-direction: row; align-items: center; gap: 0.6rem; }
      .mono { font-family: var(--font-mono, ui-monospace); font-size: 0.85rem; }

      .section-title {
        margin: 1.2rem 0 0.5rem;
        font-size: 0.78rem;
        text-transform: uppercase;
        letter-spacing: 0.05em;
        color: var(--ft-text-muted);
        font-weight: 600;
      }
      .row-actions { margin-bottom: 0.5rem; }
      .cell-action { width: 3rem; text-align: right; }
      .col-day { width: 8rem; }
      .col-action { width: 11rem; }
      .empty-row {
        text-align: center;
        color: var(--ft-text-subtle);
        font-style: italic;
        padding: 1rem 0;
      }

      :host ::ng-deep .w-full { width: 100%; }
      :host ::ng-deep .day-input { width: 100%; }
      :host ::ng-deep .day-input .p-inputnumber { width: 100%; }
    `
  ]
})
export class DunningCampaignFormDialogComponent implements OnChanges {
  @Input() visible = false;
  @Input() editing: DunningCampaignDto | null = null;
  @Input() busy = false;

  @Output() visibleChange = new EventEmitter<boolean>();
  @Output() confirmed = new EventEmitter<{
    isEdit: boolean;
    id?: string;
    create?: CreateDunningCampaignRequest;
    update?: UpdateDunningCampaignRequest;
  }>();
  @Output() cancelled = new EventEmitter<void>();

  protected name = '';
  protected description = '';
  protected activateImmediately = false;

  protected readonly steps = signal<StepRow[]>(this.buildDefaultSteps());

  protected readonly actionOptions = [
    { label: this.t('action.email'), value: DunningStepActionValue.SendEmail },
    { label: this.t('action.markPastDue'), value: DunningStepActionValue.MarkPastDue },
    { label: this.t('action.suspend'), value: DunningStepActionValue.SuspendSubscription }
  ];

  protected readonly isEdit = computed(() => this.editing !== null);

  protected readonly canConfirm = computed(() => {
    const trimmedName = this.name.trim();
    if (trimmedName.length < 2) return false;
    const list = this.steps();
    if (list.length === 0) return false;
    return list.every(s =>
      s.label.trim().length >= 2
      && s.daysAfterDueDate >= 0
      && (s.action !== DunningStepActionValue.SendEmail || s.emailTemplateCode.trim().length > 0)
    );
  });

  protected t(key: keyof typeof DUNNING_FR): string {
    return DUNNING_FR[key];
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['visible'] && this.visible) {
      if (this.editing) this.applyEditing(this.editing);
      else this.resetState();
    }
  }

  private resetState(): void {
    this.name = '';
    this.description = '';
    this.activateImmediately = false;
    this.steps.set(this.buildDefaultSteps());
  }

  private applyEditing(c: DunningCampaignDto): void {
    this.name = c.name;
    this.description = c.description ?? '';
    this.activateImmediately = false;
    this.steps.set(
      c.steps.length > 0
        ? c.steps.map(s => ({
            daysAfterDueDate: s.daysAfterDueDate,
            action: s.action,
            emailTemplateCode: s.emailTemplateCode ?? '',
            label: s.label
          }))
        : this.buildDefaultSteps()
    );
  }

  private buildDefaultSteps(): StepRow[] {
    return [
      { daysAfterDueDate: 1, action: DunningStepActionValue.SendEmail, emailTemplateCode: 'dunning-step-1-soft', label: 'Rappel doux (J+1)' },
      { daysAfterDueDate: 3, action: DunningStepActionValue.MarkPastDue, emailTemplateCode: 'dunning-step-2-second', label: 'Deuxième rappel — PastDue (J+3)' },
      { daysAfterDueDate: 7, action: DunningStepActionValue.SendEmail, emailTemplateCode: 'dunning-step-3-warning', label: 'Avertissement suspension (J+7)' },
      { daysAfterDueDate: 14, action: DunningStepActionValue.SuspendSubscription, emailTemplateCode: 'dunning-step-4-suspended', label: 'Suspension automatique (J+14)' }
    ];
  }

  addStep(): void {
    this.steps.update(list => [
      ...list,
      {
        daysAfterDueDate: list.length > 0 ? list[list.length - 1].daysAfterDueDate + 1 : 1,
        action: DunningStepActionValue.SendEmail,
        emailTemplateCode: '',
        label: ''
      }
    ]);
  }

  removeStep(index: number): void {
    if (this.steps().length <= 1) return;
    this.steps.update(list => list.filter((_, i) => i !== index));
  }

  onVisibleChange(v: boolean): void {
    this.visible = v;
    this.visibleChange.emit(v);
  }

  onCancel(): void {
    this.cancelled.emit();
    this.onVisibleChange(false);
  }

  onConfirm(): void {
    if (!this.canConfirm() || this.busy) return;
    // Tri par DaysAfterDueDate croissant pour cohérence.
    const cleanSteps: DunningStepInput[] = this.steps()
      .map(s => ({
        daysAfterDueDate: s.daysAfterDueDate,
        action: s.action as 0 | 1 | 2,
        emailTemplateCode: s.action === DunningStepActionValue.SendEmail ? s.emailTemplateCode.trim() : null,
        label: s.label.trim()
      }))
      .sort((a, b) => a.daysAfterDueDate - b.daysAfterDueDate);

    const description = this.description?.trim() || null;

    if (this.isEdit() && this.editing) {
      const update: UpdateDunningCampaignRequest = {
        name: this.name.trim(),
        description,
        steps: cleanSteps
      };
      this.confirmed.emit({ isEdit: true, id: this.editing.id, update });
    } else {
      const create: CreateDunningCampaignRequest = {
        name: this.name.trim(),
        description,
        steps: cleanSteps,
        activateImmediately: this.activateImmediately
      };
      this.confirmed.emit({ isEdit: false, create });
    }
  }
}
