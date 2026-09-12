import { DOC_CHAPTERS, getChapterById } from './doc-chapters';

describe('DOC_CHAPTERS', () => {
  it('déclare le chapitre « Studio IA » pointant vers 13-studio-ia.md (cible du lien de l’atelier)', () => {
    const chapter = getChapterById('studio-ia');
    expect(chapter).toBeDefined();
    expect(chapter?.title).toBe('Studio IA');
    expect(chapter?.file).toBe('13-studio-ia.md');
    expect(chapter?.icon).toBe('fa-wand-magic-sparkles');
  });

  it('garde des identifiants et des fichiers uniques', () => {
    const ids = DOC_CHAPTERS.map(c => c.id);
    const files = DOC_CHAPTERS.map(c => c.file);
    expect(new Set(ids).size).toBe(ids.length);
    expect(new Set(files).size).toBe(files.length);
  });

  it('renvoie undefined pour un chapitre inconnu', () => {
    expect(getChapterById('inconnu')).toBeUndefined();
  });
});
