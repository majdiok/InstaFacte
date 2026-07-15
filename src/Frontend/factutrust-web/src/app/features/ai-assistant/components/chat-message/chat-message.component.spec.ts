import { SimpleChange } from '@angular/core';
import { Router } from '@angular/router';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ChatMessageComponent } from './chat-message.component';
import { AiChatSessionService } from '../../services/ai-chat-session.service';
import { MessageSelectionService } from '../../services/message-selection.service';
import { ChatMessage, MessageRole } from '../../models/ai-chat.models';

/**
 * Non-régression : la réponse assistant doit s'afficher même quand le contenu est posé pendant le
 * streaming (via `content_replace`) puis figé à la finalisation (`isStreaming` true→false sans
 * changement de `content`). Avant le correctif, `refreshAssistantDisplayContent()` n'était jamais
 * appelé dans ce cas → corps vide.
 */
describe('ChatMessageComponent (display refresh on stream end)', () => {
  let fixture: ComponentFixture<ChatMessageComponent>;
  let component: ChatMessageComponent;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ChatMessageComponent],
      providers: [
        { provide: AiChatSessionService, useValue: { activeConversationId: () => null, conversations: () => [] } },
        { provide: MessageSelectionService, useValue: { isSelectionMode: () => false, isSelected: () => false } },
        { provide: Router, useValue: {} }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(ChatMessageComponent);
    component = fixture.componentInstance;
  });

  function assistantMessage(content: string, isStreaming: boolean): ChatMessage {
    return {
      id: 'm1',
      role: MessageRole.Assistant,
      content,
      createdAt: new Date(),
      isStreaming
    };
  }

  it('renders content delivered via content_replace once streaming ends (unchanged content)', () => {
    const content =
      'Vos 5 meilleurs clients par chiffre d\'affaires sont listés ci-dessous avec leurs montants en TND.';

    // 1) Message en cours de streaming (contenu déjà posé via content_replace).
    const streaming = assistantMessage(content, true);
    component.message = streaming;
    component.ngOnChanges({ message: new SimpleChange(undefined, streaming, true) });
    // Pendant le streaming, on ne calcule pas l'affichage final.
    expect(component.renderedMarkdown).toBe('');

    // 2) Finalisation : isStreaming true→false, contenu INCHANGÉ.
    const finalized: ChatMessage = { ...streaming, isStreaming: false };
    component.message = finalized;
    component.ngOnChanges({ message: new SimpleChange(streaming, finalized, false) });

    // Le corps doit désormais être rempli (correctif streamingJustEnded).
    expect(component.renderedMarkdown.length).toBeGreaterThan(0);
    expect(component.renderedMarkdown).toContain('meilleurs clients');
  });

  it('still computes display content on first change for an already-finalized message', () => {
    const content = 'Réponse hydratée depuis une conversation persistée, suffisamment longue pour l\'affichage.';
    const finalized = assistantMessage(content, false);
    component.message = finalized;
    component.ngOnChanges({ message: new SimpleChange(undefined, finalized, true) });
    expect(component.renderedMarkdown.length).toBeGreaterThan(0);
  });
});
