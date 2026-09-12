import { ChatAttachment } from '../models/ai-chat.models';
import { AiDocumentExtractResponse } from '../services/ai-chat.service';
import {
  buildAttachmentRequests,
  buildDefaultPromptForAttachments,
  composeBackendMessage,
  inferAttachmentFormat,
  toChatAttachment
} from './chat-attachment-payload.util';

/**
 * Tests « golden » : ils figent le comportement d'origine (ChatInputComponent / AiChatSessionService)
 * avant l'extraction dans ce util. Toute divergence de format d'invite, de marqueur de pièce jointe
 * ou de budget d'images casserait ces tests.
 */
describe('chat-attachment-payload.util', () => {
  function attachment(over: Partial<ChatAttachment> = {}): ChatAttachment {
    return {
      id: 'a1',
      fileName: 'facture.pdf',
      format: 'pdf',
      sizeBytes: 1024,
      pageCount: 1,
      ocrApplied: false,
      truncated: false,
      fullText: 'Texte extrait',
      pages: [],
      warnings: [],
      ...over
    };
  }

  describe('inferAttachmentFormat', () => {
    it('maps known extensions', () => {
      expect(inferAttachmentFormat('a.pdf')).toBe('pdf');
      expect(inferAttachmentFormat('a.DOCX')).toBe('docx');
      expect(inferAttachmentFormat('a.xlsx')).toBe('xlsx');
      expect(inferAttachmentFormat('a.xlsm')).toBe('xlsx');
      expect(inferAttachmentFormat('a.csv')).toBe('csv');
      expect(inferAttachmentFormat('a.txt')).toBe('txt');
    });

    it('maps image extensions to image', () => {
      for (const name of ['a.png', 'a.jpg', 'a.jpeg', 'a.webp', 'a.bmp', 'a.tif', 'a.TIFF']) {
        expect(inferAttachmentFormat(name)).toBe('image');
      }
    });

    it('falls back to txt for unknown or missing extensions', () => {
      expect(inferAttachmentFormat('archive.zip')).toBe('txt');
      expect(inferAttachmentFormat('sans-extension')).toBe('txt');
      expect(inferAttachmentFormat('')).toBe('txt');
    });
  });

  describe('buildDefaultPromptForAttachments', () => {
    it('names the single attachment', () => {
      expect(buildDefaultPromptForAttachments([attachment()])).toBe('Analyse cette pièce jointe : facture.pdf');
    });

    it('counts multiple attachments', () => {
      expect(buildDefaultPromptForAttachments([attachment(), attachment({ id: 'a2' })]))
        .toBe('Analyse ces 2 pièces jointes.');
    });
  });

  describe('toChatAttachment', () => {
    const file = new File(['x'], 'scan.png', { type: 'image/png' });

    it('prefers the server values and maps pages', () => {
      const res: AiDocumentExtractResponse = {
        text: 'contenu',
        truncated: true,
        fileName: 'scan.png',
        format: 'image',
        ocrApplied: true,
        pageCount: 2,
        sizeBytes: 4096,
        pages: [{ pageIndex: 0, text: 'p0', imageBase64: 'AAA', width: 10, height: 20, ocrApplied: true }],
        warnings: ['w']
      };

      const att = toChatAttachment(res, file, 'id-1');

      expect(att).toEqual({
        id: 'id-1',
        fileName: 'scan.png',
        format: 'image',
        sizeBytes: 4096,
        pageCount: 2,
        ocrApplied: true,
        truncated: true,
        fullText: 'contenu',
        pages: [{ pageIndex: 0, text: 'p0', imageBase64: 'AAA', width: 10, height: 20, ocrApplied: true }],
        warnings: ['w']
      });
    });

    it('falls back to the local file when the server omits the extended fields', () => {
      const res = { text: 'contenu', truncated: false, fileName: 'scan.png' } as AiDocumentExtractResponse;

      const att = toChatAttachment(res, file, 'id-2');

      expect(att.format).toBe('image');
      expect(att.sizeBytes).toBe(file.size);
      expect(att.pageCount).toBe(1);
      expect(att.ocrApplied).toBeFalse();
      expect(att.pages).toEqual([]);
      expect(att.warnings).toEqual([]);
    });
  });

  describe('composeBackendMessage', () => {
    it('returns the text unchanged without attachments', () => {
      expect(composeBackendMessage('Bonjour')).toBe('Bonjour');
      expect(composeBackendMessage('Bonjour', [])).toBe('Bonjour');
    });

    it('appends one block per attachment with its markers', () => {
      const out = composeBackendMessage('Analyse', [attachment({ fullText: 'ABC' })]);
      expect(out).toBe('Analyse\n\n[PIÈCE JOINTE : facture.pdf]\nABC\n[FIN PIÈCE JOINTE]');
    });

    it('adds pages / OCR / truncation details to the header', () => {
      const out = composeBackendMessage('', [attachment({ pageCount: 3, ocrApplied: true, truncated: true, fullText: 'ABC' })]);
      expect(out).toBe('[PIÈCE JOINTE : facture.pdf — 3 pages — OCR appliqué — texte tronqué]\nABC\n[FIN PIÈCE JOINTE]');
    });

    it('ignores attachments without extracted text', () => {
      expect(composeBackendMessage('Analyse', [attachment({ fullText: '' })])).toBe('Analyse');
    });
  });

  describe('buildAttachmentRequests', () => {
    it('returns an empty list without attachments', () => {
      expect(buildAttachmentRequests(undefined, true)).toEqual([]);
      expect(buildAttachmentRequests([], true)).toEqual([]);
    });

    it('sends metadata only when the model has no vision', () => {
      const att = attachment({ pages: [{ pageIndex: 0, text: 'p', imageBase64: 'AAA', ocrApplied: false }] });
      expect(buildAttachmentRequests([att], false)).toEqual([
        { fileName: 'facture.pdf', format: 'pdf', pageCount: 1, ocrApplied: false, truncated: false }
      ]);
    });

    it('includes page images for a vision model', () => {
      const att = attachment({
        pages: [
          { pageIndex: 0, text: 'p0', imageBase64: 'AAA', ocrApplied: false },
          { pageIndex: 1, text: 'p1', ocrApplied: false }
        ]
      });
      expect(buildAttachmentRequests([att], true)[0].imagesBase64).toEqual(['AAA']);
    });

    it('caps the total number of images at 10 across attachments', () => {
      const pages = (n: number, prefix: string) =>
        Array.from({ length: n }, (_, i) => ({ pageIndex: i, text: '', imageBase64: `${prefix}${i}`, ocrApplied: false }));
      const first = attachment({ id: 'a1', pages: pages(8, 'a') });
      const second = attachment({ id: 'a2', pages: pages(5, 'b') });
      const third = attachment({ id: 'a3', pages: pages(2, 'c') });

      const requests = buildAttachmentRequests([first, second, third], true);

      expect(requests[0].imagesBase64?.length).toBe(8);
      expect(requests[1].imagesBase64?.length).toBe(2);
      expect(requests[2].imagesBase64).toBeUndefined();
    });
  });
});
