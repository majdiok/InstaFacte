"""Write FactuTrust numbering settings frontend files."""
import pathlib

ROOT = pathlib.Path(r"c:\Solution\FactuTrust - Copy")
WEB = ROOT / "src" / "Frontend" / "factutrust-web" / "src" / "app"

FILES: dict[pathlib.Path, str] = {}

FILES[WEB / "features" / "settings" / "numbering" / "models" / "numbering.models.ts"] = r'''export enum NumberingDocumentType {
  Invoice = 0,
  CreditNote = 1,
  Quote = 2,
  DeliveryNote = 3,
  PurchaseOrder = 4,
  StockTransfer = 5,
  PhysicalInventory = 6,
  CashReceipt = 7,
  CashExpense = 8,
  BankDeposit = 9
}

export enum NumberingBlockType {
  FreeText = 0,
  Separator = 1,
  DocumentNumber = 2,
  DocumentNumberPadded3 = 3,
  DocumentNumberPadded4 = 4,
  DocumentNumberPadded5 = 5,
  DocumentNumberPadded6 = 6,
  Day = 7,
  Month = 8,
  Year4 = 9,
  Year2 = 10
}

export interface NumberingBlock {
  type: NumberingBlockType;
  value?: string | null;
  order: number;
  label?: string;
}

export interface NumberingScheme {
  documentType: NumberingDocumentType;
  documentTypeDisplay: string;
  fiscalYear: number;
  startNumber: number;
  currentSequence: number;
  blocks: NumberingBlock[];
  isFormatLocked: boolean;
  examplePreview: string;
}

export interface SaveNumberingSchemeRequest {
  startNumber: number;
  blocks: NumberingBlock[];
}

export interface PreviewNumberingRequest {
  startNumber: number;
  blocks: NumberingBlock[];
  fiscalYear?: number;
}

export interface PreviewNumberingResponse {
  example: string;
}

export const BLOCK_TYPE_LABELS: Record<NumberingBlockType, string> = {
  [NumberingBlockType.FreeText]: 'Texte libre',
  [NumberingBlockType.Separator]: 'Séparateur',
  [NumberingBlockType.DocumentNumber]: 'Numéro de document',
  [NumberingBlockType.DocumentNumberPadded3]: 'Numéro de document à 3 chiffres',
  [NumberingBlockType.DocumentNumberPadded4]: 'Numéro de document à 4 chiffres',
  [NumberingBlockType.DocumentNumberPadded5]: 'Numéro de document à 5 chiffres',
  [NumberingBlockType.DocumentNumberPadded6]: 'Numéro de document à 6 chiffres',
  [NumberingBlockType.Day]: 'Jour (JJ)',
  [NumberingBlockType.Month]: 'Mois (MM)',
  [NumberingBlockType.Year4]: 'Année (AAAA)',
  [NumberingBlockType.Year2]: 'Année (AA)'
};

export interface PaletteBlockTemplate {
  type: NumberingBlockType;
  value?: string | null;
}

export const PALETTE_BLOCKS: readonly PaletteBlockTemplate[] = [
  { type: NumberingBlockType.FreeText, value: '' },
  { type: NumberingBlockType.Separator, value: '-' },
  { type: NumberingBlockType.Separator, value: '/' },
  { type: NumberingBlockType.DocumentNumber, value: null },
  { type: NumberingBlockType.DocumentNumberPadded3, value: null },
  { type: NumberingBlockType.DocumentNumberPadded4, value: null },
  { type: NumberingBlockType.DocumentNumberPadded5, value: null },
  { type: NumberingBlockType.DocumentNumberPadded6, value: null },
  { type: NumberingBlockType.Day, value: null },
  { type: NumberingBlockType.Month, value: null },
  { type: NumberingBlockType.Year4, value: null },
  { type: NumberingBlockType.Year2, value: null }
];

export const DOCUMENT_TYPE_TABS: ReadonlyArray<{ documentType: NumberingDocumentType; label: string }> = [
  { documentType: NumberingDocumentType.Invoice, label: 'Facture' },
  { documentType: NumberingDocumentType.CreditNote, label: 'Facture avoir' },
  { documentType: NumberingDocumentType.Quote, label: 'Devis' },
  { documentType: NumberingDocumentType.DeliveryNote, label: 'Bon de livraison' },
  { documentType: NumberingDocumentType.PurchaseOrder, label: 'Bon de commande' },
  { documentType: NumberingDocumentType.StockTransfer, label: 'Transfert stock' },
  { documentType: NumberingDocumentType.PhysicalInventory, label: 'Inventaire physique' },
  { documentType: NumberingDocumentType.CashReceipt, label: 'Encaissement caisse' },
  { documentType: NumberingDocumentType.CashExpense, label: 'Décaissement caisse' },
  { documentType: NumberingDocumentType.BankDeposit, label: 'Remise bancaire' }
];

export const DEFAULT_FREE_TEXT: Record<NumberingDocumentType, string> = {
  [NumberingDocumentType.Invoice]: 'FAC',
  [NumberingDocumentType.CreditNote]: 'AVO',
  [NumberingDocumentType.Quote]: 'DEV',
  [NumberingDocumentType.DeliveryNote]: 'BL',
  [NumberingDocumentType.PurchaseOrder]: 'BC',
  [NumberingDocumentType.StockTransfer]: 'TR',
  [NumberingDocumentType.PhysicalInventory]: 'INVE',
  [NumberingDocumentType.CashReceipt]: 'ENC',
  [NumberingDocumentType.CashExpense]: 'DEP',
  [NumberingDocumentType.BankDeposit]: 'REM'
};

export function isDocumentNumberBlock(type: NumberingBlockType): boolean {
  return (
    type === NumberingBlockType.DocumentNumber ||
    type === NumberingBlockType.DocumentNumberPadded3 ||
    type === NumberingBlockType.DocumentNumberPadded4 ||
    type === NumberingBlockType.DocumentNumberPadded5 ||
    type === NumberingBlockType.DocumentNumberPadded6
  );
}

export function cloneBlock(block: NumberingBlock, order: number): NumberingBlock {
  return {
    type: block.type,
    value: block.value,
    order,
    label: block.label ?? BLOCK_TYPE_LABELS[block.type]
  };
}
'''

