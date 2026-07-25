import { Component, OnInit, ViewChild, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Table, TableModule } from 'primeng/table';
import { InputTextModule } from 'primeng/inputtext';
import { PageHeaderComponent } from '@shared/components/page-header/page-header.component';
import { ButtonComponent } from '@shared/components/button/button.component';
import { AccountingService, JournalEntryAttachmentDto } from '../services/accounting.service';
import { AccountingStatusBannerComponent } from '../shared/accounting-status-banner.component';
import {
  firstDayOfMonthLocalYmd,
  parseLocalDateString,
  todayLocalYmd,
  validateDateRange
} from '../shared/accounting-date-utils';
import { AccountingMonitoringService } from '../shared/accounting-monitoring.service';
import { AnalyzeWithAiButtonComponent } from '@features/ai-assistant/components/analyze-with-ai-button/analyze-with-ai-button.component';
import { wrapLegacyAnalyzePayload } from '@features/ai-assistant/utils/ai-screen-payload.factory';
import { AccountingFilterBarComponent } from '../shared/accounting-filter-bar.component';
import { AccountingExportMenuComponent } from '../shared/accounting-export-menu.component';
import { AccountingToolbarActionsComponent } from '../shared/accounting-toolbar-actions.component';
import { AccountingTableActionsComponent } from '../shared/accounting-table-actions.component';
import { AccountingExportFormat, downloadBlob, exportExtension } from '../shared/accounting-download.util';
import { AuthService } from '@core/services/auth.service';
import { canValidateAccountingEntries } from '@core/utils/accounting-access';
import { AccountingJournalCatalogService } from '../shared/accounting-journal-catalog.service';
import { AccountingJournalTab } from '../shared/accounting-journal-tabs.model';

type JournalFlatRow = {
  entryId: string;
  date: string;
  journal: string;
  piece: number;
  label: string;
  account: string;
  debit: number;
  credit: number;
  status: number;
  isDraft: boolean;
  isReversed: boolean;
  /** Référence de la pièce externe de l'écriture (facultative). */
  pieceRef: string | null;
  /** Nombre de pièces jointes (GED) de l'écriture. */
  attachmentCount: number;
  /** Vrai sur la première ligne d'une écriture : porte le badge de statut et l'action de validation. */
  firstOfEntry: boolean;
};

