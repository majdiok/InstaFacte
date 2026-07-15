import { Injectable, signal } from '@angular/core';

/** Tracks tenant-level infrastructure errors surfaced by the API (e.g. migration failures). */
@Injectable({ providedIn: 'root' })
export class TenantSystemStatusService {
  private readonly _tenantMigrationFailed = signal(false);
  readonly tenantMigrationFailed = this._tenantMigrationFailed.asReadonly();

  reportTenantMigrationFailure(): void {
    this._tenantMigrationFailed.set(true);
  }

  clearTenantMigrationFailure(): void {
    this._tenantMigrationFailed.set(false);
  }
}