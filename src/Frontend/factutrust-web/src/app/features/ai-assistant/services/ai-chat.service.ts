import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, map } from 'rxjs';

export interface AiDocumentExtractResponse {
  text: string;
  truncated: boolean;
  fileName: string;
  // ── Champs étendus ──
  format?: string;
  ocrApplied?: boolean;
  pageCount?: number;
  sizeBytes?: number;
  pages?: AiDocumentExtractPage[];
  warnings?: string[];
}

export interface AiDocumentExtractPage {
  pageIndex: number;
  text: string;
  imageBase64?: string;
  width?: number;
  height?: number;
  ocrApplied: boolean;
}
import { environment } from '@environments/environment';
import {
  ConversationDto,
  ConversationDetailDto,
  DailyAiBriefing,
  AiActiveModelDto,
  AiConfiguredStatusDto
} from '../models/ai-chat.models';

interface ApiResponse<T> {
  success: boolean;
  data?: T;
  message?: string;
  errors?: string[];
}

@Injectable({ providedIn: 'root' })
export class AiChatService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/ai`;

  /**
   * Surface cabinet de l'agent Chef de mission. Le scope y est imposé côté serveur : aucun
   * paramètre `scope` n'est envoyé, contrairement à la surface tenant.
   */
  private readonly firmBaseUrl = `${environment.apiUrl}/firm/ai`;

  private resolveBaseUrl(firmSurface: boolean): string {
    return firmSurface ? this.firmBaseUrl : this.baseUrl;
  }

  /** Liste des conversations du scope demandé (0/omis = assistant global, comportement historique). */
  getConversations(scope: number = 0, firmSurface = false): Observable<ConversationDto[]> {
    if (firmSurface) {
      return this.http
        .get<ApiResponse<ConversationDto[]>>(`${this.firmBaseUrl}/conversations`)
        .pipe(map(res => res.data ?? []));
    }

    const options = scope > 0 ? { params: { scope } } : {};
    return this.http
      .get<ApiResponse<ConversationDto[]>>(`${this.baseUrl}/conversations`, options)
      .pipe(map(res => res.data ?? []));
  }

  getConversation(id: string, firmSurface = false): Observable<ConversationDetailDto> {
    return this.http
      .get<ApiResponse<ConversationDetailDto>>(`${this.resolveBaseUrl(firmSurface)}/conversations/${id}`)
      .pipe(map(res => res.data!));
  }

  deleteConversation(id: string, firmSurface = false): Observable<void> {
    return this.http.delete<void>(`${this.resolveBaseUrl(firmSurface)}/conversations/${id}`);
  }

  checkHealth(): Observable<{ available: boolean }> {
    return this.http.get<{ available: boolean }>(`${this.baseUrl}/health`);
  }

  getConfiguredStatus(): Observable<AiConfiguredStatusDto> {
    return this.http
      .get<ApiResponse<AiConfiguredStatusDto>>(`${this.baseUrl}/configured-status`)
      .pipe(
        map(
          res =>
            res.data ?? {
              hasOllamaModels: false,
              hasCloudProvider: false,
              isFullyConfigured: false
            }
        )
      );
  }

  /** Pre-warm the tenant model so it's loaded in memory before the user sends a message. */
  warmUp(): Observable<{ warmed: boolean }> {
    return this.http.post<{ warmed: boolean }>(`${this.baseUrl}/warm-up`, {});
  }

  getDailyBriefing(): Observable<DailyAiBriefing> {
    return this.http
      .get<ApiResponse<DailyAiBriefing>>(`${this.baseUrl}/daily-briefing`)
      .pipe(map(res => res.data!));
  }

  extractDocument(file: File, options?: { renderImages?: boolean }): Observable<AiDocumentExtractResponse> {
    const fd = new FormData();
    fd.append('file', file, file.name);
    const renderImages = options?.renderImages === true;
    const url = renderImages
      ? `${this.baseUrl}/document-extract?renderImages=true`
      : `${this.baseUrl}/document-extract`;
    return this.http
      .post<ApiResponse<AiDocumentExtractResponse>>(url, fd)
      .pipe(map(res => res.data!));
  }

  /** The AI model effectively used by the assistant (configured in the platform back-office). */
  getActiveModel(): Observable<AiActiveModelDto> {
    return this.http
      .get<ApiResponse<AiActiveModelDto>>(`${this.baseUrl}/active-model`)
      .pipe(map(res => res.data!));
  }
}

