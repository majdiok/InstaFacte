import { ComponentFixture, TestBed } from '@angular/core/testing';
import { signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { of, throwError } from 'rxjs';
import { ChatInputComponent } from './chat-input.component';
import { ChatSpeechTranscriptionService } from '../../services/chat-speech-transcription.service';
import { AiChatService, AiDocumentExtractResponse } from '../../services/ai-chat.service';

describe('ChatInputComponent', () => {
  let fixture: ComponentFixture<ChatInputComponent>;
  let speechStop: jasmine.Spy;
  let extractDocument: jasmine.Spy;

  beforeEach(async () => {
    const listening = signal(false);
    const lastError = signal<string | null>(null);
    speechStop = jasmine.createSpy('stop');
    extractDocument = jasmine.createSpy('extractDocument');

    const speechMock: Pick<
      ChatSpeechTranscriptionService,
      'isSupported' | 'listening' | 'lastError' | 'stop' | 'start'
    > = {
      isSupported: () => true,
      listening,
      lastError,
      stop: speechStop,
      start: jasmine.createSpy('start')
    };

    await TestBed.configureTestingModule({
      imports: [ChatInputComponent, FormsModule],
      providers: [
        { provide: ChatSpeechTranscriptionService, useValue: speechMock },
        { provide: AiChatService, useValue: { extractDocument } }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(ChatInputComponent);
    fixture.detectChanges();
  });

  it('calls speech.stop when disabled becomes true', () => {
    speechStop.calls.reset();
    fixture.componentRef.setInput('disabled', true);
    fixture.detectChanges();
    expect(speechStop).toHaveBeenCalled();
  });

  it('calls speech.stop on destroy', () => {
    speechStop.calls.reset();
    fixture.destroy();
    expect(speechStop).toHaveBeenCalled();
  });

  it('stores extracted documents as pendingAttachments (no longer injects text into the textarea)', () => {
    const cmp = fixture.componentInstance;
    const fakeResponse: AiDocumentExtractResponse = {
      text: 'Date facture : 09/05/2026',
      truncated: false,
      fileName: 'facture.pdf',
      format: 'pdf',
      ocrApplied: false,
      pageCount: 1,
      sizeBytes: 1234,
      pages: [{ pageIndex: 0, text: 'Date facture : 09/05/2026', ocrApplied: false }],
      warnings: []
    };
    extractDocument.and.returnValue(of(fakeResponse));

    const file = new File(['dummy'], 'facture.pdf', { type: 'application/pdf' });
    const fakeEvent = { target: { files: [file], value: '' } } as unknown as Event;
    cmp.onFileSelected(fakeEvent);

    expect(extractDocument).toHaveBeenCalledTimes(1);
    expect(cmp.pendingAttachments().length).toBe(1);
    expect(cmp.pendingAttachments()[0].fileName).toBe('facture.pdf');
    expect(cmp.pendingAttachments()[0].fullText).toContain('Date facture');
    // Le texte n'est PAS injecté dans le textarea (objet de la refonte)
    expect(cmp.text).toBe('');
  });

  it('removeAttachment removes the matching attachment from the pending list', () => {
    const cmp = fixture.componentInstance;
    extractDocument.and.returnValue(of({
      text: 'x', truncated: false, fileName: 'a.pdf',
      format: 'pdf', pageCount: 1, sizeBytes: 0, ocrApplied: false, pages: [], warnings: []
    } as AiDocumentExtractResponse));

    cmp.onFileSelected({ target: { files: [new File([''], 'a.pdf')], value: '' } } as unknown as Event);
    cmp.onFileSelected({ target: { files: [new File([''], 'b.pdf')], value: '' } } as unknown as Event);
    expect(cmp.pendingAttachments().length).toBe(2);

    const firstId = cmp.pendingAttachments()[0].id;
    cmp.removeAttachment(firstId);

    expect(cmp.pendingAttachments().length).toBe(1);
    expect(cmp.pendingAttachments()[0].id).not.toBe(firstId);
  });

  it('sets extractError when extraction fails', () => {
    const cmp = fixture.componentInstance;
    extractDocument.and.returnValue(throwError(() => new Error('boom')));

    const file = new File(['dummy'], 'broken.pdf', { type: 'application/pdf' });
    cmp.onFileSelected({ target: { files: [file], value: '' } } as unknown as Event);

    expect(cmp.extractError()).toBeTruthy();
    expect(cmp.pendingAttachments().length).toBe(0);
  });

  it('send emits messageSent with text and attachments together, then clears them', () => {
    const cmp = fixture.componentInstance;
    extractDocument.and.returnValue(of({
      text: 'extrait', truncated: false, fileName: 'f.pdf',
      format: 'pdf', pageCount: 1, sizeBytes: 0, ocrApplied: false, pages: [], warnings: []
    } as AiDocumentExtractResponse));
    cmp.onFileSelected({ target: { files: [new File([''], 'f.pdf')], value: '' } } as unknown as Event);

    cmp.text = 'Analyse cette facture';
    let received: any = null;
    cmp.messageSent.subscribe(p => received = p);

    cmp.send();

    expect(received).toEqual(jasmine.objectContaining({ text: 'Analyse cette facture' }));
    expect(received.attachments.length).toBe(1);
    expect(cmp.text).toBe('');
    expect(cmp.pendingAttachments().length).toBe(0);
  });

  it('send is enabled when only an attachment is present (no typed text)', () => {
    const cmp = fixture.componentInstance;
    extractDocument.and.returnValue(of({
      text: 'extrait', truncated: false, fileName: 'f.pdf',
      format: 'pdf', pageCount: 1, sizeBytes: 0, ocrApplied: false, pages: [], warnings: []
    } as AiDocumentExtractResponse));
    cmp.onFileSelected({ target: { files: [new File([''], 'f.pdf')], value: '' } } as unknown as Event);

    cmp.text = '';
    let received: any = null;
    cmp.messageSent.subscribe(p => received = p);

    cmp.send();

    expect(received).toBeTruthy();
    expect(received.text).toContain('f.pdf');
  });
});
