import { bootstrapApplication } from '@angular/platform-browser';
import { provideRouter, withViewTransitions } from '@angular/router';
import { provideHttpClient, withInterceptors, withFetch } from '@angular/common/http';
import { provideAnimations } from '@angular/platform-browser/animations';
import { APP_INITIALIZER, importProvidersFrom, isDevMode } from '@angular/core';
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

bootstrapApplication(AppComponent, {
  providers: [
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