FILES[WEB / "core" / "services" / "numbering.service.ts"] = r'''import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '@environments/environment';
import { ApiResponse } from './auth.service';
import {
  NumberingDocumentType,
  NumberingScheme,
  PreviewNumberingRequest,
  PreviewNumberingResponse,
  SaveNumberingSchemeRequest
} from '@features/settings/numbering/models/numbering.models';

@Injectable({
  providedIn: 'root'
})
export class NumberingService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/settings/numbering`;

  getSchemes(fiscalYear?: number): Observable<ApiResponse<NumberingScheme[]>> {
    let params = new HttpParams();
    if (fiscalYear !== undefined && fiscalYear !== null) {
      params = params.set('fiscalYear', String(fiscalYear));
    }
    return this.http.get<ApiResponse<NumberingScheme[]>>(this.baseUrl, { params });
  }

  getScheme(
    documentType: NumberingDocumentType,
    fiscalYear?: number
  ): Observable<ApiResponse<NumberingScheme>> {
    let params = new HttpParams();
    if (fiscalYear !== undefined && fiscalYear !== null) {
      params = params.set('fiscalYear', String(fiscalYear));
    }
    return this.http.get<ApiResponse<NumberingScheme>>(`${this.baseUrl}/${documentType}`, { params });
  }

  saveScheme(
    documentType: NumberingDocumentType,
    request: SaveNumberingSchemeRequest,
    fiscalYear?: number
  ): Observable<ApiResponse<NumberingScheme>> {
    let params = new HttpParams();
    if (fiscalYear !== undefined && fiscalYear !== null) {
      params = params.set('fiscalYear', String(fiscalYear));
    }
    return this.http.put<ApiResponse<NumberingScheme>>(
      `${this.baseUrl}/${documentType}`,
      request,
      { params }
    );
  }

  preview(
    documentType: NumberingDocumentType,
    request: PreviewNumberingRequest
  ): Observable<ApiResponse<PreviewNumberingResponse>> {
    return this.http.post<ApiResponse<PreviewNumberingResponse>>(
      `${this.baseUrl}/${documentType}/preview`,
      request
    );
  }

  reset(
    documentType: NumberingDocumentType,
    fiscalYear?: number
  ): Observable<ApiResponse<NumberingScheme>> {
    let params = new HttpParams();
    if (fiscalYear !== undefined && fiscalYear !== null) {
      params = params.set('fiscalYear', String(fiscalYear));
    }
    return this.http.post<ApiResponse<NumberingScheme>>(
      `${this.baseUrl}/${documentType}/reset`,
      null,
      { params }
    );
  }
}
'''

