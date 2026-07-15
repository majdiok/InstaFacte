import {
  AfterViewInit,
  Directive,
  ElementRef,
  HostListener,
  Input,
  OnDestroy,
  inject
} from '@angular/core';

/**
 * Lightweight focus-trap for modal dialogs (WCAG 2.1 AA — guideline 2.4.3 Focus Order
 * and SC 2.1.2 No Keyboard Trap — confines focus inside the dialog while it is open).
 *
 * Usage:
 *   <div class="modal-panel" appFocusTrap>…</div>
 *
 * Behaviour:
 *   • On <c>ngAfterViewInit</c>, focuses the first focusable element inside the host
 *     (or the host itself when nothing is focusable).
 *   • On <c>Tab</c>, if focus is on the last focusable element, wraps to the first.
 *   • On <c>Shift+Tab</c>, if focus is on the first, wraps to the last.
 *   • On destroy, restores focus to the element that had it before the modal opened.
 *
 * No external dependency (no @angular/cdk needed). Intended for the Forecasting V2 modals.
 */
@Directive({
  selector: '[appFocusTrap]',
  standalone: true
})
export class FocusTrapDirective implements AfterViewInit, OnDestroy {
  /** Set false to disable (e.g. while modal is loading). Default true. */
  @Input() appFocusTrap: boolean | '' = true;

  private readonly host = inject(ElementRef<HTMLElement>);
  private previouslyFocused: HTMLElement | null = null;

  ngAfterViewInit(): void {
    if (this.appFocusTrap === false) return;
    this.previouslyFocused = document.activeElement as HTMLElement | null;
    // Defer to next macrotask so dynamically-rendered Angular content is in the DOM.
    queueMicrotask(() => this.focusFirst());
  }

  ngOnDestroy(): void {
    // Return focus to the element that had it before the modal opened — important for keyboard users.
    if (this.previouslyFocused && typeof this.previouslyFocused.focus === 'function') {
      try { this.previouslyFocused.focus(); } catch { /* element may have been removed */ }
    }
  }

  /** Cycles focus on Tab / Shift+Tab to keep it inside the dialog. */
  @HostListener('keydown.Tab', ['$event'])
  @HostListener('keydown.Shift.Tab', ['$event'])
  onTab(event: KeyboardEvent): void {
    if (this.appFocusTrap === false) return;
    const focusables = this.getFocusable();
    if (focusables.length === 0) {
      event.preventDefault();
      this.host.nativeElement.focus();
      return;
    }
    const first = focusables[0];
    const last = focusables[focusables.length - 1];
    const active = document.activeElement as HTMLElement;

    if (event.shiftKey && active === first) {
      event.preventDefault();
      last.focus();
    } else if (!event.shiftKey && active === last) {
      event.preventDefault();
      first.focus();
    }
  }

  private focusFirst(): void {
    const focusables = this.getFocusable();
    // Prefer the first NON-close button (don't auto-focus "Fermer" — confusing for screen reader users).
    const target = focusables.find(el => !this.isCloseButton(el)) ?? focusables[0];
    if (target) {
      target.focus();
    } else {
      // No focusable children — make the host itself focusable.
      const host = this.host.nativeElement;
      if (!host.hasAttribute('tabindex')) host.setAttribute('tabindex', '-1');
      host.focus();
    }
  }

  private getFocusable(): HTMLElement[] {
    // Standard list of natively-focusable selectors. Excludes disabled / hidden.
    const selectors = [
      'a[href]',
      'button:not([disabled])',
      'input:not([disabled]):not([type="hidden"])',
      'select:not([disabled])',
      'textarea:not([disabled])',
      '[tabindex]:not([tabindex="-1"])'
    ].join(',');
    const host: HTMLElement = this.host.nativeElement;
    const nodes = Array.from(host.querySelectorAll(selectors)) as HTMLElement[];
    return nodes.filter(n => this.isVisible(n));
  }

  private isVisible(el: HTMLElement): boolean {
    // Cheap visibility check — skip elements hidden via display:none / visibility:hidden.
    return !!(el.offsetWidth || el.offsetHeight || el.getClientRects().length);
  }

  private isCloseButton(el: HTMLElement): boolean {
    // Heuristic: class name or aria-label containing "close" / "Fermer".
    if (el.classList.contains('close-btn')) return true;
    const label = (el.getAttribute('aria-label') ?? '').toLowerCase();
    return label.includes('fermer') || label.includes('close');
  }
}
