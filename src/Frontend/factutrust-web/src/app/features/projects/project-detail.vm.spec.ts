import {
  budgetUsedPercent,
  computeBillableHoursDenominator,
  computeProjectHealth,
  computeTaskProgressPercent,
  computeTaskStatusBreakdown,
  countOpenTasks,
  countOverdueTasks,
  formatPeriodSubtext,
  formatTimeAgoFr,
  isTaskOpen,
  upcomingTasks
} from './project-detail.vm';
import { ProjectTask } from './project-api.service';

function task(partial: Partial<ProjectTask> & Pick<ProjectTask, 'id' | 'status'>): ProjectTask {
  return {
    projectId: 'p1',
    phaseId: 'ph1',
    phaseName: 'Phase',
    title: 'Tâche',
    statusDisplay: '',
    priority: 'Normal',
    priorityDisplay: '',
    progressPercent: 0,
    estimatedHours: 8,
    loggedHours: 0,
    isOverdue: false,
    ...partial
  };
}

describe('project-detail.vm', () => {
  it('counts open and overdue tasks', () => {
    const tasks = [
      task({ id: '1', status: 'Todo' }),
      task({ id: '2', status: 'Done' }),
      task({ id: '3', status: 'Cancelled' }),
      task({ id: '4', status: 'InProgress', isOverdue: true })
    ];
    expect(countOpenTasks(tasks)).toBe(2);
    expect(countOverdueTasks(tasks)).toBe(1);
    expect(isTaskOpen('Done')).toBe(false);
    expect(isTaskOpen(1)).toBe(true);
  });

  it('computes task progress excluding cancelled', () => {
    const tasks = [
      task({ id: '1', status: 'Done' }),
      task({ id: '2', status: 'Todo' }),
      task({ id: '3', status: 'Cancelled' })
    ];
    expect(computeTaskProgressPercent(tasks)).toBe(50);
  });

  it('computes budget used percent from actual cost', () => {
    expect(budgetUsedPercent({ budgetHt: 1000, actualCostHt: 100, timeCostHt: 20, remainingHt: 900, loggedHours: 0, billableUninvoicedHours: 0 })).toBe(10);
    expect(budgetUsedPercent(null, 0)).toBe(0);
  });

  it('uses estimated hours as billable denominator with logged fallback', () => {
    const tasks = [
      task({ id: '1', status: 'Todo', estimatedHours: 10 }),
      task({ id: '2', status: 'Cancelled', estimatedHours: 50 })
    ];
    expect(computeBillableHoursDenominator(tasks, 5)).toBe(10);
    expect(computeBillableHoursDenominator([], 12)).toBe(12);
  });

  it('formats period subtext from end date', () => {
    expect(formatPeriodSubtext('2099-12-31')).toContain('restants');
    expect(formatPeriodSubtext('2000-01-01')).toContain('retard');
  });

  it('computes health labels', () => {
    const overdueTasks = [task({ id: '1', status: 'Todo', isOverdue: true })];
    expect(computeProjectHealth(overdueTasks, null).tone).toBe('danger');

    const okTasks = [task({ id: '1', status: 'Done' }), task({ id: '2', status: 'Todo' })];
    expect(computeProjectHealth(okTasks, null).text).toBe('En bonne voie');
  });

  it('breaks down task statuses with overdue priority', () => {
    const breakdown = computeTaskStatusBreakdown([
      task({ id: '1', status: 'Done' }),
      task({ id: '2', status: 'InProgress' }),
      task({ id: '3', status: 'Waiting', isOverdue: true }),
      task({ id: '4', status: 'Cancelled' })
    ]);
    expect(breakdown.total).toBe(3);
    expect(breakdown.done).toBe(1);
    expect(breakdown.inProgress).toBe(1);
    expect(breakdown.overdue).toBe(1);
  });

  it('sorts upcoming tasks by due date', () => {
    const tasks = [
      task({ id: '1', status: 'Todo', dueDate: '2026-12-01' }),
      task({ id: '2', status: 'Todo', dueDate: '2026-08-01' }),
      task({ id: '3', status: 'Done', dueDate: '2026-07-01' })
    ];
    expect(upcomingTasks(tasks).map(t => t.id)).toEqual(['2', '1']);
  });

  it('formats relative time in French', () => {
    const now = new Date('2026-08-21T12:00:00Z');
    const twoHoursAgo = new Date('2026-08-21T10:00:00Z').toISOString();
    expect(formatTimeAgoFr(twoHoursAgo, now)).toBe('il y a 2 h');
  });
});
