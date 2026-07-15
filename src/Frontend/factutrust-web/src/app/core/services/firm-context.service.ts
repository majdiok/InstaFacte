import { Injectable, inject, signal, computed } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { environment } from '@environments/environment';
import { createHttpContextSkipGlobalErrorUi } from '@core/http-context';
import { AuthService, ApiResponse, AuthResponse } from './auth.service';
import { ErrorHandlerService } from './error-handler.service';
import { ToastService } from './toast.service';

export interface FirmClientDossier {
  assignmentId: string;
  companyTenantId: string;
  companyName: string;
  activeSince: string;
}

export interface FirmContextState {
  clientTenantId?: string;
  clientCompanyName?: string;
  accessMode: 'native' | 'delegated';
}

@Injectable({ providedIn: 'root' })
export class FirmContextService {
  private readonly http = inject(HttpClient);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly toast = inject(ToastService);
  private readonly errorHandler = inject(ErrorHandlerService);
  private readonly baseUrl = `${environment.apiUrl}/firm/context`;
  private readonly contextHttpOptions = { context: createHttpContextSkipGlobalErrorUi() };

  private readonly contextSignal = signal<FirmContextState>({ accessMode: 'native' });

  readonly context = this.contextSignal.asReadonly();
  readonly activeClient = computed(() => {
    const c = this.contextSignal();
    return c.accessMode === 'delegated' && c.clientTenantId
      ? { tenantId: c.clientTenantId, companyName: c.clientCompanyName ?? '' }
      : null;
  });

  syncFromUser(): void {
    const user = this.auth.user();
    if (!user) {
      this.contextSignal.set({ accessMode: 'native' });
      return;
    }
    this.contextSignal.set({
      accessMode: (user.accessMode as 'native' | 'delegated') ?? 'native',
      clientTenantId: user.contextTenantId,
      clientCompanyName: user.contextCompanyName
    });
  }

  async switchClient(clientTenantId: string): Promise<void> {
    try {
      const res = await firstValueFrom(
        this.http.post<ApiResponse<AuthResponse>>(
          `${this.baseUrl}/switch`,
          { clientTenantId },
          this.contextHttpOptions
        )
      );
      if (!res.success || !res.data) {
        throw new Error(res.message ?? 'Impossible d\'ouvrir ce dossier client.');
      }
      this.auth.applyAuthResponse(res.data);
      this.syncFromUser();
    } catch (error) {
      this.showContextError('Impossible d\'ouvrir ce dossier client.', error);
      throw error;
    }
  }

  async clearContext(): Promise<boolean> {
    const user = this.auth.user();
    if (user && user.accessMode !== 'delegated' && !user.contextTenantId) {
      return true;
    }

    try {
      const res = await firstValueFrom(
        this.http.post<ApiResponse<AuthResponse>>(
          `${this.baseUrl}/clear`,
          {},
          this.contextHttpOptions
        )
      );
      if (!res.success || !res.data) {
        this.showContextError('Impossible de quitter le dossier client.');
        return false;
      }
      this.auth.applyAuthResponse(res.data);
      this.syncFromUser();
      const updated = this.auth.user();
      if (updated?.accessMode === 'delegated' || updated?.contextTenantId) {
        this.showContextError('Le contexte cabinet n\'a pas pu être restauré.');
        return false;
      }
      return true;
    } catch (error) {
      this.showContextError('Impossible de quitter le dossier client.', error);
      return false;
    }
  }

  async returnToFirmHome(): Promise<boolean> {
    const cleared = await this.clearContext();
    if (!cleared) {
      return false;
    }
    await this.router.navigate(['/firm/dashboard']);
    return true;
  }

  async navigateToClientList(): Promise<boolean> {
    const cleared = await this.clearContext();
    if (!cleared) {
      return false;
    }
    await this.router.navigate(['/firm/clients']);
    return true;
  }

  private showContextError(fallback: string, error?: unknown): void {
    const detail =
      error instanceof HttpErrorResponse
        ? this.errorHandler.extractErrorMessage(error)
        : error instanceof Error && error.message
          ? error.message
          : fallback;
    this.toast.add({
      severity: 'error',
      summary: 'Erreur',
      detail: detail || fallback
    });
  }
}
