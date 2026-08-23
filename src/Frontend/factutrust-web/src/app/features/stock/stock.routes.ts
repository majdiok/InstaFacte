import { Routes } from '@angular/router';
import { permissionGuard } from '@core/guards/permission.guard';
import { PERMISSIONS } from '@core/config/permission-keys';

export const STOCK_ROUTES: Routes = [
    {
        path: '',
        loadComponent: () => import('./stock-simple/stock-simple.component').then(m => m.StockSimpleComponent),
        title: 'Mon Stock - InstaFact'
    },
    {
        path: 'advanced',
        loadComponent: () => import('./stock-list/stock-list.component').then(m => m.StockListComponent),
        title: 'Gestion du Stock (Avancée) - InstaFact'
    },
    {
        path: 'entries/new',
        canActivate: [permissionGuard],
        data: { permissions: [PERMISSIONS.stockVouchers.create], kind: 'Entry' },
        loadComponent: () => import('../stock-vouchers/stock-voucher-form.component').then(m => m.StockVoucherFormComponent),
        title: "Nouveau bon d'entrée - InstaFact"
    },
    {
        path: 'entries/:id/edit',
        canActivate: [permissionGuard],
        data: { permissions: [PERMISSIONS.stockVouchers.update], kind: 'Entry' },
        loadComponent: () => import('../stock-vouchers/stock-voucher-form.component').then(m => m.StockVoucherFormComponent),
        title: "Modifier bon d'entrée - InstaFact"
    },
    {
        path: 'entries/:id',
        canActivate: [permissionGuard],
        data: { permissions: [PERMISSIONS.stockVouchers.read], kind: 'Entry' },
        loadComponent: () => import('../stock-vouchers/stock-voucher-detail.component').then(m => m.StockVoucherDetailComponent),
        title: "Détail bon d'entrée - InstaFact"
    },
    {
        path: 'entries',
        canActivate: [permissionGuard],
        data: { permissions: [PERMISSIONS.stockVouchers.read], kind: 'Entry' },
        loadComponent: () => import('../stock-vouchers/stock-voucher-list.component').then(m => m.StockVoucherListComponent),
        title: "Bons d'entrée - InstaFact"
    },
    {
        path: 'issues/new',
        canActivate: [permissionGuard],
        data: { permissions: [PERMISSIONS.stockVouchers.create], kind: 'Issue' },
        loadComponent: () => import('../stock-vouchers/stock-voucher-form.component').then(m => m.StockVoucherFormComponent),
        title: 'Nouveau bon de sortie - InstaFact'
    },
    {
        path: 'issues/:id/edit',
        canActivate: [permissionGuard],
        data: { permissions: [PERMISSIONS.stockVouchers.update], kind: 'Issue' },
        loadComponent: () => import('../stock-vouchers/stock-voucher-form.component').then(m => m.StockVoucherFormComponent),
        title: 'Modifier bon de sortie - InstaFact'
    },
    {
        path: 'issues/:id',
        canActivate: [permissionGuard],
        data: { permissions: [PERMISSIONS.stockVouchers.read], kind: 'Issue' },
        loadComponent: () => import('../stock-vouchers/stock-voucher-detail.component').then(m => m.StockVoucherDetailComponent),
        title: 'Détail bon de sortie - InstaFact'
    },
    {
        path: 'issues',
        canActivate: [permissionGuard],
        data: { permissions: [PERMISSIONS.stockVouchers.read], kind: 'Issue' },
        loadComponent: () => import('../stock-vouchers/stock-voucher-list.component').then(m => m.StockVoucherListComponent),
        title: 'Bons de sortie - InstaFact'
    },
    {
        path: 'entry',
        redirectTo: '/stock/entries/new',
        pathMatch: 'full'
    },
    {
        path: 'exit',
        redirectTo: '/stock/issues/new',
        pathMatch: 'full'
    },
    {
        path: 'advanced/entry',
        redirectTo: '/stock/entries/new',
        pathMatch: 'full'
    },
    {
        path: 'advanced/exit',
        redirectTo: '/stock/issues/new',
        pathMatch: 'full'
    },
    {
        path: 'advanced/:id/history',
        loadComponent: () => import('./stock-history/stock-history.component').then(m => m.StockHistoryComponent),
        title: 'Historique mouvements (Avancée) - InstaFact'
    },
    {
        path: ':id/history',
        loadComponent: () => import('./stock-history/stock-history.component').then(m => m.StockHistoryComponent),
        title: 'Historique mouvements - InstaFact'
    }
];
