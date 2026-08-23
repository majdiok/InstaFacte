import { Component, EventEmitter, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import {
  ProjectActivity,
  ProjectAssignableUser,
  ProjectBudget,
  ProjectDetail,
  ProjectMember,
  ProjectTask
} from '../project-api.service';
import { ProjectOverviewInfoCardComponent } from '../components/overview/project-overview-info-card.component';
import { ProjectOverviewBudgetCardComponent } from '../components/overview/project-overview-budget-card.component';
import { ProjectOverviewSummaryCardComponent } from '../components/overview/project-overview-summary-card.component';
import { ProjectTaskProgressCardComponent } from '../components/overview/project-task-progress-card.component';
import { ProjectTeamPreviewCardComponent } from '../components/overview/project-team-preview-card.component';
import { ProjectUpcomingDeadlinesCardComponent } from '../components/overview/project-upcoming-deadlines-card.component';
import { ProjectActivityTimelineComponent } from '../components/overview/project-activity-timeline.component';

@Component({
  selector: 'app-project-overview-tab',
  standalone: true,
  imports: [
    CommonModule,
    ProjectOverviewInfoCardComponent,
    ProjectOverviewBudgetCardComponent,
    ProjectOverviewSummaryCardComponent,
    ProjectTaskProgressCardComponent,
    ProjectTeamPreviewCardComponent,
    ProjectUpcomingDeadlinesCardComponent,
    ProjectActivityTimelineComponent
  ],
  template: `
    @if (project) {
      <div class="proj-detail-overview-grid">
        <app-project-overview-info-card class="proj-detail-overview-grid__info" [project]="project" />
        <app-project-overview-budget-card
          class="proj-detail-overview-grid__budget"
          [project]="project"
          [budget]="budget"
          (viewBudget)="viewBudget.emit()" />
        <app-project-overview-summary-card
          class="proj-detail-overview-grid__summary"
          [project]="project"
          [budget]="budget"
          [tasks]="tasks" />
        <app-project-task-progress-card class="proj-detail-overview-grid__tasks" [tasks]="tasks" />
        <app-project-team-preview-card
          class="proj-detail-overview-grid__team"
          [members]="members"
          (viewTeam)="viewTeam.emit()" />
        <app-project-upcoming-deadlines-card
          class="proj-detail-overview-grid__deadlines"
          [project]="project"
          [tasks]="tasks" />
        <app-project-activity-timeline
          class="proj-detail-overview-grid__activity"
          [activities]="activities"
          [users]="users"
          [compact]="true"
          (viewAll)="viewActivity.emit()" />
      </div>
    }
  `
})
export class ProjectOverviewTabComponent {
  @Input() project: ProjectDetail | null = null;
  @Input() budget: ProjectBudget | null = null;
  @Input() tasks: ProjectTask[] = [];
  @Input() members: ProjectMember[] = [];
  @Input() users: ProjectAssignableUser[] = [];
  @Input() activities: ProjectActivity[] = [];

  @Output() viewBudget = new EventEmitter<void>();
  @Output() viewTeam = new EventEmitter<void>();
  @Output() viewActivity = new EventEmitter<void>();
}
