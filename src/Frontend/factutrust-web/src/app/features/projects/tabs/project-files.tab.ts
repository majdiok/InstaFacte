import { Component, EventEmitter, Input, Output } from '@angular/core';

import { CommonModule } from '@angular/common';

import { FormsModule } from '@angular/forms';

import { TableModule } from 'primeng/table';

import { Textarea } from 'primeng/textarea';

import { ButtonComponent } from '@shared/components/button/button.component';

import { EmptyStateComponent } from '@shared/components/empty-state/empty-state.component';

import { ProjectAttachment, ProjectComment } from '../project-api.service';

import { formatFileSize } from '../project-enums';



@Component({

  selector: 'app-project-files-tab',

  standalone: true,

  imports: [CommonModule, FormsModule, TableModule, Textarea, ButtonComponent, EmptyStateComponent],

  template: `

    @if (canUpdate) {

      <div class="proj-file-input mb-3">

        <input type="file" id="proj-file-upload" class="hidden" (change)="onFile($event)" />

        <label for="proj-file-upload" class="cursor-pointer">

          <i class="pi pi-upload mr-2"></i>

          Choisir un fichier

        </label>

        @if (pending) {

          <p class="mt-2 mb-0 text-sm">{{ pending.name }} ({{ formatFileSize(pending.size) }})</p>

        }

        <p class="text-sm text-color-secondary mt-2 mb-0">Taille maximale : 10 Mo</p>

        <app-button class="mt-2" variant="primary" [disabled]="!pending" (click)="upload.emit(pending!)">Téléverser</app-button>

      </div>

    } @else {

      <p class="text-sm text-color-secondary mb-3">Taille maximale : 10 Mo</p>

    }

    <p-table [value]="files" styleClass="p-datatable-sm">

      <ng-template pTemplate="header"><tr><th>Fichier</th><th>Taille</th><th>Date</th><th></th></tr></ng-template>

      <ng-template pTemplate="body" let-f>

        <tr>

          <td>{{ f.fileName }}</td>

          <td>{{ formatFileSize(f.sizeBytes) }}</td>

          <td>{{ f.createdAt | date:'short' }}</td>

          <td>

            <app-button size="sm" variant="outline" (click)="download.emit(f)">Télécharger</app-button>

            @if (canUpdate) {

              <app-button size="sm" variant="danger" (click)="remove.emit(f.id)">Supprimer</app-button>

            }

          </td>

        </tr>

      </ng-template>

      <ng-template pTemplate="emptymessage">

        <tr><td colspan="4">

          <app-empty-state icon="pi-file" title="Aucun fichier" description="Téléversez un document (max 10 Mo)." [showAction]="false" />

        </td></tr>

      </ng-template>

    </p-table>

    <h3>Commentaires</h3>

    @if (canUpdate) {

      <textarea pTextarea [(ngModel)]="commentBody" rows="2" class="w-full"></textarea>

      <app-button class="mt-2" (click)="sendComment()">Commenter</app-button>

    }

    <ul>

      @for (c of comments; track c.id) {

        <li><strong>{{ c.authorName }}</strong> — {{ c.body }}</li>

      }

    </ul>

  `,

  styles: [`

    .hidden { position: absolute; width: 1px; height: 1px; overflow: hidden; clip: rect(0,0,0,0); }

    .cursor-pointer { cursor: pointer; }

  `],

})

export class ProjectFilesTabComponent {

  @Input() files: ProjectAttachment[] = [];

  @Input() comments: ProjectComment[] = [];

  @Input() canUpdate = false;

  @Output() upload = new EventEmitter<File>();

  @Output() download = new EventEmitter<ProjectAttachment>();

  @Output() remove = new EventEmitter<string>();

  @Output() comment = new EventEmitter<string>();



  pending: File | null = null;

  commentBody = '';

  readonly formatFileSize = formatFileSize;



  onFile(ev: Event): void {

    const input = ev.target as HTMLInputElement;

    this.pending = input.files?.[0] ?? null;

  }



  sendComment(): void {

    if (!this.commentBody.trim()) return;

    this.comment.emit(this.commentBody);

    this.commentBody = '';

  }

}