@Component({
  selector: 'app-accounting-journal',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    TableModule,
    InputTextModule,
    PageHeaderComponent,
    ButtonComponent,
    AccountingStatusBannerComponent,
    AnalyzeWithAiButtonComponent,
    AccountingFilterBarComponent,
    AccountingExportMenuComponent,
    AccountingToolbarActionsComponent,
    AccountingTableActionsComponent
  ],
  template: `
    <app-page-header title="Journal comptable" subtitle="Écritures par période" />
    <div class="card journal-filters-card accounting-filters-card">
      <app-accounting-filter-bar ariaLabel="Période et journal">
        <div accountingFilterFields class="journal-toolbar-fields">
          <div class="form-field journal-field-date">
            <label class="field-label" for="jrn-from">Du</label>
            <input
              id="jrn-from"
              type="date"
              [(ngModel)]="fromStr"
              class="journal-date-input"
              [disabled]="loading()" />
          </div>
          <div class="form-field journal-field-date">
            <label class="field-label" for="jrn-to">Au</label>
            <input
              id="jrn-to"
              type="date"
              [(ngModel)]="toStr"
              class="journal-date-input"
              [disabled]="loading()" />
          </div>
          <div class="form-field journal-field-journal">
            <label class="field-label" for="jrn-code">Journal</label>
            <select
              id="jrn-code"
              [(ngModel)]="journalCode"
              class="journal-date-input"
              [disabled]="loading()">
              <option value="">Tous</option>
              @for (j of journals(); track j.code) {
                <option [value]="j.code">{{ j.code }} — {{ j.label }}</option>
              }
            </select>
          </div>
        </div>
        <div accountingFilterActions>
          <app-accounting-toolbar-actions>
          <app-button
            variant="secondary"
            size="sm"
            icon="pi pi-refresh"
            iconPos="left"
            type="button"
            (click)="load()"
            [disabled]="loading()"
            ariaLabel="Actualiser le journal comptable pour la période sélectionnée">
            Actualiser
          </app-button>
          <app-analyze-with-ai-button
            screenId="accounting-journal"
            density="toolbar"
            [payloadBuilder]="buildJournalAnalyzePayload"
            [disabled]="loading()" />
          <app-accounting-export-menu
            [disabled]="loading() || exporting() || flatRows().length === 0"
            (exportFormat)="onExport($event)" />
          </app-accounting-toolbar-actions>
        </div>
      </app-accounting-filter-bar>
    </div>

    <div class="accounting-kpi-row" aria-label="Totaux sur la période chargée">
      <div class="accounting-kpi-item">
        <span class="accounting-kpi-label">Total débit</span>
        <span class="accounting-kpi-value">{{ totals().debit | number : '1.3-3' }}</span>
      </div>
      <div class="accounting-kpi-item">
        <span class="accounting-kpi-label">Total crédit</span>
        <span class="accounting-kpi-value">{{ totals().credit | number : '1.3-3' }}</span>
      </div>
    </div>

    @if (draftEntryCount() > 0) {
      <div class="journal-draft-banner" role="status" aria-live="polite">
        <i class="pi pi-pencil" aria-hidden="true"></i>
        <span>
          @if (canValidate()) {
            {{ draftEntryCount() }} écriture{{ draftEntryCount() === 1 ? '' : 's' }} en brouillard sur la période — à valider avant clôture ou export.
          } @else {
            {{ draftEntryCount() }} écriture{{ draftEntryCount() === 1 ? '' : 's' }} en brouillard — en attente de validation par votre cabinet comptable.
          }
        </span>
      </div>
    }

    <app-accounting-status-banner
      variant="error"
      [message]="error() ?? ''"
      [showRetry]="!!error()"
      retryLabel="Réessayer"
      (retry)="load()" />

    <div class="card journal-table-card">
      <div class="journal-table-toolbar">
        <span class="p-input-icon-left journal-search-wrap">
          <i class="pi pi-search" aria-hidden="true"></i>
          <input
            type="text"
            pInputText
            class="journal-search-input"
            [(ngModel)]="searchTerm"
            (ngModelChange)="onSearchChange()"
            placeholder="Filtrer journal, libellé, compte, date…"
            aria-label="Filtrer les lignes du journal" />
        </span>
        <p class="journal-count" aria-live="polite">
          <span class="journal-count-value">{{ flatRows().length }}</span>
          <span class="journal-count-label"> ligne{{ flatRows().length === 1 ? '' : 's' }} sur la période</span>
        </p>
      </div>
      <p-table
        #dt
        [value]="flatRows()"
        [paginator]="true"
        [rows]="20"
        [rowsPerPageOptions]="[20, 50, 100]"
        [globalFilterFields]="['journal', 'label', 'account', 'date']"
        [loading]="loading()"
        [rowHover]="true"
        [showCurrentPageReport]="true"
        currentPageReportTemplate="Affichage de {first} à {last} sur {totalRecords} lignes"
        styleClass="p-datatable-sm journal-table accounting-datatable">
        <ng-template pTemplate="header">
          <tr>
            <th scope="col">Date</th>
            <th scope="col">Journal</th>
            <th scope="col" class="journal-col-narrow">N°</th>
            <th scope="col">Libellé</th>
            <th scope="col" class="journal-col-account">Compte</th>
            <th scope="col" class="journal-col-amount">Débit</th>
            <th scope="col" class="journal-col-amount">Crédit</th>
            <th scope="col" class="journal-col-narrow">Statut</th>
            <th scope="col" class="journal-col-narrow">Actions</th>
          </tr>
        </ng-template>
        <ng-template pTemplate="body" let-r>
          <tr [class.journal-row-draft]="r.isDraft">
            <td data-label="Date">{{ r.date | date : 'shortDate' }}</td>
            <td data-label="Journal">{{ r.journal }}</td>
            <td class="journal-col-narrow journal-cell-num" data-label="N°">{{ r.piece }}</td>
            <td data-label="Libellé">
              {{ r.label }}
              @if (r.firstOfEntry && r.pieceRef) {
                <span class="journal-piece-ref" [title]="'Pièce : ' + r.pieceRef">Pièce {{ r.pieceRef }}</span>
              }
            </td>
            <td class="journal-col-account" data-label="Compte">
              <span class="journal-account-code">{{ r.account }}</span>
            </td>
            <td class="journal-col-amount" data-label="Débit">{{ r.debit | number : '1.3-3' }}</td>
            <td class="journal-col-amount" data-label="Crédit">{{ r.credit | number : '1.3-3' }}</td>
            <td class="journal-col-narrow" data-label="Statut">
              @if (r.firstOfEntry) {
                <span class="journal-badge" [class.journal-badge-draft]="r.isDraft" [class.journal-badge-valid]="!r.isDraft">
                  {{ r.isDraft ? 'Brouillon' : r.status === 2 ? 'Clôturée' : 'Validée' }}
                </span>
                @if (r.isReversed) {
                  <span class="journal-badge journal-badge-reversed" title="Écriture extournée">Extournée</span>
                }
              }
            </td>
            <td class="journal-col-narrow" data-label="Actions">
              <app-accounting-table-actions>
              @if (r.firstOfEntry && r.isDraft && canValidate()) {
                <app-button
                  variant="success"
                  size="sm"
                  icon="pi-check"
                  iconPos="left"
                  type="button"
                  (click)="validateEntry(r.entryId)"
                  [disabled]="validatingId() === r.entryId"
                  [attr.aria-label]="'Valider l\\'écriture ' + r.journal + ' n° ' + r.piece">
                  Valider
                </app-button>
              }
              @if (r.firstOfEntry && !r.isDraft && !r.isReversed) {
                <app-button
                  variant="secondary"
                  size="sm"
                  icon="pi-replay"
                  iconPos="left"
                  type="button"
                  (click)="openReverse(r)"
                  [attr.aria-label]="'Extourner l\\'écriture ' + r.journal + ' n° ' + r.piece">
                  Extourner
                </app-button>
              }
              @if (r.firstOfEntry) {
                <app-button
                  variant="ghost"
                  size="sm"
                  icon="pi-paperclip"
                  [iconOnly]="true"
                  [iconAlwaysVisible]="true"
                  type="button"
                  (click)="openAttachments(r)"
                  [attr.aria-label]="'Pièces jointes de l\\'écriture ' + r.journal + ' n° ' + r.piece + (r.attachmentCount > 0 ? ' (' + r.attachmentCount + ')' : '')"
                  title="Pièces justificatives" />
                @if (r.attachmentCount > 0) {
                  <span class="journal-attach-count">{{ r.attachmentCount }}</span>
                }
              }
              </app-accounting-table-actions>
            </td>
          </tr>
        </ng-template>
        <ng-template pTemplate="emptymessage">
          <tr>
            <td colspan="9" class="journal-empty">
              <p class="journal-empty-title">Aucune ligne</p>
              <p class="journal-empty-hint">Élargissez la période, actualisez ou modifiez le filtre de recherche.</p>
            </td>
          </tr>
        </ng-template>
      </p-table>
    </div>

    @if (reverseTarget(); as target) {
      <div class="journal-modal-backdrop" role="dialog" aria-modal="true" aria-labelledby="reverse-title">
        <div class="journal-modal">
          <h2 id="reverse-title" class="journal-modal-title">
            Extourner l'écriture {{ target.journal }} n° {{ target.piece }}
          </h2>
          <p class="journal-modal-desc">
            Une écriture inverse (contre-passation) sera créée dans la période d'origine si elle est ouverte,
            sinon à la date du jour. Indiquez le motif de la correction.
          </p>
          <label class="field-label" for="reverse-reason">Motif</label>
          <textarea
            id="reverse-reason"
            class="journal-modal-textarea"
            rows="3"
            [(ngModel)]="reverseReason"
            [disabled]="reversing()"
            placeholder="Ex. erreur d'imputation, montant erroné…"></textarea>
          <div class="journal-modal-actions">
            <button
              type="button"
              class="journal-modal-btn journal-modal-btn-cancel"
              (click)="cancelReverse()"
              [disabled]="reversing()">
              Annuler
            </button>
            <button
              type="button"
              class="journal-modal-btn journal-modal-btn-confirm"
              (click)="confirmReverse()"
              [disabled]="reversing() || !reverseReason.trim()">
              {{ reversing() ? 'Extourne…' : "Confirmer l'extourne" }}
            </button>
          </div>
        </div>
      </div>
    }

    @if (attachTarget(); as target) {
      <div class="journal-modal-backdrop" role="dialog" aria-modal="true" aria-labelledby="attach-title">
        <div class="journal-modal journal-attach-modal">
          <h2 id="attach-title" class="journal-modal-title">
            Pièces justificatives — {{ target.journal }} n° {{ target.piece }}
          </h2>
          <p class="journal-modal-desc">
            PDF, images, Excel ou Word — 10 Mo maximum. Les pièces d'une période clôturée
            restent consultables mais ne peuvent plus être supprimées.
          </p>

          @if (attachError()) {
            <p class="journal-attach-error" role="alert">{{ attachError() }}</p>
          }

          @if (attachLoading()) {
            <p class="journal-attach-loading">Chargement…</p>
          } @else if (attachments().length === 0) {
            <p class="journal-attach-empty">Aucune pièce jointe pour cette écriture.</p>
          } @else {
            <ul class="journal-attach-list" role="list">
              @for (a of attachments(); track a.id) {
                <li class="journal-attach-item">
                  <i class="pi pi-file" aria-hidden="true"></i>
                  <span class="journal-attach-name" [title]="a.fileName">{{ a.fileName }}</span>
                  <span class="journal-attach-meta">{{ formatSize(a.sizeBytes) }} · {{ a.createdAt | date : 'shortDate' }}</span>
                  <button
                    type="button"
                    class="journal-attach-action"
                    (click)="downloadAttachment(a)"
                    [disabled]="attachBusyId() === a.id"
                    [attr.aria-label]="'Télécharger ' + a.fileName"
                    title="Télécharger">
                    <i class="pi pi-download" aria-hidden="true"></i>
                  </button>
                  <button
                    type="button"
                    class="journal-attach-action journal-attach-action-danger"
                    (click)="deleteAttachment(a)"
                    [disabled]="attachBusyId() === a.id"
                    [attr.aria-label]="'Supprimer ' + a.fileName"
                    title="Supprimer">
                    <i class="pi pi-trash" aria-hidden="true"></i>
                  </button>
                </li>
              }
            </ul>
          }

          <div class="journal-modal-actions journal-attach-actions">
            <label class="journal-modal-btn journal-attach-upload" [class.journal-attach-upload-busy]="uploading()">
              <input
                type="file"
                class="journal-attach-input"
                accept=".pdf,.jpg,.jpeg,.png,.webp,.xlsx,.docx"
                (change)="onAttachmentSelected($event)"
                [disabled]="uploading()" />
              <i class="pi pi-upload" aria-hidden="true"></i>
              {{ uploading() ? 'Envoi…' : 'Ajouter une pièce' }}
            </label>
            <button
              type="button"
              class="journal-modal-btn journal-modal-btn-cancel"
              (click)="closeAttachments()">
              Fermer
            </button>
          </div>
        </div>
      </div>
    }
  `,
  styles: `
    @use '../shared/accounting-layout';
    .journal-filters-card,
    .journal-table-card {
      padding: var(--spacing-5);
      border-radius: var(--radius-lg);
      box-shadow: var(--shadow-sm, 0 1px 3px rgba(15, 23, 42, 0.08));
      margin-bottom: var(--spacing-4);
    }
    .journal-toolbar-fields {
      display: flex;
      flex-wrap: wrap;
      align-items: flex-end;
      gap: var(--spacing-4);
    }
    .form-field {
      display: flex;
      flex-direction: column;
      gap: var(--spacing-2);
      min-width: 0;
    }
    .journal-field-date {
      flex: 0 1 auto;
    }
    .field-label {
      font-size: var(--font-size-sm);
      font-weight: var(--font-weight-semibold);
      color: var(--color-text-primary);
      margin: 0;
    }
    .journal-date-input {
      padding: var(--spacing-2) var(--spacing-3);
      border: 1px solid var(--color-border-default);
      border-radius: var(--radius-md);
      background: var(--color-background-elevated);
      color: var(--color-text-primary);
      font-size: var(--font-size-sm);
      min-height: 2.5rem;
    }
    .journal-date-input:focus {
      outline: none;
      border-color: var(--color-primary-500);
      box-shadow: 0 0 0 3px var(--color-primary-200);
    }
    .journal-date-input:disabled {
      opacity: 0.65;
      cursor: not-allowed;
    }
    .journal-error {
      margin: 0 0 var(--spacing-3);
    }
    .journal-table-toolbar {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      justify-content: space-between;
      gap: var(--spacing-4);
      margin-bottom: var(--spacing-4);
      padding-bottom: var(--spacing-4);
      border-bottom: 1px solid var(--color-border-subtle);
    }
    .journal-search-wrap.p-input-icon-left {
      position: relative;
      flex: 1 1 220px;
      max-width: 420px;
      min-width: 180px;
    }
    .journal-search-wrap.p-input-icon-left i {
      position: absolute;
      left: var(--spacing-3);
      top: 50%;
      transform: translateY(-50%);
      color: var(--color-text-secondary);
      pointer-events: none;
    }
    :host ::ng-deep .journal-search-wrap .p-inputtext,
    :host ::ng-deep .journal-search-input {
      width: 100%;
      padding-left: var(--spacing-8);
    }
    .journal-count {
      margin: 0;
      font-size: var(--font-size-sm);
      color: var(--color-text-secondary);
    }
    .journal-count-value {
      font-weight: var(--font-weight-semibold);
      color: var(--color-text-primary);
      font-variant-numeric: tabular-nums;
    }
    .journal-col-narrow {
      width: 1%;
      white-space: nowrap;
    }
    .journal-col-account {
      width: 1%;
      white-space: nowrap;
    }
    .journal-account-code {
      font-family: ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, monospace;
      font-variant-numeric: tabular-nums;
      font-weight: var(--font-weight-medium);
    }
    .journal-col-amount {
      text-align: right;
      font-variant-numeric: tabular-nums;
    }
    .journal-cell-num {
      font-variant-numeric: tabular-nums;
      text-align: right;
    }
    .journal-draft-banner {
      display: flex;
      align-items: center;
      gap: var(--spacing-2);
      margin-bottom: var(--spacing-4);
      padding: var(--spacing-3) var(--spacing-4);
      border-radius: var(--radius-md);
      background: var(--color-warning-50, #fffbeb);
      border: 1px solid var(--color-warning-200, #fde68a);
      color: var(--color-warning-700, #b45309);
      font-size: var(--font-size-sm);
      font-weight: var(--font-weight-medium);
    }
    .journal-badge {
      display: inline-block;
      padding: 0.1rem 0.5rem;
      border-radius: var(--radius-pill, 999px);
      font-size: var(--font-size-xs);
      font-weight: var(--font-weight-semibold);
      white-space: nowrap;
    }
    .journal-piece-ref {
      display: inline-block;
      margin-inline-start: var(--spacing-2);
      padding: 0 0.4rem;
      border: 1px solid var(--color-border-subtle);
      border-radius: var(--radius-md);
      font-size: var(--font-size-xs);
      color: var(--color-text-tertiary);
      background: var(--color-background-subtle);
      white-space: nowrap;
    }
    .journal-attach-count {
      font-weight: var(--font-weight-semibold);
      font-size: var(--font-size-xs);
      color: var(--color-primary-700, #1d4ed8);
    }
    .journal-attach-modal { max-width: 34rem; }
    .journal-attach-error { margin: 0 0 var(--spacing-3); color: var(--color-danger-600, #dc2626); font-size: var(--font-size-sm); }
    .journal-attach-loading, .journal-attach-empty { margin: var(--spacing-3) 0; color: var(--color-text-tertiary); font-size: var(--font-size-sm); }
    .journal-attach-list { list-style: none; margin: var(--spacing-3) 0; padding: 0; display: flex; flex-direction: column; gap: var(--spacing-1); max-height: 40vh; overflow: auto; }
    .journal-attach-item {
      display: flex; align-items: center; gap: var(--spacing-2);
      padding: var(--spacing-2) var(--spacing-3);
      border: 1px solid var(--color-border-subtle); border-radius: var(--radius-md);
      background: var(--color-background-subtle);
    }
    .journal-attach-name { flex: 1 1 auto; min-width: 0; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; font-size: var(--font-size-sm); }
    .journal-attach-meta { flex: 0 0 auto; color: var(--color-text-tertiary); font-size: var(--font-size-xs); white-space: nowrap; }
    .journal-attach-action {
      display: inline-flex; align-items: center; justify-content: center;
      width: 1.75rem; height: 1.75rem; padding: 0;
      border: 1px solid var(--color-border-default); border-radius: var(--radius-md);
      background: var(--color-background-elevated); color: var(--color-text-secondary); cursor: pointer;
    }
    .journal-attach-action:hover:not(:disabled) { border-color: var(--color-primary-500); color: var(--color-primary-700); }
    .journal-attach-action-danger:hover:not(:disabled) { border-color: var(--color-danger-500, #dc2626); color: var(--color-danger-600, #dc2626); }
    .journal-attach-action:disabled { opacity: 0.5; cursor: not-allowed; }
    .journal-attach-actions { justify-content: space-between; }
    .journal-attach-upload { position: relative; display: inline-flex; align-items: center; gap: var(--spacing-2); cursor: pointer; }
    .journal-attach-upload-busy { opacity: 0.7; cursor: wait; }
    .journal-attach-input { position: absolute; inset: 0; opacity: 0; cursor: pointer; }
    .journal-badge-draft {
      background: var(--color-warning-100, #fef3c7);
      color: var(--color-warning-700, #b45309);
    }
    .journal-badge-valid {
      background: var(--color-success-100, #dcfce7);
      color: var(--color-success-700, #15803d);
    }
    .journal-row-draft > td {
      background: var(--color-warning-50, #fffbeb);
    }
    .journal-badge-reversed {
      margin-left: var(--spacing-1);
      background: var(--color-neutral-100, #f1f5f9);
      color: var(--color-text-secondary);
    }
    .journal-modal-backdrop {
      position: fixed;
      inset: 0;
      background: rgba(15, 23, 42, 0.45);
      display: flex;
      align-items: center;
      justify-content: center;
      z-index: 1000;
      padding: var(--spacing-4);
    }
    .journal-modal {
      background: var(--color-background-elevated);
      border-radius: var(--radius-lg);
      box-shadow: var(--shadow-lg, 0 10px 30px rgba(15, 23, 42, 0.2));
      padding: var(--spacing-5);
      width: 100%;
      max-width: 480px;
    }
    .journal-modal-title {
      margin: 0 0 var(--spacing-2);
      font-size: var(--font-size-lg);
      font-weight: var(--font-weight-semibold);
      color: var(--color-text-primary);
    }
    .journal-modal-desc {
      margin: 0 0 var(--spacing-4);
      font-size: var(--font-size-sm);
      color: var(--color-text-secondary);
    }
    .journal-modal-textarea {
      width: 100%;
      margin-top: var(--spacing-2);
      padding: var(--spacing-2) var(--spacing-3);
      border: 1px solid var(--color-border-default);
      border-radius: var(--radius-md);
      background: var(--color-background-elevated);
      color: var(--color-text-primary);
      font-size: var(--font-size-sm);
      font-family: inherit;
      resize: vertical;
    }
    .journal-modal-actions {
      display: flex;
      justify-content: flex-end;
      gap: var(--spacing-3);
      margin-top: var(--spacing-4);
    }
    .journal-modal-btn {
      padding: var(--spacing-2) var(--spacing-4);
      border-radius: var(--radius-md);
      font-size: var(--font-size-sm);
      font-weight: var(--font-weight-semibold);
      cursor: pointer;
      border: 1px solid transparent;
    }
    .journal-modal-btn-cancel {
      background: var(--color-background-subtle);
      color: var(--color-text-primary);
      border-color: var(--color-border-default);
    }
    .journal-modal-btn-confirm {
      background: var(--color-primary-500, #2563eb);
      color: #fff;
    }
    .journal-modal-btn:disabled {
      opacity: 0.6;
      cursor: not-allowed;
    }
    .journal-empty {
      text-align: center;
      padding: var(--spacing-8) var(--spacing-4) !important;
      border: none !important;
    }
    .journal-empty-title {
      margin: 0 0 var(--spacing-2);
      font-weight: var(--font-weight-semibold);
      color: var(--color-text-primary);
    }
    .journal-empty-hint {
      margin: 0;
      font-size: var(--font-size-sm);
      color: var(--color-text-secondary);
    }
    :host ::ng-deep .journal-table .p-datatable-thead > tr > th {
      text-transform: uppercase;
      font-size: var(--font-size-xs);
      letter-spacing: 0.04em;
      color: var(--color-text-tertiary);
      background: var(--color-background-subtle);
    }
  `
})
export class JournalComponent implements OnInit {
  private readonly api = inject(AccountingService);
  private readonly monitoring = inject(AccountingMonitoringService);
  private readonly auth = inject(AuthService);
  private readonly journalCatalog = inject(AccountingJournalCatalogService);

