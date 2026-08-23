import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { TabsModule } from 'primeng/tabs';
import { InputTextModule } from 'primeng/inputtext';
import { InputNumberModule } from 'primeng/inputnumber';
import { SelectModule } from 'primeng/select';
import { DatePickerModule } from 'primeng/datepicker';
import { Textarea } from 'primeng/textarea';
import { ProgressBarModule } from 'primeng/progressbar';
import { TableModule } from 'primeng/table';
import { PageHeaderComponent } from '@shared/components/page-header/page-header.component';
import { ButtonComponent } from '@shared/components/button/button.component';
import { BreadcrumbComponent, BreadcrumbItem } from '@shared/components/breadcrumb/breadcrumb.component';
import { StatusBadgeComponent } from '@shared/components/status-badge/status-badge.component';
import { AuthService } from '@core/services/auth.service';
import { ToastService } from '@core/services/toast.service';
import { ErrorHandlerService } from '@core/services/error-handler.service';
import { ConfirmationService } from '@core/services/confirmation.service';
import { PERMISSIONS } from '@core/config/permission-keys';
import {
  ProjectActivity,
  ProjectApiService,
  ProjectAssignableUser,
  ProjectAttachment,
  ProjectComment,
  ProjectTask,
  ProjectTimeEntry,
  UpsertTaskPayload
} from './project-api.service';
import {
  PROJECT_PRIORITY_OPTIONS,
  PROJECT_TASK_STATUS_OPTIONS,
  daysUntilDue,
  formatFileSize,
  initialsFromName,
  parseProjectTaskPriority,
  parseProjectTaskStatus,
  taskStatusBadge,
  toIsoDate
} from './project-enums';
import {
  ProjectStatusChangeDialogComponent,
  ProjectStatusChangePayload
} from './components/project-status-change-dialog.component';

type TaskTab = 'overview' | 'subtasks' | 'files' | 'time' | 'history';

