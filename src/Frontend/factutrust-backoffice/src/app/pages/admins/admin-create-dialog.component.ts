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
import { SelectModule } from 'primeng/select';
import { PasswordModule } from 'primeng/password';
import { PlatformRole, type CreatePlatformAdminRequest } from '@core/models/platform.models';
import { ADMINS_FR, roleDescription, roleLabel } from './admins.i18n.fr';

@Component({
  selector: 'app-admin-create-dialog',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    FormsModule,
    DialogModule,
    ButtonModule,
    InputTextModule,
    SelectModule,
    PasswordModule
  ],
  template: `
    <p-dialog
      [visible]="visible"
      (visibleChange)="onVisibleChange($event)"
      [modal]="true"
      [closable]="!busy"
      [closeOnEscape]="!busy"
      [draggable]="false"
      [resizable]="false"
      [style]="{ width: '32rem', maxWidth: '95vw' }"
      [header]="t('create.title')"
    >
      <p class="intro">{{ t('create.intro') }}</p>

      <div class="grid">
        <div class="field field--full">
          <label for="adm-email">{{ t('create.field.email') }}</label>
          <input
            id="adm-email"
            type="email"
            pInputText
            [(ngModel)]="email"
            (ngModelChange)="emailSig.set($event)"
            [disabled]="busy"
            autocomplete="off"
          />
        </div>

        <div class="field">
          <label for="adm-firstName">{{ t('create.field.firstName') }}</label>
          <input
            id="adm-firstName"
            type="text"
            pInputText
            [(ngModel)]="firstName"
            (ngModelChange)="firstNameSig.set($event)"
            [disabled]="busy"
          />
        </div>

        <div class="field">
          <label for="adm-lastName">{{ t('create.field.lastName') }}</label>
          <input
            id="adm-lastName"
            type="text"
            pInputText
            [(ngModel)]="lastName"
            (ngModelChange)="lastNameSig.set($event)"
            [disabled]="busy"
          />
        </div>

        <div class="field field--full">
          <label for="adm-role">{{ t('create.field.role') }}</label>
          <p-select
            inputId="adm-role"
            [options]="roleOptions"
            [(ngModel)]="role"
            (ngModelChange)="roleSig.set($event)"
            optionLabel="label"
            optionValue="value"
            [disabled]="busy"
            styleClass="w-full"
          />
          @if (role) {
            <small class="role-desc">{{ roleDescription(role) }}</small>
          }
        </div>

        <div class="field field--full">
          <label for="adm-password">{{ t('create.field.password') }}</label>
          <p-password
            inputId="adm-password"
            [(ngModel)]="password"
            (ngModelChange)="passwordSig.set($event)"
            [disabled]="busy"
            [feedback]="true"
            [toggleMask]="true"
            promptLabel="Saisir un mot de passe"
            weakLabel="Trop faible"
            mediumLabel="Moyen"
            strongLabel="Fort"
            styleClass="w-full"
          />
          <small class="hint">{{ t('create.field.passwordHint') }}</small>
        </div>
      </div>

      <ng-template pTemplate="footer">
        <p-button label="Annuler" [text]="true" severity="secondary" [disabled]="busy" (onClick)="onCancel()" />
        <p-button
          [label]="t('create.confirm')"
          icon="pi pi-user-plus"
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

      .field input,
      .field :host ::ng-deep .p-select,
      .field :host ::ng-deep .p-password input {
        width: 100%;
      }

      :host ::ng-deep .w-full {
        width: 100%;
      }

      :host ::ng-deep .w-full input {
        width: 100%;
      }

      .hint,
      .role-desc {
        color: var(--ft-text-subtle);
        font-size: 0.75rem;
      }

      .role-desc {
        color: var(--ft-info-text);
      }
    `
  ]
})
export class AdminCreateDialogComponent implements OnChanges {
  @Input() visible = false;
  @Input() busy = false;

  @Output() visibleChange = new EventEmitter<boolean>();
  @Output() confirmed = new EventEmitter<CreatePlatformAdminRequest>();
  @Output() cancelled = new EventEmitter<void>();

  protected readonly roleOptions = Object.values(PlatformRole).map((value) => ({
    label: roleLabel(value),
    value
  }));

  protected email = '';
  protected firstName = '';
  protected lastName = '';
  protected role: string = PlatformRole.SupportAgent;
  protected password = '';

  protected emailSig = signal('');
  protected firstNameSig = signal('');
  protected lastNameSig = signal('');
  protected roleSig = signal<string>(PlatformRole.SupportAgent);
  protected passwordSig = signal('');

  protected readonly canConfirm = computed(() => {
    return (
      isValidEmail(this.emailSig()) &&
      this.firstNameSig().trim().length >= 1 &&
      this.lastNameSig().trim().length >= 1 &&
      !!this.roleSig() &&
      this.passwordSig().length >= 14
    );
  });

  protected t(key: keyof typeof ADMINS_FR): string {
    return ADMINS_FR[key];
  }

  protected roleDescription(role: string): string {
    return roleDescription(role);
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['visible'] && changes['visible'].currentValue === true) {
      this.resetState();
    }
  }

  onVisibleChange(value: boolean): void {
    this.visible = value;
    this.visibleChange.emit(value);
    if (!value) this.resetState();
  }

  onCancel(): void {
    this.cancelled.emit();
    this.onVisibleChange(false);
  }

  onConfirm(): void {
    if (!this.canConfirm() || this.busy) return;
    this.confirmed.emit({
      email: this.emailSig().trim(),
      firstName: this.firstNameSig().trim(),
      lastName: this.lastNameSig().trim(),
      role: this.roleSig(),
      initialPassword: this.passwordSig()
    });
  }

  private resetState(): void {
    this.email = '';
    this.firstName = '';
    this.lastName = '';
    this.role = PlatformRole.SupportAgent;
    this.password = '';
    this.emailSig.set('');
    this.firstNameSig.set('');
    this.lastNameSig.set('');
    this.roleSig.set(PlatformRole.SupportAgent);
    this.passwordSig.set('');
  }
}

function isValidEmail(value: string): boolean {
  // Validation simple — la validation forte est côté serveur
  return /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(value.trim());
}
