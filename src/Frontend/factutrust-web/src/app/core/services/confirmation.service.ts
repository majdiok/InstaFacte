import { Injectable, inject } from '@angular/core';
import { NgbModal } from '@ng-bootstrap/ng-bootstrap';
import { ConfirmModalComponent } from '@shared/components/confirm-modal/confirm-modal.component';

export interface Confirmation {
  message?: string;
  header?: string;
  icon?: string;
  acceptLabel?: string;
  rejectLabel?: string;
  acceptButtonStyleClass?: string;
  accept?: () => void;
  reject?: () => void;
}

export interface AlertConfig {
  message: string;
  header?: string;
  icon?: string;
  /** Modal width (default `sm`). Use `md` for longer validation messages. */
  size?: 'sm' | 'md' | 'lg';
  /** Scrollable body for long content (ng-bootstrap). */
  scrollable?: boolean;
}

/**
 * Service de confirmation pour afficher des modales de confirmation.
 * Utilise ng-bootstrap pour garantir la compatibilité avec le design system.
 */
@Injectable({ providedIn: 'root' })
export class ConfirmationService {
  private modal = inject(NgbModal);

  /**
   * Affiche une modale de confirmation avec un message et des actions.
   * 
   * @param confirmation Configuration de la modale (message, labels, callbacks, etc.)
   * 
   * @example
   * confirmationService.confirm({
   *   message: 'Êtes-vous sûr de vouloir supprimer cet élément ?',
   *   header: 'Confirmation de suppression',
   *   icon: 'pi pi-exclamation-triangle',
   *   acceptLabel: 'Supprimer',
   *   rejectLabel: 'Annuler',
   *   acceptButtonStyleClass: 'p-button-danger',
   *   accept: () => { this.deleteItem(); },
   *   reject: () => { console.log('Cancelled'); }
   * });
   */
  confirm(confirmation: Confirmation): void {
    const ref = this.modal.open(ConfirmModalComponent, {
      container: 'body', // Garantit que la modal est attachée au body (au-dessus de tout)
      centered: true,
      backdrop: 'static', // Empêche la fermeture en cliquant sur le backdrop
      keyboard: true, // Permet la fermeture avec ESC
      size: 'sm',
      windowClass: 'confirm-modal-window', // Classe personnalisée pour le z-index
      modalDialogClass: 'confirm-modal-dialog', // Classe pour le dialog
      scrollable: false
    });

    // Configuration du composant modal
    ref.componentInstance.message = confirmation.message ?? 'Êtes-vous sûr ?';
    ref.componentInstance.header = confirmation.header ?? 'Confirmation';
    ref.componentInstance.icon = confirmation.icon ?? '';
    ref.componentInstance.acceptLabel = confirmation.acceptLabel ?? 'Oui';
    ref.componentInstance.rejectLabel = confirmation.rejectLabel ?? 'Non';
    ref.componentInstance.acceptButtonStyleClass = confirmation.acceptButtonStyleClass ?? 'btn-primary';

    // Gestion des résultats
    ref.result.then(
      // Accepté (modal.close() appelé)
      () => {
        try {
          confirmation.accept?.();
        } catch (error) {
          console.error('[ConfirmationService] Error in accept callback:', error);
        }
      },
      // Rejeté (modal.dismiss() appelé ou ESC pressé)
      (reason) => {
        try {
          confirmation.reject?.();
        } catch (error) {
          console.error('[ConfirmationService] Error in reject callback:', error);
        }
      }
    ).catch((error) => {
      // Gestion des erreurs non prévues
      console.error('[ConfirmationService] Unexpected error:', error);
      try {
        confirmation.reject?.();
      } catch (callbackError) {
        console.error('[ConfirmationService] Error in reject callback after error:', callbackError);
      }
    });
  }

  /**
   * Affiche une modale d'alerte avec un message (un seul bouton OK).
   * Utilisé pour les erreurs métier (ex. impossible de supprimer car factures liées).
   */
  alert(config: AlertConfig): void {
    const ref = this.modal.open(ConfirmModalComponent, {
      container: 'body',
      centered: true,
      backdrop: 'static',
      keyboard: true,
      size: config.size ?? 'sm',
      windowClass: 'confirm-modal-window',
      modalDialogClass: 'confirm-modal-dialog',
      scrollable: config.scrollable ?? false
    });

    ref.componentInstance.message = config.message;
    ref.componentInstance.header = config.header ?? 'Erreur';
    ref.componentInstance.icon = config.icon ?? 'pi pi-exclamation-triangle';
    ref.componentInstance.acceptLabel = 'OK';
    ref.componentInstance.acceptButtonStyleClass = 'btn-primary';
    ref.componentInstance.showRejectButton = false;

    ref.result.then(
      () => {},
      () => {}
    ).catch(() => {});
  }
}