  readonly canValidate = computed(() => canValidateAccountingEntries(this.auth));

  @ViewChild('dt') dt?: Table;

  fromStr = '';
  toStr = '';
  journalCode = '';
  searchTerm = '';

  /** Journaux du catalogue (repli sur les journaux standards) — alimente le filtre. */
  readonly journals = signal<readonly AccountingJournalTab[]>([]);

  readonly error = signal<string | null>(null);
  readonly loading = signal(false);
  readonly exporting = signal(false);
  readonly flatRows = signal<JournalFlatRow[]>([]);

  readonly validatingId = signal<string | null>(null);
  readonly reverseTarget = signal<{ entryId: string; journal: string; piece: number } | null>(null);
  reverseReason = '';
  readonly reversing = signal(false);

  // ── Pièces justificatives (GED) ─────────────────────────────────────────────
  readonly attachTarget = signal<JournalFlatRow | null>(null);
  readonly attachments = signal<JournalEntryAttachmentDto[]>([]);
  readonly attachLoading = signal(false);
  readonly uploading = signal(false);
  readonly attachBusyId = signal<string | null>(null);
  readonly attachError = signal<string | null>(null);

  readonly totals = computed(() => {
    let debit = 0;
    let credit = 0;
    for (const r of this.flatRows()) {
      debit += Number(r.debit);
      credit += Number(r.credit);
    }
    return { debit, credit };
  });

