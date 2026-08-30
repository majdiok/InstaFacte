import { Component, Input, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormGroup, ReactiveFormsModule } from '@angular/forms';
import { trigger, transition, style, animate } from '@angular/animations';
import { RegistrationCatalogService, SegmentOption, DomainOption } from '../../registration-catalog';

@Component({
  selector: 'app-step-company-type',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  animations: [
    trigger('fadeInOut', [
      transition(':enter', [
        style({ opacity: 0, transform: 'translateY(10px)' }),
        animate('300ms ease-out', style({ opacity: 1, transform: 'translateY(0)' }))
      ])
    ])
  ],
  templateUrl: './step-company-type.component.html',
  styleUrl: './step-company-type.component.scss'
})
export class StepCompanyTypeComponent {
  @Input({ required: true }) form!: FormGroup;

  readonly catalog = inject(RegistrationCatalogService);

  get segments(): readonly SegmentOption[] {
    return this.catalog.segments;
  }

  get domains(): readonly DomainOption[] {
    return this.catalog.domains;
  }

  isSegmentSelected(code: string): boolean {
    return this.form.get('companySegment')?.value === code;
  }

  isDomainSelected(code: string): boolean {
    return this.form.get('businessDomain')?.value === code;
  }

  selectSegment(code: string): void {
    this.form.get('companySegment')?.setValue(code);
    this.form.get('companySegment')?.markAsTouched();
  }

  selectDomain(code: string): void {
    this.form.get('businessDomain')?.setValue(code);
    this.form.get('businessDomain')?.markAsTouched();
  }

  onSegmentKeydown(event: KeyboardEvent, code: string): void {
    if (event.key === 'Enter' || event.key === ' ') {
      event.preventDefault();
      this.selectSegment(code);
    }
  }
}
