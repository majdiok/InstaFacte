import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute } from '@angular/router';
import { AiChatSessionService } from './services/ai-chat-session.service';
import { AuthService } from '@core/services/auth.service';
import { canUseAiAssistant } from './utils/ai-access.util';

@Component({
  selector: 'app-ai-assistant-page',
  standalone: true,
  imports: [CommonModule],
  // En-tête riche désormais porté par <app-chat-panel> (avatar robot + statut + actions).
  // Cette page ne rend plus d'en-tête dupliqué ; elle conserve le chargement de conversation.
  template: `<div class="ai-page"></div>`,
  styles: [`
    .ai-page {
      padding-bottom: 8px;
    }
  `]
})
export class AiAssistantPageComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly session = inject(AiChatSessionService);
  private readonly auth = inject(AuthService);

  ngOnInit(): void {
    // Route non gardée par un canActivate dédié (le panneau IA du layout l'est via
    // canUseAiAssistant()) : revérifier ici avant tout appel pour qu'une navigation directe
    // vers /ai-assistant?conversationId=... ne déclenche jamais de requête hors garde.
    if (!canUseAiAssistant(this.auth)) {
      return;
    }
    this.route.queryParamMap.subscribe(params => {
      const id = params.get('conversationId');
      if (id && id !== this.session.activeConversationId()) {
        this.session.loadConversation(id);
      }
    });
  }
}