  /** Nombre d'écritures distinctes en brouillard sur la période chargée (pour le bandeau d'alerte). */
  readonly draftEntryCount = computed(() => {
    const ids = new Set<string>();
    for (const r of this.flatRows()) {
      if (r.isDraft) ids.add(r.entryId);
    }
    return ids.size;
  });

  ngOnInit(): void {
    this.fromStr = firstDayOfMonthLocalYmd();
    this.toStr = todayLocalYmd();
    this.journalCatalog.list().subscribe(list => this.journals.set(list));
    this.load();
  }

  onExport(format: AccountingExportFormat): void {
    const from = parseLocalDateString(this.fromStr);
    const to = parseLocalDateString(this.toStr);
    const vr = validateDateRange(from, to);
    if (!vr.valid) {
      this.error.set(vr.message ?? 'Période invalide.');
      return;
    }
    this.exporting.set(true);
    this.api.exportJournal(this.journalCode || undefined, from, to, format).subscribe({
      next: blob => {
        this.exporting.set(false);
        downloadBlob(blob, `journal_${this.fromStr}_${this.toStr}.${exportExtension(format)}`);
      },
      error: () => {
        this.exporting.set(false);
        this.error.set("Erreur lors de l'export.");
      }
    });
  }

  load(): void {
    this.error.set(null);
    const from = parseLocalDateString(this.fromStr);
    const to = parseLocalDateString(this.toStr);
    const vr = validateDateRange(from, to);
    if (!vr.valid) {
      this.error.set(vr.message ?? 'Période invalide.');
      return;
    }
    this.loading.set(true);
    this.api.getJournal(this.journalCode || undefined, from, to).subscribe({
      next: res => {
        this.loading.set(false);
        if (!res.success || !res.data) {
          this.error.set(res.error ?? 'Erreur');
          return;
        }
        const flat: JournalFlatRow[] = [];
        for (const e of res.data) {
          let first = true;
          for (const l of e.lines) {
            flat.push({
              entryId: e.id,
              date: e.entryDate,
              journal: e.journalCode,
              piece: e.entryNumber,
              label: l.label?.trim() ? l.label : e.label,
              account: l.accountNumber,
              debit: l.debit,
              credit: l.credit,
              status: e.status,
              isDraft: e.isDraft,
              isReversed: e.isReversed,
              pieceRef: e.pieceRef ?? null,
              attachmentCount: e.attachmentCount ?? 0,
              firstOfEntry: first
            });
            first = false;
          }
        }
        this.flatRows.set(flat);
        this.reapplyGlobalFilter();
      },
      error: err => {
        this.loading.set(false);
        this.error.set('Erreur réseau');
        this.monitoring.logError('journal.load', err);
      }
    });
  }

