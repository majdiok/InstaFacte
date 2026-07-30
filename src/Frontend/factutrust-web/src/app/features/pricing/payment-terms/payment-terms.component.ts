import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { TableModule } from 'primeng/table';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { InputNumberModule } from 'primeng/inputnumber';
import { InputSwitchModule } from 'primeng/inputswitch';
import { DropdownModule } from 'primeng/dropdown';
import { TagModule } from 'primeng/tag';
import { TooltipModule } from 'primeng/tooltip';
import { DialogModule } from 'primeng/dialog';
import { PageHeaderComponent } from '@shared/components/page-header/page-header.component';
import { BreadcrumbComponent, BreadcrumbItem } from '@shared/components/breadcrumb/breadcrumb.component';
import { SkeletonTableComponent, SkeletonColumn } from '@shared/components/skeleton/skeleton-table.component';
import { EmptyStateComponent } from '@shared/components/empty-state/empty-state.component';
import { ButtonComponent } from '@shared/components/button/button.component';
import {
  PricingService,
  PaymentTermTemplate,
  PaymentDueMode
} from '@core/services/pricing.service';
import { ToastService } from '@core/services/toast.service';
import { ErrorHandlerService } from '@core/services/error-handler.service';
import { AuthService } from '@core/services/auth.service';
import { ConfirmationService } from '@core/services/confirmation.service';
import { PERMISSIONS } from '@core/config/permission-keys';

interface DueModeOption {
  label: string;
  value: PaymentDueMode;
}

