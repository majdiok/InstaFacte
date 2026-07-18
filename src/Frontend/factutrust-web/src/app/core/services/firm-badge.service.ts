import { Injectable, inject, signal } from '@angular/core';
import { AuthService } from './auth.service';
import { FirmAssignmentService } from './firm-assignment.service';

/**
 * Compteur partagé des invitations en attente côté cabinet (badge sidebar).
 * Source unique consommée par la sidebar ; invalidé par les écrans qui
 * acceptent/refusent une invitation et rafraîchi par le polling notifications.
 */
@Injectable({ providedIn: 'root' })
export class FirmBadgeService {
  private readonly auth = inject(AuthService);
  private readonly assignments = inject(FirmAssignmentService);

  readonly pendingInvitationsCount = signal(0);

  refresh(): void {
    if (!this.auth.isAccountingFirm() || this.auth.isDelegatedMode()) {
      this.pendingInvitationsCount.set(0);
      return;
    }
    this.assignments.getIncomingInvitations().subscribe({
      next: r => {
        if (r.success) this.pendingInvitationsCount.set(r.data.length);
      },
      error: () => {
        /* silencieux : la prochaine invalidation/tick réessaiera */
      }
    });
  }

  /** À appeler après accepter/refuser/annuler pour mettre à jour le badge sans reload. */
  invalidate(): void {
    this.refresh();
  }
}