FILES[WEB / "features" / "settings" / "numbering" / "numbering-format-renderer.ts"] = r'''import {
  BLOCK_TYPE_LABELS,
  isDocumentNumberBlock,
  NumberingBlock,
  NumberingBlockType
} from './models/numbering.models';

export const MAX_RENDERED_LENGTH = 50;

export interface RenderValidationResult {
  valid: boolean;
  error?: string;
}

function padSequence(sequence: number, digits: number): string {
  return sequence.toString().padStart(digits, '0');
}

function renderBlock(
  block: NumberingBlock,
  sequence: number,
  referenceDate: Date,
  freeTextOverride?: string
): string | null {
  switch (block.type) {
    case NumberingBlockType.FreeText:
      return (freeTextOverride ?? block.value ?? '').trim().toUpperCase();
    case NumberingBlockType.Separator:
      return block.value ?? '-';
    case NumberingBlockType.DocumentNumber:
      return sequence.toString();
    case NumberingBlockType.DocumentNumberPadded3:
      return padSequence(sequence, 3);
    case NumberingBlockType.DocumentNumberPadded4:
      return padSequence(sequence, 4);
    case NumberingBlockType.DocumentNumberPadded5:
      return padSequence(sequence, 5);
    case NumberingBlockType.DocumentNumberPadded6:
      return padSequence(sequence, 6);
    case NumberingBlockType.Day:
      return referenceDate.getDate().toString().padStart(2, '0');
    case NumberingBlockType.Month:
      return (referenceDate.getMonth() + 1).toString().padStart(2, '0');
    case NumberingBlockType.Year4:
      return referenceDate.getFullYear().toString();
    case NumberingBlockType.Year2:
      return (referenceDate.getFullYear() % 100).toString().padStart(2, '0');
    default:
      return null;
  }
}

export function render(
  blocks: NumberingBlock[],
  sequence: number,
  referenceDate: Date,
  freeTextOverride?: string
): { value: string; error?: string } {
  const validation = validateBlocks(blocks);
  if (!validation.valid) {
    return { value: '', error: validation.error };
  }

  const ordered = [...blocks].sort((a, b) => a.order - b.order);
  const parts: string[] = [];

  for (const block of ordered) {
    const part = renderBlock(block, sequence, referenceDate, freeTextOverride);
    if (part === null) {
      return { value: '', error: `Type de bloc inconnu: ${block.type}` };
    }
    parts.push(part);
  }

  const rendered = parts.join('');
  if (rendered.length > MAX_RENDERED_LENGTH) {
    return {
      value: '',
      error: `Le numéro généré dépasse ${MAX_RENDERED_LENGTH} caractères.`
    };
  }

  return { value: rendered };
}

export function validateBlocks(blocks: NumberingBlock[]): RenderValidationResult {
  if (!blocks.length) {
    return { valid: false, error: 'Le format de numérotation est vide.' };
  }

  if (!blocks.some((b) => isDocumentNumberBlock(b.type))) {
    return {
      valid: false,
      error: 'Le format doit contenir au moins un bloc numéro de document.'
    };
  }

  for (const block of blocks) {
    if (block.type === NumberingBlockType.FreeText) {
      if (!block.value || !block.value.trim()) {
        return { valid: false, error: 'Le texte libre est obligatoire.' };
      }
      if (block.value.trim().length > 20) {
        return { valid: false, error: 'Le texte libre ne peut pas dépasser 20 caractères.' };
      }
    }

    if (block.type === NumberingBlockType.Separator) {
      if (block.value !== '-' && block.value !== '/') {
        return { valid: false, error: 'Le séparateur doit être « - » ou « / ».' };
      }
    }
  }

  return { valid: true };
}

export function blockDisplayLabel(block: NumberingBlock): string {
  return block.label ?? BLOCK_TYPE_LABELS[block.type];
}
'''

