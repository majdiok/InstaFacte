import { isDevMode } from '@angular/core';
import { MessageRole } from '../models/ai-chat.models';

/**
 * Maps API JSON role values (string enums from ASP.NET) to the numeric MessageRole used in the UI.
 */
export function parseMessageRole(raw: unknown): MessageRole {
  if (typeof raw === 'number' && Number.isInteger(raw) && raw >= 0 && raw <= 3) {
    return raw as MessageRole;
  }

  if (typeof raw === 'string') {
    switch (raw.trim().toLowerCase()) {
      case 'system':
        return MessageRole.System;
      case 'user':
        return MessageRole.User;
      case 'assistant':
        return MessageRole.Assistant;
      case 'tool':
        return MessageRole.Tool;
      default:
        if (isDevMode()) {
          console.warn('[AiChat] Unknown message role from API:', raw);
        }
        return MessageRole.Assistant;
    }
  }

  if (isDevMode()) {
    console.warn('[AiChat] Invalid message role from API:', raw);
  }
  return MessageRole.Assistant;
}
