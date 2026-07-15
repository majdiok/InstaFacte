import {
  Component,
  ViewChild,
  ElementRef,
  AfterViewInit,
  signal,
  computed,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { NgbActiveModal } from '@ng-bootstrap/ng-bootstrap';

@Component({
  selector: 'app-refund-invoice-id-dialog',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="refund-dialog-header">
      <i class="pi pi-file-edit refund-dialog-icon" aria-hidden="true"></i>
      <h5 class="refund-dialog-title" id="refundDialogTitle">Créer un avoir</h5>
      <button
        type="button"
        class="refund-dialog-close"
        (click)="modal.dismiss()"
        aria-label="Fermer"
        tabindex="0"><span aria-hidden="true">&times;</span>
      </button>
    </div>
    <div class="refund-dialog-body">
      <p class="refund-dialog-message">
        Saisissez l'ID ou le numéro de la facture à rembourser.
      </p>
      <label for="refundInvoiceIdInput" class="refund-dialog-label">
        ID ou numéro de facture
      </label>
      <input
        #invoiceIdInput
        id="refundInvoiceIdInput"
        type="text"
        class="refund-dialog-input"
        [ngModel]="invoiceId()"
        (ngModelChange)="invoiceId.set($event)"
        (keydown.enter)="onSubmit()"
        placeholder="Ex. INV-2024-001"
        aria-label="ID ou numéro de la facture à rembourser"
        aria-describedby="refundDialogTitle" />
    </div>
    <div class="refund-dialog-footer">
      <button
        type="button"
        class="refund-dialog-btn reject"
        (click)="modal.dismiss()"
        aria-label="Annuler"
        tabindex="0">
        Annuler
      </button>
      <button
        type="button"
        class="refund-dialog-btn accept"
        [disabled]="!canSubmit()"
        (click)="onSubmit()"
        aria-label="Valider">
        Valider
      </button>
    </div>
  `,
  styles: [`
    :host {
      display: block;
      pointer-events: auto !important;
    }

    .refund-dialog-header,
    .refund-dialog-body,
    .refund-dialog-footer {
      pointer-events: auto !important;
    }

    .refund-dialog-header {
      display: flex;
      align-items: center;
      gap: var(--spacing-3, 0.75rem);
      border-bottom: 1px solid var(--color-border-subtle, #e2e8f0);
      padding: var(--spacing-4, 1rem) var(--spacing-5, 1.25rem);
    }

    .refund-dialog-icon {
      font-size: var(--font-size-2xl, 1.5rem);
      color: var(--color-warning-600, #d97706);
      flex-shrink: 0;
      font-family: 'primeicons';
    }

    .refund-dialog-title {
      flex: 1;
      margin: 0;
      font-size: var(--font-size-lg, 1.125rem);
      font-weight: var(--font-weight-semibold, 600);
      color: var(--color-text-primary, #0f172a);
    }

    .refund-dialog-close {
      width: 32px;
      height: 32px;
      border: none;
      border-radius: var(--radius-md, 0.375rem);
      background: transparent;
      cursor: pointer;
      opacity: 0.6;
      display: flex;
      align-items: center;
      justify-content: center;
      transition: opacity var(--transition-fast, 150ms);
    }

    .refund-dialog-close span {
      font-size: 1.25rem;
      line-height: 1;
      color: var(--color-text-secondary, #475569);
    }

    .refund-dialog-close:hover,
    .refund-dialog-close:focus {
      opacity: 1;
    }

    .refund-dialog-close:focus-visible {
      outline: 2px solid var(--color-primary-500, #3b82f6);
      outline-offset: 2px;
    }

    .refund-dialog-body {
      padding: var(--spacing-5, 1.25rem);
    }

    .refund-dialog-message {
      margin: 0 0 var(--spacing-4, 1rem);
      font-size: var(--font-size-base, 1rem);
      line-height: var(--line-height-normal, 1.5);
      color: var(--color-text-secondary, #475569);
    }

    .refund-dialog-label {
      display: block;
      margin-bottom: var(--spacing-2, 0.5rem);
      font-size: var(--font-size-sm, 0.875rem);
      font-weight: var(--font-weight-medium, 500);
      color: var(--color-text-primary, #0f172a);
    }

    .refund-dialog-input {
      width: 100%;
      padding: var(--spacing-3, 0.75rem) var(--spacing-4, 1rem);
      border: 1px solid var(--color-border-default, #cbd5e1);
      border-radius: var(--radius-lg, 0.5rem);
      font-size: var(--font-size-base, 1rem);
      font-family: var(--font-family);
      color: var(--color-text-primary, #0f172a);
      background: var(--color-white, #ffffff);
      transition: border-color var(--transition-fast, 150ms);
    }

    .refund-dialog-input::placeholder {
      color: var(--color-text-tertiary, #64748b);
    }

    .refund-dialog-input:hover:not(:focus) {
      border-color: var(--color-border-strong, #94a3b8);
    }

    .refund-dialog-input:focus {
      outline: none;
      border-color: var(--color-primary-500, #3b82f6);
      box-shadow: 0 0 0 3px rgba(59, 130, 246, 0.2);
    }

    .refund-dialog-input:focus-visible {
      outline: none;
      border-color: var(--color-primary-500, #3b82f6);
      box-shadow: 0 0 0 3px rgba(59, 130, 246, 0.2);
    }

    .refund-dialog-footer {
      display: flex;
      justify-content: flex-end;
      gap: var(--spacing-3, 0.75rem);
      padding: var(--spacing-4, 1rem) var(--spacing-5, 1.25rem);
      border-top: 1px solid var(--color-border-subtle, #e2e8f0);
      background: var(--color-background-subtle, #f8fafc);
    }

    .refund-dialog-btn {
      pointer-events: auto !important;
      cursor: pointer !important;
      position: relative;
      z-index: 1;
      min-width: 100px;
      padding: var(--spacing-2, 0.5rem) var(--spacing-4, 1rem);
      font-size: var(--font-size-sm, 0.875rem);
      font-weight: var(--font-weight-medium, 500);
      border-radius: var(--radius-md, 0.375rem);
      transition: all var(--transition-fast, 150ms);
      font-family: var(--font-family);
    }

    .refund-dialog-btn.reject {
      background: var(--color-white, #ffffff);
      border: 1px solid var(--color-border-default, #cbd5e1);
      color: var(--color-text-primary, #0f172a);
    }

    .refund-dialog-btn.reject:hover {
      background: var(--color-neutral-100, #f1f5f9);
      border-color: var(--color-border-strong, #94a3b8);
    }

    .refund-dialog-btn.reject:focus-visible {
      outline: 2px solid var(--color-primary-500, #3b82f6);
      outline-offset: 2px;
    }

    .refund-dialog-btn.accept {
      background: var(--color-primary-600, #2563eb);
      border: 1px solid var(--color-primary-600, #2563eb);
      color: white;
    }

    .refund-dialog-btn.accept:hover:not(:disabled) {
      background: var(--color-primary-700, #1d4ed8);
      border-color: var(--color-primary-700, #1d4ed8);
    }

    .refund-dialog-btn.accept:disabled {
      opacity: 0.5;
      cursor: not-allowed;
    }

    .refund-dialog-btn.accept:focus-visible:not(:disabled) {
      outline: 2px solid var(--color-primary-500, #3b82f6);
      outline-offset: 2px;
    }

    @media (max-width: 576px) {
      .refund-dialog-footer {
        flex-direction: column;
      }

      .refund-dialog-footer .refund-dialog-btn {
        width: 100%;
      }
    }
  `],
})
export class RefundInvoiceIdDialogComponent implements AfterViewInit {
  @ViewChild('invoiceIdInput') inputRef!: ElementRef<HTMLInputElement>;

  invoiceId = signal('');

  canSubmit = computed(() => (this.invoiceId() ?? '').trim().length > 0);

  constructor(public modal: NgbActiveModal) {}

  ngAfterViewInit(): void {
    setTimeout(() => {
      const el = this.inputRef?.nativeElement;
      if (el) {
        el.focus();
      }
    }, 0);
  }

  onSubmit(): void {
    if (!this.canSubmit()) return;
    const id = (this.invoiceId() ?? '').trim();
    this.modal.close(id);
  }
}
