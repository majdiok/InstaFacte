import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ProjectTeamTabComponent } from './project-team.tab';
import { ProjectMember } from '../project-api.service';

describe('ProjectTeamTabComponent', () => {
  let fixture: ComponentFixture<ProjectTeamTabComponent>;
  let component: ProjectTeamTabComponent;

  const sampleMember: ProjectMember = {
    id: 'm1',
    userId: 'u1',
    userName: 'Alice Martin',
    role: 'Member',
    roleDisplay: 'Membre',
    dailyRate: 500,
    hourlyCost: 60,
    weeklyCapacityHours: 40
  };

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ProjectTeamTabComponent]
    }).compileComponents();

    fixture = TestBed.createComponent(ProjectTeamTabComponent);
    component = fixture.componentInstance;
    component.users = [{ id: 'u2', displayName: 'Bob Dupont' }];
    fixture.detectChanges();
  });

  it('hides add form when canManage is false', () => {
    component.canManage = false;
    fixture.detectChanges();

    const addGrid = fixture.nativeElement.querySelector('.proj-team-add-grid');
    expect(addGrid).toBeNull();
  });

  it('shows add form when canManage is true', () => {
    component.canManage = true;
    fixture.detectChanges();

    const addGrid = fixture.nativeElement.querySelector('.proj-team-add-grid');
    expect(addGrid).not.toBeNull();
  });

  it('shows empty state when members list is empty', () => {
    component.members = [];
    fixture.detectChanges();

    const emptyState = fixture.nativeElement.querySelector('app-empty-state');
    expect(emptyState).not.toBeNull();
  });

  it('does not emit addMember when userId is missing', () => {
    component.canManage = true;
    component.userId = '';
    fixture.detectChanges();

    const spy = spyOn(component.addMember, 'emit');
    component.add();

    expect(spy).not.toHaveBeenCalled();
  });

  it('emits addMember with payload when userId is set', () => {
    component.canManage = true;
    component.userId = 'u2';
    component.role = 'Manager';
    component.dailyRate = 400;
    component.hourlyCost = 50;
    component.capacity = 35;
    fixture.detectChanges();

    const spy = spyOn(component.addMember, 'emit');
    component.add();

    expect(spy).toHaveBeenCalledWith({
      userId: 'u2',
      role: 'Manager',
      dailyRate: 400,
      hourlyCost: 50,
      weeklyCapacityHours: 35
    });
    expect(component.userId).toBe('');
  });

  it('emits updateMember on saveEdit after openEdit', () => {
    component.members = [sampleMember];
    component.openEdit(sampleMember);

    const spy = spyOn(component.updateMember, 'emit');
    component.editRole = 'Manager';
    component.editDaily = 550;
    component.editHourly = 70;
    component.editCapacity = 32;
    component.saveEdit();

    expect(spy).toHaveBeenCalledWith({
      id: 'm1',
      payload: {
        userId: 'u1',
        role: 'Manager',
        dailyRate: 550,
        hourlyCost: 70,
        weeklyCapacityHours: 32
      }
    });
    expect(component.editVisible).toBe(false);
  });
});