  readonly buildJournalAnalyzePayload = (): unknown =>
    wrapLegacyAnalyzePayload(
      'accounting-journal',
      {
        screen: 'accounting-journal',
        filters: {
          from: this.fromStr || null,
          to: this.toStr || null,
          journalCode: this.journalCode || null
        },
        summary: {
          totalRows: this.flatRows().length,
          totalDebit: this.totals().debit,
          totalCredit: this.totals().credit
        },
        rows: this.flatRows().slice(0, 200).map(r => ({
          date: r.date,
          journal: r.journal,
          piece: r.piece,
          label: r.label,
          account: r.account,
          debit: r.debit,
          credit: r.credit
        }))
      } as Record<string, unknown>
    );

  /** Valide une écriture en brouillard (la rend définitive), puis recharge le journal. */
  validateEntry(entryId: string): void {
    if (!this.canValidate()) return;
    if (this.validatingId()) return;
    this.validatingId.set(entryId);
    this.api.validateJournalEntry(entryId).subscribe({
      next: res => {
        this.validatingId.set(null);
        if (!res.success) {
          this.error.set(res.error ?? 'La validation a échoué.');
          return;
        }
        this.load();
      },
      error: err => {
        this.validatingId.set(null);
        this.error.set('Erreur réseau lors de la validation.');
        this.monitoring.logError('journal.validate', err);
      }
    });
  }

