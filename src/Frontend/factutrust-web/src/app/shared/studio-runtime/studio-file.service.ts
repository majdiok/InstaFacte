import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, of } from 'rxjs';
import { catchError, map, shareReplay, take } from 'rxjs/operators';
import { environment } from '@environments/environment';

/**
 * Récupération authentifiée des fichiers Studio (Attachment / Signature). Les enregistrements
 * stockent des URLs relatives (/uploads/tenants/{t}/studio/{entityKey}/{fichier}) qui ne sont PLUS
 * servies statiquement : le fichier est téléchargé en blob via l'endpoint authentifié
 * (GET api/studio/records/{entityKey}/files/{fichier} — le Bearer part via l'intercepteur, le
 * serveur contrôle le tenant du contexte) puis affiché via une object URL. Cache par valeur pour
 * la durée de la session ; les échecs ne sont pas mis en cache.
 */
@Injectable({ providedIn: 'root' })
export class StudioFileService {
  private readonly http = inject(HttpClient);
  private readonly cache = new Map<string, Observable<string | null>>();

  /** URL de l'endpoint authentifié pour une valeur stockée, ou null si la forme est inconnue. */
  endpointUrl(value: string): string | null {
    const m = /^\/uploads\/tenants\/[0-9a-fA-F-]{36}\/studio\/([a-z][a-z0-9_]{1,63})\/([0-9a-f]{32}\.[a-z0-9]{2,5})$/
      .exec(value);
    if (!m) return null;
    return `${environment.apiUrl}/studio/records/${m[1]}/files/${m[2]}`;
  }

  /**
   * Object URL (blob:) affichable dans un <img>. Émet null si la valeur est irrécupérable.
   * Les URLs absolues (http…) sont retournées telles quelles.
   */
  objectUrl(value: string): Observable<string | null> {
    const cached = this.cache.get(value);
    if (cached) return cached;

    let obs: Observable<string | null>;
    if (/^https?:/i.test(value)) {
      obs = of(value);
    } else {
      const endpoint = this.endpointUrl(value);
      obs = endpoint
        ? this.http.get(endpoint, { responseType: 'blob' }).pipe(
            map(blob => URL.createObjectURL(blob)),
            catchError(() => {
              this.cache.delete(value); // pas de cache d'échec : une prochaine souscription retentera
              return of(null);
            }),
            shareReplay(1))
        : of(null);
    }
    this.cache.set(value, obs);
    return obs;
  }

  /**
   * Ouvre le fichier dans un nouvel onglet (viewer natif images/PDF). L'onglet est ouvert
   * de façon synchrone pendant le geste utilisateur (anti popup-blocker) puis redirigé
   * vers la blob URL une fois le fichier récupéré.
   */
  open(value: string): void {
    const win = window.open('', '_blank');
    this.objectUrl(value).pipe(take(1)).subscribe(url => {
      if (url && win) {
        win.location.href = url;
      } else {
        win?.close();
      }
    });
  }
}
