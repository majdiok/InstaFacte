import { Routes } from '@angular/router';

export const POS_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () => import('./pos.component').then(m => m.PosComponent),
    title: 'Point de Vente - InstaFact',
    data: { fullWidth: true, hideLayout: true }
  },
  {
    path: 'customer-display',
    loadComponent: () => import('./components/customer-display/customer-display.component').then(m => m.CustomerDisplayComponent),
    title: 'Affichage client - POS',
    data: { fullWidth: true, hideLayout: true }
  }
];
