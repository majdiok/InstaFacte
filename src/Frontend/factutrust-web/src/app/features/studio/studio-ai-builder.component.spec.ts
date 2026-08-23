import { stripStudioAssistantText } from './studio-ai-builder.component';

describe('stripStudioAssistantText', () => {
  it('keeps useful French prose', () => {
    const text = "D'accord, j'ajoute les champs Statut et Durée à la table Rendez-vous.";
    expect(stripStudioAssistantText(text)).toBe(text);
  });

  it('removes an unclosed ```json fence', () => {
    const text =
      'Voici le brouillon :\n```json\n{\n  "entity": {\n    "displayName": "Contrats Clients"\n  },\n  "fields": [\n    { "label": "Montant"';
    const clean = stripStudioAssistantText(text);
    expect(clean).not.toContain('```');
    expect(clean).not.toContain('"entity"');
    expect(clean).not.toContain('Montant');
    expect(clean).toContain('Voici le brouillon');
  });

  it('removes a bare entity/fields JSON object', () => {
    const text =
      'Je prépare la table des contrats.\n{"entity":{"displayName":"Contrats Clients"},"fields":[{"label":"Montant","type":"money"}]}';
    const clean = stripStudioAssistantText(text);
    expect(clean).toContain('Je prépare la table des contrats.');
    expect(clean).not.toContain('"entity"');
    expect(clean).not.toContain('Montant');
  });

  it('removes a truncated bare entity JSON tail', () => {
    const text =
      '{\n  "entity": {\n    "displayName": "Contrats Clients"\n  },\n  "fields": [\n    {\n      "label": "Montant"';
    const clean = stripStudioAssistantText(text);
    expect(clean).not.toContain('"entity"');
    expect(clean).not.toContain('Montant');
  });
});
