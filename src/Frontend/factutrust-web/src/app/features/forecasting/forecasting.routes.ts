import { Routes } from '@angular/router';
import { permissionGuard } from '@core/guards/permission.guard';

const FORECASTING_VIEW = 'forecasting:view';
const TREASURY_FORECAST_VIEW = 'treasury_forecast:view';

export const FORECASTING_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () =>
      import('./pages/forecasting-hub/forecasting-hub.component').then(m => m.ForecastingHubComponent),
    canActivate: [permissionGuard],
    data: { permissions: [FORECASTING_VIEW] },
    title: 'Prévisions IA — InstaFact',
    children: [
      {
        path: '',
        redirectTo: 'revenue',
        pathMatch: 'full'
      },
      {
        path: 'revenue',
        loadComponent: () =>
          import('./pages/revenue-forecast/revenue-forecast.component').then(m => m.RevenueForecastComponent),
        canActivate: [permissionGuard],
        data: { permissions: [FORECASTING_VIEW] }
      },
      {
        // Single canonical URL since the 2026-05-13 V1 cutover.
        path: 'replenishment',
        loadComponent: () =>
          import('./pages/replenishment-board/replenishment-board.component').then(m => m.ReplenishmentBoardComponent),
        canActivate: [permissionGuard],
        data: { permissions: [FORECASTING_VIEW] }
      },
      {
        path: 'promotions',
        loadComponent: () =>
          import('./pages/promotions-board/promotions-board.component').then(m => m.PromotionsBoardComponent),
        canActivate: [permissionGuard],
        data: { permissions: [FORECASTING_VIEW] }
      },
      {
        path: 'abc-xyz',
        loadComponent: () =>
          import('./pages/abc-xyz-matrix/abc-xyz-matrix.component').then(m => m.AbcXyzMatrixComponent),
        canActivate: [permissionGuard],
        data: { permissions: [FORECASTING_VIEW] }
      },
      {
        path: 'calendar',
        loadComponent: () =>
          import('./pages/calendar-impact/calendar-impact.component').then(m => m.CalendarImpactComponent),
        canActivate: [permissionGuard],
        data: { permissions: [FORECASTING_VIEW] }
      },
      {
        // Même écran que /treasury/cash-forecast : le prévisionnel de trésorerie appartient à la
        // famille « Prévisions IA », mais sa permission est celle du module Trésorerie — le rôle
        // Comptable possède la seconde et pas la première.
        path: 'treasury',
        loadComponent: () =>
          import('../treasury/cash-forecast/cash-forecast.component').then(m => m.CashForecastComponent),
        canActivate: [permissionGuard],
        data: { permissions: [TREASURY_FORECAST_VIEW] }
      }
    ]
  }
];
