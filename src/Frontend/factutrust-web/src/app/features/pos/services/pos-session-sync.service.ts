import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, of } from 'rxjs';
import { catchError, map } from 'rxjs/operators';
import { environment } from '@environments/environment';
import { PosStateService, PosState } from './pos-state.service';

const STORAGE_KEY = 'factutrust_pos_cloud_draft';

@Injectable({
  providedIn: 'root'
})
export class PosSessionSyncService {
  private readonly http = inject(HttpClient);
  private readonly posState = inject(PosStateService);
  private readonly apiUrl = `${environment.apiUrl}/pos`;

  saveToCloud(): Observable<boolean> {
    const snapshot = this.posState.getSnapshot();
    if (snapshot.lines.length === 0) return of(false);
    return this.http
      .post<{ success: boolean }>(`${this.apiUrl}/session`, { state: snapshot })
      .pipe(
        map(() => true),
        catchError(() => {
          try {
            localStorage.setItem(STORAGE_KEY, JSON.stringify(snapshot));
          } catch {
            // ignore
          }
          return of(true);
        })
      );
  }

  getFromCloud(): Observable<PosState | null> {
    return this.http.get<{ data?: { state?: PosState } }>(`${this.apiUrl}/session`).pipe(
      map(res => res?.data?.state ?? null),
      catchError(() => {
        try {
          const raw = localStorage.getItem(STORAGE_KEY);
          if (raw) return of(JSON.parse(raw) as PosState);
        } catch {
          // ignore
        }
        return of(null);
      })
    );
  }

  hasCloudSession(): Observable<boolean> {
    return this.getFromCloud().pipe(map(s => !!s && (s.lines?.length ?? 0) > 0));
  }

  clearCloudSession(): void {
    this.http.post(`${this.apiUrl}/session/clear`, {}).pipe(catchError(() => of(null))).subscribe(() => {});
    try {
      localStorage.removeItem(STORAGE_KEY);
    } catch {
      // ignore
    }
  }
}
