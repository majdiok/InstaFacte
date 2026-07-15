import { Component, ViewChild } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { FocusTrapDirective } from './focus-trap.directive';

@Component({
  standalone: true,
  imports: [FocusTrapDirective],
  template: `
    <button id="outside-before" type="button">Outside before</button>
    <div appFocusTrap role="dialog" class="modal-panel">
      <button class="close-btn" type="button" aria-label="Fermer">×</button>
      <input id="first-input" type="text" />
      <button id="middle" type="button">Middle</button>
      <button id="last" type="button">Last</button>
    </div>
    <button id="outside-after" type="button">Outside after</button>
  `
})
class HostComponent {
  @ViewChild(FocusTrapDirective) directive!: FocusTrapDirective;
}

describe('FocusTrapDirective', () => {
  let fixture: ComponentFixture<HostComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [HostComponent]
    }).compileComponents();
    fixture = TestBed.createComponent(HostComponent);
    document.body.appendChild(fixture.nativeElement);
    fixture.detectChanges();
  });

  afterEach(() => {
    // .remove() est un no-op si le nœud n'est plus attaché (robuste aux tests qui manipulent le DOM).
    (fixture.nativeElement as HTMLElement).remove();
  });

  it('focuses the first non-close-button focusable on init', async () => {
    // Wait for queueMicrotask + change detection cycle.
    await new Promise<void>(resolve => queueMicrotask(() => { fixture.detectChanges(); resolve(); }));
    expect(document.activeElement?.id).toBe('first-input');
  });

  it('cycles focus on Tab from last to first', () => {
    const last = fixture.nativeElement.querySelector('#last') as HTMLButtonElement;
    const first = fixture.nativeElement.querySelector('#first-input') as HTMLInputElement;
    last.focus();
    expect(document.activeElement).toBe(last);

    const event = new KeyboardEvent('keydown', { key: 'Tab', bubbles: true });
    spyOn(event, 'preventDefault').and.callThrough();
    const panel = fixture.debugElement.query(By.directive(FocusTrapDirective)).nativeElement as HTMLElement;
    panel.dispatchEvent(event);

    // The directive prevents default and refocuses the close button (first focusable overall).
    expect(event.preventDefault).toHaveBeenCalled();
    expect(document.activeElement).toBe(panel.querySelector('.close-btn'));
  });

  it('cycles focus on Shift+Tab from first to last', () => {
    const closeBtn = fixture.nativeElement.querySelector('.close-btn') as HTMLButtonElement;
    const last = fixture.nativeElement.querySelector('#last') as HTMLButtonElement;
    closeBtn.focus();

    const event = new KeyboardEvent('keydown', { key: 'Tab', shiftKey: true, bubbles: true });
    spyOn(event, 'preventDefault').and.callThrough();
    const panel = fixture.debugElement.query(By.directive(FocusTrapDirective)).nativeElement as HTMLElement;
    panel.dispatchEvent(event);

    expect(event.preventDefault).toHaveBeenCalled();
    expect(document.activeElement).toBe(last);
  });

  it('restores focus on destroy to previously-active element', async () => {
    // Élément déclencheur dédié (sans directive) pour simuler l'ouverture d'une modale :
    // c'est lui qui doit récupérer le focus à la destruction. Auto-contenu ⇒ pas d'interférence
    // avec la microtâche de focus résiduelle du fixture du beforeEach.
    const trigger = document.createElement('button');
    trigger.id = 'restore-target';
    document.body.appendChild(trigger);
    trigger.focus();
    expect(document.activeElement?.id).toBe('restore-target');

    const fx2 = TestBed.createComponent(HostComponent);
    document.body.appendChild(fx2.nativeElement);
    fx2.detectChanges();
    // La directive capture l'élément actif à l'init puis focus le 1er input via queueMicrotask.
    await new Promise<void>(resolve => queueMicrotask(() => { fx2.detectChanges(); resolve(); }));
    expect(document.activeElement?.id).toBe('first-input');

    // À la destruction, la directive doit restaurer le focus sur le déclencheur.
    fx2.destroy();
    (fx2.nativeElement as HTMLElement).remove();
    expect(document.activeElement?.id).toBe('restore-target');

    trigger.remove();
  });
});
