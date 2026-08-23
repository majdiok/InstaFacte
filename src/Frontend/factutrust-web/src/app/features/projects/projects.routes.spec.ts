import { PROJECTS_ROUTES } from './projects.routes';

describe('projects.routes', () => {
  it('registers dashboard and time before the :id wildcard', () => {
    expect(PROJECTS_ROUTES.map(r => r.path)).toEqual([
      'dashboard',
      '',
      'time',
      ':id',
      ':id/tasks/:taskId'
    ]);
  });

  it('exposes native FactuTrust screens including dashboard', () => {
    expect(PROJECTS_ROUTES.every(r => r.loadComponent)).toBe(true);
  });
});
