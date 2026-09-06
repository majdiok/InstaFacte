import {
  classifyStudioFailure,
  sanitizeStudioFailureMessage,
  looksTechnical,
  formatStudioPeriod,
  buildStudioDiagnostic
} from './studio-ai-failure.util';

describe('studio-ai-failure.util', () => {
  describe('classifyStudioFailure', () => {
    it('classe la phrase technique « Indiquez soit preset soit source » en clarification non réessayable', () => {
      const view = classifyStudioFailure({
        message: 'Indiquez soit `preset` (état prêt à l’emploi), soit `source` (table à analyser).',
        fromReportTool: true
      });
      expect(view.kind).toBe('clarification');
      expect(view.retryable).toBeFalse();
      expect(view.showSuggestions).toBeTrue();
      // Le message serveur technique ne fuite jamais tel quel.
      expect(view.message).not.toContain('preset');
      expect(view.message).not.toContain('`');
    });

    it('un code métier explicite prime sur toute heuristique', () => {
      const view = classifyStudioFailure({
        message: 'calcul impossible',
        code: 'AI_PROVIDER_UNAVAILABLE',
        fromReportTool: true
      });
      expect(view.kind).toBe('provider');
      expect(view.code).toBe('AI_PROVIDER_UNAVAILABLE');
    });

    it('un 403 est un refus sans relance ni suggestions', () => {
      const view = classifyStudioFailure({ message: 'Accès refusé.', httpStatus: 403 });
      expect(view.kind).toBe('forbidden');
      expect(view.retryable).toBeFalse();
      expect(view.showSuggestions).toBeFalse();
    });

    it('un 401 est une session expirée sans relance', () => {
      const view = classifyStudioFailure({ message: '', httpStatus: 401 });
      expect(view.kind).toBe('session');
      expect(view.retryable).toBeFalse();
    });

    it('un délai dépassé est réessayable et conserve les suggestions', () => {
      const view = classifyStudioFailure({
        message: 'Le calcul a dépassé le délai imparti.',
        fromReportTool: true
      });
      expect(view.kind).toBe('timeout');
      expect(view.retryable).toBeTrue();
      expect(view.showSuggestions).toBeTrue();
    });

    it('une panne fournisseur est réessayable une fois mais sans suggestions', () => {
      const view = classifyStudioFailure({ message: 'Le moteur IA est indisponible.', providerFailed: true });
      expect(view.kind).toBe('provider');
      expect(view.retryable).toBeTrue();
      expect(view.showSuggestions).toBeFalse();
    });

    it('un retryable:false serveur retire la relance même sur erreur transitoire', () => {
      const view = classifyStudioFailure({
        message: 'Le calcul a dépassé le délai imparti.',
        retryable: false
      });
      expect(view.kind).toBe('timeout');
      expect(view.retryable).toBeFalse();
    });

    it('une erreur générique est classée serveur et réessayable', () => {
      const view = classifyStudioFailure({ message: 'Une erreur est survenue.', fromReportTool: true });
      expect(view.kind).toBe('server');
      expect(view.retryable).toBeTrue();
    });
  });

  describe('sanitizeStudioFailureMessage', () => {
    it('réécrit la demande de source en phrase utilisateur', () => {
      const out = sanitizeStudioFailureMessage('Indiquez soit `preset` (état prêt à l’emploi), soit `source` (table à analyser).');
      expect(out).not.toContain('preset');
      expect(out).not.toContain('source');
      expect(out.length).toBeGreaterThan(0);
    });

    it('neutralise un message contenant un nom de colonne technique', () => {
      expect(sanitizeStudioFailureMessage('Erreur sur InvoiceLines_Total: colonne introuvable')).toBe('');
      expect(looksTechnical('InvoiceLines_Total: colonne introuvable')).toBeTrue();
    });

    it('conserve un libellé métier non technique', () => {
      const out = sanitizeStudioFailureMessage("Vous n'avez pas accès aux données de paie.");
      expect(out).toBe("Vous n'avez pas accès aux données de paie.");
    });
  });

  describe('helpers', () => {
    it('formate une période', () => {
      expect(formatStudioPeriod({ from: '2022-01-01', to: '2026-09-06' })).toBe('du 01/01/2022 au 06/09/2026');
    });

    it('construit un diagnostic sans contenu métier', () => {
      const diag = buildStudioDiagnostic(
        classifyStudioFailure({ message: 'Le calcul a dépassé le délai imparti.', fromReportTool: true }),
        'trace-123'
      );
      expect(diag).toContain('REPORT_TIMEOUT');
      expect(diag).toContain('trace-123');
    });
  });
});
