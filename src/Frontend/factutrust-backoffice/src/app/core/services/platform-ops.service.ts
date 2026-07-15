import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '@environments/environment';
import type { ApiResponse } from '@core/models/api-response.model';
import type { OpsHealthDto } from '@core/models/platform.models';

@Injectable({ providedIn: 'root' })
export class PlatformOpsService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/platform/ops`;

  health(): Observable<ApiResponse<OpsHealthDto>> {
    return this.http.get<ApiResponse<OpsHealthDto>>(`${this.base}/health`);
  }
}
