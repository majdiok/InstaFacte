import {
  extractDashboardConfig,
  extractSuggestedPromptsFromContent,
  stripDashboardJsonFence,
  stripNonDashboardJsonFences,
  buildAssistantMarkdownForDisplay,
  closePartialFences,
  enhanceAssistantContentForDisplay,
  humanizeBusinessJsonFences,
  measureVisibleProseLength,
  resolveAssistantDisplayState,
  isDashboardConfig,
  humanizeBusinessJson,
  replaceSectionsOnlyFences,
  sanitizeInternalToolNamesForDisplay,
  stripDisallowedScriptsForDisplay,
  HUMANIZE_MAX_ROWS
} from './assistant-message-display';

describe('assistant-message-display', () => {
  describe('isDashboardConfig', () => {
    it('accepts valid shape', () => {
      expect(isDashboardConfig({ title: 'T', sections: [] })).toBe(true);
    });
    it('rejects missing title', () => {
      expect(isDashboardConfig({ sections: [] })).toBe(false);
    });
    it('rejects non-array sections', () => {
      expect(isDashboardConfig({ title: 'T', sections: {} })).toBe(false);
    });
  });

  describe('extractDashboardConfig', () => {
    it('parses valid dashboard JSON fence', () => {
      const content = 'Hello\n```json\n{"title":"T","sections":[]}\n```';
      const r = extractDashboardConfig(content);
      expect(r?.config.title).toBe('T');
      expect(r?.config.sections).toEqual([]);
      expect(r?.rawFence).toContain('```json');
    });

    it('returns null for invalid JSON', () => {
      expect(extractDashboardConfig('```json\nnot json\n```')).toBeNull();
    });

    it('returns null when JSON is not dashboard shape', () => {
      expect(extractDashboardConfig('```json\n{"foo":1}\n```')).toBeNull();
    });

    it('uses first fence only when multiple present', () => {
      const content =
        '```json\n{"title":"A","sections":[]}\n```\n\n```json\n{"title":"B","sections":[]}\n```';
      const r = extractDashboardConfig(content);
      expect(r?.config.title).toBe('A');
    });
  });

  describe('stripDashboardJsonFence', () => {
    it('removes first dashboard fence only from display', () => {
      const content = 'Résumé\n\n```json\n{"title":"D","sections":[]}\n```\n';
      expect(stripDashboardJsonFence(content).trim()).toBe('Résumé');
    });

    it('leaves content unchanged when no valid dashboard', () => {
      const content = 'Text only';
      expect(stripDashboardJsonFence(content)).toBe('Text only');
    });
  });

  describe('stripNonDashboardJsonFences', () => {
    it('strips tool-like JSON fence but keeps prose', () => {
      const toolJson = JSON.stringify([{ productName: 'x', quantity: 1 }]);
      const content = `\`\`\`json\n${toolJson}\n\`\`\`\n\nVoici le résumé.`;
      expect(stripNonDashboardJsonFences(content).trim()).toBe('Voici le résumé.');
    });

    it('keeps unknown object JSON fence', () => {
      const toolJson = JSON.stringify([{ a: 1 }]);
      const content = `\`\`\`json\n${toolJson}\n\`\`\`\n\nTexte`;
      expect(stripNonDashboardJsonFences(content)).toContain('```json');
      expect(stripNonDashboardJsonFences(content).trim()).toContain('Texte');
    });

    it('keeps dashboard-shaped JSON fence', () => {
      const dash = JSON.stringify({ title: 'T', sections: [] });
      const content = `Intro\n\`\`\`json\n${dash}\n\`\`\``;
      expect(stripNonDashboardJsonFences(content)).toContain('```json');
      expect(stripNonDashboardJsonFences(content)).toContain('"title"');
    });

    it('keeps invalid JSON fence (not a known tool payload)', () => {
      const content = 'A\n```json\nnot-json\n```\nB';
      expect(stripNonDashboardJsonFences(content).trim()).toBe('A\n```json\nnot-json\n```\nB');
    });
  });

  describe('humanizeBusinessJsonFences', () => {
    it('humanizes CA object', () => {
      const json = JSON.stringify({ ca: 1250.5, periode: '16/06/2026' });
      const content = `\`\`\`json\n${json}\n\`\`\``;
      const result = humanizeBusinessJsonFences(content);
      expect(result).toContain('TND');
      expect(result).toContain('16/06/2026');
    });
  });

  describe('sanitizeInternalToolNamesForDisplay', () => {
    it('replaces known tool names (with or without backticks) by French labels', () => {
      const content = 'Utilisez `forecast_product_demand` puis get_promotion_recommendations.';
      const out = sanitizeInternalToolNamesForDisplay(content);
      expect(out).not.toContain('forecast_product_demand');
      expect(out).not.toContain('get_promotion_recommendations');
      expect(out).toContain('Prévision de la demande produit');
      expect(out).toContain('Recommandations de promotion');
      expect(out).not.toContain('`');
    });

    it('prettifies unknown prefixed tokens (never raw snake_case)', () => {
      const out = sanitizeInternalToolNamesForDisplay('Essayez get_super_magic_report.');
      expect(out).not.toContain('get_super_magic_report');
      expect(out).toContain('Super magic report');
    });

    it('leaves code fences and normal prose untouched', () => {
      const fence = '```json\n{"tool":"get_sales_revenue"}\n```';
      const content = `Voir get_sales_revenue.\n${fence}\nLe code REF_2026 est inchangé.`;
      const out = sanitizeInternalToolNamesForDisplay(content);
      expect(out).toContain('{"tool":"get_sales_revenue"}');
      expect(out).toContain("Analyse du chiffre d'affaires");
      expect(out).toContain('REF_2026');
    });
  });

  describe('stripDisallowedScriptsForDisplay', () => {
    it('leaves French prose unchanged', () => {
      const content = "Ce mois-ci votre chiffre d'affaires s'élève à 1 250,500 TND (16/06/2026).";
      expect(stripDisallowedScriptsForDisplay(content)).toBe(content);
    });

    it('keeps French and drops Chinese paragraphs plus translator note', () => {
      const french =
        "Aucun collaborateur n'a été trouvé dans votre portefeuille actuel. " +
        "Cela pourrait signifier que tous les dossiers sont bien suivis par vos équipes, " +
        "ou qu'il y a une partie du portefeuille qui n'a pas pu être lue.";
      const content =
        french +
        '\n\n助手：未找到任何协作人员。\n\n' +
        '注意：以上翻译保持了原文的语气和内容，并已根据中文表达习惯进行了适当调整。';
      const out = stripDisallowedScriptsForDisplay(content);
      expect(out.trim()).toBe(french);
      expect(out).not.toContain('助手');
    });

    it('preserves Arabic names', () => {
      const content = 'Le client شركة النور a un solde de 200,000 TND.';
      expect(stripDisallowedScriptsForDisplay(content)).toBe(content);
    });

    it('preserves JSON fences even when they contain CJK', () => {
      const content =
        'Voici le tableau.\n```json\n{"title":"Trésorerie","note":"中文"}\n```\n助手：忽略。';
      const out = stripDisallowedScriptsForDisplay(content);
      expect(out).toContain('```json');
      expect(out).toContain('"note":"中文"');
      expect(out).toContain('Voici le tableau.');
      expect(out).not.toContain('助手');
    });
  });

  describe('buildAssistantMarkdownForDisplay CJK', () => {
    it('strips Chinese from persisted mixed replies', () => {
      const french = "Aucun collaborateur n'a été trouvé dans votre portefeuille actuel.";
      const md = buildAssistantMarkdownForDisplay(french + '\n\n助手：未找到任何协作人员。');
      expect(md).toContain(french);
      expect(md).not.toContain('助手');
    });
  });

  describe('replaceSectionsOnlyFences', () => {
    it('turns a title-less sections fence into readable KPI bullets', () => {
      const fence = JSON.stringify({
        sections: [
          { type: 'kpi_card', title: 'Solde Caisse Primaire', data: { value: 0, label: 'TND' } },
          { type: 'kpi_card', title: 'Solde Caisse Secondaire', data: { value: 6600.86, label: 'TND' } }
        ]
      });
      const content = `Indicateurs clés\n\`\`\`json\n${fence}\n\`\`\``;
      const out = replaceSectionsOnlyFences(content);
      expect(out).not.toContain('```');
      expect(out).toContain('- **Solde Caisse Primaire** :');
      expect(out).toContain('- **Solde Caisse Secondaire** :');
      expect(out).toContain('TND');
    });

    it('keeps a valid dashboard fence (title + sections) intact', () => {
      const fence = JSON.stringify({ title: 'Trésorerie', sections: [] });
      const content = `Texte\n\`\`\`json\n${fence}\n\`\`\``;
      expect(replaceSectionsOnlyFences(content)).toContain('```json');
    });

    it('keeps invalid JSON fences intact', () => {
      const content = 'Texte\n```json\n{"sections":[oops\n```';
      expect(replaceSectionsOnlyFences(content)).toContain('{"sections":[oops');
    });

    it('is applied by buildAssistantMarkdownForDisplay', () => {
      const fence = JSON.stringify({
        sections: [{ type: 'kpi_card', title: 'Solde', data: { value: 10, label: 'TND' } }]
      });
      const longProse = 'x'.repeat(100);
      const content = `${longProse}\n\`\`\`json\n${fence}\n\`\`\``;
      const out = buildAssistantMarkdownForDisplay(content);
      expect(out).toContain('- **Solde** :');
      expect(out).not.toContain('kpi_card');
    });

    it('humanizes a tool-arguments echo ({title, sections_json}) even with title', () => {
      const fence = JSON.stringify({
        title: 'Récapitulatif Trésorerie',
        sections_json: [
          { type: 'kpi_card', title: 'Solde Caisse Primaire', data: { value: 0, label: 'TND' } },
          { type: 'table', title: 'Flux de Trésorerie par Mode de Paiement' }
        ]
      });
      const content = `Tableau suggéré :\n\`\`\`json\n${fence}\n\`\`\``;
      const out = replaceSectionsOnlyFences(content);
      expect(out).not.toContain('sections_json');
      expect(out).not.toContain('```');
      expect(out).toContain('- **Solde Caisse Primaire** :');
      expect(out).toContain('- Flux de Trésorerie par Mode de Paiement');
    });

    it('handles sections_json provided as a JSON string', () => {
      const fence = JSON.stringify({
        title: 'Récap',
        sections_json: JSON.stringify([
          { type: 'kpi_card', title: 'Solde', data: { value: 10, label: 'TND' } }
        ])
      });
      const out = replaceSectionsOnlyFences(`x\n\`\`\`json\n${fence}\n\`\`\``);
      expect(out).toContain('- **Solde** :');
      expect(out).not.toContain('sections_json');
    });
  });

  describe('closePartialFences', () => {
    it('temporarily closes an open fence for stable streaming render', () => {
      const streaming = 'Voici :\n```json\n{"a":1,';
      expect(closePartialFences(streaming)).toBe(`${streaming}\n\`\`\``);
    });

    it('leaves balanced content unchanged', () => {
      const balanced = 'Avant\n```json\n{"a":1}\n```\nAprès';
      expect(closePartialFences(balanced)).toBe(balanced);
      expect(closePartialFences('sans fence')).toBe('sans fence');
    });
  });

  describe('humanizeBusinessJson — revenue envelope', () => {
    it('renders deterministic total then breakdown', () => {
      const envelope = JSON.stringify({
        totalRevenue: 2143.5,
        rowCount: 3,
        currency: 'TND',
        groupBy: 'Product',
        rows: [
          { groupKey: 'ordinateur portable', revenue: 1498, quantity: 1 },
          { groupKey: 'bureau', revenue: 476, quantity: 1 },
          { groupKey: 'lit', revenue: 169.5, quantity: 1 }
        ]
      });
      const result = humanizeBusinessJson(envelope);
      expect(result).toContain('CA total');
      expect(result).toContain('143,5'); // 2 143,500 (jamais 1 498 seul)
      expect(result).toContain('ordinateur portable');
      const bullets = (result ?? '').split('\n').filter((l) => l.startsWith('- ')).length;
      expect(bullets).toBe(3);
    });

    it('says no sales for an empty envelope', () => {
      const envelope = JSON.stringify({
        totalRevenue: 0,
        rowCount: 0,
        currency: 'TND',
        groupBy: 'Product',
        rows: []
      });
      expect(humanizeBusinessJson(envelope)).toContain('Aucune vente');
    });

    it('cites the period from the envelope (header and empty message)', () => {
      const withRows = JSON.stringify({
        totalRevenue: 100,
        rowCount: 1,
        currency: 'TND',
        groupBy: 'Client',
        period: { from: '2026-06-03', to: '2026-07-02' },
        rows: [{ groupKey: 'Client A', revenue: 100, quantity: 1 }]
      });
      expect(humanizeBusinessJson(withRows)).toContain('du 03/06/2026 au 02/07/2026');

      const empty = JSON.stringify({
        totalRevenue: 0,
        rowCount: 0,
        currency: 'TND',
        groupBy: 'Client',
        period: { from: '2026-07-01', to: '2026-07-02' },
        rows: []
      });
      expect(humanizeBusinessJson(empty)).toContain('Aucune vente sur la période du 01/07/2026 au 02/07/2026');
    });
  });

  describe('enhanceAssistantContentForDisplay', () => {
    it('adds humanized summary for preamble + JSON', () => {
      const json = JSON.stringify({ ca: 500, periode: 'today' });
      const content = `Pour\n\`\`\`json\n${json}\n\`\`\``;
      const enhanced = enhanceAssistantContentForDisplay(content);
      expect(enhanced).toContain('Pour');
      expect(enhanced).toContain('TND');
    });
  });

  describe('buildAssistantMarkdownForDisplay', () => {
    it('strips business tool fence then dashboard fence', () => {
      const dash = { title: 'D', sections: [] };
      const toolJson = JSON.stringify([{ productName: 'x', amount: 10 }]);
      const content = `\`\`\`json\n${toolJson}\n\`\`\`\n\nTexte\n\`\`\`json\n${JSON.stringify(dash)}\n\`\`\``;
      expect(buildAssistantMarkdownForDisplay(content).trim()).toBe('Texte');
    });

    it('humanizes short JSON-only reply', () => {
      const json = JSON.stringify({ ca: 100, periode: 'aujourd\'hui' });
      const content = `Pour\n\`\`\`json\n${json}\n\`\`\``;
      const result = buildAssistantMarkdownForDisplay(content);
      expect(result.length).toBeGreaterThan(10);
      expect(result).toContain('TND');
    });

    it('returns empty for whitespace-only content', () => {
      expect(buildAssistantMarkdownForDisplay('   ')).toBe('');
    });

    it('strips ft-meta fence from display', () => {
      const meta = '```ft-meta\n{"suggestedPrompts":["A","B"]}\n```';
      expect(buildAssistantMarkdownForDisplay(`Hello\n\n${meta}`).trim()).toBe('Hello');
    });
  });

  describe('measureVisibleProseLength', () => {
    it('ignores json fences', () => {
      expect(measureVisibleProseLength('Bonjour\n```json\n{"a":1}\n```')).toBe(7);
    });
  });

  describe('resolveAssistantDisplayState', () => {
    it('uses humanized markdown for short JSON-only reply', () => {
      const json = JSON.stringify({ ca: 100, periode: 'aujourd\'hui' });
      const content = `Pour\n\`\`\`json\n${json}\n\`\`\``;
      const state = resolveAssistantDisplayState(content);
      expect(state.displayFallbackText).toBe('');
      expect(state.renderedMarkdown.length).toBeGreaterThan(10);
      expect(state.renderedMarkdown).toContain('TND');
    });

    it('keeps stripped prose when long enough for display', () => {
      const prose = 'A'.repeat(100);
      const toolJson = JSON.stringify([{ productName: 'x', amount: 10 }]);
      const content = `${prose}\n\`\`\`json\n${toolJson}\n\`\`\``;
      const state = resolveAssistantDisplayState(content);
      expect(state.displayFallbackText).toBe('');
      expect(state.renderedMarkdown.trim()).toBe(prose);
    });

    it('humanizes preamble plus dashboard fence in content', () => {
      const dash = { title: 'CA', sections: [{ type: 'kpi', items: [{ label: 'CA', value: '100' }] }] };
      const json = JSON.stringify({ ca: 500, periode: 'today' });
      const content = `Pour\n\`\`\`json\n${json}\n\`\`\`\n\`\`\`json\n${JSON.stringify(dash)}\n\`\`\``;
      const result = buildAssistantMarkdownForDisplay(content);
      expect(result).toContain('TND');
      expect(result.length).toBeGreaterThan(10);
    });

    it('shows dashboard hint when markdown empty but dashboard parsed', () => {
      const toolJson = JSON.stringify([{ productName: 'x', amount: 10 }]);
      const content = `\`\`\`json\n${toolJson}\n\`\`\``;
      const state = resolveAssistantDisplayState(content, {
        hasParsedDashboard: true,
        smartJsonFallback: false
      });
      expect(state.renderedMarkdown).toBe('');
      expect(state.displayFallbackText).toContain('tableau de bord');
      // Le tableau de bord se rend AU-DESSUS du texte : le bouchon dit « ci-dessus ».
      expect(state.displayFallbackText).toContain('ci-dessus');
      expect(state.displayFallbackText).not.toContain('ci-dessous');
    });

    it('points to the dashboard rendered above when short prose accompanies it', () => {
      const state = resolveAssistantDisplayState('Voici.', {
        hasParsedDashboard: true,
        smartJsonFallback: false
      });
      expect(state.renderedMarkdown).toContain('ci-dessus');
      expect(state.renderedMarkdown).not.toContain('ci-dessous');
    });

    it('falls back to raw content instead of a blank body when stripping empties the markdown', () => {
      // Charge utile outil reconnue mais non humanisable (aucun champ nom) : strippée → markdown vide.
      const toolJson = JSON.stringify([{ ca: 5 }]);
      const content = `\`\`\`json\n${toolJson}\n\`\`\``;
      const state = resolveAssistantDisplayState(content);
      expect(state.renderedMarkdown).toBe('');
      // Plutôt qu'un corps totalement vide, on surface le brut.
      expect(state.displayFallbackText).toBe(content.trim());
    });
  });

  describe('extractSuggestedPromptsFromContent', () => {
    it('parses suggested prompts', () => {
      const c = 'x\n```ft-meta\n{"suggestedPrompts":["Un","Deux"]}\n```';
      expect(extractSuggestedPromptsFromContent(c)).toEqual(['Un', 'Deux']);
    });
  });

  describe('humanizeBusinessJson array cap', () => {
    const bulletCount = (text: string): number => (text.match(/^- /gm) ?? []).length;

    it('renders every row and no "autres" suffix below the cap', () => {
      const rows = [
        { name: 'Client A', ca: 100 },
        { name: 'Client B', ca: 80 },
        { name: 'Client C', ca: 60 }
      ];
      const out = humanizeBusinessJson(JSON.stringify(rows));
      expect(out).not.toBeNull();
      expect(bulletCount(out!)).toBe(3);
      expect(out!).not.toContain('autre(s) ligne(s)');
    });

    it('caps at HUMANIZE_MAX_ROWS with an accurate remainder suffix', () => {
      const total = HUMANIZE_MAX_ROWS + 10;
      const rows = Array.from({ length: total }, (_, i) => ({ name: `Client ${i + 1}`, ca: total - i }));
      const out = humanizeBusinessJson(JSON.stringify(rows));
      expect(out).not.toBeNull();
      expect(bulletCount(out!)).toBe(HUMANIZE_MAX_ROWS);
      expect(out!).toContain(`… et ${total - HUMANIZE_MAX_ROWS} autre(s) ligne(s).`);
    });

    it('uses a cap well above the previous hard-coded 5', () => {
      expect(HUMANIZE_MAX_ROWS).toBeGreaterThan(5);
    });

    it('returns the empty-period message for an empty array', () => {
      expect(humanizeBusinessJson('[]')).toBe('Aucune donnée pour cette période.');
    });
  });
});