FILES[WEB / "features" / "settings" / "numbering" / "numbering.component.ts"] = r'''import { Component, OnDestroy, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import {
  CdkDragDrop,
  DragDropModule,
  copyArrayItem,
  moveItemInArray
} from '@angular/cdk/drag-drop';
import { Subject, takeUntil } from 'rxjs';
import { TabViewModule } from 'primeng/tabview';
import { ButtonModule } from 'primeng/button';
import { InputNumberModule } from 'primeng/inputnumber';
import { InputTextModule } from 'primeng/inputtext';
import { ToastModule } from 'primeng/toast';
import { TagModule } from 'primeng/tag';
import { TooltipModule } from 'primeng/tooltip';
import { PageHeaderComponent } from '@shared/components/page-header/page-header.component';
import { BreadcrumbComponent, BreadcrumbItem } from '@shared/components/breadcrumb/breadcrumb.component';
import { AuthService } from '@core/services/auth.service';
import { PERMISSIONS } from '@core/config/permission-keys';
import { ToastService } from '@core/services/toast.service';
import { ErrorHandlerService } from '@core/services/error-handler.service';
import { ConfirmationService } from '@core/services/confirmation.service';
import { NumberingService } from '@core/services/numbering.service';
import {
  BLOCK_TYPE_LABELS,
  DEFAULT_FREE_TEXT,
  DOCUMENT_TYPE_TABS,
  NumberingBlock,
  NumberingBlockType,
  NumberingDocumentType,
  NumberingScheme,
  PALETTE_BLOCKS,
  PaletteBlockTemplate,
  cloneBlock,
  isDocumentNumberBlock
} from './models/numbering.models';
import { blockDisplayLabel, render, validateBlocks } from './numbering-format-renderer';

@Component({
  selector: 'app-numbering',
  standalone: true,
  imports: [
    CommonModule,
    RouterModule,
    FormsModule,
    DragDropModule,
    TabViewModule,
    ButtonModule,
    InputNumberModule,
    InputTextModule,
    ToastModule,
    TagModule,
    TooltipModule,
    PageHeaderComponent,
    BreadcrumbComponent
  ],
  template: `
    <app-breadcrumb [items]="breadcrumbItems"></app-breadcrumb>

    <app-page-header
      title="Numérotations"
      subtitle="Définissez le format et le numéro de départ pour chaque type de document">
      <div class="header-actions">
        <p-button
          label="Retour"
          icon="pi pi-arrow-left"
          [outlined]="true"
          routerLink="/settings">
        </p-button>
        @if (canUpdate()) {
          <p-button
            label="Réinitialiser"
            icon="pi pi-refresh"
            [outlined]="true"
            severity="secondary"
            (onClick)="confirmReset()"
            [disabled]="loading() || saving()">
          </p-button>
          <p-button
            label="Enregistrer"
            icon="pi pi-check"
            (onClick)="save()"
            [loading]="saving()"
            [disabled]="loading() || saving() || !previewValid()">
          </p-button>
        }
      </div>
    </app-page-header>

    @if (loading() && !schemes().length) {
      <div class="loading-container" role="status" aria-live="polite">
        <i class="pi pi-spin pi-spinner" style="font-size: 2rem" aria-hidden="true"></i>
        <p>Chargement des numérotations...</p>
      </div>
    } @else {
      <p-tabView
        [(activeIndex)]="activeTabIndex"
        [scrollable]="true"
        styleClass="numbering-tabview"
        (onChange)="onTabChange()">
        @for (tab of documentTabs; track tab.documentType) {
          <p-tabPanel [header]="tab.label"></p-tabPanel>
        }
      </p-tabView>

      <div class="numbering-content">
        <div class="meta-row">
          <div class="meta-item">
            <span class="meta-label">Année fiscale</span>
            <span class="meta-value">{{ fiscalYear() }}</span>
          </div>
          <div class="meta-item">
            <span class="meta-label">Séquence actuelle</span>
            <span class="meta-value">{{ currentSequence() }}</span>
          </div>
          @if (isFormatLocked()) {
            <p-tag
              severity="warning"
              icon="pi pi-lock"
              value="Format verrouillé — des documents existent déjà"
              styleClass="lock-tag">
            </p-tag>
          }
        </div>

        <div class="numbering-grid">
          @if (!isFormatLocked()) {
            <section class="panel palette-panel" aria-labelledby="palette-title">
              <h3 id="palette-title" class="panel-title">Blocs disponibles</h3>
              <p class="panel-hint">Glissez un bloc vers le format ci-dessous.</p>
              <div
                class="palette-list"
                cdkDropList
                #paletteList="cdkDropList"
                [cdkDropListData]="paletteBlocks"
                [cdkDropListConnectedTo]="[builderList]"
                [cdkDropListSortingDisabled]="true">
                @for (template of paletteBlocks; track $index) {
                  <div class="palette-block" cdkDrag [cdkDragData]="template">
                    <i class="pi pi-plus-circle" aria-hidden="true"></i>
                    <span>{{ paletteLabel(template) }}</span>
                  </div>
                }
              </div>
            </section>
          }

          <section class="panel builder-panel" aria-labelledby="builder-title">
            <h3 id="builder-title" class="panel-title">Format de numérotation</h3>
            @if (isFormatLocked()) {
              <p class="panel-hint">
                Le format ne peut plus être modifié car des documents ont déjà été créés.
                Vous pouvez uniquement ajuster le numéro de départ.
              </p>
            } @else {
              <p class="panel-hint">Réorganisez les blocs par glisser-déposer.</p>
            }

            <div
              class="builder-list"
              cdkDropList
              #builderList="cdkDropList"
              [cdkDropListData]="blocks()"
              [cdkDropListConnectedTo]="isFormatLocked() ? [] : [paletteList]"
              (cdkDropListDropped)="onDrop($event)">
              @for (block of blocks(); track trackBlock($index, block); let i = $index) {
                <div
                  class="builder-block"
                  [class.builder-block--locked]="isFormatLocked()"
                  cdkDrag
                  [cdkDragDisabled]="isFormatLocked()">
                  <div class="builder-block__handle" cdkDragHandle [class.hidden]="isFormatLocked()">
                    <i class="pi pi-bars" aria-hidden="true"></i>
                  </div>
                  <div class="builder-block__content">
                    <span class="builder-block__label">{{ blockDisplayLabel(block) }}</span>
                    @if (!isFormatLocked() && block.type === blockTypeEnum.FreeText) {
                      <input
                        pInputText
                        class="builder-block__input"
                        [ngModel]="block.value ?? ''"
                        (ngModelChange)="onFreeTextChange(i, $event)"
                        maxlength="20"
                        [attr.aria-label]="'Texte libre du bloc ' + (i + 1)" />
                    } @else if (!isFormatLocked() && block.type === blockTypeEnum.Separator) {
                      <select
                        class="builder-block__select"
                        [ngModel]="block.value ?? '-'"
                        (ngModelChange)="onSeparatorChange(i, $event)"
                        [attr.aria-label]="'Séparateur du bloc ' + (i + 1)">
                        <option value="-">-</option>
                        <option value="/">/</option>
                      </select>
                    } @else if (block.value) {
                      <span class="builder-block__value">{{ block.value }}</span>
                    }
                  </div>
                  @if (!isFormatLocked()) {
                    <p-button
                      icon="pi pi-times"
                      [text]="true"
                      [rounded]="true"
                      severity="danger"
                      (onClick)="removeBlock(i)"
                      [attr.aria-label]="'Supprimer le bloc ' + (i + 1)">
                    </p-button>
                  }
                </div>
              } @empty {
                <div class="builder-empty">
                  Aucun bloc. Glissez des éléments depuis la palette ou réinitialisez le format.
                </div>
              }
            </div>
          </section>

          <section class="panel preview-panel" aria-labelledby="preview-title">
            <h3 id="preview-title" class="panel-title">Aperçu en direct</h3>
            <div class="preview-box" [class.preview-box--error]="!previewValid()">
              @if (previewValid()) {
                <span class="preview-value">{{ previewText() }}</span>
              } @else {
                <span class="preview-error">{{ previewError() }}</span>
              }
            </div>
            <p class="panel-hint preview-hint">
              Exemple avec le numéro de départ et la date du jour.
            </p>
          </section>
        </div>

        <div class="start-number-card">
          <label class="start-number-label" for="start-number">
            Numéro de départ
          </label>
          <p-inputNumber
            inputId="start-number"
            [ngModel]="startNumber()"
            (ngModelChange)="onStartNumberChange($event)"
            [min]="1"
            [useGrouping]="false"
            [disabled]="!canUpdate()"
            inputStyleClass="start-number-input"
            styleClass="start-number-field">
          </p-inputNumber>
          <p class="panel-hint">
            Le prochain document utilisera ce numéro si aucun document n'existe encore pour l'année.
          </p>
        </div>
      </div>
    }

    <p-toast></p-toast>
  `,
  styles: [`
    .header-actions {
      display: flex;
      gap: var(--spacing-2);
      flex-wrap: wrap;
      align-items: center;
    }

    .loading-container {
      display: flex;
      flex-direction: column;
      align-items: center;
      justify-content: center;
      padding: var(--spacing-8);
      gap: var(--spacing-3);
      color: var(--color-neutral-600);
    }

    .numbering-content {
      margin-top: var(--spacing-4);
    }

    .meta-row {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: var(--spacing-4);
      margin-bottom: var(--spacing-4);
    }

    .meta-item {
      display: flex;
      flex-direction: column;
      gap: var(--spacing-1);
    }

    .meta-label {
      font-size: var(--font-size-xs);
      font-weight: var(--font-weight-semibold);
      text-transform: uppercase;
      letter-spacing: 0.06em;
      color: var(--color-neutral-500);
    }

    .meta-value {
      font-size: var(--font-size-lg);
      font-weight: var(--font-weight-semibold);
      color: var(--color-neutral-900);
    }

  .lock-tag {
      margin-left: auto;
    }

    .numbering-grid {
      display: grid;
      grid-template-columns: minmax(220px, 1fr) minmax(280px, 2fr) minmax(200px, 1fr);
      gap: var(--spacing-4);
      align-items: start;
    }

    @media (max-width: 1024px) {
      .numbering-grid {
        grid-template-columns: 1fr;
      }
    }

    .panel {
      background: white;
      border: 1px solid var(--color-neutral-200);
      border-radius: var(--radius-xl);
      padding: var(--spacing-5);
      min-height: 200px;
    }

    .panel-title {
      margin: 0 0 var(--spacing-2);
      font-size: var(--font-size-lg);
      font-weight: var(--font-weight-semibold);
      color: var(--color-neutral-900);
    }

    .panel-hint {
      margin: 0 0 var(--spacing-4);
      font-size: var(--font-size-sm);
      color: var(--color-neutral-500);
      line-height: 1.5;
    }

    .palette-list {
      display: flex;
      flex-direction: column;
      gap: var(--spacing-2);
      min-height: 120px;
    }

    .palette-block {
      display: flex;
      align-items: center;
      gap: var(--spacing-2);
      padding: var(--spacing-3);
      border: 1px dashed var(--color-neutral-300);
      border-radius: var(--radius-md);
      background: var(--color-neutral-50);
      cursor: grab;
      font-size: var(--font-size-sm);
      color: var(--color-neutral-700);

      i {
        color: var(--color-primary-500);
      }
    }

    .builder-list {
      display: flex;
      flex-direction: column;
      gap: var(--spacing-2);
      min-height: 160px;
    }

    .builder-block {
      display: flex;
      align-items: center;
      gap: var(--spacing-2);
      padding: var(--spacing-3);
      border: 1px solid var(--color-neutral-200);
      border-radius: var(--radius-md);
      background: white;
    }

    .builder-block--locked {
      background: var(--color-neutral-50);
    }

    .builder-block__handle {
      color: var(--color-neutral-400);
      cursor: grab;

      &.hidden {
        display: none;
      }
    }

    .builder-block__content {
      flex: 1;
      display: flex;
      flex-direction: column;
      gap: var(--spacing-2);
      min-width: 0;
    }

    .builder-block__label {
      font-size: var(--font-size-sm);
      font-weight: var(--font-weight-medium);
      color: var(--color-neutral-700);
    }

    .builder-block__input,
    .builder-block__select {
      width: 100%;
      max-width: 240px;
    }

    .builder-block__value {
      font-size: var(--font-size-sm);
      color: var(--color-neutral-500);
    }

    .builder-empty {
      padding: var(--spacing-6);
      text-align: center;
      color: var(--color-neutral-500);
      border: 1px dashed var(--color-neutral-300);
      border-radius: var(--radius-md);
      font-size: var(--font-size-sm);
    }

    .preview-box {
      padding: var(--spacing-5);
      border-radius: var(--radius-lg);
      background: var(--color-neutral-50);
      border: 1px solid var(--color-neutral-200);
      min-height: 72px;
      display: flex;
      align-items: center;
      justify-content: center;
    }

    .preview-box--error {
      border-color: var(--color-error-300);
      background: var(--color-error-50);
    }

    .preview-value {
      font-size: var(--font-size-xl);
      font-weight: var(--font-weight-bold);
      font-family: ui-monospace, monospace;
      color: var(--color-primary-700);
      letter-spacing: 0.02em;
    }

    .preview-error {
      font-size: var(--font-size-sm);
      color: var(--color-error-600);
      text-align: center;
    }

    .preview-hint {
      margin-top: var(--spacing-3);
      margin-bottom: 0;
    }

    .start-number-card {
      margin-top: var(--spacing-4);
      padding: var(--spacing-5);
      background: white;
      border: 1px solid var(--color-neutral-200);
      border-radius: var(--radius-xl);
    }

    .start-number-label {
      display: block;
      margin-bottom: var(--spacing-2);
      font-weight: var(--font-weight-medium);
      color: var(--color-neutral-700);
      font-size: var(--font-size-sm);
    }

    .start-number-card .panel-hint {
      margin-top: var(--spacing-3);
      margin-bottom: 0;
    }

    :host ::ng-deep .numbering-tabview .p-tabview-panels {
      display: none;
    }

    :host ::ng-deep .start-number-field,
    :host ::ng-deep .start-number-field .p-inputnumber-input {
      width: 100%;
      max-width: 200px;
    }

    .cdk-drag-preview {
      box-shadow: var(--shadow-md);
    }

    .cdk-drag-placeholder {
      opacity: 0.35;
    }
  `]
})
export class NumberingComponent implements OnInit, OnDestroy {
  private readonly numberingService = inject(NumberingService);
  private readonly auth = inject(AuthService);
  private readonly toast = inject(ToastService);
  private readonly errorHandler = inject(ErrorHandlerService);
  private readonly confirmation = inject(ConfirmationService);
  private readonly destroy$ = new Subject<void>();

  readonly documentTabs = DOCUMENT_TYPE_TABS;
  readonly paletteBlocks = PALETTE_BLOCKS;
  readonly blockTypeEnum = NumberingBlockType;

  breadcrumbItems: BreadcrumbItem[] = [
    { label: 'Tableau de bord', route: '/dashboard', icon: 'pi-home' },
    { label: 'Paramètres', route: '/settings' },
    { label: 'Numérotations' }
  ];

  loading = signal(false);
  saving = signal(false);
  schemes = signal<NumberingScheme[]>([]);
  activeTabIndex = 0;
  blocks = signal<NumberingBlock[]>([]);
  startNumber = signal(1);
  fiscalYear = signal(new Date().getFullYear());
  currentSequence = signal(0);
  isFormatLocked = signal(false);

  canUpdate = computed(() => this.auth.hasPermission(PERMISSIONS.settings.update));

  currentDocumentType = computed(
    () => DOCUMENT_TYPE_TABS[this.activeTabIndex]?.documentType ?? NumberingDocumentType.Invoice
  );

  previewResult = computed(() => {
    const freeText = this.blocks().find((b) => b.type === NumberingBlockType.FreeText)?.value;
    return render(this.blocks(), this.startNumber(), new Date(), freeText);
  });

  previewText = computed(() => this.previewResult().value);
  previewError = computed(() => this.previewResult().error ?? 'Format invalide.');
  previewValid = computed(() => validateBlocks(this.blocks()).valid && !!this.previewText());

  blockDisplayLabel = blockDisplayLabel;

  ngOnInit(): void {
    this.loadSchemes();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  loadSchemes(): void {
    this.loading.set(true);
    this.numberingService
      .getSchemes()
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (res) => {
          if (res.success && res.data) {
            this.schemes.set(res.data);
            this.applySchemeForCurrentTab();
          } else {
            this.toast.add({
              severity: 'error',
              summary: 'Erreur',
              detail: res.errors?.join(', ') || 'Chargement impossible'
            });
          }
          this.loading.set(false);
        },
        error: (err) => {
          this.errorHandler.logError('Failed to load numbering schemes', err);
          this.toast.add({
            severity: 'error',
            summary: 'Erreur',
            detail:
              this.errorHandler.extractErrorMessage(err) ||
              'Impossible de charger les numérotations'
          });
          this.loading.set(false);
        }
      });
  }

  onTabChange(): void {
    this.applySchemeForCurrentTab();
  }

  private applySchemeForCurrentTab(): void {
    const docType = this.currentDocumentType();
    const scheme = this.schemes().find((s) => s.documentType === docType);
    if (!scheme) {
      return;
    }

    this.fiscalYear.set(scheme.fiscalYear);
    this.startNumber.set(scheme.startNumber);
    this.currentSequence.set(scheme.currentSequence);
    this.isFormatLocked.set(scheme.isFormatLocked);
    this.blocks.set(
      scheme.blocks.map((b, index) =>
        cloneBlock({ ...b, label: b.label ?? BLOCK_TYPE_LABELS[b.type] }, b.order ?? index)
      )
    );
  }

  paletteLabel(template: PaletteBlockTemplate): string {
    if (template.type === NumberingBlockType.Separator) {
      return `Séparateur « ${template.value ?? '-'} »`;
    }
    return BLOCK_TYPE_LABELS[template.type];
  }

  trackBlock(index: number, block: NumberingBlock): string {
    return `${block.type}-${block.order}-${index}`;
  }

  onDrop(event: CdkDragDrop<NumberingBlock[] | readonly PaletteBlockTemplate[]>): void {
    if (this.isFormatLocked()) {
      return;
    }

    if (event.previousContainer === event.container) {
      moveItemInArray(event.container.data as NumberingBlock[], event.previousIndex, event.currentIndex);
      this.reindexBlocks();
      return;
    }

    const template = event.previousContainer.data[event.previousIndex] as PaletteBlockTemplate;
    const newBlock = this.templateToBlock(template, event.currentIndex);
    const target = event.container.data as NumberingBlock[];
    target.splice(event.currentIndex, 0, newBlock);
    this.reindexBlocks();
  }

  private templateToBlock(template: PaletteBlockTemplate, order: number): NumberingBlock {
    let value = template.value;
    if (template.type === NumberingBlockType.FreeText && !value) {
      value = DEFAULT_FREE_TEXT[this.currentDocumentType()];
    }
    return {
      type: template.type,
      value,
      order,
      label: BLOCK_TYPE_LABELS[template.type]
    };
  }

  private reindexBlocks(): void {
    this.blocks.update((list) =>
      list.map((block, index) => ({ ...block, order: index }))
    );
  }

  onFreeTextChange(index: number, value: string): void {
    this.blocks.update((list) => {
      const copy = [...list];
      copy[index] = { ...copy[index], value };
      return copy;
    });
  }

  onSeparatorChange(index: number, value: string): void {
    this.blocks.update((list) => {
      const copy = [...list];
      copy[index] = { ...copy[index], value };
      return copy;
    });
  }

  onStartNumberChange(value: number | null): void {
    if (value !== null && value >= 1) {
      this.startNumber.set(value);
    }
  }

  removeBlock(index: number): void {
    if (this.isFormatLocked()) {
      return;
    }
    this.blocks.update((list) => {
      const copy = [...list];
      copy.splice(index, 1);
      return copy;
    });
    this.reindexBlocks();
  }

  save(): void {
    if (!this.canUpdate() || !this.previewValid()) {
      return;
    }

    const request = {
      startNumber: this.startNumber(),
      blocks: this.blocks().map((b, index) => ({
        type: b.type,
        value: b.value,
        order: index,
        label: b.label ?? BLOCK_TYPE_LABELS[b.type]
      }))
    };

    this.saving.set(true);
    this.numberingService
      .saveScheme(this.currentDocumentType(), request)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (res) => {
          this.saving.set(false);
          if (res.success && res.data) {
            this.toast.add({
              severity: 'success',
              summary: 'Succès',
              detail: res.message || 'Numérotation enregistrée'
            });
            this.schemes.update((list) => {
              const idx = list.findIndex((s) => s.documentType === res.data!.documentType);
              if (idx >= 0) {
                const updated = [...list];
                updated[idx] = res.data!;
                return updated;
              }
              return [...list, res.data!];
            });
            this.applySchemeForCurrentTab();
          } else {
            this.toast.add({
              severity: 'error',
              summary: 'Erreur',
              detail: res.errors?.join(', ') || 'Échec de l\'enregistrement'
            });
          }
        },
        error: (err) => {
          this.saving.set(false);
          this.toast.add({
            severity: 'error',
            summary: 'Erreur',
            detail:
              this.errorHandler.extractErrorMessage(err) ||
              'Échec de l\'enregistrement'
          });
        }
      });
  }

  confirmReset(): void {
    this.confirmation.confirm({
      header: 'Réinitialiser la numérotation',
      message:
        'Réinitialiser le format et le numéro de départ pour ce type de document ? Cette action est irréversible si des documents existent.',
      icon: 'pi pi-exclamation-triangle',
      acceptLabel: 'Réinitialiser',
      rejectLabel: 'Annuler',
      acceptButtonStyleClass: 'p-button-danger',
      accept: () => this.resetScheme()
    });
  }

  private resetScheme(): void {
    this.saving.set(true);
    this.numberingService
      .reset(this.currentDocumentType())
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (res) => {
          this.saving.set(false);
          if (res.success && res.data) {
            this.toast.add({
              severity: 'success',
              summary: 'Succès',
              detail: res.message || 'Numérotation réinitialisée'
            });
            this.schemes.update((list) => {
              const idx = list.findIndex((s) => s.documentType === res.data!.documentType);
              if (idx >= 0) {
                const updated = [...list];
                updated[idx] = res.data!;
                return updated;
              }
              return [...list, res.data!];
            });
            this.applySchemeForCurrentTab();
          } else {
            this.toast.add({
              severity: 'error',
              summary: 'Erreur',
              detail: res.errors?.join(', ') || 'Échec de la réinitialisation'
            });
          }
        },
        error: (err) => {
          this.saving.set(false);
          this.toast.add({
            severity: 'error',
            summary: 'Erreur',
            detail:
              this.errorHandler.extractErrorMessage(err) ||
              'Échec de la réinitialisation'
          });
        }
      });
  }
}
'''

