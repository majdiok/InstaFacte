import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute } from '@angular/router';
import { AiChatSessionService } from './services/ai-chat-session.service';

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

  ngOnInit(): void {
    this.route.queryParamMap.subscribe(params => {
      const id = params.get('conversationId');
      if (id && id !== this.session.activeConversationId()) {
        this.session.loadConversation(id);
      }
    });
  }
}
