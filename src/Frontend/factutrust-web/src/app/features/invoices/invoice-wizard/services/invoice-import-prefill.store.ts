import { Injectable } from '@angular/core';
import { InvoiceImportResult } from '../models/invoice-import.models';

/**
 * Relais transitoire en mémoire entre la modale d'import et le wizard de facture.
 *
 * La modale dépose ici le résultat extrait par l'IA puis navigue vers /invoices/new.
 * Le wizard, dans ngOnInit (après reset()), consomme la donnée pour se pré-remplir.
 *
 * Consommation unique : `consume()` vide le store, ce qui évite tout pré-remplissage
 * parasite si l'utilisateur recharge ou revient sur /invoices/new sans réimporter.
 */
@Injectable({ providedIn: 'root' })
export class InvoiceImportPrefillStore {
  private pending: InvoiceImportResult | null = null;

  /** Dépose un résultat d'import à appliquer au prochain chargement du wizard. */
  set(result: InvoiceImportResult): void {
    this.pending = result;
  }

  /** Récupère et efface le résultat en attente (null si aucun). */
  consume(): InvoiceImportResult | null {
    const result = this.pending;
    this.pending = null;
    return result;
  }

  /** True si un import est en attente d'application. */
  get hasPending(): boolean {
    return this.pending !== null;
  }
}
