import { Routes } from '@angular/router';
import { permissionGuard } from '@core/guards/permission.guard';
import { PERMISSIONS } from '@core/config/permission-keys';

export const STOCK_ROUTES: Routes = [
    {
        // Vue simplifiée par défaut (pour utilisateurs non techniciens)
        path: '',
        loadComponent: () => import('./stock-simple/stock-simple.component').then(m => m.StockSimpleComponent),
        title: 'Mon Stock - InstaFact'
    },
    {
        // Vue technique avancée (pour utilisateurs expérimentés)
        path: 'advanced',
        loadComponent: () => import('./stock-list/stock-list.component').then(m => m.StockListComponent),
        title: 'Gestion du Stock (Avancée) - InstaFact'
    },
    {
        // Entrée de stock depuis la vue avancée
        path: 'advanced/entry',
        canActivate: [permissionGuard],
        data: { permissions: [PERMISSIONS.stock.create] },
        loadComponent: () => import('./stock-entry-form/stock-entry-form.component').then(m => m.StockEntryFormComponent),
        title: 'Nouvelle entrée (Avancée) - InstaFact'
    },
    {
        // Sortie de stock depuis la vue avancée
        path: 'advanced/exit',
        canActivate: [permissionGuard],
        data: { permissions: [PERMISSIONS.stock.create] },
        loadComponent: () => import('./stock-exit-form/stock-exit-form.component').then(m => m.StockExitFormComponent),
        title: 'Nouvelle sortie (Avancée) - InstaFact'
    },
    {
        // Historique des mouvements depuis la vue avancée
        path: 'advanced/:id/history',
        loadComponent: () => import('./stock-history/stock-history.component').then(m => m.StockHistoryComponent),
        title: 'Historique mouvements (Avancée) - InstaFact'
    },
    {
        path: 'entry',
        canActivate: [permissionGuard],
        data: { permissions: [PERMISSIONS.stock.create] },
        loadComponent: () => import('./stock-entry-form/stock-entry-form.component').then(m => m.StockEntryFormComponent),
        title: 'Nouvelle entrée - InstaFact'
    },
    {
        path: 'exit',
        canActivate: [permissionGuard],
        data: { permissions: [PERMISSIONS.stock.create] },
        loadComponent: () => import('./stock-exit-form/stock-exit-form.component').then(m => m.StockExitFormComponent),
        title: 'Nouvelle sortie - InstaFact'
    },
    {
        path: ':id/history',
        loadComponent: () => import('./stock-history/stock-history.component').then(m => m.StockHistoryComponent),
        title: 'Historique mouvements - InstaFact'
    }
];

