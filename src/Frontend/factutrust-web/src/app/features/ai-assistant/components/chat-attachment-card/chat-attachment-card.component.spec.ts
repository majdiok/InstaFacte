import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ChatAttachmentCardComponent } from './chat-attachment-card.component';
import type { ChatAttachment } from '../../models/ai-chat.models';

describe('ChatAttachmentCardComponent', () => {
  let fixture: ComponentFixture<ChatAttachmentCardComponent>;
  let cmp: ChatAttachmentCardComponent;

  const baseAttachment: ChatAttachment = {
    id: 'att-1',
    fileName: 'facture.pdf',
    format: 'pdf',
    sizeBytes: 24576,
    pageCount: 2,
    ocrApplied: false,
    truncated: false,
    fullText: 'Date facture : 09/05/2026\nTotal HT : 2,010.500 TND',
    pages: [],
    warnings: []
  };

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ChatAttachmentCardComponent]
    }).compileComponents();

    fixture = TestBed.createComponent(ChatAttachmentCardComponent);
    cmp = fixture.componentInstance;
  });

  it('renders the file name and the format label', () => {
    fixture.componentRef.setInput('attachment', baseAttachment);
    fixture.detectChanges();

    const el: HTMLElement = fixture.nativeElement;
    expect(el.textContent).toContain('facture.pdf');
    expect(el.textContent).toContain('PDF');
    expect(el.textContent).toContain('2 pages');
  });

  it('formats size in Ko / Mo', () => {
    fixture.componentRef.setInput('attachment', { ...baseAttachment, sizeBytes: 24576 });
    fixture.detectChanges();
    expect(cmp.sizeLabel).toBe('24 Ko');

    fixture.componentRef.setInput('attachment', { ...baseAttachment, sizeBytes: 1_572_864 });
    fixture.detectChanges();
    expect(cmp.sizeLabel).toBe('1.5 Mo');
  });

  it('shows OCR and Truncated badges only when flags are set', () => {
    fixture.componentRef.setInput('attachment', { ...baseAttachment, ocrApplied: true, truncated: true });
    fixture.detectChanges();
    const el: HTMLElement = fixture.nativeElement;
    expect(el.textContent).toContain('OCR');
    expect(el.textContent).toContain('Tronqué');
  });

  it('does NOT show extracted text by default; expands when toggled', () => {
    fixture.componentRef.setInput('attachment', baseAttachment);
    fixture.detectChanges();
    let pre = fixture.nativeElement.querySelector('pre.extract') as HTMLElement | null;
    expect(pre).toBeNull();

    cmp.toggleExpanded();
    fixture.detectChanges();
    pre = fixture.nativeElement.querySelector('pre.extract') as HTMLElement | null;
    expect(pre).not.toBeNull();
    expect(pre!.textContent).toContain('Date facture : 09/05/2026');
    expect(pre!.textContent).toContain('Total HT : 2,010.500 TND');
  });

  it('hides the remove button when not editable', () => {
    fixture.componentRef.setInput('attachment', baseAttachment);
    fixture.componentRef.setInput('editable', false);
    fixture.detectChanges();
    const removeBtn = fixture.nativeElement.querySelector('button[aria-label="Supprimer cette pièce jointe"]');
    expect(removeBtn).toBeNull();
  });

  it('emits removed with the attachment id when the remove button is clicked', () => {
    fixture.componentRef.setInput('attachment', baseAttachment);
    fixture.componentRef.setInput('editable', true);
    fixture.detectChanges();

    const received: { id: string | null } = { id: null };
    cmp.removed.subscribe(id => (received.id = id));

    const removeBtn = fixture.nativeElement.querySelector('button[aria-label="Supprimer cette pièce jointe"]') as HTMLButtonElement;
    expect(removeBtn).toBeTruthy();
    removeBtn.click();

    expect(received.id).toBe('att-1');
  });

  it('renders warnings when present', () => {
    fixture.componentRef.setInput('attachment', {
      ...baseAttachment,
      warnings: ['OCR appliqué sur au moins une page scannée — la précision peut être réduite.']
    });
    fixture.detectChanges();
    const el: HTMLElement = fixture.nativeElement;
    expect(el.textContent).toContain('OCR appliqué');
  });

  it('icon and label vary with format', () => {
    fixture.componentRef.setInput('attachment', { ...baseAttachment, format: 'image' });
    fixture.detectChanges();
    expect(cmp.icon).toBe('fa-file-image');
    expect(cmp.formatLabel).toBe('Image');

    fixture.componentRef.setInput('attachment', { ...baseAttachment, format: 'xlsx' });
    fixture.detectChanges();
    expect(cmp.icon).toBe('fa-file-excel');
    expect(cmp.formatLabel).toBe('Excel');

    fixture.componentRef.setInput('attachment', { ...baseAttachment, format: 'docx' });
    fixture.detectChanges();
    expect(cmp.icon).toBe('fa-file-word');
    expect(cmp.formatLabel).toBe('Word');
  });
});
