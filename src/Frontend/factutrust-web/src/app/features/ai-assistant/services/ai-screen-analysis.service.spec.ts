import { TestBed } from '@angular/core/testing';
import { AiScreenAnalysisService } from './ai-screen-analysis.service';
import { AiAssistantShellService } from './ai-assistant-shell.service';
import { AiVolatileAnalysisStore } from './ai-volatile-analysis.store';
import { AiChatSessionService, SendMessageInput } from './ai-chat-session.service';
import { resolveScreenAnalysisBackendPrompt } from '../utils/ai-screen-analysis-prompts';

describe('AiScreenAnalysisService', () => {
  let service: AiScreenAnalysisService;
  let sessionSpy: jasmine.SpyObj<AiChatSessionService>;
  let shellSpy: jasmine.SpyObj<AiAssistantShellService>;
  let storeSpy: jasmine.SpyObj<AiVolatileAnalysisStore>;

  beforeEach(() => {
    sessionSpy = jasmine.createSpyObj('AiChatSessionService', [
      'startNewConversation',
      'sendMessage',
      'isStreaming',
      'abortActiveStream',
      'setLastScreenAnalysisTrace'
    ]);
    sessionSpy.isStreaming.and.returnValue(false);

    shellSpy = jasmine.createSpyObj('AiAssistantShellService', ['requestOpenPanel']);
    storeSpy = jasmine.createSpyObj('AiVolatileAnalysisStore', ['setPending']);

    TestBed.configureTestingModule({
      providers: [
        AiScreenAnalysisService,
        { provide: AiChatSessionService, useValue: sessionSpy },
        { provide: AiAssistantShellService, useValue: shellSpy },
        { provide: AiVolatileAnalysisStore, useValue: storeSpy }
      ]
    });

    service = TestBed.inject(AiScreenAnalysisService);
  });

  it('should create a fresh conversation and open the panel', () => {
    service.startAnalysis('stock', {});

    expect(sessionSpy.startNewConversation).toHaveBeenCalledTimes(1);
    expect(shellSpy.requestOpenPanel).toHaveBeenCalledTimes(1);
  });

  it('should send the analysis prompt with volatile context by default', () => {
    service.startAnalysis('accounting-ledger', { rows: [] });

    expect(sessionSpy.sendMessage).toHaveBeenCalledTimes(1);
    const input = sessionSpy.sendMessage.calls.mostRecent().args[0] as SendMessageInput;
    expect(input).toEqual(
      jasmine.objectContaining({
        displayText: "Analyse de l'écran : Grand livre",
        backendText: resolveScreenAnalysisBackendPrompt('accounting-ledger'),
        screenAnalysis: true,
        volatileContext: jasmine.objectContaining({
          screenId: 'accounting-ledger'
        })
      })
    );
    expect(input.volatileContext?.analysisSummary).toContain('accounting-ledger');
    expect(sessionSpy.setLastScreenAnalysisTrace).toHaveBeenCalled();
    expect(storeSpy.setPending).not.toHaveBeenCalled();
  });

  it('should store pending context when autoSend is false', () => {
    service.startAnalysis('stock', {}, { autoSend: false });

    expect(storeSpy.setPending).toHaveBeenCalledTimes(1);
    expect(sessionSpy.sendMessage).not.toHaveBeenCalled();
  });

  it('should abort an active stream before sending analysis', () => {
    sessionSpy.isStreaming.and.returnValue(true);

    service.startAnalysis('stock', {});

    expect(sessionSpy.abortActiveStream).toHaveBeenCalledTimes(1);
    expect(sessionSpy.sendMessage).toHaveBeenCalledTimes(1);
  });
});
