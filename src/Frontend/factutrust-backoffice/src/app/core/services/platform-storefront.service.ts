import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '@environments/environment';
import type { ApiResponse } from '@core/models/api-response.model';

/**
 * DTO complet d'une vitrine côté backoffice plateforme (Lot A4).
 *
 * Champs étendus depuis le DTO backend `StorefrontProfileDto` :
 * brand colors, logo, cover image, catégorie, thème, contacts secondaires,
 * dates de cycle de vie, motifs de refus / suspension, version de consentement.
 */
export interface PlatformStorefrontProfileDto {
  id: string;
  tenantId: string;
  slug: string;
  displayName: string;
  tagline?: string | null;
  descriptionMarkdown?: string | null;
  brandPrimaryColorHex: string;
  brandSecondaryColorHex: string;
  publicLogoUrl?: string | null;
  publicCoverImageUrl?: string | null;
  /** Enum (string camelCase ou int 0..6) — voir StorefrontCategory côté backend */
  category: string | number;
  /** Enum (string camelCase ou int) — voir FacadeTheme côté backend */
  facadeTheme: string | number;
  /** Enum API : chaîne camelCase (`pendingReview`) ou entier 0–3 selon client. */
  status: string | number;
  publicContactEmail: string;
  publicContactPhone?: string | null;
  publicContactWhatsApp?: string | null;
  orderSubmissionEnabled?: boolean;
  streetPositionIndex?: number | null;
  publishedAt?: string | null;
  suspendedAt?: string | null;
  rejectionReason?: string | null;
  suspensionReason?: string | null;
  consentVersion: string;
  consentAcceptedAt?: string;
}

/** Statistiques agrégées pour la page Vitrines 3D (Lot A4). */
export interface StorefrontStatsDto {
  pending: number;
  published: number;
  rejected: number;
  suspended: number;
}

/** Filtres de listing reconnus par l'API : pending | published | rejected | suspended | draft. */
export type StorefrontListFilter = 'pending' | 'published' | 'rejected' | 'suspended' | 'draft';

@Injectable({ providedIn: 'root' })
export class PlatformStorefrontService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/platform/storefronts`;

  /** Listing legacy : vitrines en attente de validation. */
  listPending(): Observable<ApiResponse<PlatformStorefrontProfileDto[]>> {
    return this.http.get<ApiResponse<PlatformStorefrontProfileDto[]>>(`${this.base}/pending`);
  }

  /** Lot A4 : listing par statut métier. */
  listByStatus(filter: StorefrontListFilter): Observable<ApiResponse<PlatformStorefrontProfileDto[]>> {
    const params = new HttpParams().set('filter', filter);
    return this.http.get<ApiResponse<PlatformStorefrontProfileDto[]>>(`${this.base}/by-status`, {
      params
    });
  }

  /** Lot A4 : KPIs agrégés (pending / published / rejected / suspended). */
  stats(): Observable<ApiResponse<StorefrontStatsDto>> {
    return this.http.get<ApiResponse<StorefrontStatsDto>>(`${this.base}/stats`);
  }

  approve(id: string): Observable<ApiResponse<PlatformStorefrontProfileDto>> {
    return this.http.post<ApiResponse<PlatformStorefrontProfileDto>>(`${this.base}/${id}/approve`, {});
  }

  reject(id: string, reason: string): Observable<ApiResponse<PlatformStorefrontProfileDto>> {
    return this.http.post<ApiResponse<PlatformStorefrontProfileDto>>(`${this.base}/${id}/reject`, {
      reason
    });
  }
}
