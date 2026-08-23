import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { By } from '@angular/platform-browser';

import {
  GlobalSearchComponent,
  computeGlobalSearchDropdownPosition
} from './global-search.component';
import { GlobalSearchService } from '../../services/global-search.service';
import { AuthService } from '../../services/auth.service';

describe('computeGlobalSearchDropdownPosition', () => {
  const inputRect = { left: 200, bottom: 48, width: 400 };

  it('places dropdown below secondary nav when visible', () => {
    const pos = computeGlobalSearchDropdownPosition(inputRect, {
      secondaryNavRect: { bottom: 92, height: 44 },
      viewportWidth: 1280,
      viewportHeight: 800,
      gap: 8
    });

    expect(pos.top).toBe(100); // 92 + 8
    expect(pos.left).toBe(200);
    expect(pos.width).toBe(400);
    expect(pos.maxHeight).toBeLessThanOrEqual(420);
    expect(pos.top).toBeGreaterThan(inputRect.bottom);
  });

  it('places dropdown below input when secondary nav is absent', () => {
    const pos = computeGlobalSearchDropdownPosition(inputRect, {
      secondaryNavRect: null,
      viewportWidth: 1280,
      viewportHeight: 800,
      gap: 8
    });

    expect(pos.top).toBe(56); // 48 + 8
  });

  it('ignores secondary nav below desktop breakpoint', () => {
    const pos = computeGlobalSearchDropdownPosition(inputRect, {
      secondaryNavRect: { bottom: 92, height: 44 },
      viewportWidth: 800,
      viewportHeight: 800,
      gap: 8
    });

    expect(pos.top).toBe(56);
  });

  it('ignores secondary nav with zero height', () => {
    const pos = computeGlobalSearchDropdownPosition(inputRect, {
      secondaryNavRect: { bottom: 92, height: 0 },
      viewportWidth: 1280,
      viewportHeight: 800,
      gap: 8
    });

    expect(pos.top).toBe(56);
  });

  it('clamps left/width to viewport padding', () => {
    const pos = computeGlobalSearchDropdownPosition(
      { left: 2, bottom: 48, width: 2000 },
      {
        viewportWidth: 400,
        viewportHeight: 800,
        viewportPad: 8
      }
    );

    expect(pos.left).toBe(8);
    expect(pos.width).toBe(400 - 8 - 8);
  });
});

describe('GlobalSearchComponent', () => {
  let fixture: ComponentFixture<GlobalSearchComponent>;
  let component: GlobalSearchComponent;
  let searchService: GlobalSearchService;

  const authStub = {
    canAccessPlatformSettings: () => true,
    hasAllModules: () => true,
    hasAllPermissions: () => true,
    isAccountingFirm: () => false,
    isDelegatedMode: () => false
  } as unknown as AuthService;

  beforeEach(async () => {
    localStorage.clear();
    await TestBed.configureTestingModule({
      imports: [GlobalSearchComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        { provide: AuthService, useValue: authStub }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(GlobalSearchComponent);
    component = fixture.componentInstance;
    searchService = TestBed.inject(GlobalSearchService);
    fixture.detectChanges();
  });

  afterEach(() => {
    document.querySelectorAll('.global-search__dropdown').forEach(el => el.remove());
    localStorage.clear();
  });

  it('portals dropdown to document.body on focus and cleans up on close', () => {
    if (!searchService.isEnabled()) {
      pending('globalSearchEnabled is false in this environment');
    }

    const input = fixture.debugElement.query(By.css('.global-search__input--header'));
    expect(input).toBeTruthy();
    input.nativeElement.focus();
    component.onFocus();
    fixture.detectChanges();

    const dropdown = document.body.querySelector('.global-search__dropdown');
    expect(dropdown).toBeTruthy();
    expect(dropdown!.parentElement).toBe(document.body);

    component.closeAll();
    fixture.detectChanges();

    expect(document.body.querySelector('.global-search__dropdown')).toBeNull();
  });

  it('repositionDropdown places panel under secondary nav when present', () => {
    if (!searchService.isEnabled()) {
      pending('globalSearchEnabled is false in this environment');
    }

    const navHost = document.createElement('app-secondary-nav');
    const nav = document.createElement('div');
    nav.className = 'secondary-nav';
    navHost.appendChild(nav);
    document.body.appendChild(navHost);

    spyOn(nav, 'getBoundingClientRect').and.returnValue({
      top: 48,
      bottom: 92,
      left: 0,
      right: 1280,
      width: 1280,
      height: 44,
      x: 0,
      y: 48,
      toJSON: () => ({})
    } as DOMRect);

    component.onFocus();
    fixture.detectChanges();

    const wrap = component['inputWrap']?.nativeElement as HTMLElement;
    spyOn(wrap, 'getBoundingClientRect').and.returnValue({
      top: 8,
      bottom: 40,
      left: 300,
      right: 700,
      width: 400,
      height: 32,
      x: 300,
      y: 8,
      toJSON: () => ({})
    } as DOMRect);

    Object.defineProperty(window, 'innerWidth', { configurable: true, value: 1280 });
    Object.defineProperty(window, 'innerHeight', { configurable: true, value: 800 });

    component.repositionDropdown();

    expect(component.dropdownPos().top).toBe(100);
    expect(component.dropdownPos().left).toBe(300);
    expect(component.dropdownPos().width).toBe(400);

    navHost.remove();
  });

  it('does not close dropdown when clicking the portaled panel', () => {
    if (!searchService.isEnabled()) {
      pending('globalSearchEnabled is false in this environment');
    }

    component.onFocus();
    fixture.detectChanges();
    expect(component.dropdownOpen()).toBe(true);

    const dropdown = document.body.querySelector('.global-search__dropdown') as HTMLElement;
    expect(dropdown).toBeTruthy();

    component.onDocumentClick({ target: dropdown } as unknown as MouseEvent);
    expect(component.dropdownOpen()).toBe(true);

    const outside = document.createElement('div');
    document.body.appendChild(outside);
    component.onDocumentClick({ target: outside } as unknown as MouseEvent);
    expect(component.dropdownOpen()).toBe(false);
    outside.remove();
  });

  it('onGlobalKeydown ignores events with missing key', () => {
    if (!searchService.isEnabled()) {
      pending('globalSearchEnabled is false in this environment');
    }

    expect(() =>
      component.onGlobalKeydown({ key: undefined } as unknown as KeyboardEvent)
    ).not.toThrow();
    expect(searchService.paletteOpen()).toBe(false);
  });
});
