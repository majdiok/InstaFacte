import type { ChatAttachment, ChatAttachmentRequest } from '../models/ai-chat.models';
import type { AiDocumentExtractResponse } from '../services/ai-chat.service';

/**
 * Helpers purs des pièces jointes du chat, extraits sans changement de comportement de
 * `ChatInputComponent` (choix du format, invite par défaut, matérialisation de la réponse
 * d'extraction) et de `AiChatSessionService` (message enrichi envoyé au LLM, payload backend).
 *
 * Ils vivent ici pour être partagés par le composer de l'atelier Studio IA, qui envoie les mêmes
 * pièces jointes au même endpoint de chat, et pour être testables sans TestBed.
 */

/** Nombre maximal d'images base64 transmises pour une requête (budget partagé entre les pièces jointes). */
const VISION_IMAGE_BUDGET = 10;

/** Format déduit de l'extension du fichier ; `txt` par défaut (extension inconnue ou absente). */
export function inferAttachmentFormat(fileName: string): ChatAttachment['format'] {
  const ext = (fileName ?? '').toLowerCase().split('.').pop() ?? '';
  switch (ext) {
    case 'pdf': return 'pdf';
    case 'docx': return 'docx';
    case 'xlsx': case 'xlsm': return 'xlsx';
    case 'csv': return 'csv';
    case 'txt': return 'txt';
    case 'png': case 'jpg': case 'jpeg': case 'webp': case 'bmp': case 'tif': case 'tiff':
      return 'image';
    default: return 'txt';
  }
}

/** Invite utilisée quand l'utilisateur envoie des pièces jointes sans écrire de texte. */
export function buildDefaultPromptForAttachments(attachments: ChatAttachment[]): string {
  if (attachments.length === 1) {
    return `Analyse cette pièce jointe : ${attachments[0].fileName}`;
  }
  return `Analyse ces ${attachments.length} pièces jointes.`;
}

/**
 * Matérialise la réponse de `POST ai/document-extract` en pièce jointe d'interface : le serveur reste
 * la source de vérité, le fichier local ne sert que de repli (format, taille).
 */
export function toChatAttachment(res: AiDocumentExtractResponse, file: File, id: string): ChatAttachment {
  return {
    id,
    fileName: res.fileName,
    format: (res.format ?? inferAttachmentFormat(file.name)) as ChatAttachment['format'],
    sizeBytes: res.sizeBytes ?? file.size,
    pageCount: res.pageCount ?? 1,
    ocrApplied: res.ocrApplied ?? false,
    truncated: res.truncated ?? false,
    fullText: res.text ?? '',
    pages: (res.pages ?? []).map(p => ({
      pageIndex: p.pageIndex,
      text: p.text,
      imageBase64: p.imageBase64,
      width: p.width,
      height: p.height,
      ocrApplied: p.ocrApplied
    })),
    warnings: res.warnings ?? []
  };
}

/**
 * Construit le payload texte envoyé au LLM en intégrant le texte extrait
 * des pièces jointes (entre marqueurs). Le texte tapé par l'utilisateur
 * reste affiché tel quel dans la bulle ; on n'enrichit que ce qu'on envoie.
 */
export function composeBackendMessage(userText: string, attachments?: ChatAttachment[]): string {
  if (!attachments || attachments.length === 0) {
    return userText;
  }
  const blocks: string[] = [];
  for (const att of attachments) {
    if (!att.fullText) continue;
    const header = `[PIÈCE JOINTE : ${att.fileName}` +
      (att.pageCount > 1 ? ` — ${att.pageCount} pages` : '') +
      (att.ocrApplied ? ' — OCR appliqué' : '') +
      (att.truncated ? ' — texte tronqué' : '') +
      ']';
    blocks.push(`${header}\n${att.fullText}\n[FIN PIÈCE JOINTE]`);
  }
  if (blocks.length === 0) {
    return userText;
  }
  return userText ? `${userText}\n\n${blocks.join('\n\n')}` : blocks.join('\n\n');
}

/**
 * Construit la liste de ChatAttachmentRequest pour le backend.
 * - Pour les modèles vision : inclut les imageBase64 des pages (max 10 au total).
 * - Sinon : transmet seulement les métadonnées (le texte est déjà dans le message).
 */
export function buildAttachmentRequests(
  attachments: ChatAttachment[] | undefined,
  visionCapable: boolean
): ChatAttachmentRequest[] {
  if (!attachments || attachments.length === 0) return [];
  const requests: ChatAttachmentRequest[] = [];
  let imageBudget = VISION_IMAGE_BUDGET;
  for (const att of attachments) {
    let images: string[] | undefined;
    if (visionCapable && imageBudget > 0) {
      const pageImages = att.pages
        .map(p => p.imageBase64)
        .filter((b): b is string => !!b);
      if (pageImages.length > 0) {
        images = pageImages.slice(0, imageBudget);
        imageBudget -= images.length;
      }
    }
    requests.push({
      fileName: att.fileName,
      format: att.format,
      pageCount: att.pageCount,
      ocrApplied: att.ocrApplied,
      truncated: att.truncated,
      ...(images && images.length > 0 ? { imagesBase64: images } : {})
    });
  }
  return requests;
}
