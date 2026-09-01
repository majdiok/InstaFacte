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

  /**
   * True right after the wizard cleared `businessDomain` because it stopped being
   * available for the newly-selected segment (plan WP-F2). Wizard resets this on the
   * next domain pick.
   */
  @Input() domainClearedNotice = false;

  readonly catalog = inject(RegistrationCatalogService);

  get segments(): readonly SegmentOption[] {
    return this.catalog.segments;
  }

  /** Segment-filtered, ordered domain list (plan §3.1/§3.3). Matrix-filtered in both remote and fallback modes. */
  get domains(): readonly DomainOption[] {
    return this.catalog.domainsForSegment(this.selectedSegmentCode);
  }

  /** Remote mode + no segment chosen yet ⇒ domain list disabled, placeholder shown instead. */
  get domainsDisabled(): boolean {
    return this.catalog.isRemote && !this.selectedSegmentCode;
  }

  /** Remote mode + segment chosen but its catalog list resolves to "Autre domaine" only (misconfiguration). */
  get showNoSpecificDomainHint(): boolean {
    return this.catalog.isRemote && !!this.selectedSegmentCode
      && this.domains.length === 1 && this.domains[0].code === 'autre';
  }

  /** « N domaines adaptés à votre segment : X » chip shown above the filtered domain list.
   * Shown in both remote and fallback modes (plan §3.5), but only when the list is
   * genuinely restricted (fewer domains than the full catalog) — otherwise the chip
   * would misleadingly claim a segment is "adapted" when every domain is available. */
  get filteredDomainsChipLabel(): string | null {
    if (!this.selectedSegmentCode || this.showNoSpecificDomainHint) return null;
    const count = this.domains.length;
    const total = this.catalog.domains.length;
    if (count === 0 || count >= total) return null;
    const plural = count > 1 ? 's' : '';
    return `${count} domaine${plural} adapté${plural} à votre segment : ${this.selectedSegmentLabel}`;
  }

  get selectedSegmentLabel(): string {
    return this.catalog.segmentLabel(this.selectedSegmentCode);
  }

  private get selectedSegmentCode(): string {
    return this.form.get('companySegment')?.value ?? '';
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

