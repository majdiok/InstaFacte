import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpContext, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '@environments/environment';
import { ApiResponse } from './auth.service';
import { SKIP_ERROR_TOAST } from '@core/http-context';
import {
  DocumentTemplateCatalogItemDto,
  DocumentTemplatePreferenceDto,
  PrintableDocumentType,
  SaveDocumentTemplateRequest
} from '@features/settings/templates/models/document-template.models';

@Injectable({ providedIn: 'root' })
export class DocumentTemplateService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/settings/document-templates`;
  private readonly skipGlobalErrorUi = new HttpContext().set(SKIP_ERROR_TOAST, true);

  getPreferences(): Observable<ApiResponse<DocumentTemplatePreferenceDto[]>> {
    return this.http.get<ApiResponse<DocumentTemplatePreferenceDto[]>>(this.baseUrl, {
      context: this.skipGlobalErrorUi
    });
  }

  getCatalog(documentType?: PrintableDocumentType): Observable<ApiResponse<DocumentTemplateCatalogItemDto[]>> {
    let params = new HttpParams();
    if (documentType) {
      params = params.set('documentType', documentType);
    }
    return this.http.get<ApiResponse<DocumentTemplateCatalogItemDto[]>>(`${this.baseUrl}/catalog`, {
      params,
      context: this.skipGlobalErrorUi
    });
  }

  save(
    documentType: PrintableDocumentType,
    request: SaveDocumentTemplateRequest
  ): Observable<ApiResponse<DocumentTemplatePreferenceDto>> {
    return this.http.put<ApiResponse<DocumentTemplatePreferenceDto>>(
      `${this.baseUrl}/${documentType}`,
      request,
      { context: this.skipGlobalErrorUi }
    );
  }

  /** Récupère l'aperçu PDF d'un modèle pour un type de document (blob). */
  preview(documentType: PrintableDocumentType, templateKey: string): Observable<Blob> {
    const params = new HttpParams().set('templateKey', templateKey);
    return this.http.get(`${this.baseUrl}/${documentType}/preview`, {
      params,
      responseType: 'blob',
      context: this.skipGlobalErrorUi
    });
  }
}