  // ── Pièces justificatives (GED) ─────────────────────────────────────────────

  openAttachments(row: JournalFlatRow): void {
    this.attachTarget.set(row);
    this.attachError.set(null);
    this.reloadAttachments(row.entryId);
  }

  closeAttachments(): void {
    this.attachTarget.set(null);
    this.attachments.set([]);
    this.attachError.set(null);
    // Rafraîchit les compteurs de pièces du tableau.
    this.load();
  }

  private reloadAttachments(entryId: string): void {
    this.attachLoading.set(true);
    this.api.getEntryAttachments(entryId).subscribe({
      next: res => {
        this.attachLoading.set(false);
        if (res.success && res.data) this.attachments.set(res.data);
        else this.attachError.set(res.error ?? 'Erreur de chargement des pièces.');
      },
      error: () => {
        this.attachLoading.set(false);
        this.attachError.set('Erreur réseau lors du chargement des pièces.');
      }
    });
  }

  onAttachmentSelected(event: Event): void {
    const target = this.attachTarget();
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    input.value = '';
    if (!target || !file) return;

    this.uploading.set(true);
    this.attachError.set(null);
    this.api.uploadEntryAttachment(target.entryId, file).subscribe({
      next: res => {
        this.uploading.set(false);
        if (res.success) this.reloadAttachments(target.entryId);
        else this.attachError.set(res.error ?? "Erreur lors de l'ajout de la pièce.");
      },
      error: () => {
        this.uploading.set(false);
        this.attachError.set("Erreur réseau lors de l'ajout de la pièce.");
      }
    });
  }

