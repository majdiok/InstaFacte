import { Injectable, inject, signal } from '@angular/core';

import { Router, NavigationEnd } from '@angular/router';

import { filter } from 'rxjs/operators';



export interface VolatileAnalysisPending {

  screenId: string;

  /** JSON string, already bounded client-side */

  analysisSummary: string;

}



/** Drop stale pending context after this many ms (manual send with autoSend:false). */

const STALE_PENDING_MS = 120_000;



interface StoredPending extends VolatileAnalysisPending {

  capturedAt: number;

}



/**

 * One-shot volatile context for the next POST /ai/chat. Cleared after {@link takePendingForSend},

 * {@link clear}, logout, or when stale after a route change.

 */

@Injectable({ providedIn: 'root' })

export class AiVolatileAnalysisStore {

  private readonly router = inject(Router);

  private readonly pending = signal<StoredPending | null>(null);



  constructor() {

    this.router.events.pipe(filter((e): e is NavigationEnd => e instanceof NavigationEnd)).subscribe(() => {

      const current = this.pending();

      if (!current) {

        return;

      }

      if (Date.now() - current.capturedAt > STALE_PENDING_MS) {

        this.pending.set(null);

      }

    });

  }



  setPending(value: VolatileAnalysisPending | null): void {

    if (!value) {

      this.pending.set(null);

      return;

    }

    this.pending.set({ ...value, capturedAt: Date.now() });

  }



  peek(): VolatileAnalysisPending | null {

    const v = this.pending();

    return v ? { screenId: v.screenId, analysisSummary: v.analysisSummary } : null;

  }



  /** Consumes and clears; call once when building the chat request body. */

  takePendingForSend(): VolatileAnalysisPending | null {

    const v = this.pending();

    this.pending.set(null);

    return v ? { screenId: v.screenId, analysisSummary: v.analysisSummary } : null;

  }



  clear(): void {

    this.pending.set(null);

  }

}

