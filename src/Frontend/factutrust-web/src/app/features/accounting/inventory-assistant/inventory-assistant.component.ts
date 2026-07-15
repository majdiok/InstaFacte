import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { PageHeaderComponent } from '@shared/components/page-header/page-header.component';
import { ButtonComponent } from '@shared/components/button/button.component';
import { AccountingStatusBannerComponent } from '../shared/accounting-status-banner.component';
import { ToastService } from '@core/services/toast.service';
import {
  AccountingService,
  ChartOfAccountDto,
  InventoryEntryKindDto,
  CreateInventoryEntryRequest
} from '../services/accounting.service';
import { todayLocalYmd } from '../shared/accounting-date-utils';

/**
 * Assistant d'écritures d'inventaire : sélection d'un type de régularisation (CCA/PCA, charges
 * à payer, produits à recevoir, provisions, variation de stocks), comptes pré-remplis, montant,
 * et génération de l'écriture + son extourne automatique au nouvel exercice le cas échéant.
 */
@Component({
  selector: 'app-inventory-assistant',
  standalone: true,
  imports: [CommonModule, FormsModule, PageHeaderComponent, ButtonComponent, AccountingStatusBannerComponent],
  template: `
    <app-page-header title="Assistant d'écritures d'inventaire" subtitle="Régularisations de fin d'exercice (CCA/PCA, provisions…)" />

    <app-accounting-status-banner variant="error" [message]="error() ?? ''" />

    <div class="card inv-kinds-card">
      <h3 class="inv-section-title">Type de régularisation</h3>
      <div class="inv-kinds">
        @for (k of kinds(); track k.kind) {
          <button
            type="button"
            class="inv-kind"
            [class.inv-kind--on]="selectedKind()?.kind === k.kind"
            (click)="selectKind(k)">
            <span class="inv-kind-label">{{ k.label }}</span>
            <span class="inv-kind-accounts">{{ k.defaultDebitAccount }} → {{ k.defaultCreditAccount }}</span>
            @if (k.autoReverse) { <span class="inv-kind-tag">extournée</span> }
          </button>
        }
      </div>
    </div>

    @if (selectedKind(); as k) {
      <div class="card inv-form-card">
        <p class="inv-hint">{{ k.hint }}</p>
        <div class="inv-fields">
          <div class="inv-field">
            <label class="field-label" for="inv-date">Date de l'écriture</label>
            <input id="inv-date" type="date" class="inv-input" [(ngModel)]="form.entryDate" />
          </div>
          <div class="inv-field">
            <label class="field-label" for="inv-debit">Compte débit</label>
            <input id="inv-debit" class="inv-input" [(ngModel)]="form.debitAccount" list="inv-accounts" placeholder="Compte à débiter" />
          </div>
          <div class="inv-field">
            <label class="field-label" for="inv-credit">Compte crédit</label>
            <input id="inv-credit" class="inv-input" [(ngModel)]="form.creditAccount" list="inv-accounts" placeholder="Compte à créditer" />
          </div>
          <div class="inv-field">
            <label class="field-label" for="inv-amount">Montant</label>
            <input id="inv-amount" type="number" min="0" step="0.001" class="inv-input" [(ngModel)]="form.amount" />
          </div>
          <div class="inv-field inv-field-grow">
            <label class="field-label" for="inv-label">Libellé</label>
            <input id="inv-label" class="inv-input" [(ngModel)]="form.label" [placeholder]="k.label" />
          </div>
        </div>
        <datalist id="inv-accounts">
          @for (a of accounts(); track a.accountNumber) {
            <option [value]="a.accountNumber">{{ a.accountNumber }} — {{ a.label }}</option>
          }
        </datalist>

        <label class="inv-reverse-toggle" for="inv-reverse">
          <input id="inv-reverse" type="checkbox" [(ngModel)]="form.autoReverse" />
          <span>Extourner automatiquement au 1er de la période suivante</span>
        </label>

        @if (previewValid()) {
          <div class="inv-preview" role="note">
            <h4 class="inv-preview-title">Aperçu</h4>
            <p class="inv-preview-line">
              <strong>Écriture</strong> ({{ form.entryDate }}) :
              débit {{ form.debitAccount }} / crédit {{ form.creditAccount }} — {{ form.amount | number : '1.3-3' }}
            </p>
            @if (form.autoReverse) {
              <p class="inv-preview-line inv-preview-reversal">
                <strong>Extourne</strong> (au nouvel exercice) :
                débit {{ form.creditAccount }} / crédit {{ form.debitAccount }} — {{ form.amount | number : '1.3-3' }}
              </p>
            }
          </div>
        }

        <div class="inv-actions">
          <app-button variant="secondary" type="button" (click)="reset()" [disabled]="saving()">Réinitialiser</app-button>
          <app-button variant="primary" icon="pi pi-check" type="button" (click)="submit()" [disabled]="saving() || !previewValid()">
            {{ saving() ? 'Enregistrement…' : 'Enregistrer l\\'écriture' }}
          </app-button>
        </div>
      </div>
    }
  `,
  styles: `
    .inv-kinds-card, .inv-form-card { padding: var(--spacing-4); border-radius: var(--radius-lg); box-shadow: var(--shadow-sm); margin-bottom: var(--spacing-4); }
    .inv-section-title { margin: 0 0 var(--spacing-3); font-size: var(--font-size-md); font-weight: var(--font-weight-semibold); }
    .inv-kinds { display: grid; grid-template-columns: repeat(auto-fill, minmax(15rem, 1fr)); gap: var(--spacing-3); }
    .inv-kind {
      display: flex; flex-direction: column; gap: 2px; align-items: flex-start;
      padding: var(--spacing-3); border: 1px solid var(--color-border-default); border-radius: var(--radius-md);
      background: var(--color-background-elevated); cursor: pointer; text-align: left;
    }
    .inv-kind--on { border-color: var(--color-primary-500, #2563eb); background: var(--color-primary-50, #eff6ff); }
    .inv-kind-label { font-weight: var(--font-weight-semibold); color: var(--color-text-primary); }
    .inv-kind-accounts { font-size: var(--font-size-xs); color: var(--color-text-tertiary); font-variant-numeric: tabular-nums; }
    .inv-kind-tag { font-size: var(--font-size-xs); color: var(--color-warning-700, #a16207); background: var(--color-warning-100, #fef3c7); border-radius: var(--radius-pill, 999px); padding: 0 0.4rem; margin-top: 2px; }
    .inv-hint { margin: 0 0 var(--spacing-3); font-size: var(--font-size-sm); color: var(--color-text-secondary); }
    .inv-fields { display: flex; flex-wrap: wrap; gap: var(--spacing-3); }
    .inv-field { display: flex; flex-direction: column; gap: var(--spacing-1); flex: 1 1 9rem; min-width: 8rem; }
    .inv-field-grow { flex: 2 1 16rem; }
    .field-label { font-size: var(--font-size-sm); font-weight: var(--font-weight-semibold); color: var(--color-text-primary); }
    .inv-input { padding: var(--spacing-2) var(--spacing-3); border: 1px solid var(--color-border-default); border-radius: var(--radius-md); background: var(--color-background-elevated); color: var(--color-text-primary); font-size: var(--font-size-sm); width: 100%; }
    .inv-reverse-toggle { display: inline-flex; align-items: center; gap: var(--spacing-2); margin-top: var(--spacing-3); font-size: var(--font-size-sm); color: var(--color-text-secondary); cursor: pointer; }
    .inv-preview { margin-top: var(--spacing-3); padding: var(--spacing-3); background: var(--color-background-subtle); border: 1px solid var(--color-border-subtle); border-radius: var(--radius-md); }
    .inv-preview-title { margin: 0 0 var(--spacing-2); font-size: var(--font-size-sm); font-weight: var(--font-weight-semibold); }
    .inv-preview-line { margin: 0 0 4px; font-size: var(--font-size-sm); font-variant-numeric: tabular-nums; }
    .inv-preview-reversal { color: var(--color-text-secondary); }
    .inv-actions { display: flex; justify-content: flex-end; gap: var(--spacing-3); margin-top: var(--spacing-4); padding-top: var(--spacing-3); border-top: 1px solid var(--color-border-subtle); }
  `
})
export class InventoryAssistantComponent implements OnInit {
  private readonly api = inject(AccountingService);
  private readonly toast = inject(ToastService);

