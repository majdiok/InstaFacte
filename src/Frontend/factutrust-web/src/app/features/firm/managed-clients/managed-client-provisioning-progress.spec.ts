import {
  PROVISIONING_PERCENT_CAP,
  applyRunningSchedule,
  completeProvisioningSteps,
  createInitialProvisioningSteps,
  failProvisioningSteps,
  scheduleProvisioningProgress
} from './managed-client-provisioning-progress';

describe('managed-client-provisioning-progress', () => {
  it('starts at validating with low percent at elapsed 0', () => {
    const s = scheduleProvisioningProgress(0);
    expect(s.activePhaseId).toBe('validating');
    expect(s.percent).toBe(0);
    expect(s.label).toContain('Validation');
  });

  it('moves to creatingDatabase around 5s', () => {
    const s = scheduleProvisioningProgress(5);
    expect(s.activePhaseId).toBe('creatingDatabase');
    expect(s.percent).toBeGreaterThanOrEqual(8);
    expect(s.percent).toBeLessThanOrEqual(20);
  });

  it('is in installingChart at 20s with mid-range percent', () => {
    const s = scheduleProvisioningProgress(20);
    expect(s.activePhaseId).toBe('installingChart');
    expect(s.percent).toBeGreaterThanOrEqual(20);
    expect(s.percent).toBeLessThan(75);
  });

  it('is in finalizing at 60s and respects the 92% cap', () => {
    const s = scheduleProvisioningProgress(60);
    expect(s.activePhaseId).toBe('finalizing');
    expect(s.percent).toBeLessThanOrEqual(PROVISIONING_PERCENT_CAP);
    expect(s.percent).toBeGreaterThanOrEqual(85);
  });

  it('never exceeds the cap while scheduling', () => {
    for (const elapsed of [0, 3, 8, 30, 45, 55, 90, 300]) {
      expect(scheduleProvisioningProgress(elapsed).percent).toBeLessThanOrEqual(PROVISIONING_PERCENT_CAP);
    }
  });

  it('createInitialProvisioningSteps marks first as running', () => {
    const steps = createInitialProvisioningSteps();
    expect(steps.length).toBe(5);
    expect(steps[0].status).toBe('running');
    expect(steps.slice(1).every(s => s.status === 'pending')).toBeTrue();
  });

  it('applyRunningSchedule marks prior steps done', () => {
    const steps = applyRunningSchedule(
      createInitialProvisioningSteps(),
      scheduleProvisioningProgress(20)
    );
    expect(steps.find(s => s.id === 'validating')!.status).toBe('done');
    expect(steps.find(s => s.id === 'creatingDatabase')!.status).toBe('done');
    expect(steps.find(s => s.id === 'installingChart')!.status).toBe('running');
    expect(steps.find(s => s.id === 'fiscalParams')!.status).toBe('pending');
  });

  it('completeProvisioningSteps marks all done', () => {
    const done = completeProvisioningSteps(createInitialProvisioningSteps());
    expect(done.every(s => s.status === 'done')).toBeTrue();
  });

  it('failProvisioningSteps marks the running step as error', () => {
    const running = applyRunningSchedule(
      createInitialProvisioningSteps(),
      scheduleProvisioningProgress(5)
    );
    const failed = failProvisioningSteps(running);
    expect(failed.find(s => s.id === 'creatingDatabase')!.status).toBe('error');
    expect(failed.find(s => s.id === 'validating')!.status).toBe('done');
  });
});
