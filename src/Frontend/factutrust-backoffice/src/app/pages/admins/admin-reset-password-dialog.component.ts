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
import { PasswordModule } from 'primeng/password';
import type { PlatformAdminListItemDto } from '@core/models/platform.models';
import { ADMINS_FR } from './admins.i18n.fr';

@Component({
  selector: 'app-admin-reset-password-dialog',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [FormsModule, DialogModule, ButtonModule, PasswordModule],
  template: `
    <p-dialog
      [visible]="visible"
      (visibleChange)="onVisibleChange($event)"
      [modal]="true"
      [closable]="!busy"
      [closeOnEscape]="!busy"
      [draggable]="false"
      [resizable]="false"
      [style]="{ width: '28rem', maxWidth: '95vw' }"
      [header]="t('reset.title')"
    >
      @if (data) {
        <p class="intro">
          <strong>{{ data.fullName }}</strong> · <code>{{ data.email }}</code>
          <br />
          {{ t('reset.intro') }}
        </p>

        <div class="field">
          <label for="adm-reset-password">{{ t('reset.field.password') }}</label>
          <p-password
            inputId="adm-reset-password"
            [(ngModel)]="password"
            (ngModelChange)="passwordSig.set($event)"
            [disabled]="busy"
            [feedback]="true"
            [toggleMask]="true"
            promptLabel="Saisir un nouveau mot de passe"
            weakLabel="Trop faible"
            mediumLabel="Moyen"
            strongLabel="Fort"
            styleClass="w-full"
          />
          <small class="hint">{{ t('create.field.passwordHint') }}</small>
        </div>
      }

      <ng-template pTemplate="footer">
        <p-button label="Annuler" [text]="true" severity="secondary" [disabled]="busy" (onClick)="onCancel()" />
        <p-button
          [label]="t('reset.confirm')"
          icon="pi pi-key"
          severity="warning"
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

      .intro strong {
        color: var(--ft-text);
      }

      .intro code {
        font-family: ui-monospace, SFMono-Regular, monospace;
        font-size: 0.85em;
        color: var(--ft-accent);
      }

      .field {
        display: flex;
        flex-direction: column;
        gap: 0.4rem;
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

      :host ::ng-deep .w-full input {
        width: 100%;
      }

      .hint {
        color: var(--ft-text-subtle);
        font-size: 0.75rem;
      }
    `
  ]
})
export class AdminResetPasswordDialogComponent implements OnChanges {
  @Input() visible = false;
  @Input() data: PlatformAdminListItemDto | null = null;
  @Input() busy = false;

  @Output() visibleChange = new EventEmitter<boolean>();
  @Output() confirmed = new EventEmitter<{ data: PlatformAdminListItemDto; newPassword: string }>();
  @Output() cancelled = new EventEmitter<void>();

  protected password = '';
  protected passwordSig = signal('');

  protected readonly canConfirm = computed(() => this.passwordSig().length >= 14);

  protected t(key: keyof typeof ADMINS_FR): string {
    return ADMINS_FR[key];
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['visible'] && changes['visible'].currentValue === true) {
      this.password = '';
      this.passwordSig.set('');
    }
  }

  onVisibleChange(value: boolean): void {
    this.visible = value;
    this.visibleChange.emit(value);
    if (!value) {
      this.password = '';
      this.passwordSig.set('');
    }
  }

  onCancel(): void {
    this.cancelled.emit();
    this.onVisibleChange(false);
  }

  onConfirm(): void {
    if (!this.canConfirm() || !this.data || this.busy) return;
    this.confirmed.emit({ data: this.data, newPassword: this.passwordSig() });
  }
}
