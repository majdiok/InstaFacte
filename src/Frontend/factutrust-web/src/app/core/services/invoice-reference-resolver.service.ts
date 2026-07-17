import { Injectable, inject } from '@angular/core';
import { Observable, of, throwError } from 'rxjs';
import { map, switchMap } from 'rxjs/operators';
import {
  InvoiceDetail,
  InvoiceListItem,
  InvoiceService
} from './invoice.service';

const GUID_REGEX =
  /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

const INELIGIBLE_CREDIT_NOTE_STATUSES = new Set(['Annulée', 'Brouillon']);

export interface LinkedInvoiceRef {
  id: string;
  number: string;
  clientName: string;
  status: string;
  totalTTC: number;
}

export function isValidInvoiceGuid(value: string | null | undefined): boolean {
  return !!value && GUID_REGEX.test(value.trim());
}

@Injectable({
  providedIn: 'root'
})
export class InvoiceReferenceResolverService {
  private readonly invoiceService = inject(InvoiceService);

  searchInvoices(query: string): Observable<InvoiceListItem[]> {
    const trimmed = query?.trim() ?? '';
    if (trimmed.length < 1) {
      return of([]);
    }

    return this.invoiceService.getInvoices({ search: trimmed, pageSize: 20 }).pipe(
      map(res => {
        if (!res?.success || !res.data?.items) {
          return [];
        }
        return this.filterEligibleInvoices(res.data.items);
      })
    );
  }

  resolveReference(input: string): Observable<LinkedInvoiceRef> {
    const trimmed = input?.trim() ?? '';
    if (!trimmed) {
      return throwError(() => new Error('Veuillez saisir ou sélectionner une facture.'));
    }

    if (isValidInvoiceGuid(trimmed)) {
      return this.invoiceService.getInvoice(trimmed).pipe(
        switchMap(res => {
          if (!res?.success || !res.data) {
            return throwError(() => new Error('Facture introuvable. Vérifiez le numéro ou sélectionnez une facture dans la liste.'));
          }
          return this.toRefFromDetailOrError(res.data);
        })
      );
    }

    return this.invoiceService.getInvoices({ search: trimmed, pageSize: 20 }).pipe(
      switchMap(res => {
        if (!res?.success || !res.data?.items) {
          return throwError(() => new Error('Facture introuvable. Vérifiez le numéro ou sélectionnez une facture dans la liste.'));
        }

        const eligible = this.filterEligibleInvoices(res.data.items);
        const exact = eligible.find(
          inv => inv.number.localeCompare(trimmed, undefined, { sensitivity: 'accent' }) === 0
        );
        if (exact) {
          return of(this.toRefFromListItem(exact));
        }
        if (eligible.length === 1) {
          return of(this.toRefFromListItem(eligible[0]));
        }
        if (eligible.length === 0) {
          return throwError(() => new Error('Facture introuvable. Vérifiez le numéro ou sélectionnez une facture dans la liste.'));
        }
        return throwError(() => new Error('Plusieurs factures correspondent. Veuillez sélectionner une facture dans la liste.'));
      })
    );
  }

  private filterEligibleInvoices(items: InvoiceListItem[]): InvoiceListItem[] {
    return items.filter(
      inv => !inv.isCreditNote && !INELIGIBLE_CREDIT_NOTE_STATUSES.has(inv.status)
    );
  }

  private toRefFromDetailOrError(detail: InvoiceDetail): Observable<LinkedInvoiceRef> {
    if (detail.isCreditNote) {
      return throwError(() => new Error('Impossible de créer un avoir à partir d\'une facture d\'avoir.'));
    }
    if (INELIGIBLE_CREDIT_NOTE_STATUSES.has(detail.status)) {
      return throwError(() => new Error('Impossible de créer un avoir sur une facture annulée ou en brouillon.'));
    }
    return of({
      id: detail.id,
      number: detail.number,
      clientName: detail.client?.name ?? '',
      status: detail.status,
      totalTTC: Math.abs(detail.totalAmount)
    });
  }

  private toRefFromListItem(item: InvoiceListItem): LinkedInvoiceRef {
    return {
      id: item.id,
      number: item.number,
      clientName: item.clientName,
      status: item.status,
      totalTTC: Math.abs(item.totalAmount)
    };
  }
}