@Component({
  selector: 'app-project-task-detail',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    RouterLink,
    TabsModule,
    InputTextModule,
    InputNumberModule,
    SelectModule,
    DatePickerModule,
    Textarea,
    ProgressBarModule,
    TableModule,
    PageHeaderComponent,
    ButtonComponent,
    BreadcrumbComponent,
    StatusBadgeComponent,
    ProjectStatusChangeDialogComponent
  ],
  styleUrls: ['./projects.scss'],
  template: `
    @if (task(); as t) {
      <app-breadcrumb [items]="crumbs(t)"></app-breadcrumb>
      <app-page-header [title]="t.title" [subtitle]="t.phaseName">
        <app-status-badge [status]="taskStatusBadge(t.status)" [label]="t.statusDisplay" />
        @if (canUpdate) {
          <app-button variant="outline" (click)="openStatusDialog()">Changer statut</app-button>
          <app-button variant="primary" (click)="save()">Enregistrer</app-button>
          <app-button variant="danger" (click)="deleteTask()">Supprimer</app-button>
        }
        <a routerLink="/projects/{{ t.projectId }}" [queryParams]="{ tab: 'tasks' }" class="p-button p-button-outlined p-button-sm">Retour au projet</a>
      </app-page-header>

      <div class="proj-meta-chips mb-2">
        @if (canUpdate) {
          <p-select [options]="statusOptions" [(ngModel)]="status" optionLabel="label" optionValue="value" placeholder="Statut" />
          <p-select [options]="priorityOptions" [(ngModel)]="priority" optionLabel="label" optionValue="value" />
        }
        <span class="proj-chip">
          <i class="pi pi-calendar"></i>
          {{ dueDate ? (dueDate | date:'shortDate') : 'Sans échéance' }}
          @if (daysLeft() !== null && daysLeft()! <= 5 && daysLeft()! >= 0) {
            <span class="text-danger"> · J{{ daysLeft() }}</span>
          }
        </span>
      </div>

      <div class="task-detail-grid">
        <div class="task-detail-main-tabs">
          <p-tabs [value]="tab()" (valueChange)="tab.set($any($event))">
            <p-tablist>
              <p-tab value="overview">Aperçu</p-tab>
              <p-tab value="subtasks">Sous-tâches {{ doneSubtasks() }}/{{ subtasks().length }}</p-tab>
              <p-tab value="files">Fichiers {{ taskFiles().length }}</p-tab>
              <p-tab value="time">Temps passé</p-tab>
              <p-tab value="history">Historique</p-tab>
            </p-tablist>
            <p-tabpanels>
              <p-tabpanel value="overview">
                <div class="card p-3">
                  @if (canUpdate) {
                    <input pInputText class="w-full mb-2" [(ngModel)]="title" />
                    <textarea pTextarea class="w-full" [(ngModel)]="description" rows="4" placeholder="Description"></textarea>
                    <div class="flex flex-wrap gap-2 mt-2">
                      <p-select [options]="users()" [(ngModel)]="assigneeId" optionLabel="displayName" optionValue="id" placeholder="Assigné" [showClear]="true" />
                      <p-datepicker [(ngModel)]="dueDate" dateFormat="dd/mm/yy" placeholder="Échéance" />
                      <p-inputNumber [(ngModel)]="estimatedHours" [min]="0" placeholder="Estimé h" />
                      <p-inputNumber [(ngModel)]="progress" [min]="0" [max]="100" suffix=" %" />
                    </div>
                  } @else {
                    <p>{{ t.description || 'Pas de description.' }}</p>
                  }
                  <div class="mt-3">
                    <strong>{{ t.progressPercent }} %</strong>
                    <p-progressBar [value]="t.progressPercent" />
                    <p class="text-sm text-color-secondary m-0">{{ doneSubtasks() }}/{{ subtasks().length }} sous-tâches terminées</p>
                  </div>
                  <div class="flex gap-3 mt-3">
                    <div class="proj-kpi-card flex-1">
                      <span class="proj-kpi-label">Temps estimé</span>
                      <strong>{{ t.estimatedHours | number:'1.0-1' }} h</strong>
                    </div>
                    <div class="proj-kpi-card flex-1">
                      <span class="proj-kpi-label">Temps passé</span>
                      <strong>{{ t.loggedHours | number:'1.0-1' }} h</strong>
                    </div>
                  </div>
                </div>
                <div class="card p-3 mt-3">
                  <h3 class="mt-0">Commentaires</h3>
                  @if (canUpdate) {
                    <textarea pTextarea class="w-full" [(ngModel)]="body" rows="3"></textarea>
                    <app-button class="mt-2" variant="primary" (click)="comment()">Ajouter</app-button>
                  }
                  <ul class="proj-sidebar-list">
                    @for (c of comments(); track c.id) {
                      <li><strong>{{ c.authorName }}</strong> — {{ c.body }} <time class="text-sm">{{ c.createdAt | date:'short' }}</time></li>
                    }
                  </ul>
                </div>
              </p-tabpanel>
              <p-tabpanel value="subtasks">
                <div class="card p-3">
                  <p-table [value]="subtasks()" styleClass="p-datatable-sm">
                    <ng-template pTemplate="header"><tr><th>Titre</th><th>Statut</th><th>Avancement</th></tr></ng-template>
                    <ng-template pTemplate="body" let-s>
                      <tr [routerLink]="['/projects', s.projectId, 'tasks', s.id]" style="cursor:pointer">
                        <td>{{ s.title }}</td>
                        <td><app-status-badge [status]="taskStatusBadge(s.status)" [label]="s.statusDisplay" /></td>
                        <td>{{ s.progressPercent }}%</td>
                      </tr>
                    </ng-template>
                  </p-table>
                  @if (canUpdate) {
                    <div class="flex gap-2 mt-2">
                      <input pInputText [(ngModel)]="subTitle" placeholder="Nouvelle sous-tâche" class="flex-1" />
                      <app-button (click)="addSub()">Ajouter</app-button>
                    </div>
                  }
                </div>
              </p-tabpanel>
              <p-tabpanel value="files">
                <div class="card p-3">
                  @if (taskFiles().length) {
                    <ul class="proj-sidebar-list">
                      @for (f of taskFiles(); track f.id) {
                        <li>
                          <strong>{{ f.fileName }}</strong>
                          <span class="text-sm text-color-secondary">{{ formatSize(f.sizeBytes) }} · {{ f.createdAt | date:'shortDate' }}</span>
                          <app-button size="sm" variant="outline" (click)="downloadFile(f)">Télécharger</app-button>
                        </li>
                      }
                    </ul>
                  } @else {
                    <p class="text-color-secondary m-0">Aucun fichier rattaché à cette tâche.</p>
                  }
                </div>
              </p-tabpanel>
              <p-tabpanel value="time">
                <div class="card p-3">
                  <p-table [value]="timeEntries()" styleClass="p-datatable-sm">
                    <ng-template pTemplate="header"><tr><th>Date</th><th>Heures</th><th>Statut</th><th>Facturable</th></tr></ng-template>
                    <ng-template pTemplate="body" let-e>
                      <tr>
                        <td>{{ e.workDate | date:'shortDate' }}</td>
                        <td>{{ e.hours | number:'1.0-1' }} h</td>
                        <td>{{ e.statusDisplay }}</td>
                        <td>{{ e.isBillable ? 'Oui' : 'Non' }}</td>
                      </tr>
                    </ng-template>
                    <ng-template pTemplate="emptymessage"><tr><td colspan="4">Aucun temps saisi sur cette tâche.</td></tr></ng-template>
                  </p-table>
                </div>
              </p-tabpanel>
              <p-tabpanel value="history">
                <div class="card p-3">
                  <ul class="proj-sidebar-activity">
                    @for (a of taskActivity(); track a.id) {
                      <li>
                        <span class="proj-sidebar-activity-msg">{{ a.message }}</span>
                        <time>{{ a.createdAt | date:'short' }}</time>
                      </li>
                    } @empty {
                      <li class="text-color-secondary">Aucun historique.</li>
                    }
                  </ul>
                </div>
              </p-tabpanel>
            </p-tabpanels>
          </p-tabs>
        </div>

        <aside class="proj-sidebar-panel">
          <section class="proj-sidebar-section">
            <h3 class="proj-sidebar-title">Informations</h3>
            <dl class="proj-sidebar-dl">
              <dt>ID</dt><dd>{{ t.id.slice(0, 8) }}</dd>
              <dt>Projet</dt><dd><a [routerLink]="['/projects', t.projectId]">Voir le projet</a></dd>
              <dt>Colonne</dt><dd>{{ t.phaseName }}</dd>
              <dt>Assigné</dt><dd>{{ t.assigneeUserName || '—' }}</dd>
              <dt>Priorité</dt><dd>{{ t.priorityDisplay }}</dd>
            </dl>
          </section>
          <section class="proj-sidebar-section">
            <h3 class="proj-sidebar-title">Activité</h3>
            <ul class="proj-sidebar-activity">
              @for (a of taskActivity().slice(0, 5); track a.id) {
                <li>
                  <span class="proj-sidebar-activity-msg">{{ a.message }}</span>
                  <time>{{ a.createdAt | date:'short' }}</time>
                </li>
              }
            </ul>
          </section>
        </aside>
      </div>

      <app-project-status-change-dialog
        [(visible)]="statusDialogVisible"
        [currentStatus]="status"
        (confirmChange)="onStatusChange($event)" />
    }
  `
})
export class ProjectTaskDetailComponent implements OnInit {
  private readonly api = inject(ProjectApiService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly auth = inject(AuthService);
  private readonly toast = inject(ToastService);
  private readonly errors = inject(ErrorHandlerService);
  private readonly confirm = inject(ConfirmationService);

  readonly task = signal<ProjectTask | null>(null);
  readonly comments = signal<ProjectComment[]>([]);
  readonly users = signal<ProjectAssignableUser[]>([]);
  readonly subtasks = signal<ProjectTask[]>([]);
  readonly attachments = signal<ProjectAttachment[]>([]);
  readonly timeEntries = signal<ProjectTimeEntry[]>([]);
  readonly activities = signal<ProjectActivity[]>([]);
  readonly tab = signal<TaskTab>('overview');
  statusDialogVisible = false;
  body = '';
  subTitle = '';
  title = '';
  description = '';
  priority: 'Low' | 'Normal' | 'High' | 'Urgent' = 'Normal';
  status: 'Todo' | 'InProgress' | 'Waiting' | 'Done' | 'Cancelled' = 'Todo';
  assigneeId: string | null = null;
  dueDate: Date | null = null;
  estimatedHours = 0;
  progress = 0;
  readonly priorityOptions = PROJECT_PRIORITY_OPTIONS;
  readonly statusOptions = PROJECT_TASK_STATUS_OPTIONS;
  readonly taskStatusBadge = taskStatusBadge;
  readonly formatSize = formatFileSize;

  get canUpdate(): boolean { return this.auth.hasPermission(PERMISSIONS.projectTasks.update); }

  taskFiles(): ProjectAttachment[] {
    const id = this.task()?.id;
    return id ? this.attachments().filter(a => a.taskId === id) : [];
  }

  taskActivity(): ProjectActivity[] {
    return this.activities();
  }

  doneSubtasks(): number {
    return this.subtasks().filter(s => s.status === 'Done' || s.status === 3).length;
  }

  daysLeft(): number | null {
    return daysUntilDue(this.task()?.dueDate);
  }

  crumbs(t: ProjectTask): BreadcrumbItem[] {
    return [
      { label: 'Accueil', route: '/' },
      { label: 'Projets', route: '/projects' },
      { label: 'Projet', route: `/projects/${t.projectId}` },
      { label: t.title }
    ];
  }

  ngOnInit(): void {
    const taskId = this.route.snapshot.paramMap.get('taskId') ?? '';
    this.api.users().subscribe(r => { if (r.success && r.data) this.users.set(r.data); });
    this.reload(taskId);
  }

  openStatusDialog(): void {
    this.statusDialogVisible = true;
  }

  onStatusChange(payload: ProjectStatusChangePayload): void {
    this.status = payload.status;
    if (payload.comment) {
      const t = this.task();
      if (t) {
        this.api.addComment(t.projectId, payload.comment, t.id).subscribe();
      }
    }
    if (payload.notifyCollaborators) {
      this.toast.add({ severity: 'info', summary: 'Notification', detail: 'Les collaborateurs seront notifiés prochainement.' });
    }
    this.save();
  }

  private reload(taskId: string): void {
    this.api.getTask(taskId).subscribe({
      next: r => {
        if (r.success && r.data) {
          const t = r.data;
          this.task.set(t);
          this.title = t.title;
          this.description = t.description ?? '';
          this.priority = parseProjectTaskPriority(t.priority) ?? 'Normal';
          this.status = parseProjectTaskStatus(t.status) ?? 'Todo';
          this.assigneeId = t.assigneeUserId ?? null;
          this.dueDate = t.dueDate ? new Date(t.dueDate) : null;
          this.estimatedHours = t.estimatedHours;
          this.progress = t.progressPercent;
          this.api.comments(t.projectId, t.id).subscribe(c => {
            if (c.success && c.data) this.comments.set(c.data);
          });
          this.api.tasks(t.projectId).subscribe(list => {
            if (list.success && list.data) {
              this.subtasks.set(list.data.filter(x => x.parentTaskId === t.id));
            }
          });
          this.api.attachments(t.projectId).subscribe(a => {
            if (a.success && a.data) this.attachments.set(a.data);
          });
          this.api.time({ projectId: t.projectId }).subscribe(te => {
            if (te.success && te.data) {
              this.timeEntries.set(te.data.filter(e => e.taskId === t.id));
            }
          });
          this.api.activity(t.projectId).subscribe(act => {
            if (act.success && act.data) this.activities.set(act.data);
          });
        }
      },
      error: err => this.toast.add({ severity: 'error', summary: 'Tâche', detail: this.errors.extractErrorMessage(err) })
    });
  }

  save(): void {
    const t = this.task();
    if (!t) return;
    const payload: UpsertTaskPayload = {
      phaseId: t.phaseId,
      parentTaskId: t.parentTaskId,
      title: this.title,
      description: this.description,
      priority: this.priority,
      status: this.status,
      dueDate: toIsoDate(this.dueDate),
      assigneeUserId: this.assigneeId,
      estimatedHours: this.estimatedHours,
      progressPercent: this.progress
    };
    this.api.updateTask(t.id, payload).subscribe({
      next: () => {
        this.toast.add({ severity: 'success', summary: 'Tâche enregistrée' });
        this.reload(t.id);
      },
      error: err => this.toast.add({ severity: 'error', summary: 'Enregistrement', detail: this.errors.extractErrorMessage(err) })
    });
  }

  addSub(): void {
    const t = this.task();
    if (!t || !this.subTitle.trim()) return;
    this.api.createTask(t.projectId, {
      phaseId: t.phaseId,
      parentTaskId: t.id,
      title: this.subTitle.trim(),
      priority: 'Normal',
      estimatedHours: 0,
      progressPercent: 0
    }).subscribe({
      next: () => { this.subTitle = ''; this.reload(t.id); },
      error: err => this.toast.add({ severity: 'error', summary: 'Sous-tâche', detail: this.errors.extractErrorMessage(err) })
    });
  }

  comment(): void {
    const t = this.task();
    if (!t || !this.body.trim()) return;
    this.api.addComment(t.projectId, this.body, t.id).subscribe({
      next: () => {
        this.body = '';
        this.api.comments(t.projectId, t.id).subscribe(c => {
          if (c.success && c.data) this.comments.set(c.data);
        });
      },
      error: err => this.toast.add({ severity: 'error', summary: 'Commentaire', detail: this.errors.extractErrorMessage(err) })
    });
  }

  downloadFile(f: ProjectAttachment): void {
    const t = this.task();
    if (!t) return;
    this.api.downloadAttachment(t.projectId, f.id).subscribe({
      next: blob => {
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = f.fileName;
        a.click();
        URL.revokeObjectURL(url);
      },
      error: err => this.toast.add({ severity: 'error', summary: 'Téléchargement', detail: this.errors.extractErrorMessage(err) })
    });
  }

  deleteTask(): void {
    const t = this.task();
    if (!t) return;
    this.confirm.confirm({
      header: 'Supprimer la tâche',
      message: 'Cette action est irréversible. Supprimer cette tâche ?',
      accept: () => this.api.deleteTask(t.id).subscribe({
        next: () => {
          this.toast.add({ severity: 'success', summary: 'Tâche supprimée' });
          void this.router.navigate(['/projects', t.projectId], { queryParams: { tab: 'tasks' } });
        },
        error: err => this.toast.add({ severity: 'error', summary: 'Suppression', detail: this.errors.extractErrorMessage(err) })
      })
    });
  }
}
