import { TestBed } from '@angular/core/testing';
import { PLATFORM_ID } from '@angular/core';
import { ChatSpeechTranscriptionService } from './chat-speech-transcription.service';

describe('ChatSpeechTranscriptionService', () => {
  let service: ChatSpeechTranscriptionService;
  let lastFakeRec: FakeRecognition | null = null;

  class FakeRecognition {
    continuous = false;
    interimResults = false;
    lang = '';
    onresult: ((e: FakeSpeechResultEvent) => void) | null = null;
    onerror: ((e: { error: string }) => void) | null = null;
    onend: (() => void) | null = null;
    start(): void {
      lastFakeRec = this;
    }
    stop(): void {
      this.onend?.();
    }
    abort(): void {}
  }

  interface FakeSpeechResultEvent {
    resultIndex: number;
    results: {
      length: number;
      [i: number]: { isFinal: boolean; 0: { transcript: string } };
    };
  }

  function setupBrowser(): void {
    lastFakeRec = null;
    (window as unknown as { webkitSpeechRecognition: typeof FakeRecognition }).webkitSpeechRecognition =
      FakeRecognition;
    (window as unknown as { SpeechRecognition: typeof FakeRecognition }).SpeechRecognition = FakeRecognition;
    // Reset avant de configurer : le service est providedIn:'root' (singleton). Sans reset, en ordre
    // de tests aléatoire, l'instance PLATFORM_ID='server' d'un autre test peut être réinjectée ici
    // (start() sort alors tôt, lastFakeRec reste null). Le reset garantit une instance 'browser' fraîche.
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      providers: [ChatSpeechTranscriptionService, { provide: PLATFORM_ID, useValue: 'browser' }]
    });
    service = TestBed.inject(ChatSpeechTranscriptionService);
  }

  afterEach(() => {
    delete (window as unknown as { webkitSpeechRecognition?: unknown }).webkitSpeechRecognition;
    delete (window as unknown as { SpeechRecognition?: unknown }).SpeechRecognition;
    lastFakeRec = null;
  });

  it('isSupported is true when webkitSpeechRecognition exists', () => {
    setupBrowser();
    expect(service.isSupported()).toBe(true);
  });

  it('isSupported is false on server platform', () => {
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      providers: [ChatSpeechTranscriptionService, { provide: PLATFORM_ID, useValue: 'server' }]
    });
    const srv = TestBed.inject(ChatSpeechTranscriptionService);
    expect(srv.isSupported()).toBe(false);
  });

  it('start sets listening and merges final + interim into display text', () => {
    setupBrowser();
    const displays: string[] = [];
    service.start({
      baseText: 'Hi ',
      onDisplayText: (t) => displays.push(t)
    });
    expect(service.listening()).toBe(true);
    expect(lastFakeRec).not.toBeNull();
    expect(lastFakeRec!.continuous).toBe(true);
    expect(lastFakeRec!.interimResults).toBe(true);
    expect(lastFakeRec!.lang).toBe('fr-FR');

    lastFakeRec!.onresult!({
      resultIndex: 0,
      results: {
        length: 1,
        0: { isFinal: true, 0: { transcript: 'world' } }
      }
    });
    expect(displays[displays.length - 1]).toBe('Hi world');

    lastFakeRec!.onresult!({
      resultIndex: 1,
      results: {
        length: 2,
        0: { isFinal: true, 0: { transcript: 'world' } },
        1: { isFinal: false, 0: { transcript: ' again' } }
      }
    });
    expect(displays[displays.length - 1]).toBe('Hi world again');
  });

  it('stop clears listening and detaches handlers', () => {
    setupBrowser();
    service.start({
      baseText: '',
      onDisplayText: () => {}
    });
    expect(service.listening()).toBe(true);
    service.stop();
    expect(service.listening()).toBe(false);
    expect(lastFakeRec!.onresult).toBeNull();
  });

  it('onerror not-allowed sets lastError and clears listening', () => {
    setupBrowser();
    service.start({
      baseText: '',
      onDisplayText: () => {}
    });
    lastFakeRec!.onerror!({ error: 'not-allowed' });
    expect(service.listening()).toBe(false);
    expect(service.lastError()).toContain('microphone');
  });
});
