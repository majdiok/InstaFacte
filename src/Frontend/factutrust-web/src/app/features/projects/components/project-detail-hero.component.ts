import { Component, EventEmitter, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { StatusBadgeComponent } from '@shared/components/status-badge/status-badge.component';
import { ButtonComponent } from '@shared/components/button/button.component';
import { DocumentActionsMenuComponent } from '@shared/components/document-actions-menu/document-actions-menu.component';
import { MenuItem } from '@shared/models/menu-item.model';
import { ProjectDetail } from '../project-api.service';
import {
  canActivate,
  canCancel,
  canComplete,
  canEditProject,
  canHold,
  projectStatusBadge
} from '../project-enums';

@Component({
  selector: 'app-project-detail-hero',
  standalone: true,
  imports: [CommonModule, StatusBadgeComponent, ButtonComponent, DocumentActionsMenuComponent],
  template: `
    @if (project) {
      <header class="proj-detail-hero">
        <div class="proj-detail-hero__main">
          <div class="proj-detail-hero__title-row">
            <h1 class="proj-detail-hero__title">{{ project.name }}</h1>
            <app-status-badge [status]="statusBadge(project.status)" [label]="project.statusDisplay" />
          </div>
          <p class="proj-detail-hero__meta">
            <span><i class="pi pi-building"></i> {{ project.clientName }}</span>
            <span class="proj-detail-hero__dot">·</span>
            <span>{{ project.kindDisplay }}</span>
            @if (showSiteChip && project.siteAddress) {
              <span class="proj-detail-hero__dot">·</span>
              <span><i class="pi pi-map-marker"></i> {{ project.siteAddress }}</span>
            }
          </p>
        </div>
        <div class="proj-detail-hero__actions">
          @if (canUpdate && canActivate(project.status)) {
            <app-button variant="secondary" icon="pi-play" (click)="activate.emit()">Activer</app-button>
          }
          @if (canUpdate && canHold(project.status)) {
            <app-button variant="secondary" icon="pi-pause" (click)="hold.emit()">Mettre en pause</app-button>
          }
          @if (canUpdate && canComplete(project.status)) {
            <app-button variant="secondary" icon="pi-check" (click)="complete.emit()">Clôturer</app-button>
          }
          @if (canUpdate && canEditProject(project.status)) {
            <app-button variant="primary" icon="pi-pencil" (click)="edit.emit()">Modifier le projet</app-button>
          }
          <app-document-actions-menu [items]="menuItems" label="" ariaLabel="Actions du projet" />
        </div>
      </header>
    }
  `
})
export class ProjectDetailHeroComponent {
  @Input() project: ProjectDetail | null = null;
  @Input() canUpdate = false;
  @Input() showSiteChip = false;
  @Input() favorite = false;

  @Output() activate = new EventEmitter<void>();
  @Output() hold = new EventEmitter<void>();
  @Output() complete = new EventEmitter<void>();
  @Output() cancel = new EventEmitter<void>();
  @Output() edit = new EventEmitter<void>();
  @Output() toggleFavorite = new EventEmitter<void>();
  @Output() goList = new EventEmitter<void>();

  readonly canActivate = canActivate;
  readonly canHold = canHold;
  readonly canComplete = canComplete;
  readonly canCancel = canCancel;
  readonly canEditProject = canEditProject;
  readonly statusBadge = projectStatusBadge;

  get menuItems(): MenuItem[] {
    const p = this.project;
    const items: MenuItem[] = [
      {
        label: this.favorite ? 'Retirer des favoris' : 'Ajouter aux favoris',
        icon: this.favorite ? 'pi pi-star-fill' : 'pi pi-star',
        command: () => this.toggleFavorite.emit()
      },
      { separator: true },
      {
        label: 'Retour à la liste',
        icon: 'pi pi-arrow-left',
        command: () => this.goList.emit()
      }
    ];
    if (this.canUpdate && p && canCancel(p.status)) {
      items.unshift({
        label: 'Annuler le projet',
        icon: 'pi pi-times-circle',
        command: () => this.cancel.emit()
      });
    }
    return items;
  }
}
