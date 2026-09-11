import { Routes } from '@angular/router';
import { permissionGuard } from '@core/guards/permission.guard';
import { PERMISSIONS } from '@core/config/permission-keys';

export const PROJECTS_ROUTES: Routes = [
  {
    path: 'dashboard',
    loadComponent: () => import('./pages/project-dashboard.component').then(m => m.ProjectDashboardComponent),
    title: 'Tableau de bord projets - FactuTrust',
    canActivate: [permissionGuard],
    data: { permissions: [PERMISSIONS.projects.read] }
  },
  {
    path: '',
    loadComponent: () => import('./project-list.component').then(m => m.ProjectListComponent),
    title: 'Projets - FactuTrust',
    canActivate: [permissionGuard],
    data: { permissions: [PERMISSIONS.projects.read] }
  },
  {
    path: 'time',
    redirectTo: '/timesheets',
    pathMatch: 'full'
  },
  {
    path: ':id',
    loadComponent: () => import('./project-detail.component').then(m => m.ProjectDetailComponent),
    title: 'Fiche projet - FactuTrust',
    canActivate: [permissionGuard],
    data: { permissions: [PERMISSIONS.projects.read] }
  },
  {
    path: ':id/tasks/:taskId',
    loadComponent: () => import('./project-task-detail.component').then(m => m.ProjectTaskDetailComponent),
    title: 'Tâche projet - FactuTrust',
    canActivate: [permissionGuard],
    data: { permissions: [PERMISSIONS.projectTasks.read] }
  }
];
