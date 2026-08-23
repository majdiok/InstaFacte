import { APP_INITIALIZER, ApplicationConfig, LOCALE_ID, importProvidersFrom } from '@angular/core';
import { provideRouter, withViewTransitions } from '@angular/router';
import { provideHttpClient, withFetch, withInterceptors } from '@angular/common/http';
import { provideAnimations } from '@angular/platform-browser/animations';
import { DOCUMENT } from '@angular/common';
import { MarkdownModule } from 'ngx-markdown';
import { MessageService } from 'primeng/api';
import { providePrimeNG } from 'primeng/config';
import Aura from '@primeuix/themes/aura';
import { routes } from './app.routes';
import { authInterceptor } from './core/interceptors/auth.interceptor';
import { errorInterceptor } from './core/interceptors/error.interceptor';
import { AuthService } from './core/services/auth.service';

export const appConfig: ApplicationConfig = {
  providers: [
    { provide: DOCUMENT, useFactory: () => document },
    { provide: LOCALE_ID, useValue: 'fr-TN' },
    provideRouter(routes, withViewTransitions()),
    provideHttpClient(
      withFetch(),
      withInterceptors([authInterceptor, errorInterceptor])
    ),
    provideAnimations(),
    providePrimeNG({
      theme: {
        preset: Aura,
        options: {
          darkModeSelector: '.dark-mode'
        }
      }
    }),
    importProvidersFrom(MarkdownModule.forRoot()),
    MessageService,
    {
      provide: APP_INITIALIZER,
      useFactory: (auth: AuthService) => () => auth.bootstrapRefresh(),
      deps: [AuthService],
      multi: true
    }
  ]
};
