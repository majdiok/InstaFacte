import { Injectable, inject, PLATFORM_ID } from '@angular/core';
import { DOCUMENT, isPlatformBrowser } from '@angular/common';

const CHAIN_THEME_LINK_ID_PREFIX = 'factutrust-chain-theme-';
const CHAIN_THEME_SCRIPT_ID_PREFIX = 'factutrust-chain-theme-script-';

const CHAIN_CSS_HREFS = [
  'assets/theme/chain/vendor/bootstrap/css/bootstrap.min.css',
  'assets/theme/chain/assets/css/fontawesome.css',
  'assets/theme/chain/assets/css/animated.css',
  'assets/theme/chain/assets/css/owl.css',
  'assets/theme/chain/assets/css/templatemo-chain-app-dev.css'
];

const CHAIN_JS_HREFS = [
  'assets/theme/chain/assets/js/animation.js'
];

/**
 * Charge et décharge les CSS et JS du thème Chain uniquement sur la page d'accueil,
 * pour éviter tout impact sur les autres routes (auth, dashboard, formulaires).
 */
@Injectable({ providedIn: 'root' })
export class HomeThemeService {
  private readonly doc = inject(DOCUMENT);
  private readonly platformId = inject(PLATFORM_ID);
  private loadedLinkElements: HTMLLinkElement[] = [];
  private loadedScriptElements: HTMLScriptElement[] = [];

  loadChainTheme(): void {
    if (!isPlatformBrowser(this.platformId) || !this.doc?.head) return;
    if (this.loadedLinkElements.length > 0) return;

    for (let i = 0; i < CHAIN_CSS_HREFS.length; i++) {
      const link = this.doc.createElement('link');
      link.rel = 'stylesheet';
      link.href = CHAIN_CSS_HREFS[i];
      link.id = `${CHAIN_THEME_LINK_ID_PREFIX}${i}`;
      this.doc.head.appendChild(link);
      this.loadedLinkElements.push(link);
    }

    for (let i = 0; i < CHAIN_JS_HREFS.length; i++) {
      const script = this.doc.createElement('script');
      script.type = 'text/javascript';
      script.src = CHAIN_JS_HREFS[i];
      script.id = `${CHAIN_THEME_SCRIPT_ID_PREFIX}${i}`;
      this.doc.body.appendChild(script);
      this.loadedScriptElements.push(script);
    }
  }

  unloadChainTheme(): void {
    if (!isPlatformBrowser(this.platformId) || !this.doc?.head) return;
    for (const link of this.loadedLinkElements) {
      link.remove();
    }
    this.loadedLinkElements = [];

    for (const script of this.loadedScriptElements) {
      script.remove();
    }
    this.loadedScriptElements = [];

    // Stop global window WOW object listeners and intervals from leaking into other angular routes
    if ((globalThis as any).wow && typeof (globalThis as any).wow.stop === 'function') {
      (globalThis as any).wow.stop();
    }
  }
}
