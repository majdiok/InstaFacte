import { ApplicationConfig, LOCALE_ID } from '@angular/core';
import { provideRouter } from '@angular/router';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { provideAnimations } from '@angular/platform-browser/animations';
import { registerLocaleData } from '@angular/common';
import localeFrTn from '@angular/common/locales/fr-TN';
import localeFrTnExtra from '@angular/common/locales/extra/fr-TN';
import { MessageService } from 'primeng/api';
import { providePrimeNG } from 'primeng/config';
import Aura from '@primeuix/themes/aura';
import { routes } from './app.routes';
import { platformAuthInterceptor } from '@core/interceptors/platform-auth.interceptor';

// Enregistre la locale fr-TN (Tunisie) — exigée par DatePipe / DecimalPipe / PercentPipe
// avec format de date dd/MM/yyyy, séparateur décimal "," et symbole monétaire TND.
registerLocaleData(localeFrTn, 'fr-TN', localeFrTnExtra);

export const appConfig: ApplicationConfig = {
  providers: [
    provideRouter(routes),
    provideHttpClient(withInterceptors([platformAuthInterceptor])),
    provideAnimations(),
    providePrimeNG({
      theme: {
        preset: Aura,
        options: {
          darkModeSelector: '.dark-mode'
        }
      }
    }),
    { provide: LOCALE_ID, useValue: 'fr-TN' },
    MessageService
  ]
};
