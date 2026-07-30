import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '@environments/environment';
import { createHttpContextSkipGlobalErrorUi } from '@core/http-context';
import { ApiResponse } from './client.service';

/** Origine du prix retenu par le serveur. */
export type PriceSource = 'Catalog' | 'PriceList' | 'ClientPrice';

export interface ResolvedPrice {
  unitPriceHT: number;
  currency: string;
  source: PriceSource;
  /** Vrai si le prix vient d'une grille ou d'un accord client, et non du catalogue. */
  isNegotiated: boolean;
}

export interface ResolvedPriceLine extends ResolvedPrice {
  productId: string;
}

export interface ResolvePriceItem {
  productId: string;
  quantity: number;
}

/**
 * Interroge le point de résolution de prix unique du serveur.
 *
 * A appeler avant d'afficher un prix de ligne : le serveur applique la priorite
 * prix negocie -> grille du client -> catalogue. Afficher le prix catalogue sans
 * demander laisserait l'ecran divergent de la facture finalement emise.
 */
@Injectable({ providedIn: 'root' })
export class PricingService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/pricing`;

  /** Prix applicable pour un produit. `clientId` absent = vente sans client identifie. */
  resolve(
    productId: string,
    clientId: string | null,
    quantity = 1,
    date?: string
  ): Observable<ApiResponse<ResolvedPrice>> {
    let params = new HttpParams()
      .set('productId', productId)
      .set('quantity', String(quantity));

    if (clientId) {
      params = params.set('clientId', clientId);
    }
    if (date) {
      params = params.set('date', date);
    }

    return this.http.get<ApiResponse<ResolvedPrice>>(`${this.baseUrl}/resolve`, {
      params,
      context: createHttpContextSkipGlobalErrorUi()
    });
  }

  /**
   * Prix applicables pour plusieurs produits en une requete — utilise par la caisse pour
   * retarifer un ticket entier lorsqu'il est rattache a un client.
   */
  resolveBatch(
    items: ResolvePriceItem[],
    clientId: string | null,
    date?: string
  ): Observable<ApiResponse<ResolvedPriceLine[]>> {
    return this.http.post<ApiResponse<ResolvedPriceLine[]>>(
      `${this.baseUrl}/resolve-batch`,
      { clientId, items, date },
      { context: createHttpContextSkipGlobalErrorUi() }
    );
  }
}