def write_files() -> list[str]:
    written: list[str] = []
    for path, content in FILES.items():
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(content, encoding='utf-8')
        written.append(str(path.relative_to(ROOT)))
    return written


def patch_settings_routes() -> None:
    path = WEB / "features" / "settings" / "settings.routes.ts"
    text = path.read_text(encoding='utf-8')
    route_block = """  {
    path: 'numbering',
    canActivate: [platformSettingsGuard],
    loadComponent: () => import('./numbering/numbering.component').then(m => m.NumberingComponent),
    title: 'Numérotations - FactuTrust'
  },
"""
    if "'numbering'" not in text:
        text = text.replace(
            "  {\n    path: 'taxes',",
            route_block + "  {\n    path: 'taxes',"
        )
        path.write_text(text, encoding='utf-8')


def patch_settings_component() -> None:
    path = WEB / "features" / "settings" / "settings.component.ts"
    text = path.read_text(encoding='utf-8')
    card = """        {
          title: 'Numérotations',
          description: 'Configurez le format de numérotation et le numéro de départ pour chaque type de document',
          icon: 'pi pi-sort-numeric-down',
          route: 'numbering',
          color: 'var(--color-primary-500)'
        },
"""
    if "route: 'numbering'" not in text:
        text = text.replace(
            "          route: 'warehouses',\n          color: 'var(--color-neutral-600)'\n        }",
            "          route: 'warehouses',\n          color: 'var(--color-neutral-600)'\n        },\n" + card.rstrip()
        )
        path.write_text(text, encoding='utf-8')


if __name__ == '__main__':
    created = write_files()
    patch_settings_routes()
    patch_settings_component()
    print('Written files:')
    for f in created:
        print(f)
    print('Patched settings.routes.ts and settings.component.ts')
