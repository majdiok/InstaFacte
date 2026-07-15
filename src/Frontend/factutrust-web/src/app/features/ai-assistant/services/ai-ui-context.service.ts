import { Injectable, inject } from '@angular/core';
import { Router } from '@angular/router';
import { ChatUiContext } from '../models/ai-chat.models';

/**
 * Builds a non-persisted snapshot of the current shell route for POST /ai/chat.
 */
@Injectable({ providedIn: 'root' })
export class AiUiContextService {
  private readonly router = inject(Router);

  buildSnapshot(): ChatUiContext | undefined {
    const url = this.router.url;
    if (!url || url === '/') {
      return undefined;
    }

    const qIndex = url.indexOf('?');
    const path = (qIndex >= 0 ? url.slice(0, qIndex) : url).trim();
    if (!path) {
      return undefined;
    }

    let queryParams: Record<string, string> | undefined;
    if (qIndex >= 0) {
      const search = url.slice(qIndex + 1);
      const params = new URLSearchParams(search);
      queryParams = {};
      params.forEach((v, k) => {
        queryParams![k] = v;
      });
      if (Object.keys(queryParams).length === 0) {
        queryParams = undefined;
      }
    }

    const entity = this.tryInferEntity(path);

    return {
      route: path,
      ...(queryParams ? { queryParams } : {}),
      ...(entity ? { entity } : {})
    };
  }

  private tryInferEntity(path: string): { type: string; id: string } | undefined {
    const segments = path.split('/').filter(Boolean);
    if (segments.length < 2) {
      return undefined;
    }

    const uuidRe = /^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i;
    const last = segments[segments.length - 1];
    if (!uuidRe.test(last)) {
      return undefined;
    }

    const parent = segments[segments.length - 2].toLowerCase();
    const typeMap: Record<string, string> = {
      invoices: 'invoice',
      quotes: 'quote',
      clients: 'client',
      products: 'product'
    };
    const type = typeMap[parent];
    return type ? { type, id: last } : undefined;
  }
}
