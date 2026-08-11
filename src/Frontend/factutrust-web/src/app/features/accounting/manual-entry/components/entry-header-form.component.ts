import { Component, inject, input } from '@angular/core';

import { CommonModule } from '@angular/common';

import { FormsModule } from '@angular/forms';

import { EntryFormStore } from '../services/entry-form.store';

import { EntryReferenceStore } from '../services/entry-reference.store';

import { PeriodBadgeComponent } from './period-badge.component';

import { formatPeriodLabel } from '../models/guided-scenarios.catalog';



@Component({

  selector: 'app-entry-header-form',

  standalone: true,

  imports: [CommonModule, FormsModule, PeriodBadgeComponent],

  template: `

    <section class="entry-header card" aria-labelledby="entry-header-title">

      <h2 id="entry-header-title" class="entry-header__title">Informations générales</h2>



      <div class="entry-header__grid" [class.entry-header__grid--compact]="compact()">

        <div class="form-field">

          <label class="field-label" for="eh-journal">Journal <span class="req">*</span></label>

          <select id="eh-journal" class="me-input"
                  [ngModel]="store.journalCode()" (ngModelChange)="store.journalCode.set($event)"
                  [disabled]="lockJournal()">

            @for (opt of store.journalOptions(); track opt.code) {

              <option [value]="opt.code">{{ opt.code }} — {{ opt.label }}</option>

            }

          </select>

        </div>



        <div class="form-field">

          <label class="field-label" for="eh-date">Date d'écriture <span class="req">*</span></label>

          <input id="eh-date" type="date" class="me-input"

                 [ngModel]="store.entryDate()" (ngModelChange)="store.setEntryDate($event)" />

          @if (store.periodsLoaded()) {

            <app-period-badge [date]="store.entryDate()" [periods]="store.periods()" />

          }

        </div>



        <div class="form-field">

          <label class="field-label" for="eh-period">Période <span class="req">*</span></label>

          <select id="eh-period" class="me-input"

                  [ngModel]="store.periodId()" (ngModelChange)="store.setPeriodId($event || null)">

            <option [ngValue]="null">— Sélectionner —</option>

            @for (p of refs.openPeriods(); track p.id) {

              <option [ngValue]="p.id">{{ periodLabel(p) }}</option>

            }

          </select>

        </div>



        <div class="form-field form-field--wide">

          <label class="field-label" for="eh-label">Libellé général <span class="req">*</span></label>

          <input id="eh-label" type="text" class="me-input" placeholder="Libellé de l'écriture"

                 [ngModel]="store.entryLabel()" (ngModelChange)="onLabelChange($event)" />

        </div>

      </div>



      <details class="entry-header__piece-options">

        <summary>Options pièce</summary>

        <div class="entry-header__grid entry-header__grid--secondary">

          <div class="form-field">

            <label class="field-label" for="eh-piece-num">N° pièce</label>

            <input id="eh-piece-num" type="text" class="me-input me-input--locked" readonly

                   value="Attribué à l'enregistrement" title="Le numéro de pièce est généré automatiquement" />

          </div>



          <div class="form-field">

            <label class="field-label" for="eh-ref">Référence</label>

            <input id="eh-ref" type="text" class="me-input" maxlength="50"

                   placeholder="Réf. pièce externe"

                   [ngModel]="store.pieceRef()" (ngModelChange)="store.pieceRef.set($event)" />

          </div>



          <div class="form-field">

            <label class="field-label" for="eh-piece-date">Date pièce</label>

            <input id="eh-piece-date" type="date" class="me-input"

                   [ngModel]="store.pieceDate()" (ngModelChange)="store.pieceDate.set($event)" />

          </div>

        </div>

      </details>

    </section>

  `,

  styles: `

    .entry-header { padding:var(--spacing-5); margin-bottom:var(--spacing-4); }

    .entry-header__title { font-size:var(--font-size-md); font-weight:var(--font-weight-semibold); margin:0 0 var(--spacing-4); }

    .entry-header__grid { display:grid; grid-template-columns:repeat(auto-fill, minmax(200px, 1fr)); gap:var(--spacing-4); align-items:end; }

    .entry-header__grid--compact { grid-template-columns:repeat(auto-fill, minmax(180px, 1fr)); }


    .entry-header__grid--secondary { margin-top:var(--spacing-3); }

    .entry-header__piece-options { margin-top:var(--spacing-2); }

    .entry-header__piece-options summary { cursor:pointer; font-size:var(--font-size-sm); font-weight:var(--font-weight-semibold); color:var(--color-text-secondary); padding:var(--spacing-2) 0; }

    .form-field { display:flex; flex-direction:column; gap:var(--spacing-2); }

    .form-field--wide { grid-column:1 / -1; }

    .field-label { font-size:var(--font-size-sm); font-weight:var(--font-weight-semibold); }

    .req { color:var(--color-error-600); }

    .me-input { padding:var(--spacing-2) var(--spacing-3); border:1px solid var(--color-border-default); border-radius:var(--radius-md); background:var(--color-background-elevated); font-size:var(--font-size-sm); min-height:2.5rem; width:100%; box-sizing:border-box; }

    .me-input--locked { background:var(--color-background-subtle); color:var(--color-text-tertiary); cursor:not-allowed; }

    .me-input:focus { outline:none; border-color:var(--color-primary-500); box-shadow:0 0 0 3px var(--color-primary-200); }

  `

})

export class EntryHeaderFormComponent {

  readonly store = inject(EntryFormStore);

  readonly refs = inject(EntryReferenceStore);

  readonly compact = input(false);

  readonly lockJournal = input(false);



  periodLabel(p: { fiscalYear: number; month: number }): string {

    return formatPeriodLabel(p.fiscalYear, p.month);

  }



  onLabelChange(label: string): void {

    this.store.entryLabel.set(label);

    if (this.store.propagateLabelToLines()) {

      const lines = this.store.lines().map(l =>

        l.lineLabel.trim() ? l : { ...l, lineLabel: label }

      );

      this.store.setLines(lines);

    }

  }

}


