import { ComponentFixture, TestBed } from '@angular/core/testing';
import { SimpleChanges } from '@angular/core';
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

function dropEvent(
  source: ProjectTask[],
  target: ProjectTask[],
  item: ProjectTask,
  currentIndex: number,
  previousIndex = 0
): CdkDragDrop<ProjectTask[]> {
  return {
    previousContainer: { data: source },
    container: { data: target },
    previousIndex,
    currentIndex,
    item: { data: item }
  } as CdkDragDrop<ProjectTask[]>;
}

function tasksChange(current: ProjectTask[]): SimpleChanges {
  return {
    tasks: {
      previousValue: [],
      currentValue: current,
      firstChange: false,
      isFirstChange: () => false
    }
  };
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
    component.onDrop(dropEvent([dragged], [], dragged, 0), 'phase-doing');
    expect(moves).toEqual([]);
  });

  it('does not emit when dropped back into the same column', () => {
    const moves: unknown[] = [];
    component.move.subscribe(ev => moves.push(ev));
    const source = component.columnTasks['phase-todo'];
    const dragged = source[0];
    const event = dropEvent(source, source, dragged, 0);
    (event as { previousContainer: unknown; container: unknown }).previousContainer = event.container;

    component.onDrop(event, 'phase-todo');
    expect(moves).toEqual([]);
    expect(component.columnTasks['phase-todo'].map(t => t.id)).toEqual(['t1']);
  });

  it('inserts at the top of the target column when currentIndex is 0', () => {
    const moves: unknown[] = [];
    component.move.subscribe(ev => moves.push(ev));
    const source = component.columnTasks['phase-todo'];
    const target = component.columnTasks['phase-doing'];
    const dragged = source[0];

    component.onDrop(dropEvent(source, target, dragged, 0), 'phase-doing');

    expect(component.columnTasks['phase-doing'].map(t => t.id)).toEqual(['t1', 't2', 't3']);
    expect(moves).toEqual([{ taskId: 't1', phaseId: 'phase-doing', status: 'InProgress' }]);
  });

  it('inserts in the middle of the target column', () => {
    const source = component.columnTasks['phase-todo'];
    const target = component.columnTasks['phase-doing'];
    const dragged = source[0];

    component.onDrop(dropEvent(source, target, dragged, 1), 'phase-doing');

    expect(component.columnTasks['phase-doing'].map(t => t.id)).toEqual(['t2', 't1', 't3']);
    expect(dragged.status).toBe('InProgress');
  });

  it('inserts at the bottom of the target column when dropped past the last card', () => {
    const source = component.columnTasks['phase-todo'];
    const target = component.columnTasks['phase-doing'];
    const dragged = source[0];

    component.onDrop(dropEvent(source, target, dragged, target.length), 'phase-doing');

    expect(component.columnTasks['phase-doing'].map(t => t.id)).toEqual(['t2', 't3', 't1']);
  });

  it('accepts a drop into an empty column and maps Done status', () => {
    const moves: unknown[] = [];
    component.move.subscribe(ev => moves.push(ev));
    const source = component.columnTasks['phase-todo'];
    const target = component.columnTasks['phase-done'];
    const dragged = source[0];

    component.onDrop(dropEvent(source, target, dragged, 0), 'phase-done');

    expect(component.columnTasks['phase-done'].map(t => t.id)).toEqual(['t1']);
    expect(moves).toEqual([{ taskId: 't1', phaseId: 'phase-done', status: 'Done' }]);
    expect(dragged.status).toBe('Done');
    expect(dragged.progressPercent).toBe(100);
  });

  it('allows moving the same task to another column without a page reload', () => {
    const moves: unknown[] = [];
    component.move.subscribe(ev => moves.push(ev));

    const dragged = component.columnTasks['phase-todo'][0];
    component.onDrop(
      dropEvent(
        component.columnTasks['phase-todo'],
        component.columnTasks['phase-doing'],
        dragged,
        component.columnTasks['phase-doing'].length
      ),
      'phase-doing'
    );

    component.tasks = [
      task('t1', 'phase-doing', { status: 'InProgress', statusDisplay: 'En cours' }),
      task('t2', 'phase-doing'),
      task('t3', 'phase-doing')
    ];
    component.ngOnChanges(tasksChange(component.tasks));
    expect(component.isPending('t1')).toBe(false);

    const fromDoing = component.columnTasks['phase-doing'];
    const previousIndex = fromDoing.findIndex(t => t.id === 't1');
    const stillDragging = fromDoing[previousIndex];
    component.onDrop(
      dropEvent(
        fromDoing,
        component.columnTasks['phase-done'],
        stillDragging,
        0,
        previousIndex
      ),
      'phase-done'
    );

    expect(moves).toEqual([
      { taskId: 't1', phaseId: 'phase-doing', status: 'InProgress' },
      { taskId: 't1', phaseId: 'phase-done', status: 'Done' }
    ]);
    expect(stillDragging.phaseId).toBe('phase-done');
    expect(stillDragging.status).toBe('Done');
    expect(component.columnTasks['phase-done'].map(t => t.id)).toEqual(['t1']);
    expect(component.isPending('t1')).toBe(true);
  });
});