@Component({
  selector: 'app-payment-terms',
  standalone: true,
  imports: [
    CommonModule,
    RouterModule,
    FormsModule,
    TableModule,
    ButtonModule,
    InputTextModule,
    InputNumberModule,
    InputSwitchModule,
    DropdownModule,
    TagModule,
    TooltipModule,
    DialogModule,
    PageHeaderComponent,
    BreadcrumbComponent,
    SkeletonTableComponent,
    EmptyStateComponent,
    ButtonComponent
  ],
  template: `
    <app-breadcrumb [items]="breadcrumbItems"></app-breadcrumb>

    <app-page-header
      title="Conditions de règlement"
      subtitle="Délai, échéance et escompte. La condition produit le texte imprimé sur le document et une échéance exploitable pour la balance âgée.">
      @if (canCreate()) {
        <app-button variant="primary" icon="pi-plus" iconPos="left" (clicked)="openCreate()">
          Nouvelle condition
        </app-button>
      }
    </app-page-header>

    @if (loading()) {
      <app-skeleton-table [columns]="skeletonColumns" [rows]="4"></app-skeleton-table>
    } @else if (terms().length === 0) {
      <app-empty-state
        icon="pi-calendar-clock"
        title="Aucune condition de règlement"
        message="Créez une condition pour proposer un délai et calculer l'échéance automatiquement.">
      </app-empty-state>
    } @else {
      <div class="ft-table-card">
        <p-table [value]="terms()" [rowHover]="true" styleClass="ft-table">
          <ng-template pTemplate="header">
            <tr>
              <th>Libellé</th>
              <th>Ce qui s'imprime</th>
              <th class="ft-num">Délai</th>
              <th>Échéance d'un document du jour</th>
              <th>État</th>
              <th class="ft-actions-col">Actions</th>
            </tr>
          </ng-template>

          <ng-template pTemplate="body" let-term>
            <tr>
              <td>
                {{ term.name }}
                @if (term.isDefault) {
                  <p-tag severity="info" value="Par défaut"></p-tag>
                }
              </td>
              <td class="ft-muted">{{ term.documentLabel }}</td>
              <td class="ft-num">{{ term.delayDays }} j</td>
              <td>{{ term.sampleDueDate | date: 'dd/MM/yyyy' }}</td>
              <td>
                @if (term.isActive) {
                  <p-tag severity="success" value="Active"></p-tag>
                } @else {
                  <p-tag severity="secondary" value="Désactivée"></p-tag>
                }
              </td>
              <td class="ft-actions-col">
                @if (canUpdate()) {
                  <button
                    pButton
                    type="button"
                    icon="pi pi-pencil"
                    class="p-button-text p-button-sm"
                    pTooltip="Modifier"
                    (click)="openEdit(term)"></button>
                }
                @if (canDelete()) {
                  <button
                    pButton
                    type="button"
                    icon="pi pi-trash"
                    class="p-button-text p-button-sm p-button-danger"
                    pTooltip="Supprimer"
                    (click)="confirmDelete(term)"></button>
                }
              </td>
            </tr>
          </ng-template>
        </p-table>
      </div>
    }

    <p-dialog
      [header]="editing ? 'Modifier la condition' : 'Nouvelle condition'"
      [(visible)]="dialogVisible"
      [modal]="true"
      [style]="{ width: '36rem' }"
      [draggable]="false">
      <div class="ft-form-grid">
        <div class="ft-field ft-field--full">
          <label for="pt-name">Libellé <span class="ft-required">*</span></label>
          <input id="pt-name" pInputText [(ngModel)]="form.name" maxlength="100" />
        </div>

        <div class="ft-field">
          <label for="pt-delay">Délai (jours) <span class="ft-required">*</span></label>
          <p-inputNumber inputId="pt-delay" [(ngModel)]="form.delayDays" [min]="0" [max]="365"></p-inputNumber>
        </div>

        <div class="ft-field">
          <label for="pt-mode">Mode d'échéance</label>
          <p-dropdown
            inputId="pt-mode"
            [options]="dueModes"
            [(ngModel)]="form.dueMode"
            optionLabel="label"
            optionValue="value"
            appendTo="body"></p-dropdown>
        </div>

        @if (form.dueMode === 'EndOfMonthOnDay') {
          <div class="ft-field">
            <label for="pt-day">Jour de règlement <span class="ft-required">*</span></label>
            <p-inputNumber inputId="pt-day" [(ngModel)]="form.dueDayOfMonth" [min]="1" [max]="31"></p-inputNumber>
            <small class="ft-hint">
              Un jour inexistant (31 février) est ramené au dernier jour du mois.
            </small>
          </div>
        }

        <div class="ft-field">
          <label for="pt-disc">Escompte (%)</label>
          <p-inputNumber
            inputId="pt-disc"
            [(ngModel)]="form.earlyPaymentDiscountPercent"
            mode="decimal"
            [minFractionDigits]="0"
            [maxFractionDigits]="2"
            [min]="0"
            [max]="100"></p-inputNumber>
        </div>

        <div class="ft-field">
          <label for="pt-discdays">Escompte sous (jours)</label>
          <p-inputNumber inputId="pt-discdays" [(ngModel)]="form.earlyPaymentDays" [min]="1"></p-inputNumber>
          <small class="ft-hint">
            Doit être inférieur au délai, sinon l'escompte serait toujours acquis.
          </small>
        </div>

        <div class="ft-field ft-field--inline">
          <p-inputSwitch [(ngModel)]="form.isActive" inputId="pt-active"></p-inputSwitch>
          <label for="pt-active">Condition active</label>
        </div>

        <div class="ft-field ft-field--inline">
          <p-inputSwitch [(ngModel)]="form.isDefault" inputId="pt-default"></p-inputSwitch>
          <label for="pt-default">Proposée par défaut</label>
          <small class="ft-hint">Une seule condition peut l'être : l'ancienne est libérée.</small>
        </div>
      </div>

      <ng-template pTemplate="footer">
        <app-button variant="secondary" (clicked)="dialogVisible = false">Annuler</app-button>
        <app-button variant="primary" [disabled]="!canSave() || saving()" (clicked)="save()">
          Enregistrer
        </app-button>
      </ng-template>
    </p-dialog>
  `
})
export class PaymentTermsComponent implements OnInit {
  private readonly pricingService = inject(PricingService);
  private readonly toastService = inject(ToastService);
  private readonly errorHandler = inject(ErrorHandlerService);
  private readonly auth = inject(AuthService);
  private readonly confirmationService = inject(ConfirmationService);

  readonly terms = signal<PaymentTermTemplate[]>([]);
  readonly loading = signal(true);
  readonly saving = signal(false);

  dialogVisible = false;
  editing: PaymentTermTemplate | null = null;

  form = {
    name: '',
    delayDays: 30,
    dueMode: 'NetDays' as PaymentDueMode,
    dueDayOfMonth: null as number | null,
    earlyPaymentDiscountPercent: null as number | null,
    earlyPaymentDays: null as number | null,
    isActive: true,
    isDefault: false
  };

  readonly canCreate = computed(() => this.auth.hasPermission(PERMISSIONS.pricing.create));
  readonly canUpdate = computed(() => this.auth.hasPermission(PERMISSIONS.pricing.update));
  readonly canDelete = computed(() => this.auth.hasPermission(PERMISSIONS.pricing.delete));

  readonly breadcrumbItems: BreadcrumbItem[] = [
    { label: 'Ventes' },
    { label: 'Grilles tarifaires', route: '/pricing' },
    { label: 'Conditions de règlement' }
  ];

