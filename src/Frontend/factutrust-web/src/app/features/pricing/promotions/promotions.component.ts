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
  Promotion,
  PromotionDiscountType
} from '@core/services/pricing.service';
import { ToastService } from '@core/services/toast.service';
import { ErrorHandlerService } from '@core/services/error-handler.service';
import { AuthService } from '@core/services/auth.service';
import { ConfirmationService } from '@core/services/confirmation.service';
import { PERMISSIONS } from '@core/config/permission-keys';

interface DiscountTypeOption {
  label: string;
  value: PromotionDiscountType;
}

@Component({
  selector: 'app-promotions',
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
      title="Promotions"
      subtitle="Remises temporaires appliquées après le prix. Elles ne remplacent pas les grilles : un document émis garde sa remise quand la promotion se termine.">
      @if (canCreate()) {
        <app-button variant="primary" icon="pi-plus" iconPos="left" (clicked)="openCreate()">
          Nouvelle promotion
        </app-button>
      }
    </app-page-header>

    @if (loading()) {
      <app-skeleton-table [columns]="skeletonColumns" [rows]="5"></app-skeleton-table>
    } @else if (promotions().length === 0) {
      <app-empty-state
        icon="pi-megaphone"
        title="Aucune promotion"
        message="Créez une promotion datée pour appliquer une remise temporaire.">
      </app-empty-state>
    } @else {
      <div class="ft-table-card">
        <p-table [value]="promotions()" [rowHover]="true" styleClass="ft-table">
          <ng-template pTemplate="header">
            <tr>
              <th>Nom</th>
              <th>Portée</th>
              <th>Période</th>
              <th class="ft-num">Remise</th>
              <th class="ft-num">Qté min.</th>
              <th class="ft-num">Priorité</th>
              <th>État</th>
              <th class="ft-actions-col">Actions</th>
            </tr>
          </ng-template>

          <ng-template pTemplate="body" let-promo>
            <tr>
              <td>{{ promo.name }}</td>
              <td class="ft-muted">{{ promo.scopeLabel }}</td>
              <td>{{ periodLabel(promo) }}</td>
              <td class="ft-num">{{ discountLabel(promo) }}</td>
              <td class="ft-num">{{ promo.minQuantity | number: '1.0-4' }}</td>
              <td class="ft-num">{{ promo.priority }}</td>
              <td>
                @if (!promo.isActive) {
                  <p-tag severity="secondary" value="Désactivée"></p-tag>
                } @else if (promo.isRunningToday) {
                  <p-tag severity="success" value="En cours"></p-tag>
                } @else {
                  <p-tag
                    severity="warning"
                    value="Hors période"
                    pTooltip="Promotion active mais en dehors de sa fenêtre : elle n'applique aucune remise aujourd'hui."></p-tag>
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
                    (click)="openEdit(promo)"></button>
                }
                @if (canDelete()) {
                  <button
                    pButton
                    type="button"
                    icon="pi pi-trash"
                    class="p-button-text p-button-sm p-button-danger"
                    pTooltip="Supprimer"
                    (click)="confirmDelete(promo)"></button>
                }
              </td>
            </tr>
          </ng-template>
        </p-table>
      </div>
    }

    <p-dialog
      [header]="editing ? 'Modifier la promotion' : 'Nouvelle promotion'"
      [(visible)]="dialogVisible"
      [modal]="true"
      [style]="{ width: '36rem' }"
      [draggable]="false">
      <div class="ft-form-grid">
        <div class="ft-field ft-field--full">
          <label for="pr-name">Nom <span class="ft-required">*</span></label>
          <input id="pr-name" pInputText [(ngModel)]="form.name" maxlength="100" />
        </div>

        <div class="ft-field">
          <label for="pr-start">Début <span class="ft-required">*</span></label>
          <input id="pr-start" type="date" pInputText [(ngModel)]="form.startsOn" />
        </div>

        <div class="ft-field">
          <label for="pr-end">Fin <span class="ft-required">*</span></label>
          <input id="pr-end" type="date" pInputText [(ngModel)]="form.endsOn" />
        </div>

        <div class="ft-field">
          <label for="pr-type">Forme de la remise</label>
          <p-dropdown
            inputId="pr-type"
            [options]="discountTypes"
            [(ngModel)]="form.discountType"
            optionLabel="label"
            optionValue="value"
            appendTo="body"></p-dropdown>
        </div>

        <div class="ft-field">
          @if (form.discountType === 'Percentage') {
            <label for="pr-pct">Taux (%) <span class="ft-required">*</span></label>
            <p-inputNumber
              inputId="pr-pct"
              [(ngModel)]="form.discountPercent"
              mode="decimal"
              [minFractionDigits]="0"
              [maxFractionDigits]="2"
              [min]="0"
              [max]="100"></p-inputNumber>
          } @else {
            <label for="pr-amt">Montant par unité <span class="ft-required">*</span></label>
            <p-inputNumber
              inputId="pr-amt"
              [(ngModel)]="form.discountAmount"
              mode="decimal"
              [minFractionDigits]="3"
              [maxFractionDigits]="3"
              [min]="0"></p-inputNumber>
          }
        </div>

        <div class="ft-field">
          <label for="pr-minqty">Quantité minimale</label>
          <p-inputNumber
            inputId="pr-minqty"
            [(ngModel)]="form.minQuantity"
            mode="decimal"
            [minFractionDigits]="0"
            [maxFractionDigits]="4"
            [min]="1"></p-inputNumber>
        </div>

        <div class="ft-field">
          <label for="pr-priority">Priorité</label>
          <p-inputNumber inputId="pr-priority" [(ngModel)]="form.priority" [min]="0"></p-inputNumber>
          <small class="ft-hint">
            La plus élevée gagne ; à égalité, c'est la plus ciblée qui s'applique.
          </small>
        </div>

        @if (editing) {
          <div class="ft-field ft-field--inline">
            <p-inputSwitch [(ngModel)]="form.isActive" inputId="pr-active"></p-inputSwitch>
            <label for="pr-active">Promotion active</label>
            <small class="ft-hint">Désactiver n'affecte aucun document déjà émis.</small>
          </div>
        }
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
export class PromotionsComponent implements OnInit {
  private readonly pricingService = inject(PricingService);
  private readonly toastService = inject(ToastService);
  private readonly errorHandler = inject(ErrorHandlerService);
  private readonly auth = inject(AuthService);
  private readonly confirmationService = inject(ConfirmationService);

  readonly promotions = signal<Promotion[]>([]);
  readonly loading = signal(true);
  readonly saving = signal(false);

  dialogVisible = false;
  editing: Promotion | null = null;

  form = {
    name: '',
    startsOn: '',
    endsOn: '',
    discountType: 'Percentage' as PromotionDiscountType,
    discountPercent: null as number | null,
    discountAmount: null as number | null,
    minQuantity: 1,
    priority: 0,
    isActive: true
  };

  readonly canCreate = computed(() => this.auth.hasPermission(PERMISSIONS.pricing.create));
  readonly canUpdate = computed(() => this.auth.hasPermission(PERMISSIONS.pricing.update));
  readonly canDelete = computed(() => this.auth.hasPermission(PERMISSIONS.pricing.delete));

  readonly breadcrumbItems: BreadcrumbItem[] = [
    { label: 'Ventes' },
    { label: 'Grilles tarifaires', route: '/pricing' },
    { label: 'Promotions' }
  ];

  readonly discountTypes: DiscountTypeOption[] = [
    { label: 'Pourcentage', value: 'Percentage' },
    { label: 'Montant par unité', value: 'Amount' }
  ];

  readonly skeletonColumns: SkeletonColumn[] = [
    { width: '20%' },
    { width: '18%' },
    { width: '18%' },
    { width: '10%' },
    { width: '8%' },
    { width: '8%' },
    { width: '10%' },
    { width: '8%' }
  ];

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.pricingService.getPromotions().subscribe({
      next: res => {
        this.promotions.set(res.data ?? []);
        this.loading.set(false);
      },
      error: err => {
        this.showError(err, 'Impossible de charger les promotions');
        this.errorHandler.logError('Failed to load promotions', err);
        this.promotions.set([]);
        this.loading.set(false);
      }
    });
  }

  periodLabel(promo: Promotion): string {
    const from = new Date(promo.startsOn).toLocaleDateString('fr-TN');
    const to = new Date(promo.endsOn).toLocaleDateString('fr-TN');
    return `${from} → ${to}`;
  }

  discountLabel(promo: Promotion): string {
    if (promo.discountType === 'Percentage') return `${promo.discountPercent ?? 0} %`;
    return `${(promo.discountAmount ?? 0).toFixed(3)} / unité`;
  }

  openCreate(): void {
    const today = new Date().toISOString().substring(0, 10);
    this.editing = null;
    this.form = {
      name: '',
      startsOn: today,
      endsOn: today,
      discountType: 'Percentage',
      discountPercent: null,
      discountAmount: null,
      minQuantity: 1,
      priority: 0,
      isActive: true
    };
    this.dialogVisible = true;
  }

  openEdit(promo: Promotion): void {
    this.editing = promo;
    this.form = {
      name: promo.name,
      startsOn: promo.startsOn.substring(0, 10),
      endsOn: promo.endsOn.substring(0, 10),
      discountType: promo.discountType,
      discountPercent: promo.discountPercent,
      discountAmount: promo.discountAmount,
      minQuantity: promo.minQuantity,
      priority: promo.priority,
      isActive: promo.isActive
    };
    this.dialogVisible = true;
  }

  canSave(): boolean {
    if (!this.form.name.trim() || !this.form.startsOn || !this.form.endsOn) return false;

    return this.form.discountType === 'Percentage'
      ? (this.form.discountPercent ?? 0) > 0
      : (this.form.discountAmount ?? 0) > 0;
  }

  save(): void {
    this.saving.set(true);

    const done = {
      next: () => {
        this.toastService.add({
          severity: 'success',
          summary: 'Succès',
          detail: this.editing ? 'Promotion mise à jour.' : 'Promotion créée.'
        });
        this.dialogVisible = false;
        this.saving.set(false);
        this.load();
      },
      error: (err: unknown) => {
        this.showError(err, 'Enregistrement impossible');
        this.errorHandler.logError('Save promotion failed', err);
        this.saving.set(false);
      }
    };

    if (this.editing) {
      this.pricingService
        .updatePromotion(this.editing.id, {
          name: this.form.name.trim(),
          startsOn: this.form.startsOn,
          endsOn: this.form.endsOn,
          discountType: this.form.discountType,
          discountPercent: this.form.discountType === 'Percentage' ? this.form.discountPercent : null,
          discountAmount: this.form.discountType === 'Amount' ? this.form.discountAmount : null,
          minQuantity: this.form.minQuantity,
          priority: this.form.priority,
          isActive: this.form.isActive
        })
        .subscribe(done);
    } else {
      this.pricingService
        .createPromotion({
          name: this.form.name.trim(),
          startsOn: this.form.startsOn,
          endsOn: this.form.endsOn,
          discountType: this.form.discountType,
          discountPercent: this.form.discountType === 'Percentage' ? this.form.discountPercent : null,
          discountAmount: this.form.discountType === 'Amount' ? this.form.discountAmount : null,
          productId: null,
          productCategoryId: null,
          clientId: null,
          minQuantity: this.form.minQuantity,
          priority: this.form.priority
        })
        .subscribe(done);
    }
  }

  confirmDelete(promo: Promotion): void {
    this.confirmationService.confirm({
      header: 'Supprimer la promotion',
      message: `Supprimer « ${promo.name} » ? Les documents déjà émis gardent leur remise.`,
      icon: 'pi pi-exclamation-triangle',
      acceptLabel: 'Supprimer',
      rejectLabel: 'Annuler',
      acceptButtonStyleClass: 'btn-danger',
      accept: () => this.remove(promo)
    });
  }

  private remove(promo: Promotion): void {
    this.pricingService.deletePromotion(promo.id).subscribe({
      next: () => {
        this.toastService.add({ severity: 'success', summary: 'Succès', detail: 'Promotion supprimée.' });
        this.load();
      },
      error: err => {
        this.showError(err, 'Suppression impossible');
        this.errorHandler.logError('Delete promotion failed', err);
      }
    });
  }

  private showError(err: unknown, fallback: string): void {
    const msg = this.errorHandler.extractErrorMessage(err);
    this.toastService.add({ severity: 'error', summary: 'Erreur', detail: msg || fallback });
  }
}
