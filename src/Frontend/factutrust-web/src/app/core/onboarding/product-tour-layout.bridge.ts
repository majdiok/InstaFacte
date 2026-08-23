import { Injectable } from '@angular/core';

/**
 * Layout hooks registered by MainLayout so the tour can expand the sidebar
 * without importing layout components into the tour service.
 */
@Injectable({ providedIn: 'root' })
export class ProductTourLayoutBridge {
  expandSidebar: () => void = () => undefined;
  closeAiPanel: () => void = () => undefined;
  expandNavSection: (tourId: string) => string | null = () => null;
  restoreNavSection: (label: string | null) => void = () => undefined;
  hasNavItems: () => boolean = () => false;
}
