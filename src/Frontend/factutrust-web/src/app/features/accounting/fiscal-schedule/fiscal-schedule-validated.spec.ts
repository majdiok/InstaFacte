import { FISCAL_STATUS_OPTIONS } from './fiscal-schedule.view-model';
import { FiscalScheduleStatus } from '../services/fiscal-schedule.service';

describe('fiscal-schedule.view-model', () => {
  it('includes Validated status in filter options', () => {
    const validated = FISCAL_STATUS_OPTIONS.find(o => o.value === FiscalScheduleStatus.Validated);
    expect(validated?.label).toBe('Validee');
  });
});
