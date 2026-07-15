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
import type { ClonePlanRequest, PlanDto } from '@core/models/platform.models';
import { PLANS_FR } from './plans.i18n.fr';

@Component({
  selector: 'app-plan-clone-dialog',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [FormsModule, DialogModule, ButtonModule, InputTextModule],
  template: `
    <p-dialog
      [visible]="visible"
      (visibleChange)="onVisibleChange($event)"
      [modal]="true"
      [closable]="!busy"
      [draggable]="false"
      [resizable]="false"
      [style]="{ width: '32rem', maxWidth: '95vw' }"
      [header]="t('clone.title')"
    >
      @if (source) {
        <p class="intro" [innerHTML]="t('clone.intro')"></p>
        <p class="src">
          <strong>Source :</strong>
          <code>{{ source.code }}</code> — {{ source.name }}
        </p>

        <div class="field">
          <label for="cl-code">{{ t('clone.field.newCode') }}</label>
          <input
            id="cl-code"
            type="text"
            pInputText
            [(ngModel)]="newCode"
            (ngModelChange)="newCodeSig.set($event)"
            [disabled]="busy"
            autocomplete="off"
            class="w-full"
          />
        </div>
        <div class="field">
          <label for="cl-name">{{ t('clone.field.newName') }}</label>
          <input
            id="cl-name"
            type="text"
            pInputText
            [(ngModel)]="newName"
            (ngModelChange)="newNameSig.set($event)"
            [disabled]="busy"
            class="w-full"
          />
        </div>
      }

      <ng-template pTemplate="footer">
        <p-button label="Annuler" [text]="true" severity="secondary" [disabled]="busy" (onClick)="onCancel()" />
        <p-button
          [label]="t('clone.confirm')"
          icon="pi pi-copy"
          severity="primary"
          [disabled]="busy || !canConfirm()"
          [loading]="busy"
          (onClick)="onConfirm()"
        />
      </ng-template>
    </p-dialog>
  `,
  styles: [
    `
      .intro {
        margin: 0 0 var(--gap-md);
        color: var(--ft-text-muted);
        font-size: 0.9rem;
        line-height: 1.5;
      }

      .src {
        margin: 0 0 var(--gap-md);
        padding: var(--gap-xs) var(--gap-sm);
        background: var(--ft-surface-2);
        border: 1px solid var(--ft-border);
        border-radius: var(--ft-radius);
        font-size: 0.85rem;
      }

      .src code {
        font-family: ui-monospace, SFMono-Regular, monospace;
        background: var(--ft-surface);
        padding: 0.05rem 0.3rem;
        border-radius: var(--ft-radius-sm);
        color: var(--ft-accent);
      }

      .field {
        display: flex;
        flex-direction: column;
        gap: 0.4rem;
        margin-bottom: var(--gap-sm);
      }

      .field label {
        font-size: 0.78rem;
        text-transform: uppercase;
        letter-spacing: 0.05em;
        color: var(--ft-text-muted);
        font-weight: 600;
      }

      .w-full {
        width: 100%;
      }
    `
  ]
})
export class PlanCloneDialogComponent implements OnChanges {
  @Input() visible = false;
  @Input() source: PlanDto | null = null;
  @Input() busy = false;

  @Output() visibleChange = new EventEmitter<boolean>();
  @Output() confirmed = new EventEmitter<{ source: PlanDto; request: ClonePlanRequest }>();
  @Output() cancelled = new EventEmitter<void>();

  protected newCode = '';
  protected newName = '';
  protected newCodeSig = signal('');
  protected newNameSig = signal('');

  protected readonly canConfirm = computed(
    () => this.newCodeSig().trim().length >= 2 && this.newNameSig().trim().length >= 2
  );

  protected t(key: keyof typeof PLANS_FR): string {
    return PLANS_FR[key];
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['visible'] && this.visible && this.source) {
      this.newCode = this.source.code + '_COPY';
      this.newName = this.source.name + ' (copie)';
      this.newCodeSig.set(this.newCode);
      this.newNameSig.set(this.newName);
    }
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
    if (!this.canConfirm() || !this.source || this.busy) return;
    this.confirmed.emit({
      source: this.source,
      request: { newCode: this.newCodeSig().trim(), newName: this.newNameSig().trim() }
    });
  }
}