  downloadAttachment(a: JournalEntryAttachmentDto): void {
    this.attachBusyId.set(a.id);
    this.api.downloadEntryAttachment(a.journalEntryId, a.id).subscribe({
      next: blob => {
        this.attachBusyId.set(null);
        const url = URL.createObjectURL(blob);
        const link = document.createElement('a');
        link.href = url;
        link.download = a.fileName;
        link.click();
        URL.revokeObjectURL(url);
      },
      error: () => {
        this.attachBusyId.set(null);
        this.attachError.set('Erreur lors du téléchargement.');
      }
    });
  }

  deleteAttachment(a: JournalEntryAttachmentDto): void {
    if (!window.confirm(`Supprimer la pièce « ${a.fileName} » ?`)) return;
    this.attachBusyId.set(a.id);
    this.attachError.set(null);
    this.api.deleteEntryAttachment(a.journalEntryId, a.id).subscribe({
      next: res => {
        this.attachBusyId.set(null);
        if (res.success) this.reloadAttachments(a.journalEntryId);
        else this.attachError.set(res.error ?? 'Erreur lors de la suppression.');
      },
      error: () => {
        this.attachBusyId.set(null);
        this.attachError.set('Erreur réseau lors de la suppression.');
      }
    });
  }

  formatSize(bytes: number): string {
    if (bytes >= 1024 * 1024) return `${(bytes / (1024 * 1024)).toFixed(1)} Mo`;
    if (bytes >= 1024) return `${Math.round(bytes / 1024)} Ko`;
    return `${bytes} o`;
  }

