import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable, map } from 'rxjs';
import { environment } from '@environments/environment';
import { createHttpContextSkipGlobalErrorUi } from '@core/http-context';
import { ApiResponse, PagedResult } from '@core/services/client.service';
import { CustomField } from '@shared/studio-runtime/studio-runtime.models';
import { CustomRecord } from '../studio.models';
import { EntityRelationDto } from './studio-relations.models';

/** Ligne d'un lien N-N affichée dans l'onglet « Liés » (2.5e). */
export interface LinkedRecordRow {
  /** Identifiant de l'enregistrement de jonction (utilisé par `unlink`). */
  junctionRecordId: string;
  /** Identifiant de l'enregistrement cible. */
  targetId: string;
  /** Libellé lisible de la cible (`primaryLabel`). */
  targetLabel: string;
  rowVersion?: string | null;
}

/**
 * Libellé lisible d'un enregistrement : première valeur textuelle non vide du `data` selon les
 * champs du schéma (dans l'ordre), sinon `id` tronqué. Réutilisé par l'onglet « Liés » (2.5e2) et
 * les résultats de recherche.
 */
export function primaryLabel(record: CustomRecord, fields: CustomField[]): string {
  const data = record.data ?? {};
  for (const field of fields) {
    const value = data[field.key];
    if (typeof value === 'string' && value.trim().length > 0) return value.trim();
  }
  for (const value of Object.values(data)) {
    if (typeof value === 'string' && value.trim().length > 0) return value.trim();
  }
  return record.id.slice(0, 8);
}

/**
 * Accès aux enregistrements liés (relations plusieurs-à-plusieurs, PR 2.5e). **Aucun endpoint
 * « linked » dédié n'existe côté backend** : le service compose les endpoints CRUD de la jonction :
 * liste = `GET records/{jonction}?filterField=<fieldKey source>&filterValue=<recordId>`, ajout =
 * `POST records/{jonction}` avec les deux clés de la jonction, retrait = `DELETE records/{jonction}/{id}`.
 * Une paire déjà liée renvoie 409 `code: record.duplicate_link` (rendu en ligne par l'appelant,
 * d'où `createHttpContextSkipGlobalErrorUi()` sur l'écriture).
 */
@Injectable({ providedIn: 'root' })
export class StudioLinkedRecordsService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/studio/records`;

  listLinks(rel: EntityRelationDto, recordId: string, page = 1, pageSize = 50): Observable<ApiResponse<PagedResult<CustomRecord>>> {
    if (!rel.junctionEntityKey) throw new Error('listLinks exige une relation many_to_many (junctionEntityKey).');
    const params = new HttpParams()
      .set('page', page)
      .set('pageSize', Math.min(Math.max(pageSize, 1), 200))
      .set('filterField', rel.fieldKey)
      .set('filterValue', recordId);
    return this.http.get<ApiResponse<PagedResult<CustomRecord>>>(`${this.base}/${rel.junctionEntityKey}`, { params });
  }

  /** `link` : 409 `record.duplicate_link` si la paire existe déjà (pas de toast global). */
  link(rel: EntityRelationDto, recordId: string, targetId: string): Observable<ApiResponse<CustomRecord>> {
    if (!rel.junctionEntityKey || !rel.junctionTargetFieldKey) throw new Error('link exige une relation many_to_many.');
    return this.http.post<ApiResponse<CustomRecord>>(
      `${this.base}/${rel.junctionEntityKey}`,
      { data: { [rel.fieldKey]: recordId, [rel.junctionTargetFieldKey]: targetId } },
      { context: createHttpContextSkipGlobalErrorUi() });
  }

  unlink(rel: EntityRelationDto, junctionRecordId: string): Observable<void> {
    if (!rel.junctionEntityKey) throw new Error('unlink exige une relation many_to_many.');
    return this.http.delete<void>(
      `${this.base}/${rel.junctionEntityKey}/${junctionRecordId}`,
      { context: createHttpContextSkipGlobalErrorUi() });
  }

  /** Cibles candidates : `GET records/{cible}?search=…&page=1&pageSize=20` triées par libellé. */
  searchTargets(rel: EntityRelationDto, search: string | null): Observable<ApiResponse<PagedResult<CustomRecord>>> {
    let params = new HttpParams().set('page', 1).set('pageSize', 20);
    if (search) params = params.set('search', search);
    return this.http.get<ApiResponse<PagedResult<CustomRecord>>>(`${this.base}/${rel.targetEntityKey}`, { params });
  }
}
