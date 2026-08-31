import { ProjectTask } from './project-api.service';
import {
  activeFilterCount,
  computeRootTaskStatusSummary,
  emptyTaskFilters,
  filterAndSortRootTasks,
  filterRootTasks,
  matchesDueFilter,
  matchesTaskStatus,
  sortRootTasks
} from './project-tasks.vm';

function task(partial: Partial<ProjectTask> & Pick<ProjectTask, 'id'>): ProjectTask {
  return {
    projectId: 'p1',
    phaseId: 'ph1',
    phaseName: 'Maquette et design',
    title: 'Tâche',
    status: 'Todo',
    statusDisplay: 'À faire',
    priority: 'Normal',
    priorityDisplay: 'Normale',
    progressPercent: 0,
    estimatedHours: 8,
    loggedHours: 0,
    isOverdue: false,
    ...partial
  };
}

describe('project-tasks.vm', () => {
  const baseTasks: ProjectTask[] = [
    task({ id: '1', title: 'Refonte page 1', phaseName: 'Maquette et design', status: 'InProgress', statusDisplay: 'En cours', priority: 'High', assigneeUserId: 'u1', dueDate: '2026-08-26' }),
    task({ id: '2', title: 'API', phaseName: 'Développement', status: 'Done', statusDisplay: 'Terminé', priority: 'Normal', dueDate: '2026-08-10' }),
    task({ id: '3', title: 'Sub', phaseName: 'Maquette et design', status: 'Todo', parentTaskId: '1' }),
    task({ id: '4', title: 'Retard', phaseName: 'Recette', status: 'Waiting', statusDisplay: 'En attente', priority: 'Urgent', isOverdue: true, dueDate: '2026-08-01' }),
    task({ id: '5', title: 'Sans date', phaseName: 'Backlog', status: 'Todo', priority: 'Low' }),
    task({ id: '6', title: 'Annulée', phaseName: 'Backlog', status: 'Cancelled' })
  ];

  it('filters by title and phase name, ignores subtasks', () => {
    const filters = { ...emptyTaskFilters(), searchText: 'maquette' };
    const ids = filterRootTasks(baseTasks, filters).map(t => t.id);
    expect(ids).toEqual(['1']);
    expect(filterRootTasks(baseTasks, filters).every(t => !t.parentTaskId)).toBe(true);
  });

  it('matches status as string or number', () => {
    const inProgress = task({ id: 'n', status: 1 });
    expect(matchesTaskStatus(inProgress, 'InProgress')).toBe(true);
    expect(filterRootTasks([inProgress], { ...emptyTaskFilters(), status: 'InProgress' }).length).toBe(1);
  });

  it('filters assignee and priority', () => {
    const filters = { ...emptyTaskFilters(), assigneeId: 'u1', priority: 'High' as const };
    expect(filterRootTasks(baseTasks, filters).map(t => t.id)).toEqual(['1']);
  });

  it('filters due buckets', () => {
    const today = new Date();
    today.setHours(12, 0, 0, 0);
    const y = today.getFullYear();
    const m = String(today.getMonth() + 1).padStart(2, '0');
    const d = String(today.getDate()).padStart(2, '0');
    const localToday = `${y}-${m}-${d}`;
    const weekTask = task({ id: 'w', dueDate: localToday });
    expect(matchesDueFilter(task({ id: 'o', isOverdue: true, dueDate: '2020-01-01' }), 'overdue')).toBe(true);
    expect(matchesDueFilter(weekTask, 'today')).toBe(true);
    expect(matchesDueFilter(task({ id: 'n', dueDate: null }), 'none')).toBe(true);
  });

  it('counts active filters and resets', () => {
    const filters = {
      searchText: 'x',
      status: 'Todo' as const,
      assigneeId: 'u1',
      priority: 'Normal' as const,
      dueFilter: 'week' as const
    };
    expect(activeFilterCount(filters)).toBe(5);
    expect(activeFilterCount(emptyTaskFilters())).toBe(0);
  });

  it('sorts by due, priority, title, progress', () => {
    const tasks = [
      task({ id: 'a', title: 'B', priority: 'Low', progressPercent: 10, dueDate: '2026-09-01' }),
      task({ id: 'b', title: 'A', priority: 'Urgent', progressPercent: 90, dueDate: '2026-08-01' }),
      task({ id: 'c', title: 'C', priority: 'Normal', progressPercent: 50, dueDate: '2026-08-15' })
    ];
    expect(sortRootTasks(tasks, 'dueAsc').map(t => t.id)).toEqual(['b', 'c', 'a']);
    expect(sortRootTasks(tasks, 'priority').map(t => t.id)[0]).toBe('b');
    expect(sortRootTasks(tasks, 'title').map(t => t.id)).toEqual(['b', 'a', 'c']);
    expect(sortRootTasks(tasks, 'progress').map(t => t.id)[0]).toBe('b');
  });

  it('computes root summary with exclusive overdue bucket', () => {
    const summary = computeRootTaskStatusSummary(baseTasks);
    expect(summary.total).toBe(4);
    expect(summary.todo).toBe(1);
    expect(summary.done).toBe(1);
    expect(summary.inProgress).toBe(1);
    expect(summary.waiting).toBe(0);
    expect(summary.overdue).toBe(1);
    expect(summary.todoPct).toBe(25);
    expect(summary.donePct).toBe(25);
  });

  it('combines filter and sort', () => {
    const result = filterAndSortRootTasks(baseTasks, emptyTaskFilters(), 'title');
    expect(result.map(t => t.title)).toEqual(['Annulée', 'API', 'Refonte page 1', 'Retard', 'Sans date']);
  });
});
