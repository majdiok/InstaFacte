import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { MenuItem } from 'primeng/api';
import { Menu } from 'primeng/menu';
import { FtUserMenuComponent } from './ft-user-menu.component';
import { FtOverlayCleanupService } from '@core/services/ft-overlay-cleanup.service';

describe('FtUserMenuComponent', () => {
  let fixture: ComponentFixture<FtUserMenuComponent>;
  let component: FtUserMenuComponent;
  let router: Router;
  let overlayCleanup: jasmine.SpyObj<FtOverlayCleanupService>;

  beforeEach(async () => {
    overlayCleanup = jasmine.createSpyObj('FtOverlayCleanupService', ['clearOrphanOverlays']);

    await TestBed.configureTestingModule({
      imports: [FtUserMenuComponent],
      providers: [
        provideRouter([]),
        { provide: FtOverlayCleanupService, useValue: overlayCleanup }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(FtUserMenuComponent);
    component = fixture.componentInstance;
    router = TestBed.inject(Router);
    spyOn(router, 'navigateByUrl');
    component.items = [{ label: 'Test' }] as MenuItem[];
    fixture.detectChanges();
  });

  it('accepts menu items input', () => {
    expect(component.items.length).toBe(1);
  });

  it('navigateAndClose hides menu, cleans overlays and navigates', () => {
    const hideSpy = jasmine.createSpy('hide');
    component.menu = { hide: hideSpy } as unknown as Menu;

    component.navigateAndClose('/me/2fa');

    expect(hideSpy).toHaveBeenCalled();
    expect(overlayCleanup.clearOrphanOverlays).toHaveBeenCalled();
    expect(router.navigateByUrl).toHaveBeenCalledWith('/me/2fa');
  });

  it('runAndClose executes action after hide and cleanup', () => {
    const action = jasmine.createSpy('action');
    component.menu = { hide: jasmine.createSpy('hide') } as unknown as Menu;

    component.runAndClose(action);

    expect(action).toHaveBeenCalled();
    expect(overlayCleanup.clearOrphanOverlays).toHaveBeenCalled();
  });

  it('ngOnDestroy hides menu and cleans overlays', () => {
    const hideSpy = jasmine.createSpy('hide');
    component.menu = { hide: hideSpy } as unknown as Menu;

    component.ngOnDestroy();

    expect(hideSpy).toHaveBeenCalled();
    expect(overlayCleanup.clearOrphanOverlays).toHaveBeenCalled();
  });
});