  /** Ouvre la modale d'extourne pour une écriture validée. */
  openReverse(row: JournalFlatRow): void {
    this.reverseReason = '';
    this.reverseTarget.set({ entryId: row.entryId, journal: row.journal, piece: row.piece });
  }

  cancelReverse(): void {
    this.reverseTarget.set(null);
    this.reverseReason = '';
  }

  /** Confirme l'extourne (contre-passation) de l'écriture ciblée, puis recharge. */
  confirmReverse(): void {
    const target = this.reverseTarget();
    if (!target || this.reversing()) return;
    const reason = this.reverseReason.trim();
    if (!reason) {
      this.error.set("Le motif de l'extourne est obligatoire.");
      return;
    }
    this.reversing.set(true);
    this.api.reverseJournalEntry(target.entryId, reason).subscribe({
      next: res => {
        this.reversing.set(false);
        if (!res.success) {
          this.error.set(res.error ?? "L'extourne a échoué.");
          return;
        }
        this.reverseTarget.set(null);
        this.reverseReason = '';
        this.load();
      },
      error: err => {
        this.reversing.set(false);
        this.error.set("Erreur réseau lors de l'extourne.");
        this.monitoring.logError('journal.reverse', err);
      }
    });
  }

  onSearchChange(): void {
    this.dt?.filterGlobal(this.searchTerm, 'contains');
  }

  private reapplyGlobalFilter(): void {
    setTimeout(() => {
      this.dt?.filterGlobal(this.searchTerm, 'contains');
    }, 0);
  }
}