  readonly kinds = signal<InventoryEntryKindDto[]>([]);
  readonly accounts = signal<ChartOfAccountDto[]>([]);
  readonly selectedKind = signal<InventoryEntryKindDto | null>(null);
  readonly error = signal<string | null>(null);
  readonly saving = signal(false);

  form: CreateInventoryEntryRequest = this.emptyForm();

  readonly previewValid = computed(() => {
    const f = this.form;
    return !!f.entryDate && !!f.debitAccount.trim() && !!f.creditAccount.trim()
      && f.debitAccount.trim() !== f.creditAccount.trim() && Number(f.amount) > 0;
  });

  private emptyForm(): CreateInventoryEntryRequest {
    return { kind: 0, entryDate: todayLocalYmd(), debitAccount: '', creditAccount: '', amount: 0, label: '', autoReverse: true };
  }

  ngOnInit(): void {
    this.api.getInventoryKinds().subscribe({
      next: res => { if (res.success && res.data) this.kinds.set(res.data); },
      error: () => this.error.set('Erreur de chargement des types.')
    });
    this.api.getChartOfAccounts().subscribe({
      next: res => { if (res.success && res.data) this.accounts.set(res.data.filter(a => a.isActive)); }
    });
  }

  selectKind(k: InventoryEntryKindDto): void {
    this.selectedKind.set(k);
    this.form = {
      ...this.emptyForm(),
      kind: k.kind,
      debitAccount: k.defaultDebitAccount,
      creditAccount: k.defaultCreditAccount,
      label: k.label,
      autoReverse: k.autoReverse
    };
  }

  reset(): void {
    const k = this.selectedKind();
    if (k) this.selectKind(k);
  }

  submit(): void {
    if (!this.previewValid() || this.saving()) return;
    this.saving.set(true);
    this.error.set(null);
    this.api.createInventoryEntry({ ...this.form, debitAccount: this.form.debitAccount.trim(), creditAccount: this.form.creditAccount.trim() }).subscribe({
      next: res => {
        this.saving.set(false);
        if (res.success) {
          this.toast.add({
            severity: 'success',
            summary: 'Écriture enregistrée',
            detail: this.form.autoReverse ? "L'écriture d'inventaire et son extourne ont été créées." : "L'écriture d'inventaire a été créée.",
            life: 5000
          });
          this.reset();
        } else {
          this.error.set(res.error ?? "Erreur lors de l'enregistrement.");
        }
      },
      error: () => {
        this.saving.set(false);
        this.error.set('Erreur réseau.');
      }
    });
  }
}
