import { SimpleChange } from '@angular/core';
import { Router } from '@angular/router';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ChatMessageComponent } from './chat-message.component';
import { AiChatSessionService } from '../../services/ai-chat-session.service';
import { MessageSelectionService } from '../../services/message-selection.service';
import { MarkdownService } from 'ngx-markdown';
import { of } from 'rxjs';
import {
  ChatMessage,
  ConfirmFirmReminderAction,
  MessageRole
} from '../../models/ai-chat.models';

/**
 * Stub ngx-markdown : le composant standalone `<markdown>` (issu de `MarkdownModule`, importé sans
 * `forRoot()`) injecte `MarkdownService` au rendu DOM. Les tests Lot 5 qui appellent
 * `fixture.detectChanges()` rendent le corps assistant (branche `<markdown>`) et déclenchent cette
 * injection. On fournit un stub minimal : `parse` renvoie le texte brut, `render` est un no-op et
 * `reload$` un observable clos. Les assertions Lot 5 portent sur le texte rendu par le template
 * (badge, boutons, cartes), jamais sur le `innerHTML` asynchrone de `<markdown>`.
 */
const markdownServiceStub = {
  parse: (md: string): string => md ?? '',
  render: (): void => undefined,
  reload$: of(0)
};

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

/**
 * Lot 5 — ancrage visible : badge ambre « réponse non vérifiée », carte repli honnête,
 * masquage du bouton Graphique en scope firm, et carte de confirmation de relance.
 */
