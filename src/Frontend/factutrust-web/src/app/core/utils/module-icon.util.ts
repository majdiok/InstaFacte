import { AppModule } from '@core/models/app-module';

/**
 * Icon/tone presentation mapping for `AppModule` — used by the Paramètres > Modules
 * page (plan v1 §2.2) and the adaptive dashboard's "Modules actifs" block (§2.5).
 * Purely cosmetic; never used for gating logic.
 */
export const MODULE_ICON_BY_ID: Record<AppModule, { icon: string; tone: string }> = {
  [AppModule.Clients]: { icon: 'pi-users', tone: 'tone-green' },
  [AppModule.Products]: { icon: 'pi-box', tone: 'tone-orange' },
  [AppModule.Sales]: { icon: 'pi-shopping-bag', tone: 'tone-blue' },
  [AppModule.Treasury]: { icon: 'pi-wallet', tone: 'tone-teal' },
  [AppModule.Reports]: { icon: 'pi-chart-bar', tone: 'tone-indigo' },
  [AppModule.Administration]: { icon: 'pi-cog', tone: 'tone-gray' },
  [AppModule.Purchases]: { icon: 'pi-shopping-cart', tone: 'tone-green' },
  [AppModule.Stock]: { icon: 'pi-building', tone: 'tone-teal' },
  [AppModule.Accounting]: { icon: 'pi-file', tone: 'tone-yellow' },
  [AppModule.CRM]: { icon: 'pi-heart', tone: 'tone-pink' },
  [AppModule.Fiscal]: { icon: 'pi-percentage', tone: 'tone-purple' },
  [AppModule.AI]: { icon: 'pi-sparkles', tone: 'tone-indigo' },
  [AppModule.Forecasting]: { icon: 'pi-chart-line', tone: 'tone-blue' },
  [AppModule.Studio]: { icon: 'pi-th-large', tone: 'tone-gray' },
  [AppModule.Payroll]: { icon: 'pi-id-card', tone: 'tone-orange' },
  [AppModule.Honoraires]: { icon: 'pi-briefcase', tone: 'tone-gray' },
  [AppModule.Projects]: { icon: 'pi-clipboard', tone: 'tone-indigo' },
  [AppModule.RecurringContracts]: { icon: 'pi-refresh', tone: 'tone-blue' }
};
