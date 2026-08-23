import { Component, Input } from '@angular/core';
import { CommonModule, DatePipe } from '@angular/common';
import { ProjectDetail } from '../../project-api.service';
import { initialsFromName, isBtp } from '../../project-enums';

@Component({
  selector: 'app-project-overview-info-card',
  standalone: true,
  imports: [CommonModule, DatePipe],
  template: `
    @if (project) {
      <section class="proj-detail-card">
        <h3 class="proj-detail-card__title">Informations générales</h3>
        <dl class="proj-detail-dl">
          <dt>Client</dt>
          <dd>{{ project.clientName }}</dd>
          <dt>Chef de projet</dt>
          <dd class="proj-detail-dl__person">
            @if (project.ownerUserName) {
              <span class="proj-avatar">{{ initials(project.ownerUserName) }}</span>
              {{ project.ownerUserName }}
            } @else {
              —
            }
          </dd>
          <dt>Type</dt>
          <dd>{{ project.kindDisplay }}</dd>
          <dt>Facturation</dt>
          <dd>{{ project.billingModeDisplay }}</dd>
          <dt>Devise</dt>
          <dd>{{ project.currency }}</dd>
          <dt>Période</dt>
          <dd>
            {{ project.startDate ? (project.startDate | date:'shortDate') : '—' }}
            → {{ project.endDate ? (project.endDate | date:'shortDate') : '—' }}
          </dd>
          @if (isBtp(project.kind)) {
            <dt>Chantier</dt>
            <dd>{{ project.siteAddress || '—' }}</dd>
            <dt>N° marché</dt>
            <dd>{{ project.contractNumber || '—' }}</dd>
          }
        </dl>
        @if (project.description) {
          <p class="proj-detail-card__desc">{{ project.description }}</p>
        } @else {
          <p class="proj-detail-card__desc proj-detail-card__desc--muted">Pas de description.</p>
        }
      </section>
    }
  `
})
export class ProjectOverviewInfoCardComponent {
  @Input() project: ProjectDetail | null = null;
  readonly initials = initialsFromName;
  readonly isBtp = isBtp;
}
