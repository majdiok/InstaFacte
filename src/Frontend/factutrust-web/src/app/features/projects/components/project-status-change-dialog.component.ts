import { Component, EventEmitter, Input, Output, OnChanges } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { DialogModule } from 'primeng/dialog';
import { SelectModule } from 'primeng/select';
import { Textarea } from 'primeng/textarea';
import { CheckboxModule } from 'primeng/checkbox';
import { ButtonComponent } from '@shared/components/button/button.component';
import { PROJECT_TASK_STATUS_OPTIONS, ProjectTaskStatusCode } from '../project-enums';

export interface ProjectStatusChangePayload {
  status: ProjectTaskStatusCode;
  comment: string;
  notifyCollaborators: boolean;
}

@Component({
  selector: 'app-project-status-change-dialog',
  standalone: true,
  imports: [CommonModule, FormsModule, DialogModule, SelectModule, Textarea, CheckboxModule, ButtonComponent],
  template: `
    <p-dialog
      [(visible)]="visible"
      [modal]="true"
      header="Modifier le statut de la tâche"
      [style]="{ width: '32rem' }"
      (onHide)="cancel.emit()">
      <p class="text-sm text-color-secondary">Sélectionnez un nouveau statut et ajoutez un commentaire si nécessaire.</p>
      <div class="proj-status-flow mb-3">
        @for (s of flowStatuses; track s.value) {
          <span class="proj-status-flow-step" [class.proj-status-flow-step--active]="s.value === targetStatus">
            {{ s.label }}
          </span>
        }
      </div>
      <label class="block mb-2">Changer le statut à
        <p-select class="w-full mt-1" [options]="statusOptions" [(ngModel)]="targetStatus" optionLabel="label" optionValue="value" />
      </label>
      <label class="block mb-2">Commentaire
        <textarea pTextarea class="w-full mt-1" [(ngModel)]="comment" rows="3" maxlength="500" placeholder="Justification du changement…"></textarea>
        <span class="text-sm text-color-secondary">{{ comment.length }}/500</span>
      </label>
      <div class="flex align-items-center gap-2 mb-2">
        <p-checkbox [(ngModel)]="notifyCollaborators" [binary]="true" inputId="notifyCollab" />
        <label for="notifyCollab">Notifier les collaborateurs concernés</label>
      </div>
      <ng-template pTemplate="footer">
        <app-button variant="secondary" (click)="visible = false; cancel.emit()">Annuler</app-button>
        <app-button variant="primary" (click)="confirm()">Confirmer le changement</app-button>
      </ng-template>
    </p-dialog>
  `
})
export class ProjectStatusChangeDialogComponent implements OnChanges {
  @Input() visible = false;
  @Input() currentStatus: ProjectTaskStatusCode = 'Todo';
  @Output() visibleChange = new EventEmitter<boolean>();
  @Output() confirmChange = new EventEmitter<ProjectStatusChangePayload>();
  @Output() cancel = new EventEmitter<void>();

  targetStatus: ProjectTaskStatusCode = 'InProgress';
  comment = '';
  notifyCollaborators = true;
  readonly statusOptions = PROJECT_TASK_STATUS_OPTIONS.filter(o => o.value !== 'Cancelled');
  readonly flowStatuses = PROJECT_TASK_STATUS_OPTIONS.filter(o => o.value !== 'Cancelled');

  ngOnChanges(): void {
    this.targetStatus = this.currentStatus;
  }

  confirm(): void {
    if ((this.targetStatus === 'Waiting' || this.targetStatus === 'Done') && !this.comment.trim()) {
      return;
    }
    this.confirmChange.emit({
      status: this.targetStatus,
      comment: this.comment.trim(),
      notifyCollaborators: this.notifyCollaborators
    });
    this.visible = false;
    this.visibleChange.emit(false);
  }
}
