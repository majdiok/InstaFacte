import { bootstrapApplication } from '@angular/platform-browser';
import { provideRouter, withViewTransitions } from '@angular/router';
import { provideHttpClient, withInterceptors, withFetch } from '@angular/common/http';
import { provideAnimations } from '@angular/platform-browser/animations';
import { APP_INITIALIZER, LOCALE_ID, importProvidersFrom, isDevMode } from '@angular/core';
import { registerLocaleData } from '@angular/common';
import localeFrTN from '@angular/common/locales/fr-TN';
import localeFrTNExtra from '@angular/common/locales/extra/fr-TN';
import { MarkdownModule } from 'ngx-markdown';
import { MessageService } from 'primeng/api';
import { AppComponent } from './app/app.component';
import { routes } from './app/app.routes';
import { authInterceptor } from './app/core/interceptors/auth.interceptor';
import { errorInterceptor } from './app/core/interceptors/error.interceptor';
import { AuthService } from './app/core/services/auth.service';
import { installDevConsoleNoiseFilter } from './app/core/utils/dev-console-noise-filter';

// Dev uniquement : masque le bruit console des extensions navigateur (MindStudio, etc.) pour que
// les vraies erreurs restent visibles. Tree-shaken en production (garde isDevMode()).
if (isDevMode()) {
  installDevConsoleNoiseFilter();
}

// Locale globale fr-TN : les pipes number/currency/date rendent le format tunisien
// (1 234,000) partout — avant, LOCALE_ID par défaut (en-US) donnait 1,234.000 dans
// les composants utilisant DecimalPipe sans locale explicite (ex. table-totals-bar).
registerLocaleData(localeFrTN, 'fr-TN', localeFrTNExtra);

bootstrapApplication(AppComponent, {
  providers: [
    { provide: LOCALE_ID, useValue: 'fr-TN' },
    provideRouter(routes, withViewTransitions()),
    provideHttpClient(
      withFetch(), // Utiliser fetch au lieu de XMLHttpRequest pour meilleure compatibilité avec Playwright
      withInterceptors([authInterceptor, errorInterceptor])
    ),
    provideAnimations(),
    importProvidersFrom(MarkdownModule.forRoot()),
    MessageService,
    {
      provide: APP_INITIALIZER,
      useFactory: (auth: AuthService) => () => auth.bootstrapRefresh(),
      deps: [AuthService],
      multi: true
    }
  ]
}).catch(err => console.error(err));
