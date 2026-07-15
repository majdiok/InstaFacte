import { TestBed } from '@angular/core/testing';
import { Router, NavigationEnd } from '@angular/router';
import { Subject } from 'rxjs';
import { AiVolatileAnalysisStore } from './ai-volatile-analysis.store';

describe('AiVolatileAnalysisStore', () => {
  let store: AiVolatileAnalysisStore;
  let routerEvents: Subject<unknown>;

  beforeEach(() => {
    routerEvents = new Subject();
    TestBed.configureTestingModule({
      providers: [
        AiVolatileAnalysisStore,
        {
          provide: Router,
          useValue: { events: routerEvents.asObservable() }
        }
      ]
    });
    store = TestBed.inject(AiVolatileAnalysisStore);
  });

  it('takePendingForSend consumes and clears pending context', () => {
    store.setPending({ screenId: 'ledger', analysisSummary: '{"a":1}' });

    const taken = store.takePendingForSend();

    expect(taken?.screenId).toBe('ledger');
    expect(store.peek()).toBeNull();
  });

  it('does not clear fresh pending on NavigationEnd', () => {
    store.setPending({ screenId: 'balance', analysisSummary: '{"b":2}' });

    routerEvents.next(new NavigationEnd(1, '/accounting/balance', '/ai-assistant'));

    expect(store.peek()?.screenId).toBe('balance');
  });
});
