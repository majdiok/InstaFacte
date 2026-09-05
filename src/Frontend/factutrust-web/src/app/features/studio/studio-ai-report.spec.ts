import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { StudioService } from './studio.service';
import { ReportDataSourceKind, ReportSource } from './studio.models';
import { StudioAiBuilderComponent } from './studio-ai-builder.component';
import { ChatStreamEvent } from '@features/ai-assistant/models/ai-chat.models';
import { environment } from '@environments/environment';

describe('Studio — états sur les tables réelles', () => {
  describe('StudioService', () => {
    let service: StudioService;
    let http: HttpTestingController;

    beforeEach(() => {
      TestBed.configureTestingModule({
        providers: [StudioService, provideHttpClient(), provideHttpClientTesting()]
      });
      service = TestBed.inject(StudioService);
      http = TestBed.inject(HttpTestingController);
    });

    afterEach(() => http.verify());

    it('charge les champs d’une source à la demande', () => {
      service.getReportSourceFields('sql', 'InvoiceLines').subscribe();
      const req = http.expectOne(`${environment.apiUrl}/studio/reports/sources/sql/InvoiceLines/fields`);
      expect(req.request.method).toBe('GET');
      req.flush({ success: true, data: [] });
    });

    it('encode le nom de la source dans l’URL', () => {
      service.getReportSourceFields('sql', 'Invoice Lines').subscribe();
      const req = http.expectOne(`${environment.apiUrl}/studio/reports/sources/sql/Invoice%20Lines/fields`);
      expect(req.request.url).toContain('Invoice%20Lines');
      req.flush({ success: true, data: [] });
    });

    it('récupère les états prêts à l’emploi', () => {
      service.getReportPresets().subscribe();
      const req = http.expectOne(`${environment.apiUrl}/studio/reports/presets`);
      expect(req.request.method).toBe('GET');
      req.flush({ success: true, data: [] });
    });
  });

  describe('Correspondance des natures de source', () => {
    it('expose la nature SqlQuery attendue par le backend', () => {
      // Valeurs persistées : elles ne doivent jamais être réordonnées.
      expect(ReportDataSourceKind.CustomEntity).toBe(0);
      expect(ReportDataSourceKind.ExistingSource).toBe(1);
      expect(ReportDataSourceKind.SqlQuery).toBe(2);
    });

    it('accepte une source de nature sql sans champs préchargés', () => {
      const source: ReportSource = {
        kind: 'sql', ref: 'InvoiceLines', displayName: 'Lignes de facture de vente',
        fields: [], domain: 'Ventes'
      };
      expect(source.fields.length).toBe(0);
      expect(source.domain).toBe('Ventes');
    });
  });

  describe('StudioAiBuilderComponent — résultat d’état', () => {
    let component: StudioAiBuilderComponent;

    beforeEach(() => {
      TestBed.configureTestingModule({
        providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])]
      });
      component = TestBed.createComponent(StudioAiBuilderComponent).componentInstance;
    });

    const resultEvent = (payload: unknown): ChatStreamEvent =>
      ({ type: 'studio_report_result', content: JSON.stringify(payload) }) as ChatStreamEvent;

    it('affiche le tableau renvoyé par un état calculé', () => {
      component['handleEvent'](resultEvent({
        success: true,
        title: 'Ventes par produit',
        source: 'InvoiceLines',
        sourceLabel: 'Lignes de facture de vente',
        result: {
          columns: [
            { key: 'InvoiceLines_ProductName', label: 'Product Name', kind: 'dimension' },
            { key: 'sum_InvoiceLines_Total', label: 'Somme de Total', kind: 'measure' }
          ],
          rows: [{ InvoiceLines_ProductName: 'Ciment', sum_InvoiceLines_Total: 30000 }],
          totalRows: 1
        },
        warnings: [],
        message: '1 ligne(s) de résultat.'
      }));

      const report = component.reportResult();
      expect(report).not.toBeNull();
      expect(report!.title).toBe('Ventes par produit');
      expect(report!.result.rows.length).toBe(1);
      // Un état calculé n'est PAS un plan : aucune confirmation ne doit être demandée.
      expect(component.plan()).toBeNull();
      expect(component.state()).toBe('idle');
    });

    it('ignore un payload illisible sans casser le flux', () => {
      component['handleEvent']({ type: 'studio_report_result', content: '{oops' } as ChatStreamEvent);
      expect(component.reportResult()).toBeNull();
    });

    it('ignore un payload sans colonnes', () => {
      component['handleEvent'](resultEvent({ success: true, title: 'X', result: {} }));
      expect(component.reportResult()).toBeNull();
    });

    it('remet le résultat à zéro à chaque nouvelle demande', () => {
      component['reportResult'].set({
        success: true, title: 'Ancien', source: 'X', sourceLabel: 'X',
        result: { columns: [], rows: [], totalRows: 0 }, warnings: [], message: ''
      });
      component.prompt = 'Ventes par client';
      component.send();
      expect(component.reportResult()).toBeNull();
    });

    const failureEvent = (payload: unknown): ChatStreamEvent =>
      ({ type: 'studio_report_error', content: JSON.stringify(payload) }) as ChatStreamEvent;

    it('affiche une carte d’échec au lieu d’un message brut', () => {
      component['handleEvent'](failureEvent({
        message: 'Aucun des regroupements demandés n’existe sur « SupplierInvoiceLines » : Products_Name.',
        preset: 'achats_par_produit',
        title: 'Achats par produit',
        periodLabel: 'Année en cours',
        suggestions: [{ preset: 'achats_par_fournisseur', label: 'Achats par fournisseur', prompt: 'Montre-moi l’état « Achats par fournisseur »' }]
      }));

      const failure = component.reportFailure();
      expect(failure).not.toBeNull();
      expect(failure!.title).toBe('Achats par produit');
      expect(failure!.suggestions.length).toBe(1);
    });

    it('n’affiche pas l’erreur une seconde fois dans une bulle d’assistant', () => {
      component['handleEvent'](failureEvent({ message: 'Calcul impossible.', suggestions: [] }));
      component['handleEvent']({ type: 'content_replace', content: 'Calcul impossible.' } as ChatStreamEvent);
      component['handleEvent']({ type: 'done' } as ChatStreamEvent);

      expect(component.reportFailure()).not.toBeNull();
      expect(component.lines().some(l => l.role === 'assistant')).toBeFalse();
    });

    it('conserve les tableaux des demandes précédentes dans le fil', () => {
      const report = (title: string) => resultEvent({
        success: true, title, source: 'InvoiceLines', sourceLabel: 'Lignes de facture de vente',
        result: {
          columns: [{ key: 'k', label: 'K', kind: 'dimension' }],
          rows: [{ k: 'x' }], totalRows: 1
        },
        warnings: [], message: '1 ligne(s) de résultat.'
      });

      component.prompt = 'ventes par mois';
      component.send();
      component['handleEvent'](report('Ventes par mois'));

      component['state'].set('idle');
      component.prompt = 'ventes par produit';
      component.send();
      component['handleEvent'](report('Ventes par produit'));

      // Le fil garde les DEUX tableaux : une question suivante ne doit plus effacer le précédent.
      const reports = component.lines().filter(i => i.kind === 'report');
      expect(reports.length).toBe(2);
      expect(reports[0].report!.title).toBe('Ventes par mois');
      expect(reports[1].report!.title).toBe('Ventes par produit');
    });

    it('affiche des puces de reformulation quand la ventilation manque', () => {
      component['handleEvent']({
        type: 'suggested_prompts',
        suggestedPrompts: JSON.stringify(['Ventes par produit ce mois', 'Ventes par année'])
      } as ChatStreamEvent);

      expect(component.suggestions().length).toBe(2);
      const send = spyOn(component, 'send');
      component.usePromptSuggestion('Ventes par année');
      expect(send).toHaveBeenCalled();
      expect(component.prompt).toBe('Ventes par année');
    });

    it('remet la carte d’échec à zéro à chaque nouvelle demande', () => {
      component['handleEvent'](failureEvent({ message: 'Calcul impossible.', suggestions: [] }));
      component.prompt = 'Ventes par client';
      component.send();
      expect(component.reportFailure()).toBeNull();
    });

    it('Réessayer rejoue la dernière demande à l’identique', () => {
      component.prompt = 'Achats par article';
      component.send();
      expect(component.lastUserMessage).toBe('Achats par article');

      // Le flux est retombé au repos (échec ou fin) : c'est là que Réessayer est proposé.
      component['state'].set('idle');
      const send = spyOn(component, 'send');
      component.retry();
      expect(send).toHaveBeenCalled();
      expect(component.prompt).toBe('Achats par article');
    });

    it('une suggestion envoie la formulation proposée', () => {
      const send = spyOn(component, 'send');
      component.useSuggestion({ preset: 'ventes_par_client', label: 'Ventes par client', prompt: 'Montre-moi les ventes par client' });
      expect(send).toHaveBeenCalled();
      expect(component.prompt).toBe('Montre-moi les ventes par client');
    });

    it('Maj+Entrée n’envoie pas le message', () => {
      const send = spyOn(component, 'send');
      component.onComposerEnter({ shiftKey: true, preventDefault: () => undefined } as unknown as KeyboardEvent);
      expect(send).not.toHaveBeenCalled();

      component.onComposerEnter({ shiftKey: false, preventDefault: () => undefined } as unknown as KeyboardEvent);
      expect(send).toHaveBeenCalled();
    });

    it('l’enregistrement repasse par l’assistant, jamais par une écriture directe', () => {
      component['reportResult'].set({
        success: true, title: 'Ventes par produit', source: 'InvoiceLines',
        sourceLabel: 'Lignes de facture de vente',
        result: { columns: [], rows: [], totalRows: 0 }, warnings: [], message: ''
      });
      const send = spyOn(component, 'send');
      component.saveReportAsState();
      expect(send).toHaveBeenCalled();
      expect(component.prompt).toContain('Ventes par produit');
    });
  });
});
