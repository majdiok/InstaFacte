import { Injectable, inject, OnDestroy, signal } from '@angular/core';
import { ProductService, ProductListItem } from '@core/services/product.service';
import { WarehouseContextService } from '@core/services/warehouse-context.service';
import { PosStateService } from './pos-state.service';

export type BarcodeScanResult = 'added' | 'not_found' | 'multiple';

export interface BarcodeScanEvent {
  code: string;
  timestamp: number;
  result: BarcodeScanResult;
}

/**
 * Detects barcode scanner input (rapid keypresses ending with Enter)
 * and adds the matching product to the order.
 */
@Injectable({
  providedIn: 'root'
})
export class PosBarcodeService implements OnDestroy {
  private readonly productService = inject(ProductService);
  private readonly warehouseContext = inject(WarehouseContextService);
  private readonly posState = inject(PosStateService);

  readonly lastScannedCode = signal<string | null>(null);
  readonly lastScanResult = signal<BarcodeScanResult | null>(null);

  private buffer = '';
  private bufferTimeout: ReturnType<typeof setTimeout> | null = null;
  private readonly BUFFER_MS = 50;
  private readonly MIN_LENGTH = 3;

  private boundHandler: (e: KeyboardEvent) => void = this.handleKeydown.bind(this);

  startListening(): void {
    if (typeof document === 'undefined') return;
    document.addEventListener('keydown', this.boundHandler, true);
  }

  stopListening(): void {
    if (typeof document === 'undefined') return;
    document.removeEventListener('keydown', this.boundHandler, true);
    this.clearBuffer();
  }

  ngOnDestroy(): void {
    this.stopListening();
  }

  private handleKeydown(event: KeyboardEvent): void {
    const isInput = event.target instanceof HTMLInputElement || event.target instanceof HTMLTextAreaElement;
    if (isInput && event.key !== 'Enter') return;

    if (event.key === 'Enter') {
      if (isInput) return;
      event.preventDefault();
      this.flushBuffer();
      return;
    }

    if (event.key.length === 1 && !event.ctrlKey && !event.metaKey && !event.altKey && !isInput) {
      this.appendToBuffer(event.key);
    }
  }

  private appendToBuffer(char: string): void {
    if (this.bufferTimeout) {
      clearTimeout(this.bufferTimeout);
    }
    this.buffer += char;
    this.bufferTimeout = setTimeout(() => this.clearBuffer(), this.BUFFER_MS);
  }

  private clearBuffer(): void {
    this.buffer = '';
    if (this.bufferTimeout) {
      clearTimeout(this.bufferTimeout);
      this.bufferTimeout = null;
    }
  }

  private flushBuffer(): void {
    if (this.bufferTimeout) {
      clearTimeout(this.bufferTimeout);
      this.bufferTimeout = null;
    }
    const code = this.buffer.trim();
    this.clearBuffer();

    if (code.length < this.MIN_LENGTH) return;

    this.lastScannedCode.set(code);
    this.searchAndAdd(code);
  }

  /**
   * Résout un code scanné en article, en correspondance EXACTE sur le code-barres.
   *
   * L'implémentation précédente cherchait dans le CODE PRODUIT interne, puis retombait sur
   * un `includes()` bidirectionnel : scanner « 1234 » pouvait ajouter l'article « 12345 »,
   * et scanner « 12345 » l'article « 1234 ». En caisse, cela encaisse le mauvais article et
   * fausse le stock. On n'approxime plus : ce qui ne correspond pas échoue.
   *
   * Repli volontaire sur le code interne uniquement en correspondance STRICTE, pour les
   * catalogues dont les articles n'ont pas encore d'EAN renseigné.
   */
  private searchAndAdd(code: string): void {
    this.productService.getProductByBarcode(code).subscribe({
      next: response => {
        if (response.success && response.data) {
          this.posState.addProduct(response.data);
          this.lastScanResult.set('added');
          return;
        }
        this.fallbackToExactInternalCode(code);
      },
      error: () => this.fallbackToExactInternalCode(code)
    });
  }

  /**
   * Repli sur le code produit interne, en égalité STRICTE et insensible à la casse.
   * Aucune correspondance partielle : plusieurs candidats signalent l'ambiguïté au caissier
   * plutôt que d'en choisir un.
   */
  private fallbackToExactInternalCode(code: string): void {
    this.productService.getProducts({
      search: code,
      isActive: true,
      page: 1,
      pageSize: 10,
      warehouseId: this.warehouseContext.selectedWarehouseId() ?? undefined
    }).subscribe({
      next: response => {
        if (!response.success || !response.data) {
          this.lastScanResult.set('not_found');
          return;
        }

        const normalized = code.trim().toLowerCase();
        const matches = response.data.items.filter(
          p => p.code.trim().toLowerCase() === normalized
        );

        if (matches.length === 1) {
          this.posState.addProduct(matches[0]);
          this.lastScanResult.set('added');
        } else if (matches.length > 1) {
          this.lastScanResult.set('multiple');
        } else {
          this.lastScanResult.set('not_found');
        }
      },
      error: () => this.lastScanResult.set('not_found')
    });
  }

  /**
   * Recherche déclenchée depuis le catalogue. Même exigence d'exactitude que le scan.
   */
  searchByCode(code: string): void {
    this.lastScannedCode.set(code);
    this.searchAndAdd(code);
  }
}
