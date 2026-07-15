import {
  ChangeDetectionStrategy,
  Component,
  EventEmitter,
  Input,
  OnChanges,
  Output,
  SimpleChanges,
  signal
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { DialogModule } from 'primeng/dialog';
import { ButtonModule } from 'primeng/button';
import { DropdownModule } from 'primeng/dropdown';
import { PlatformRole, type PlatformAdminListItemDto } from '@core/models/platform.models';
import { ADMINS_FR, roleDescription, roleLabel } from './admins.i18n.fr';

@Component({
  selector: 'app-admin-role-dialog',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [FormsModule, DialogModule, ButtonModule, DropdownModule],
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
      [header]="t('role.title')"
    >
      @if (data) {
        <p class="intro">
          <strong>{{ data.fullName }}</strong> · <code>{{ data.email }}</code>
          <br />
          {{ t('role.intro') }}
        </p>

        <div class="field">
          <label for="adm-rrole">{{ t('create.field.role') }}</label>
          <p-dropdown
            inputId="adm-rrole"
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
      }

      <ng-template pTemplate="footer">
        <p-button label="Annuler" [text]="true" severity="secondary" [disabled]="busy" (onClick)="onCancel()" />
        <p-button
          [label]="t('role.confirm')"
          icon="pi pi-shield"
          severity="primary"
          [disabled]="busy || !roleSig() || roleSig() === currentRole"
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

      .role-desc {
        color: var(--ft-info-text);
        font-size: 0.78rem;
      }
    `
  ]
})
export class AdminRoleDialogComponent implements OnChanges {
  @Input() visible = false;
  @Input() data: PlatformAdminListItemDto | null = null;
  @Input() busy = false;

  @Output() visibleChange = new EventEmitter<boolean>();
  @Output() confirmed = new EventEmitter<{ data: PlatformAdminListItemDto; role: string }>();
  @Output() cancelled = new EventEmitter<void>();

  protected readonly roleOptions = Object.values(PlatformRole).map((value) => ({
    label: roleLabel(value),
    value
  }));

  protected role: string = PlatformRole.SupportAgent;
  protected roleSig = signal<string>(PlatformRole.SupportAgent);
  protected currentRole: string = PlatformRole.SupportAgent;

  protected t(key: keyof typeof ADMINS_FR): string {
    return ADMINS_FR[key];
  }

  protected roleDescription(role: string): string {
    return roleDescription(role);
  }

  ngOnChanges(changes: SimpleChanges): void {
    if ((changes['visible'] || changes['data']) && this.visible && this.data) {
      const initial = this.data.roles[0] ?? PlatformRole.SupportAgent;
      this.role = initial;
      this.roleSig.set(initial);
      this.currentRole = initial;
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
    if (!this.data || this.busy || !this.roleSig()) return;
    this.confirmed.emit({ data: this.data, role: this.roleSig() });
  }
}
