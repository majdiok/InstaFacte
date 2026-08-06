import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, throwError, of } from 'rxjs';
import { catchError, map, shareReplay, tap, finalize } from 'rxjs/operators';
import { environment } from '@environments/environment';
import { createHttpContextSkipGlobalErrorUi } from '@core/http-context';

export interface CompanyAddress {
  street: string;
  streetLine2: string | null;
  city: string;
  postalCode: string | null;
  governorate: string;
  country: string;
  fullAddress: string;
}

export interface Company {
  id: string;
  companyName: string;
  tradeName: string | null;
  nif: string;
  commerceRegistry: string | null;
  taxRegime: number;
  taxRegimeDisplay: string;
  address: CompanyAddress;
  email: string;
  phone: string;
  website: string | null;
  logoUrl: string | null;
  bankName: string | null;
  rib: string | null;
  iban: string | null;
  invoicePrefix: string | null;
  defaultPaymentTerms: string | null;
  invoiceFooter: string | null;
  warehouseName: string | null;
  cnssEmployerNumber: string | null;
}

export interface UpdateCompanyRequest {
  companyName: string;
  tradeName?: string | null;
  nif: string;
  commerceRegistry?: string | null;
  taxRegime: number;
  street: string;
  streetLine2?: string | null;
  city: string;
  postalCode?: string | null;
  governorate: string;
  email: string;
  phone: string;
  website?: string | null;
  logoUrl?: string | null;
  bankName?: string | null;
  rib?: string | null;
  iban?: string | null;
  invoicePrefix?: string | null;
  defaultPaymentTerms?: string | null;
  invoiceFooter?: string | null;
  cnssEmployerNumber?: string | null;
}

export interface ApiResponse<T> {
  success: boolean;
  data: T;
  message: string | null;
  errors: string[];
}

@Injectable({
  providedIn: 'root'
})
export class CompanyService {
  private readonly API_URL = `${environment.apiUrl}/company`;
  private http = inject(HttpClient);
  
  // Cache simple pour éviter les requêtes inutiles
  private cachedCompany: ApiResponse<Company> | null = null;
  private cacheTimestamp: number = 0;
  private readonly CACHE_DURATION_MS = 30000; // 30 secondes de cache
  
  // Protection contre les requêtes simultanées multiples
  private getCompanyRequest$: Observable<ApiResponse<Company>> | null = null;

  /**
   * Récupère les informations de l'entreprise avec cache (30 s). Pas de retry sur 429.
   */
  getCompany(forceRefresh: boolean = false): Observable<ApiResponse<Company>> {
    // Vérifier le cache si pas de force refresh
    if (!forceRefresh && this.cachedCompany && (Date.now() - this.cacheTimestamp) < this.CACHE_DURATION_MS) {
      return of(this.cachedCompany);
    }
    
    // Si une requête est déjà en cours, réutiliser la même observable
    if (this.getCompanyRequest$) {
      return this.getCompanyRequest$;
    }
    
    // Créer une nouvelle requête avec partage pour éviter les duplications
    this.getCompanyRequest$ = this.http.get<ApiResponse<Company>>(this.API_URL).pipe(
      tap((response) => {
        // Mettre à jour le cache lors du succès
        if (response.success && response.data) {
          this.cachedCompany = response;
          this.cacheTimestamp = Date.now();
        }
      }),
      shareReplay({ bufferSize: 1, refCount: true }), // Partager le résultat et nettoyer automatiquement
      catchError((error) => {
        // Réinitialiser la requête en cours en cas d'erreur
        this.getCompanyRequest$ = null;
        return throwError(() => error);
      }),
      finalize(() => {
        // Réinitialiser la requête en cours quand tous les subscribers sont partis
        // refCount: true dans shareReplay s'en charge automatiquement
        // On réinitialise ici pour permettre une nouvelle requête si nécessaire
        setTimeout(() => {
          this.getCompanyRequest$ = null;
        }, 100);
      })
    );
    
    return this.getCompanyRequest$;
  }

  /**
   * Charge les infos société pour affichage sans bloquer l'écran ni déclencher la modale 403.
   * Retourne null si l'appel échoue (ex. cabinet délégué sans settings:read).
   */
  getCompanyForDisplay(): Observable<Company | null> {
    const context = createHttpContextSkipGlobalErrorUi();
    return this.http.get<ApiResponse<Company>>(this.API_URL, { context }).pipe(
      map(res => (res.success && res.data ? res.data : null)),
      catchError(() => of(null))
    );
  }

  /**
   * Met à jour les informations de l'entreprise. Invalide le cache après succès.
   */
  updateCompany(request: UpdateCompanyRequest): Observable<ApiResponse<Company>> {
    return this.http.put<ApiResponse<Company>>(this.API_URL, request);
  }
  
  /**
   * Invalide le cache des données de l'entreprise.
   * Utile après une mise à jour réussie pour forcer un refresh.
   */
  invalidateCache(): void {
    this.cachedCompany = null;
    this.cacheTimestamp = 0;
  }
}