  readonly dueModes: DueModeOption[] = [
    { label: 'Net (date + délai)', value: 'NetDays' },
    { label: 'Fin de mois', value: 'EndOfMonth' },
    { label: 'Fin de mois, le N', value: 'EndOfMonthOnDay' }
  ];

  readonly skeletonColumns: SkeletonColumn[] = [
    { width: '22%' },
    { width: '28%' },
    { width: '10%' },
    { width: '18%' },
    { width: '12%' },
    { width: '10%' }
  ];

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.pricingService.getPaymentTerms().subscribe({
      next: res => {
        this.terms.set(res.data ?? []);
        this.loading.set(false);
      },
      error: err => {
        this.showError(err, 'Impossible de charger les conditions de règlement');
        this.errorHandler.logError('Failed to load payment terms', err);
        this.terms.set([]);
        this.loading.set(false);
      }
    });
  }

  openCreate(): void {
    this.editing = null;
    this.form = {
      name: '',
      delayDays: 30,
      dueMode: 'NetDays',
      dueDayOfMonth: null,
      earlyPaymentDiscountPercent: null,
      earlyPaymentDays: null,
      isActive: true,
      isDefault: false
    };
    this.dialogVisible = true;
  }

  openEdit(term: PaymentTermTemplate): void {
    this.editing = term;
    this.form = {
      name: term.name,
      delayDays: term.delayDays,
      dueMode: term.dueMode,
      dueDayOfMonth: term.dueDayOfMonth,
      earlyPaymentDiscountPercent: term.earlyPaymentDiscountPercent,
      earlyPaymentDays: term.earlyPaymentDays,
      isActive: term.isActive,
      isDefault: term.isDefault
    };
    this.dialogVisible = true;
  }

  canSave(): boolean {
    if (!this.form.name.trim()) return false;
    if (this.form.dueMode === 'EndOfMonthOnDay' && !this.form.dueDayOfMonth) return false;

    // Le serveur refuse un taux sans fenêtre : autant le bloquer ici plutôt que
    // d'envoyer une saisie que l'on sait invalide.
    if (this.form.earlyPaymentDiscountPercent && !this.form.earlyPaymentDays) return false;

    return true;
  }

  save(): void {
    this.saving.set(true);
    this.pricingService
      .upsertPaymentTerm({
        id: this.editing?.id ?? null,
        name: this.form.name.trim(),
        delayDays: this.form.delayDays,
        dueMode: this.form.dueMode,
        dueDayOfMonth: this.form.dueMode === 'EndOfMonthOnDay' ? this.form.dueDayOfMonth : null,
        earlyPaymentDiscountPercent: this.form.earlyPaymentDiscountPercent,
        earlyPaymentDays: this.form.earlyPaymentDiscountPercent ? this.form.earlyPaymentDays : null,
        isActive: this.form.isActive,
        isDefault: this.form.isDefault
      })
      .subscribe({
        next: () => {
          this.toastService.add({
            severity: 'success',
            summary: 'Succès',
            detail: 'Condition de règlement enregistrée.'
          });
          this.dialogVisible = false;
          this.saving.set(false);
          this.load();
        },
        error: err => {
          this.showError(err, 'Enregistrement impossible');
          this.errorHandler.logError('Save payment term failed', err);
          this.saving.set(false);
        }
      });
  }

  confirmDelete(term: PaymentTermTemplate): void {
    this.confirmationService.confirm({
      header: 'Supprimer la condition',
      message:
        `Supprimer « ${term.name} » ? Les documents déjà émis conservent leur texte et leur ` +
        'échéance : ils ne référencent pas le modèle.',
      icon: 'pi pi-exclamation-triangle',
      acceptLabel: 'Supprimer',
      rejectLabel: 'Annuler',
      acceptButtonStyleClass: 'btn-danger',
      accept: () => this.remove(term)
    });
  }

  private remove(term: PaymentTermTemplate): void {
    this.pricingService.deletePaymentTerm(term.id).subscribe({
      next: () => {
        this.toastService.add({ severity: 'success', summary: 'Succès', detail: 'Condition supprimée.' });
        this.load();
      },
      error: err => {
        this.showError(err, 'Suppression impossible');
        this.errorHandler.logError('Delete payment term failed', err);
      }
    });
  }

  private showError(err: unknown, fallback: string): void {
    const msg = this.errorHandler.extractErrorMessage(err);
    this.toastService.add({ severity: 'error', summary: 'Erreur', detail: msg || fallback });
  }
}
