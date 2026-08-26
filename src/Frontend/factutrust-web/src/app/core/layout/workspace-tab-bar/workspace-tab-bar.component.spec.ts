import { ComponentFixture, TestBed } from '@angular/core/testing';
import { WorkspaceTabBarComponent } from './workspace-tab-bar.component';

describe('WorkspaceTabBarComponent', () => {
  let fixture: ComponentFixture<WorkspaceTabBarComponent>;
  let component: WorkspaceTabBarComponent;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [WorkspaceTabBarComponent]
    }).compileComponents();

    fixture = TestBed.createComponent(WorkspaceTabBarComponent);
    component = fixture.componentInstance;
    fixture.componentRef.setInput('workTitle', 'Devis');
    fixture.componentRef.setInput('activePane', 'work');
    fixture.detectChanges();
  });

  it('renders work and AI tab labels', () => {
    const tabs = fixture.nativeElement.querySelectorAll('[role="tab"]');
    expect(tabs.length).toBe(2);
    expect(tabs[0].textContent).toContain('Devis');
    expect(tabs[1].textContent).toContain('Assistant IA');
  });

  it('emits selectPane when work tab clicked', () => {
    const spy = jasmine.createSpy('selectPane');
    component.selectPane.subscribe(spy);

    const workTab = fixture.nativeElement.querySelector('#workspace-tab-work') as HTMLButtonElement;
    workTab.click();

    expect(spy).toHaveBeenCalledWith('work');
  });

  it('emits selectPane when AI tab clicked', () => {
    const spy = jasmine.createSpy('selectPane');
    component.selectPane.subscribe(spy);

    const aiTab = fixture.nativeElement.querySelector('#workspace-tab-ai') as HTMLButtonElement;
    aiTab.click();

    expect(spy).toHaveBeenCalledWith('ai');
  });

  it('close button emits closeAi without selecting AI tab', () => {
    const selectSpy = jasmine.createSpy('selectPane');
    const closeSpy = jasmine.createSpy('closeAi');
    component.selectPane.subscribe(selectSpy);
    component.closeAi.subscribe(closeSpy);

    const closeBtn = fixture.nativeElement.querySelector('.workspace-tab__close') as HTMLButtonElement;
    closeBtn.click();

    expect(closeSpy).toHaveBeenCalledTimes(1);
    expect(selectSpy).not.toHaveBeenCalled();
  });

  it('renders close button with accessible label', () => {
    const closeBtn = fixture.nativeElement.querySelector('.workspace-tab__close') as HTMLButtonElement;
    expect(closeBtn).toBeTruthy();
    expect(closeBtn.getAttribute('aria-label')).toBe('Fermer l\'assistant IA');
  });

  it('marks active tab with aria-selected', () => {
    fixture.componentRef.setInput('activePane', 'ai');
    fixture.detectChanges();

    const workTab = fixture.nativeElement.querySelector('#workspace-tab-work');
    const aiTab = fixture.nativeElement.querySelector('#workspace-tab-ai');
    expect(workTab.getAttribute('aria-selected')).toBe('false');
    expect(aiTab.getAttribute('aria-selected')).toBe('true');
  });
});
