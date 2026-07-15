import { Routes } from '@angular/router';

export const DOCUMENTATION_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () => import('./documentation.component').then(m => m.DocumentationComponent),
    title: 'Documentation - InstaFact'
  },
  {
    path: ':chapterId',
    loadComponent: () => import('./doc-viewer/doc-viewer.component').then(m => m.DocViewerComponent),
    title: 'Documentation - InstaFact'
  }
];
