import { Component, EventEmitter, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ProjectTask } from '../project-api.service';
import { ProjectTaskStatusCode } from '../project-enums';
import { computeRootTaskStatusSummary, TaskDueFilterKey } from '../project-tasks.vm';

export type TaskStatusChipKey = 'total' | 'todo' | 'done' | 'inProgress' | 'waiting' | 'overdue';

@Component({
  selector: 'app-project-task-kpi-row',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="proj-tasks-status-bar" role="list" aria-label="Répartition des tâches">
      <button
        type="button"
        class="proj-tasks-status-chip"
        [class.proj-tasks-status-chip--active]="activeChip === 'total'"
        role="listitem"
        [attr.aria-label]="'Total ' + summary.total + ' tâches'"
        (click)="onChip('total')">
        <span class="proj-tasks-status-chip__label">Total</span>
        <strong class="proj-tasks-status-chip__value">{{ summary.total }}</strong>
      </button>
      <button
        type="button"
        class="proj-tasks-status-chip proj-tasks-status-chip--todo"
        [class.proj-tasks-status-chip--active]="activeChip === 'todo'"
        role="listitem"
        [attr.aria-label]="'À faire ' + summary.todo + ', ' + summary.todoPct + ' pour cent'"
        (click)="onChip('todo')">
        <span class="proj-tasks-status-chip__label">À faire</span>
        <strong class="proj-tasks-status-chip__value">{{ summary.todo }}</strong>
        <span class="proj-tasks-status-chip__pct">{{ summary.todoPct }} %</span>
      </button>
      <button
        type="button"
        class="proj-tasks-status-chip proj-tasks-status-chip--done"
        [class.proj-tasks-status-chip--active]="activeChip === 'done'"
        role="listitem"
        [attr.aria-label]="'Terminées ' + summary.done + ', ' + summary.donePct + ' pour cent'"
        (click)="onChip('done')">
        <span class="proj-tasks-status-chip__label">Terminées</span>
        <strong class="proj-tasks-status-chip__value">{{ summary.done }}</strong>
        <span class="proj-tasks-status-chip__pct">{{ summary.donePct }} %</span>
      </button>
      <button
        type="button"
        class="proj-tasks-status-chip proj-tasks-status-chip--progress"
        [class.proj-tasks-status-chip--active]="activeChip === 'inProgress'"
        role="listitem"
        [attr.aria-label]="'En cours ' + summary.inProgress + ', ' + summary.inProgressPct + ' pour cent'"
        (click)="onChip('inProgress')">
        <span class="proj-tasks-status-chip__label">En cours</span>
        <strong class="proj-tasks-status-chip__value">{{ summary.inProgress }}</strong>
        <span class="proj-tasks-status-chip__pct">{{ summary.inProgressPct }} %</span>
      </button>
      <button
        type="button"
        class="proj-tasks-status-chip proj-tasks-status-chip--waiting"
        [class.proj-tasks-status-chip--active]="activeChip === 'waiting'"
        role="listitem"
        [attr.aria-label]="'En attente ' + summary.waiting + ', ' + summary.waitingPct + ' pour cent'"
        (click)="onChip('waiting')">
        <span class="proj-tasks-status-chip__label">En attente</span>
        <strong class="proj-tasks-status-chip__value">{{ summary.waiting }}</strong>
        <span class="proj-tasks-status-chip__pct">{{ summary.waitingPct }} %</span>
      </button>
      <button
        type="button"
        class="proj-tasks-status-chip proj-tasks-status-chip--overdue"
        [class.proj-tasks-status-chip--active]="activeChip === 'overdue'"
        role="listitem"
        [attr.aria-label]="'En retard ' + summary.overdue + ', ' + summary.overduePct + ' pour cent'"
        (click)="onChip('overdue')">
        <span class="proj-tasks-status-chip__label">En retard</span>
        <strong class="proj-tasks-status-chip__value">{{ summary.overdue }}</strong>
        <span class="proj-tasks-status-chip__pct">{{ summary.overduePct }} %</span>
      </button>
    </div>
  `
})
export class ProjectTaskKpiRowComponent {
  @Input() tasks: ProjectTask[] = [];
  @Input() activeChip: TaskStatusChipKey | null = null;

  @Output() chipClick = new EventEmitter<{
    chip: TaskStatusChipKey;
    status: ProjectTaskStatusCode | null;
    dueFilter: TaskDueFilterKey | null;
  }>();

  get summary() {
    return computeRootTaskStatusSummary(this.tasks);
  }

  onChip(chip: TaskStatusChipKey): void {
    if (chip === 'total') {
      this.chipClick.emit({ chip, status: null, dueFilter: null });
      return;
    }
    if (chip === 'todo') {
      this.chipClick.emit({ chip, status: 'Todo', dueFilter: null });
      return;
    }
    if (chip === 'done') {
      this.chipClick.emit({ chip, status: 'Done', dueFilter: null });
      return;
    }
    if (chip === 'inProgress') {
      this.chipClick.emit({ chip, status: 'InProgress', dueFilter: null });
      return;
    }
    if (chip === 'waiting') {
      this.chipClick.emit({ chip, status: 'Waiting', dueFilter: null });
      return;
    }
    this.chipClick.emit({ chip, status: null, dueFilter: 'overdue' });
  }
}
