import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable, map } from 'rxjs';
import { environment } from '@environments/environment';
import { ApiResponse } from '@core/services/auth.service';
import { CreateContractRequest } from '@core/services/employee.service';
import { createHttpContextSkipGlobalErrorUi } from '@core/http-context';

export type CollaboratorCivility = 1 | 2; // Mrs=1, Mr=2
export type FirmUserRole = 11 | 12; // FirmManager=11, FirmAccountant=12

export interface FirmUserBinome {
  id: string;
  fullName: string;
  email: string;
}

export interface FirmUser {
  id: string;
  email: string;
  firstName: string;
  lastName: string;
  role: FirmUserRole;
  roleDisplay: string;
  isActive: boolean;
  emailConfirmed: boolean;
  civility?: CollaboratorCivility | null;
  qualification?: string | null;
  phoneNumber?: string | null;
  phoneLandline?: string | null;
  useFirmAddress: boolean;
  addressLine?: string | null;
  postalCode?: string | null;
  city?: string | null;
  country?: string | null;
  hasCni: boolean;
  cniUploadedAt?: string | null;
  binomesDisplay?: string | null;
  binomes: FirmUserBinome[];
  payrollEmployeeId?: string | null;
  payrollLinkSourceDisplay?: string | null;
}

export interface FirmAddressSnapshot {
  addressLine: string;
  postalCode?: string | null;
  city: string;
  country: string;
  governorate?: string | null;
}

export interface CreateFirmUserPayload {
  email: string;
  firstName: string;
  lastName: string;
  password?: string | null;
  role: FirmUserRole;
  civility: CollaboratorCivility;
  qualification?: string | null;
  phoneNumber?: string | null;
  phoneLandline?: string | null;
  useFirmAddress: boolean;
  addressLine?: string | null;
  postalCode?: string | null;
  city?: string | null;
  country?: string | null;
  sendInvite?: boolean;
  payroll?: CollaboratorPayrollOnboardingPayload;
}

export interface CollaboratorPayrollOnboardingPayload {
  employeeNumber: string;
  hireDate: string;
  contract: CreateContractRequest;
}

export interface UpdateFirmUserPayload {
  firstName?: string;
  lastName?: string;
  role?: FirmUserRole;
  newPassword?: string;
  civility?: CollaboratorCivility;
  qualification?: string | null;
  phoneNumber?: string | null;
  phoneLandline?: string | null;
  useFirmAddress?: boolean;
  addressLine?: string | null;
  postalCode?: string | null;
  city?: string | null;
  country?: string | null;
}

export interface FirmCollaboratorListFilter {
  name?: string;
  qualification?: string;
  isActive?: boolean | null;
}

@Injectable({ providedIn: 'root' })
export class FirmCollaboratorsService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/firm/users`;

  list(filter: FirmCollaboratorListFilter = {}): Observable<FirmUser[]> {
    let params = new HttpParams();
    if (filter.name) params = params.set('name', filter.name);
    if (filter.qualification) params = params.set('qualification', filter.qualification);
    if (filter.isActive === true || filter.isActive === false) {
      params = params.set('isActive', String(filter.isActive));
    }
    return this.http.get<ApiResponse<FirmUser[]>>(this.base, { params }).pipe(
      map(r => (r.success ? r.data : []))
    );
  }

  getById(id: string): Observable<FirmUser> {
    return this.http.get<ApiResponse<FirmUser>>(`${this.base}/${id}`).pipe(
      map(r => {
        if (!r.success) throw new Error(r.message || 'Collaborateur introuvable');
        return r.data;
      })
    );
  }

  getFirmAddress(): Observable<FirmAddressSnapshot> {
    return this.http.get<ApiResponse<FirmAddressSnapshot>>(`${this.base}/firm-address`).pipe(
      map(r => {
        if (!r.success) throw new Error(r.message || 'Adresse cabinet introuvable');
        return r.data;
      })
    );
  }

  create(payload: CreateFirmUserPayload, cniFile?: File | null): Observable<FirmUser> {
    const context = createHttpContextSkipGlobalErrorUi();
    if (cniFile) {
      const form = new FormData();
      form.append('user', JSON.stringify(payload));
      form.append('file', cniFile, cniFile.name);
      return this.http.post<ApiResponse<FirmUser>>(this.base, form, { context }).pipe(
        map(r => {
          if (!r.success) throw new Error(r.message || 'Création impossible');
          return r.data;
        })
      );
    }
    return this.http.post<ApiResponse<FirmUser>>(this.base, payload, { context }).pipe(
      map(r => {
        if (!r.success) throw new Error(r.message || 'Création impossible');
        return r.data;
      })
    );
  }

  update(id: string, payload: UpdateFirmUserPayload): Observable<FirmUser> {
    return this.http.patch<ApiResponse<FirmUser>>(`${this.base}/${id}`, payload, {
      context: createHttpContextSkipGlobalErrorUi()
    }).pipe(
      map(r => {
        if (!r.success) throw new Error(r.message || 'Mise à jour impossible');
        return r.data;
      })
    );
  }

  setActive(id: string, isActive: boolean): Observable<void> {
    return this.http.patch<ApiResponse<object>>(`${this.base}/${id}/status`, { isActive }, {
      context: createHttpContextSkipGlobalErrorUi()
    }).pipe(
      map(r => {
        if (!r.success) throw new Error(r.message || 'Changement de statut impossible');
      })
    );
  }

  resendInvite(id: string): Observable<void> {
    return this.http.post<ApiResponse<object>>(`${this.base}/${id}/resend-invite`, {}, {
      context: createHttpContextSkipGlobalErrorUi()
    }).pipe(
      map(r => {
        if (!r.success) throw new Error(r.message || 'Renvoi invitation impossible');
      })
    );
  }

  setBinomes(id: string, binomeUserIds: string[]): Observable<void> {
    return this.http.put<ApiResponse<object>>(`${this.base}/${id}/binomes`, { binomeUserIds }, {
      context: createHttpContextSkipGlobalErrorUi()
    }).pipe(
      map(r => {
        if (!r.success) throw new Error(r.message || 'Mise à jour binômes impossible');
      })
    );
  }

  uploadCni(id: string, file: File): Observable<void> {
    const form = new FormData();
    form.append('file', file, file.name);
    return this.http.post<ApiResponse<object>>(`${this.base}/${id}/cni`, form, {
      context: createHttpContextSkipGlobalErrorUi()
    }).pipe(
      map(r => {
        if (!r.success) throw new Error(r.message || 'Upload CNI impossible');
      })
    );
  }

  downloadCni(id: string): Observable<Blob> {
    return this.http.get(`${this.base}/${id}/cni`, { responseType: 'blob' });
  }

  deleteCni(id: string): Observable<void> {
    return this.http.delete<ApiResponse<object>>(`${this.base}/${id}/cni`, {
      context: createHttpContextSkipGlobalErrorUi()
    }).pipe(
      map(r => {
        if (!r.success) throw new Error(r.message || 'Suppression CNI impossible');
      })
    );
  }

  exportExcel(filter: FirmCollaboratorListFilter = {}): Observable<Blob> {
    let params = new HttpParams();
    if (filter.name) params = params.set('name', filter.name);
    if (filter.qualification) params = params.set('qualification', filter.qualification);
    if (filter.isActive === true || filter.isActive === false) {
      params = params.set('isActive', String(filter.isActive));
    }
    return this.http.get(`${this.base}/export`, { params, responseType: 'blob' });
  }
}
