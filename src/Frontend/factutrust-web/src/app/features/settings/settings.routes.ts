import { Routes } from '@angular/router';
import { adminGuard } from '@core/guards/admin.guard';
import { permissionGuard } from '@core/guards/permission.guard';
import { platformSettingsGuard } from '@core/guards/platform-settings.guard';
import { storefrontManageGuard } from '@core/guards/storefront-manage.guard';
import { PERMISSIONS } from '@core/config/permission-keys';

export const SETTINGS_ROUTES: Routes = [
  {
    path: '',
    canActivate: [platformSettingsGuard],
    loadComponent: () => import('./settings.component').then(m => m.SettingsComponent),
    title: 'Paramètres - InstaFact'
  },
  {
    path: 'profile',
    loadComponent: () => import('./profile/profile.component').then(m => m.ProfileComponent),
    title: 'Mon profil - InstaFact'
  },
  {
    path: 'company',
    canActivate: [platformSettingsGuard],
    loadComponent: () => import('./company/company.component').then(m => m.CompanyComponent),
    title: 'Mon entreprise - InstaFact'
  },
  {
    path: 'subscription',
    canActivate: [platformSettingsGuard],
    loadComponent: () => import('./subscription/subscription.component').then(m => m.SubscriptionComponent),
    title: 'Mon abonnement - InstaFact'
  },
  {
    path: 'numbering',
    canActivate: [platformSettingsGuard],
    loadComponent: () => import('./numbering/numbering.component').then(m => m.NumberingComponent),
    title: 'Numérotations - InstaFact'
  },
  {
    path: 'templates',
    canActivate: [platformSettingsGuard],
    loadComponent: () => import('./templates/templates.component').then(m => m.DocumentTemplatesComponent),
    title: 'Modèles de documents - InstaFact'
  },
  {
    path: 'payment-terms',
    canActivate: [platformSettingsGuard],
    loadComponent: () =>
      import('./payment-terms/payment-terms.component').then(m => m.PaymentTermsComponent),
    title: 'Conditions de règlement - InstaFact'
  },
  {
    path: 'taxes',
    canActivate: [platformSettingsGuard],
    loadComponent: () => import('./taxes/taxes.component').then(m => m.TaxesComponent),
    title: 'Taxes et TVA - InstaFact'
  },
  {
    path: 'promotions',
    canActivate: [permissionGuard],
    data: { permissions: [PERMISSIONS.pricing.read] },
    loadComponent: () =>
      import('./promotions/promotions-page.component').then(m => m.PromotionsPageComponent),
    title: 'Promotions - InstaFact'
  },
  {
    path: 'variant-axes',
    canActivate: [platformSettingsGuard],
    loadChildren: () =>
      import('./variant-axes/variant-axes.routes').then(m => m.VARIANT_AXES_ROUTES)
  },
  {
    path: 'warehouses',
    canActivate: [platformSettingsGuard],
    loadComponent: () => import('./warehouses/warehouses.component').then(m => m.WarehousesComponent),
    title: 'Entrepôts - InstaFact'
  },
  {
    path: 'users',
    canActivate: [platformSettingsGuard, adminGuard],
    loadComponent: () => import('./users/tenant-users-list.component').then(m => m.TenantUsersListComponent),
    title: 'Utilisateurs - InstaFact'
  },
  {
    path: 'users/add',
    canActivate: [platformSettingsGuard, adminGuard],
    loadComponent: () => import('./users/tenant-users-bulk.component').then(m => m.TenantUsersBulkComponent),
    title: 'Ajouter des utilisateurs - InstaFact'
  },
  {
    path: 'storefront',
    canActivate: [platformSettingsGuard, storefrontManageGuard],
    loadComponent: () =>
      import('./storefront/storefront-settings.component').then(m => m.StorefrontSettingsComponent),
    title: 'Vitrine publique - InstaFact'
  },
  {
    path: 'accounting-firm',
    canActivate: [platformSettingsGuard, adminGuard],
    loadComponent: () =>
      import('./accounting-firm/accounting-firm-settings.component').then(m => m.AccountingFirmSettingsComponent),
    title: 'Cabinet comptable - InstaFact'
  },
  {
    // Page personnelle (chaque utilisateur gère SA liaison WhatsApp) : pas de platformSettingsGuard,
    // même patron que la route `profile`.
    path: 'channels',
    loadComponent: () =>
      import('./channels/channels-settings.component').then(m => m.ChannelsSettingsComponent),
    title: 'WhatsApp - InstaFact'
  }
];
