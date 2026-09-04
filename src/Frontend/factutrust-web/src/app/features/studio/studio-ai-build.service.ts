import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, firstValueFrom } from 'rxjs';
import { environment } from '@environments/environment';
import { AuthService } from '@core/services/auth.service';
import { ApiResponse } from '@core/services/client.service';
import { ChatStreamEvent } from '@features/ai-assistant/models/ai-chat.models';
import { ReportResult } from '@shared/studio-runtime/studio-runtime.models';

/** Étape de l'aperçu (checklist) d'un plan Studio IA. */
export interface StudioPlanStep {
  key: string;
  label: string;
  detail: string;
}

export interface StudioPlanEntity {
  displayName: string;
  fieldCount: number;
  relationCount: number;
}

/** Contenu de `summaryJson` — miroir de `StudioAiPlanSummary.PlanSummary` côté backend. */
export interface StudioPlanSummary {
  kind: string;
  title: string;
  steps: StudioPlanStep[];
  entities: StudioPlanEntity[];
  warnings: string[];
  /** Présent pour un plan d'ÉTAT : quelques lignes réelles, pour valider sur des chiffres. */
  sample?: ReportResult | null;
}

/** Payload de l'événement SSE `studio_report_result` (retour de l'outil studio_run_report). */
export interface StudioReportResultEvent {
  success: boolean;
  title: string;
  source: string;
  sourceLabel: string;
  preset?: string | null;
  result: ReportResult;
  warnings: string[];
  message: string;
}

/** Une reformulation proposée après un échec d'état. */
export interface StudioReportSuggestion {
  preset: string;
  label: string;
  prompt: string;
}

/** Payload de l'événement SSE `studio_report_error` — miroir de `StudioReportFailurePayload`. */
export interface StudioReportFailureEvent {
  message: string;
  preset?: string | null;
  title?: string | null;
  periodLabel?: string | null;
  suggestions: StudioReportSuggestion[];
}

/** Payload de l'événement SSE `studio_plan` (retour de l'outil studio_plan_*). */
export interface StudioPlanEvent {
  success: boolean;
  requiresConfirmation: boolean;
  planId: string;
  kind: string;
  status: string;
  expiresAt: string;
  summary: StudioPlanSummary;
  message: string;
}

/** Miroir de `StudioAiPlanDto`. */
export interface StudioAiPlanDto {
  id: string;
  kind: string;
  status: string;
  summaryJson: string;
  resultJson?: string;
  errorMessage?: string;
  createdAt: string;
  expiresAt: string;
  executedAt?: string;
}

/**
 * Cycle de vie des plans Studio IA. La confirmation est un POST SSE : l'exécution est
 * déterministe côté serveur (aucun LLM) et la progression arrive étape par étape.
 */
@Injectable({ providedIn: 'root' })
export class StudioAiBuildService {
  private readonly http = inject(HttpClient);
  private readonly auth = inject(AuthService);
  private readonly baseUrl = `${environment.apiUrl}/studio/ai/plans`;

  getPlan(planId: string): Observable<ApiResponse<StudioAiPlanDto>> {
    return this.http.get<ApiResponse<StudioAiPlanDto>>(`${this.baseUrl}/${planId}`);
  }

  cancel(planId: string): Observable<ApiResponse<StudioAiPlanDto>> {
    return this.http.post<ApiResponse<StudioAiPlanDto>>(`${this.baseUrl}/${planId}/cancel`, {});
  }

  /**
   * Confirme le plan et streame l'exécution : `studio_progress` (étapes live),
   * puis `studio_result` ou `error`, et enfin `done`.
   */
  confirm(planId: string): Observable<ChatStreamEvent> {
    return new Observable(subscriber => {
      const abort = new AbortController();
      const url = `${this.baseUrl}/${planId}/confirm`;

      const run = async (): Promise<void> => {
        // Hors pipeline d'intercepteurs Angular (fetch streaming) : on reproduit le
        // 401 → refresh → retry unique, comme AiStreamService.
        let response = await this.post(url, abort.signal);
        if (response.status === 401 && (await this.tryRefreshToken())) {
          response = await this.post(url, abort.signal);
        }
        if (!response.ok) {
          subscriber.error(new Error(this.httpMessage(response.status, response.statusText)));
          return;
        }

        const reader = response.body?.getReader();
        if (!reader) {
          subscriber.error(new Error('Réponse sans contenu.'));
          return;
        }

        const decoder = new TextDecoder();
        let buffer = '';

        while (true) {
          const { done, value } = await reader.read();
          if (done) break;

          buffer += decoder.decode(value, { stream: true });
          const lines = buffer.split('\n');
          buffer = lines.pop() || '';

          for (const line of lines) {
            const trimmed = line.trim();
            if (!trimmed.startsWith('data: ')) continue;
            const jsonStr = trimmed.slice(6);
            if (!jsonStr) continue;
            try {
              const event: ChatStreamEvent = JSON.parse(jsonStr);
              subscriber.next(event);
              if (event.type === 'done') {
                subscriber.complete();
                return;
              }
            } catch {
              // Ligne SSE malformée : ignorée (le flux continue).
            }
          }
        }
        subscriber.complete();
      };

      run().catch((err: unknown) => {
        if ((err as Error | undefined)?.name !== 'AbortError') subscriber.error(err);
      });

      return () => abort.abort();
    });
  }

  private post(url: string, signal: AbortSignal): Promise<Response> {
    const token = this.auth.getAccessToken();
    return fetch(url, {
      method: 'POST',
      headers: token ? { Authorization: `Bearer ${token}` } : {},
      signal
    });
  }

  private async tryRefreshToken(): Promise<boolean> {
    try {
      const res = await firstValueFrom(this.auth.refreshToken());
      return !!(res?.success && res?.data);
    } catch {
      return false;
    }
  }

  private httpMessage(status: number, statusText: string): string {
    if (status === 401) return 'Session expirée. Reconnectez-vous, puis relancez la validation.';
    if (status === 403) return "Vous n'avez pas la permission de créer des tables Studio.";
    if (status === 404) return 'Ce plan est introuvable ou a expiré.';
    if (status === 409) return 'Ce plan a déjà été traité.';
    return `HTTP ${status} : ${statusText}`;
  }
}
