import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { NgbActiveModal } from '@ng-bootstrap/ng-bootstrap';

const REASON_MAX = 500;

/**
 * Modal de refus d'une invitation : le cabinet peut saisir un motif
 * (facultatif) qui sera visible par la société. Résout `modal.close(reason)`
 * à la confirmation, `dismiss()` à l'annulation.
 */
@Component({
  selector: 'app-reject-invitation-dialog',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="modal-header rij-header">
      <i class="pi pi-times-circle rij-icon" aria-hidden="true"></i>
      <h5 class="modal-title">Refuser la demande</h5>
      <button type="button" class="btn-close" (click)="modal.dismiss()" aria-label="Fermer"></button>
    </div>
    <div class="modal-body rij-body">
      <p class="rij-lead">
        Refuser la demande de liaison de <strong>{{ companyName }}</strong> ?
      </p>
      <label for="rejectReason" class="rij-label">
        Motif <span class="rij-optional">(facultatif, visible par la société)</span>
      </label>
      <textarea
        id="rejectReason"
        [(ngModel)]="reason"
        [maxlength]="REASON_MAX"
        rows="3"
        class="rij-textarea"
        placeholder="Expliquez la raison du refus (dossier incomplet, cabinet complet...)"></textarea>
      <small class="rij-hint">{{ reason.length }} / {{ REASON_MAX }}</small>
    </div>
    <div class="modal-footer rij-footer">
      <button type="button" class="btn btn-secondary" (click)="modal.dismiss()">Annuler</button>
      <button type="button" class="btn btn-danger" (click)="modal.close(reason.trim())">Refuser</button>
    </div>
  `,
  styles: [`
    .rij-header {
      display: flex;
      align-items: center;
      gap: var(--spacing-3, 0.75rem);
      border-bottom: 1px solid var(--color-border-subtle, #e2e8f0);
      padding: var(--spacing-4, 1rem) var(--spacing-5, 1.25rem);
    }
    .rij-icon { font-size: 1.4rem; color: var(--color-danger-600, #dc2626); }
    .modal-title { flex: 1; margin: 0; font-size: 1.125rem; font-weight: 600; color: var(--color-text-primary, #0f172a); }
    .rij-body { padding: var(--spacing-5, 1.25rem); }
    .rij-lead { margin: 0 0 var(--spacing-4, 1rem); color: var(--color-text-secondary, #475569); }
    .rij-label { display: block; font-size: 0.875rem; font-weight: 500; margin-bottom: 6px; color: var(--color-text-primary, #0f172a); }
    .rij-optional { color: var(--color-text-tertiary, #94a3b8); font-weight: 400; }
    .rij-textarea {
      width: 100%;
      resize: vertical;
      padding: 0.6rem 0.75rem;
      border: 1px solid var(--color-border-default, #cbd5e1);
      border-radius: var(--radius-md, 0.5rem);
      font-family: inherit;
      font-size: 0.875rem;
    }
    .rij-textarea:focus {
      outline: none;
      border-color: var(--color-danger-400, #f87171);
      box-shadow: 0 0 0 3px rgba(220, 38, 38, 0.12);
    }
    .rij-hint { display: block; margin-top: 4px; font-size: 0.75rem; color: var(--color-text-tertiary, #94a3b8); }
    .rij-footer {
      display: flex;
      justify-content: flex-end;
      gap: var(--spacing-3, 0.75rem);
      padding: var(--spacing-4, 1rem) var(--spacing-5, 1.25rem);
      border-top: 1px solid var(--color-border-subtle, #e2e8f0);
      background: var(--color-background-subtle, #f8fafc);
    }
    .rij-footer .btn { min-width: 100px; padding: 0.5rem 1rem; font-size: 0.875rem; font-weight: 500; border-radius: var(--radius-md, 0.375rem); cursor: pointer; }
    .btn-secondary { background: #fff; border: 1px solid var(--color-border-default, #cbd5e1); color: var(--color-text-primary, #0f172a); }
    .btn-danger { background: var(--color-danger-600, #dc2626); border: 1px solid var(--color-danger-600, #dc2626); color: #fff; }
    .btn-danger:hover { background: var(--color-danger-700, #b91c1c); }
  `]
})
export class RejectInvitationDialogComponent {
  @Input() companyName = '';
  readonly REASON_MAX = REASON_MAX;
  reason = '';

  constructor(public modal: NgbActiveModal) {}
}
