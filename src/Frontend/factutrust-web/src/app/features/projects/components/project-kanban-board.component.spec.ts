import { ComponentFixture, TestBed } from '@angular/core/testing';
import { CdkDragDrop } from '@angular/cdk/drag-drop';
import { ProjectKanbanBoardComponent } from './project-kanban-board.component';
import { ProjectDetail, ProjectTask } from '../project-api.service';

function task(id: string, phaseId: string, overrides: Partial<ProjectTask> = {}): ProjectTask {
  return {
    id,
    projectId: 'proj-1',
    phaseId,
    phaseName: 'Phase',
    title: `Task ${id}`,
    status: 'Todo',
    statusDisplay: 'À faire',
    priority: 'Normal',
    priorityDisplay: 'Normale',
    progressPercent: 0,
    estimatedHours: 8,
    loggedHours: 0,
    isOverdue: false,
    ...overrides
  } as ProjectTask;
}

function project(): ProjectDetail {
  return {
    id: 'proj-1',
    kind: 'Generic',
    phases: [
      { id: 'phase-todo', name: 'À faire', sortOrder: 0, color: '#94a3b8' },
      { id: 'phase-doing', name: 'En cours', sortOrder: 1, color: '#3b82f6' },
      { id: 'phase-done', name: 'Terminé', sortOrder: 3, color: '#22c55e' }
    ]
  } as ProjectDetail;
}

describe('ProjectKanbanBoardComponent', () => {
  let fixture: ComponentFixture<ProjectKanbanBoardComponent>;
  let component: ProjectKanbanBoardComponent;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ProjectKanbanBoardComponent]
    }).compileComponents();

    fixture = TestBed.createComponent(ProjectKanbanBoardComponent);
    component = fixture.componentInstance;
    component.project = project();
    component.tasks = [
      task('t1', 'phase-todo'),
      task('t2', 'phase-doing'),
      task('t3', 'phase-doing')
    ];
    component.canUpdateTask = true;
    fixture.detectChanges();
  });

  it('groups root tasks into phase columns', () => {
    expect(component.columnTasks['phase-todo'].map(t => t.id)).toEqual(['t1']);
    expect(component.columnTasks['phase-doing'].map(t => t.id)).toEqual(['t2', 't3']);
    expect(component.columnTasks['phase-done']).toEqual([]);
  });

  it('does not rebuild columns when tasks input gets a new array with same content', () => {
    component.tasks = [
      task('t1', 'phase-todo'),
      task('t2', 'phase-doing')
    ];
    component.ngOnChanges({
      tasks: {
        previousValue: [],
        currentValue: component.tasks,
        firstChange: false,
        isFirstChange: () => false
      }
    });
    const columnsBefore = component.columnTasks['phase-todo'];

    component.tasks = [
      task('t1', 'phase-todo'),
      task('t2', 'phase-doing')
    ];
    component.ngOnChanges({
      tasks: {
        previousValue: [],
        currentValue: component.tasks,
        firstChange: false,
        isFirstChange: () => false
      }
    });

    expect(component.columnTasks['phase-todo']).toBe(columnsBefore);
  });

  it('clears pending after server reload confirms the same phase/status', () => {
    const dragged = task('t1', 'phase-todo');
    component.tasks = [dragged, task('t2', 'phase-doing')];
    component.ngOnChanges({
      tasks: {
        previousValue: [],
        currentValue: component.tasks,
        firstChange: false,
        isFirstChange: () => false
      }
    });

    const sourceList = component.columnTasks['phase-todo'];
    const targetList = component.columnTasks['phase-doing'];
    const dropEvent = {
      previousContainer: { data: sourceList },
      container: { data: targetList },
      previousIndex: 0,
      currentIndex: targetList.length,
      item: { data: dragged }
    } as CdkDragDrop<ProjectTask[]>;

    component.onDrop(dropEvent, 'phase-doing');
    expect(component.isPending('t1')).toBe(true);

    // Parent reload returns equivalent layout (same sync key as optimistic state).
    component.tasks = [
      task('t1', 'phase-doing', { status: 'InProgress', statusDisplay: 'En cours' }),
      task('t2', 'phase-doing')
    ];
    const columnsBefore = component.columnTasks['phase-doing'];
    component.ngOnChanges({
      tasks: {
        previousValue: [],
        currentValue: component.tasks,
        firstChange: false,
        isFirstChange: () => false
      }
    });

    expect(component.isPending('t1')).toBe(false);
    expect(component.columnTasks['phase-doing']).toBe(columnsBefore);
  });

  it('rebuilds columns and clears pending when server rolls back a move', () => {
    const dragged = task('t1', 'phase-todo');
    component.tasks = [dragged, task('t2', 'phase-doing')];
    component.ngOnChanges({
      tasks: {
        previousValue: [],
        currentValue: component.tasks,
        firstChange: false,
        isFirstChange: () => false
      }
    });

    const dropEvent = {
      previousContainer: { data: component.columnTasks['phase-todo'] },
      container: { data: component.columnTasks['phase-doing'] },
      previousIndex: 0,
      currentIndex: 0,
      item: { data: dragged }
    } as CdkDragDrop<ProjectTask[]>;

    component.onDrop(dropEvent, 'phase-doing');
    expect(component.isPending('t1')).toBe(true);

    component.tasks = [
      task('t1', 'phase-todo'),
      task('t2', 'phase-doing')
    ];
    component.ngOnChanges({
      tasks: {
        previousValue: [],
        currentValue: component.tasks,
        firstChange: false,
        isFirstChange: () => false
      }
    });

    expect(component.isPending('t1')).toBe(false);
    expect(component.columnTasks['phase-todo'].map(t => t.id)).toEqual(['t1']);
    expect(component.columnTasks['phase-doing'].map(t => t.id)).toEqual(['t2']);
  });

  it('emits move with mapped status when dropping into another column', () => {
    const moves: unknown[] = [];
    component.move.subscribe(ev => moves.push(ev));

    const dragged = task('t1', 'phase-todo');
    const sourceList = [dragged];
    const targetList: ProjectTask[] = [];
    component.columnTasks = {
      'phase-todo': sourceList,
      'phase-doing': targetList,
      'phase-done': []
    };

    const dropEvent = {
      previousContainer: { data: sourceList },
      container: { data: targetList },
      previousIndex: 0,
      currentIndex: 0,
      item: { data: dragged }
    } as CdkDragDrop<ProjectTask[]>;

    component.onDrop(dropEvent, 'phase-doing');

    expect(moves).toEqual([
      { taskId: 't1', phaseId: 'phase-doing', status: 'InProgress' }
    ]);
    expect(dragged.phaseId).toBe('phase-doing');
    expect(dragged.status).toBe('InProgress');
    expect(component.isPending('t1')).toBe(true);
  });

  it('does not emit move when readonly', () => {
    component.canUpdateTask = false;
    const moves: unknown[] = [];
    component.move.subscribe(ev => moves.push(ev));

    const dragged = task('t1', 'phase-todo');
    const dropEvent = {
      previousIndex: 0,
      currentIndex: 0,
      item: { data: dragged }
    } as CdkDragDrop<ProjectTask[]>;

    component.onDrop(dropEvent, 'phase-doing');
    expect(moves).toEqual([]);
  });
});
