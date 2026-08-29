import { Component } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { ButtonComponent } from './button.component';

@Component({
  standalone: true,
  imports: [ButtonComponent],
  template: `<app-button [routerLink]="link">Export Excel</app-button>`
})
class RouterLinkHostComponent {
  link = '/accounting/fixed-assets';
}

describe('ButtonComponent', () => {
  let fixture: ComponentFixture<ButtonComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ButtonComponent],
      providers: [provideRouter([])]
    }).compileComponents();
    fixture = TestBed.createComponent(ButtonComponent);
  });

  it('renders primary variant by default', () => {
    fixture.detectChanges();
    const btn = fixture.nativeElement.querySelector('button') as HTMLButtonElement;
    expect(btn.classList.contains('btn-primary')).toBeTrue();
  });

  it('renders success variant when configured', () => {
    fixture.componentRef.setInput('variant', 'success');
    fixture.detectChanges();
    const btn = fixture.nativeElement.querySelector('button') as HTMLButtonElement;
    expect(btn.classList.contains('btn-success')).toBeTrue();
  });

  it('renders icon-only ghost with always-visible class', () => {
    fixture.componentRef.setInput('variant', 'ghost');
    fixture.componentRef.setInput('icon', 'pi-eye');
    fixture.componentRef.setInput('iconOnly', true);
    fixture.componentRef.setInput('iconAlwaysVisible', true);
    fixture.componentRef.setInput('ariaLabel', 'Voir');
    fixture.detectChanges();
    const btn = fixture.nativeElement.querySelector('button') as HTMLButtonElement;
    expect(btn.classList.contains('btn-icon-only')).toBeTrue();
    expect(btn.classList.contains('btn-icon-always-visible')).toBeTrue();
    expect(btn.querySelector('i.pi-eye')).toBeTruthy();
  });

  it('disables the native button when disabled input is true', () => {
    fixture.componentRef.setInput('disabled', true);
    fixture.detectChanges();
    const btn = fixture.nativeElement.querySelector('button') as HTMLButtonElement;
    expect(btn.disabled).toBeTrue();
  });

  it('renders router link anchor when routerLink is set', () => {
    fixture.componentRef.setInput('routerLink', '/test');
    fixture.componentRef.setInput('ariaLabel', 'Aller');
    fixture.detectChanges();
    const link = fixture.nativeElement.querySelector('a.btn') as HTMLAnchorElement;
    expect(link).toBeTruthy();
    expect(link.getAttribute('href')).toContain('/test');
  });

  it('emits clicked event when the button is clicked', () => {
    const spy = jasmine.createSpy('clicked');
    fixture.componentRef.setInput('variant', 'primary');
    fixture.componentInstance.clicked.subscribe(spy);
    fixture.detectChanges();
    const btn = fixture.nativeElement.querySelector('button') as HTMLButtonElement;
    btn.click();
    expect(spy).toHaveBeenCalled();
  });

  it('does not emit clicked event when the button is disabled', () => {
    const spy = jasmine.createSpy('clicked');
    fixture.componentRef.setInput('disabled', true);
    fixture.componentInstance.clicked.subscribe(spy);
    fixture.detectChanges();
    const btn = fixture.nativeElement.querySelector('button') as HTMLButtonElement;
    btn.click();
    expect(spy).not.toHaveBeenCalled();
  });

  it('emits clicked event when the anchor is clicked', () => {
    const spy = jasmine.createSpy('clicked');
    fixture.componentRef.setInput('routerLink', '/test');
    fixture.componentInstance.clicked.subscribe(spy);
    fixture.detectChanges();
    const link = fixture.nativeElement.querySelector('a.btn') as HTMLAnchorElement;
    link.click();
    expect(spy).toHaveBeenCalled();
  });
});

// C8 regression (captures 2401/2410): buttons rendered via the routerLink (<a>) branch used to
// lose their projected <ng-content> text entirely — Angular only ever attaches projected light
// DOM to the FIRST <ng-content> declared in the compiled template, regardless of which @if/@else
// branch is actually active at runtime. With two separate <ng-content> tags (one per branch), the
// <a> branch rendered as a blank box next to "Export Excel dotations". This suite hosts
// <app-button> with real projected text through a router link, which is the scenario that was
// broken (unlike the tests above that call TestBed.createComponent(ButtonComponent) directly,
// which never exercises content projection).
describe('ButtonComponent content projection through a router link (C8 regression)', () => {
  let fixture: ComponentFixture<RouterLinkHostComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [RouterLinkHostComponent],
      providers: [provideRouter([])]
    }).compileComponents();
    fixture = TestBed.createComponent(RouterLinkHostComponent);
    fixture.detectChanges();
  });

  it('projects the button label into the router link anchor instead of rendering it empty', () => {
    const link = fixture.nativeElement.querySelector('a.btn') as HTMLAnchorElement;
    expect(link).toBeTruthy();
    expect(link.textContent?.trim()).toBe('Export Excel');
  });
});
