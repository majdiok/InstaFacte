import { Component, EventEmitter, Input, Output } from '@angular/core';

import { CommonModule } from '@angular/common';

import { FormsModule } from '@angular/forms';

import { TableModule } from 'primeng/table';

import { InputNumberModule } from 'primeng/inputnumber';

import { SelectModule } from 'primeng/select';

import { MessageModule } from 'primeng/message';

import { DialogModule } from 'primeng/dialog';

import { ButtonComponent } from '@shared/components/button/button.component';

import {

  ProjectAssignableUser,

  ProjectMember,

  ProjectWorkloadRow,

  UpsertMemberPayload

} from '../project-api.service';

import { PROJECT_ROLE_OPTIONS, parseProjectMemberRole, showWorkload } from '../project-enums';

import { ProjectDetail } from '../project-api.service';



@Component({

  selector: 'app-project-team-tab',

  standalone: true,

  imports: [

    CommonModule,

    FormsModule,

    TableModule,

    InputNumberModule,

    SelectModule,

    MessageModule,

    DialogModule,

    ButtonComponent

  ],

  template: `

    @if (missingRate) {

      <p-message severity="warn" styleClass="w-full mb-3"

        text="Sans TJM ni coût horaire, la facturation régie est impossible." />

    }

    @if (canManage) {

      <div class="proj-field-row mb-3">

        <label>Utilisateur

          <p-select [options]="availableUsers" [(ngModel)]="userId" optionLabel="displayName" optionValue="id" placeholder="Utilisateur" />

        </label>

        <label>Rôle

          <p-select [options]="roleOptions" [(ngModel)]="role" optionLabel="label" optionValue="value" />

        </label>

        <label>TJM

          <p-inputNumber [(ngModel)]="dailyRate" [min]="0" />

        </label>

        <label>Coût h

          <p-inputNumber [(ngModel)]="hourlyCost" [min]="0" />

        </label>

        <label>Capacité h/sem

          <p-inputNumber [(ngModel)]="capacity" [min]="0" />

        </label>

        <app-button (click)="add()">Ajouter</app-button>

      </div>

    }

    <p-table [value]="members" styleClass="p-datatable-sm">

      <ng-template pTemplate="header">

        <tr><th>Nom</th><th>Rôle</th><th>TJM</th><th>Coût h</th><th>Capacité</th>@if (canManage) { <th></th> }</tr>

      </ng-template>

      <ng-template pTemplate="body" let-m>

        <tr>

          <td>{{ m.userName }}</td>

          <td>{{ m.roleDisplay }}</td>

          <td>{{ m.dailyRate ?? '—' }}</td>

          <td>{{ m.hourlyCost ?? '—' }}</td>

          <td>{{ m.weeklyCapacityHours }}</td>

          @if (canManage) {

            <td>

              <app-button size="sm" variant="outline" (click)="openEdit(m)">Modifier</app-button>

              <app-button size="sm" variant="danger" (click)="remove.emit(m.id)">Retirer</app-button>

            </td>

          }

        </tr>

      </ng-template>

    </p-table>

    @if (project && showWorkload(project.kind, project.billingMode) && workload.length) {

      <h3>Charge</h3>

      <p-table [value]="workload" styleClass="p-datatable-sm">

        <ng-template pTemplate="header"><tr><th>Personne</th><th>Capacité</th><th>Planifié</th><th>Saisi</th></tr></ng-template>

        <ng-template pTemplate="body" let-w>

          <tr><td>{{ w.userName }}</td><td>{{ w.weeklyCapacityHours }}</td><td>{{ w.estimatedHours }}</td><td>{{ w.loggedHours }}</td></tr>

        </ng-template>

      </p-table>

    }



    <p-dialog [(visible)]="editVisible" header="Modifier le membre" [modal]="true" [style]="{ width: '28rem' }">

      <div class="flex flex-column gap-2">

        <p-select [options]="roleOptions" [(ngModel)]="editRole" optionLabel="label" optionValue="value" />

        <p-inputNumber [(ngModel)]="editDaily" placeholder="TJM" />

        <p-inputNumber [(ngModel)]="editHourly" placeholder="Coût h" />

        <p-inputNumber [(ngModel)]="editCapacity" placeholder="Capacité" />

      </div>

      <ng-template pTemplate="footer">

        <app-button variant="secondary" (click)="editVisible = false">Annuler</app-button>

        <app-button variant="primary" (click)="saveEdit()">Enregistrer</app-button>

      </ng-template>

    </p-dialog>

  `,


})

export class ProjectTeamTabComponent {

  @Input() project: ProjectDetail | null = null;

  @Input() members: ProjectMember[] = [];

  @Input() users: ProjectAssignableUser[] = [];

  @Input() workload: ProjectWorkloadRow[] = [];

  @Input() canManage = false;

  @Output() addMember = new EventEmitter<UpsertMemberPayload>();

  @Output() updateMember = new EventEmitter<{ id: string; payload: UpsertMemberPayload }>();

  @Output() remove = new EventEmitter<string>();



  userId = '';

  role: 'Viewer' | 'Member' | 'Manager' = 'Member';

  dailyRate: number | null = null;

  hourlyCost: number | null = null;

  capacity = 40;

  readonly roleOptions = PROJECT_ROLE_OPTIONS;

  readonly showWorkload = showWorkload;



  get availableUsers(): ProjectAssignableUser[] {

    const memberIds = new Set(this.members.map(m => m.userId));

    return this.users.filter(u => !memberIds.has(u.id));

  }



  get missingRate(): boolean {

    return this.members.some(m => !(m.dailyRate && m.dailyRate > 0) && !(m.hourlyCost && m.hourlyCost > 0));

  }



  add(): void {

    if (!this.userId) return;

    this.addMember.emit({

      userId: this.userId,

      role: this.role,

      dailyRate: this.dailyRate,

      hourlyCost: this.hourlyCost,

      weeklyCapacityHours: this.capacity

    });

    this.userId = '';

  }



  editVisible = false;

  private editId = '';

  private editUserId = '';

  editRole: 'Viewer' | 'Member' | 'Manager' = 'Member';

  editDaily: number | null = null;

  editHourly: number | null = null;

  editCapacity = 40;



  openEdit(m: ProjectMember): void {

    this.editId = m.id;

    this.editUserId = m.userId;

    this.editRole = parseProjectMemberRole(m.role) ?? 'Member';

    this.editDaily = m.dailyRate ?? null;

    this.editHourly = m.hourlyCost ?? null;

    this.editCapacity = m.weeklyCapacityHours;

    this.editVisible = true;

  }



  saveEdit(): void {

    this.updateMember.emit({

      id: this.editId,

      payload: {

        userId: this.editUserId,

        role: this.editRole,

        dailyRate: this.editDaily,

        hourlyCost: this.editHourly,

        weeklyCapacityHours: this.editCapacity

      }

    });

    this.editVisible = false;

  }

}

