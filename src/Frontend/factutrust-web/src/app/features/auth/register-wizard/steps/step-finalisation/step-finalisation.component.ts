import { Component, EventEmitter, Input, Output, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormGroup, ReactiveFormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { trigger, transition, style, animate } from '@angular/animations';
import { InputTextModule } from 'primeng/inputtext';
import { SelectModule } from 'primeng/select';
import { InputMaskModule } from 'primeng/inputmask';
import { CheckboxModule } from 'primeng/checkbox';
import { AppModule } from '@core/models/app-module';
import { RegistrationCatalogService } from '../../registration-catalog';
import { TaxRegime } from '../../tax-regime.types';
import { GovernorateOption } from '../../../shared/auth-governorate.options';

@Component({
  selector: 'app-step-finalisation',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterModule, InputTextModule, SelectModule, InputMaskModule, CheckboxModule],
  animations: [
    trigger('fadeInOut', [
      transition(':enter', [
        style({ opacity: 0, transform: 'translateY(10px)' }),
        animate('300ms ease-out', style({ opacity: 1, transform: 'translateY(0)' }))
      ])
    ])
  ],
  templateUrl: './step-finalisation.component.html',
  styleUrl: './step-finalisation.component.scss'
})
export class StepFinalisationComponent {
  @Input({ required: true }) form!: FormGroup;
  @Input() taxRegimes: TaxRegime[] = [];
  @Input() governorates: GovernorateOption[] = [];

  @Output() editStep = new EventEmitter<number>();

  readonly catalog = inject(RegistrationCatalogService);

  isInvalid(field: string): boolean {
    const control = this.form.get(field);
    return !!(control?.invalid && control?.touched);
  }

  recapSegment(): string {
    return this.catalog.segmentLabel(this.form.get('companySegment')?.value) || '—';
  }

  recapDomain(): string {
    return this.catalog.domainLabel(this.form.get('businessDomain')?.value) || '—';
  }

  recapTaxRegime(): string {
    const value = this.form.get('taxRegime')?.value;
    return this.taxRegimes.find(t => t.value === value)?.label || '—';
  }

  recapModuleLabels(): string[] {
    const ids: AppModule[] = this.form.get('enabledModules')?.value ?? [];
    return ids.map(id => this.catalog.moduleLabel(id));
  }

  goTo(step: number): void {
    this.editStep.emit(step);
  }
}