describe('ChatMessageComponent (Lot 5 — grounding & reminder)', () => {
  let fixture: ComponentFixture<ChatMessageComponent>;
  let component: ChatMessageComponent;
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  let sessionMock: any;

  beforeEach(async () => {
    sessionMock = {
      activeConversationId: () => null,
      conversations: () => [],
      messages: () => [],
      sendMessage: jasmine.createSpy('sendMessage'),
      confirmFirmReminder: jasmine.createSpy('confirmFirmReminder'),
      dismissFirmReminder: jasmine.createSpy('dismissFirmReminder')
    };
    await TestBed.configureTestingModule({
      imports: [ChatMessageComponent],
      providers: [
        { provide: AiChatSessionService, useValue: sessionMock },
        { provide: MessageSelectionService, useValue: { isSelectionMode: () => false, isSelected: () => false } },
        { provide: Router, useValue: {} },
        { provide: MarkdownService, useValue: markdownServiceStub }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(ChatMessageComponent);
    component = fixture.componentInstance;
  });

  function assistantMessage(overrides: Partial<ChatMessage> = {}): ChatMessage {
    return {
      id: 'a1',
      role: MessageRole.Assistant,
      content: '',
      createdAt: new Date(),
      isStreaming: false,
      ...overrides
    };
  }

  function setMessage(msg: ChatMessage): void {
    component.message = msg;
    component.ngOnChanges({ message: new SimpleChange(undefined, msg, true) });
  }

  function reminderAction(nonce = 'n-1'): ConfirmFirmReminderAction {
    return {
      kind: 'confirm_firm_reminder',
      label: "Confirmer l'envoi de la relance",
      nonce,
      expiresAtUtc: new Date(Date.now() + 5 * 60_000).toISOString(),
      preview: {
        responsable: 'Sonia Ben Ali',
        dossier: 'SARL Marbrerie Lyonnaise',
        echeance: '2026-08-24',
        objet: '[Rappel] TVA CA3 — SARL Marbrerie Lyonnaise'
      }
    };
  }

  // ── Badge ambre « réponse non vérifiée » (5.1) ──────────────────────────────────────

  it('showUngroundedWarning false hors scope firm (warnWhenUngrounded=false → baseline inchangée)', () => {
    component.warnWhenUngrounded = false;
    setMessage(assistantMessage({ content: 'Réponse générale sans consultation des données du cabinet.' }));
    expect(component.showUngroundedWarning()).toBe(false);
  });

  it('showUngroundedWarning true en scope firm : 0 source et 0 toolCall abouti', () => {
    component.warnWhenUngrounded = true;
    setMessage(assistantMessage({ content: 'Réponse générale sans consultation des données du cabinet.', toolCalls: [] }));
    expect(component.showUngroundedWarning()).toBe(true);
  });

  it('showUngroundedWarning false quand des sources sont présentes (chips inchangées)', () => {
    component.warnWhenUngrounded = true;
    setMessage(
      assistantMessage({
        content: 'Réponse ancrée sur le portefeuille.',
        sources: [{ toolName: 'firm_overview', toolCallId: 'c1' }]
      })
    );
    expect(component.showUngroundedWarning()).toBe(false);
  });

  it('showUngroundedWarning false quand un toolCall est abouti (même sans sources)', () => {
    component.warnWhenUngrounded = true;
    setMessage(
      assistantMessage({
        content: 'Réponse après consultation.',
        toolCalls: [{ name: 'firm_overview', callId: 'c1', status: 'completed' }]
      })
    );
    expect(component.showUngroundedWarning()).toBe(false);
  });

  it('showUngroundedWarning false pendant le streaming', () => {
    component.warnWhenUngrounded = true;
    setMessage(assistantMessage({ content: 'génération en cours', isStreaming: true }));
    expect(component.showUngroundedWarning()).toBe(false);
  });

  it('rend le badge ambre « Réponse non vérifiée » + action de relance en scope firm 0-outil', () => {
    component.warnWhenUngrounded = true;
    setMessage(assistantMessage({ content: 'Réponse générale sans consultation des données du cabinet.' }));
    fixture.detectChanges();
    const text = (fixture.nativeElement as HTMLElement).textContent || '';
    expect(text).toContain('non vérifiée');
    expect(text).toContain('Relancer avec consultation des données');
  });

  it('ne rend ni badge ni carte repli en scope non-firm (baseline)', () => {
    component.warnWhenUngrounded = false;
    setMessage(assistantMessage({ content: 'Réponse générale sans consultation des données du cabinet.' }));
    fixture.detectChanges();
    const text = (fixture.nativeElement as HTMLElement).textContent || '';
    expect(text).not.toContain('non vérifiée');
    expect(text).not.toContain("n'ai pas pu consulter");
  });

  // ── Carte repli honnête (5.1) ───────────────────────────────────────────────────────

  it('isHonestFallback true pour le repli honnête (préfixe dédié) et désactive le badge ambre', () => {
    component.warnWhenUngrounded = true;
    setMessage(
      assistantMessage({
        content:
          "Je n'ai pas pu consulter les données du cabinet pour répondre de façon fiable. " +
          "Aucune lecture du portefeuille n'a abouti ce tour."
      })
    );
    expect(component.isHonestFallback).toBe(true);
    expect(component.showUngroundedWarning()).toBe(false);
  });

  it('isHonestFallback false pour une réponse ancrée normale', () => {
    setMessage(assistantMessage({ content: '3 dossiers présentent un risque d\'échéance ce mois-ci.' }));
    expect(component.isHonestFallback).toBe(false);
  });

  it('rend la carte repli honnête (titre + Réessayer la consultation) pour le repli en scope firm', () => {
    component.warnWhenUngrounded = true;
    setMessage(
      assistantMessage({
        content:
          "Je n'ai pas pu consulter les données du cabinet pour répondre de façon fiable. " +
          "Aucune lecture du portefeuille n'a abouti ce tour."
      })
    );
    fixture.detectChanges();
    const text = (fixture.nativeElement as HTMLElement).textContent || '';
    expect(text).toContain("Je n'ai pas pu consulter les données du cabinet");
    expect(text).toContain('Réessayer la consultation');
  });

  it('showAssistantActions masqué pour le repli honnête (pas de barre d\'export)', () => {
    component.warnWhenUngrounded = true;
    setMessage(
      assistantMessage({ content: "Je n'ai pas pu consulter les données du cabinet pour répondre." })
    );
    expect(component.showAssistantActions()).toBe(false);
  });

  // ── Relance de la question d'origine (5.1 action « Relancer ») ───────────────────────

  it('resendOriginalQuestion renvoie la question utilisateur précédente', () => {
    sessionMock.messages = () => [
      { id: 'u1', role: MessageRole.User, content: 'Quels dossiers en risque ce mois-ci ?', createdAt: new Date() },
      { id: 'a1', role: MessageRole.Assistant, content: 'réponse non ancrée', createdAt: new Date() }
    ];
    component.warnWhenUngrounded = true;
    setMessage(assistantMessage({ content: 'réponse non ancrée' }));
    component.resendOriginalQuestion();
    expect(sessionMock.sendMessage).toHaveBeenCalledWith('Quels dossiers en risque ce mois-ci ?');
  });

  it('resendOriginalQuestion ne fait rien sans question précédente', () => {
    sessionMock.messages = () => [assistantMessage({ id: 'a1', content: 'réponse non ancrée' })];
    component.warnWhenUngrounded = true;
    setMessage(assistantMessage({ content: 'réponse non ancrée' }));
    component.resendOriginalQuestion();
    expect(sessionMock.sendMessage).not.toHaveBeenCalled();
  });

  // ── Masquage du bouton Graphique (5.2) ──────────────────────────────────────────────

  it('masque le bouton Graphique quand hideChartFollowUp=true (scope firm)', () => {
    component.hideChartFollowUp = true;
    setMessage(assistantMessage({ content: 'Réponse avec assez de texte pour activer la barre d\'export.' }));
    fixture.detectChanges();
    const text = (fixture.nativeElement as HTMLElement).textContent || '';
    expect(text).not.toContain('Graphique');
  });

  it('affiche le bouton Graphique quand hideChartFollowUp=false', () => {
    component.hideChartFollowUp = false;
    setMessage(assistantMessage({ content: 'Réponse avec assez de texte pour activer la barre d\'export.' }));
    fixture.detectChanges();
    const text = (fixture.nativeElement as HTMLElement).textContent || '';
    expect(text).toContain('Graphique');
  });

  // ── Carte de confirmation de relance (5.3) ──────────────────────────────────────────

  it('rend la carte de confirmation (confirm_firm_reminder) avec aperçu + boutons', () => {
    setMessage(assistantMessage({ content: 'J\'ai préparé la relance, vérifiez l\'aperçu.', firmReminderAction: reminderAction() }));
    fixture.detectChanges();
    const text = (fixture.nativeElement as HTMLElement).textContent || '';
    expect(text).toContain('Confirmation requise');
    expect(text).toContain('Sonia Ben Ali');
    expect(text).toContain('SARL Marbrerie Lyonnaise');
    expect(text).toContain('Confirmer l\'envoi');
    expect(text).toContain('Annuler');
    expect(text).toContain('Expire dans');
  });

  it('rend l\'état « Relance envoyée » après confirmation', () => {
    setMessage(
      assistantMessage({
        content: 'J\'ai préparé la relance.',
        firmReminderAction: reminderAction(),
        firmReminderConfirmed: true
      })
    );
    fixture.detectChanges();
    const text = (fixture.nativeElement as HTMLElement).textContent || '';
    expect(text).toContain('Relance envoyée');
    expect(text).toContain('E-mail envoyé à Sonia Ben Ali');
    expect(text).not.toContain('Confirmer l\'envoi');
  });

  it('confirmReminder délègue au service avec l\'id du message et le nonce', () => {
    setMessage(assistantMessage({ content: 'relance prête', firmReminderAction: reminderAction('n-42') }));
    component.confirmReminder();
    expect(sessionMock.confirmFirmReminder).toHaveBeenCalledWith('a1', 'n-42');
  });

  it('confirmReminder est no-op si déjà confirmé (nonce à usage unique)', () => {
    setMessage(
      assistantMessage({
        content: 'relance prête',
        firmReminderAction: reminderAction(),
        firmReminderConfirmed: true
      })
    );
    component.confirmReminder();
    expect(sessionMock.confirmFirmReminder).not.toHaveBeenCalled();
  });

  it('cancelReminder délègue au dismiss sans déclencher d\'envoi', () => {
    setMessage(assistantMessage({ content: 'relance prête', firmReminderAction: reminderAction() }));
    component.cancelReminder();
    expect(sessionMock.dismissFirmReminder).toHaveBeenCalledWith('a1');
    expect(sessionMock.confirmFirmReminder).not.toHaveBeenCalled();
  });

  it('reminderExpiryLabel calcule les minutes restantes depuis expiresAtUtc', () => {
    setMessage(
      assistantMessage({
        content: 'relance prête',
        firmReminderAction: reminderAction()
      })
    );
    expect(component.reminderExpiryLabel).toMatch(/Expire dans [0-9]+ min/);
  });

  it('reminderExpiryLabel indique « Expirée » quand expiresAtUtc est dépassé', () => {
    const expired = reminderAction();
    expired.expiresAtUtc = new Date(Date.now() - 60_000).toISOString();
    setMessage(assistantMessage({ content: 'relance prête', firmReminderAction: expired }));
    expect(component.reminderExpiryLabel).toBe('Expirée');
  });
});
