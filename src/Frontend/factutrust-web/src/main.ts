import { bootstrapApplication } from '@angular/platform-browser';
import { isDevMode } from '@angular/core';
import { registerLocaleData } from '@angular/common';
import localeFrTN from '@angular/common/locales/fr-TN';
import localeFrTNExtra from '@angular/common/locales/extra/fr-TN';
import { AppComponent } from './app/app.component';
import { appConfig } from './app/app.config';
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

bootstrapApplication(AppComponent, appConfig).catch(err => console.error(err));
