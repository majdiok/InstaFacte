import {
  StudioAppSpec,
  StudioSystemSpec,
  countSpec,
  isAppSpec,
  isSystemBuildResult,
  isSystemSpec,
  toSystemSpecView
} from './studio-ai.models';

describe('studio-ai.models', () => {
  const systemSpec: StudioSystemSpec = {
    system: { displayName: 'Congés' },
    entities: [
      {
        ref: 'employe',
        displayName: 'Employé',
        displayNamePlural: 'Employés',
        fields: [
          { key: 'nom', label: 'Nom', type: 'text', required: true, unique: true },
          { key: 'manager', label: 'Manager', type: 'relation', required: false, unique: false, relationTo: 'employe' }
        ],
        form: { sections: [{ fields: [{ field: 'nom' }] }] },
        report: { displayName: 'Effectif' }
      },
      {
        ref: 'demande',
        displayName: 'Demande',
        displayNamePlural: 'Demandes',
        fields: [
          { key: 'employe', label: 'Employé', type: 'relation', required: true, unique: false, relationTo: 'employe' }
        ]
      }
    ],
    seed: [
      { entityRef: 'employe', records: [{ nom: 'A' }, { nom: 'B' }] },
      { entityRef: 'demande', records: [] }
    ]
  };

  const appSpec: StudioAppSpec = {
    entity: { displayName: 'Contrat', displayNamePlural: 'Contrats', icon: 'fa-file', description: 'Contrats clients' },
    fields: [{ key: 'ref', label: 'Référence', type: 'text', required: true, unique: true }],
    report: { displayName: 'Contrats par mois' }
  };

  describe('isSystemSpec / isAppSpec', () => {
    it('recognizes a system spec', () => {
      expect(isSystemSpec(systemSpec)).toBeTrue();
      expect(isAppSpec(systemSpec)).toBeFalse();
    });

    it('recognizes an app spec', () => {
      expect(isAppSpec(appSpec)).toBeTrue();
      expect(isSystemSpec(appSpec)).toBeFalse();
    });

    it('rejects anything else', () => {
      for (const value of [null, undefined, 'x', 42, {}, { entities: 'nope' }, { entity: {} }]) {
        expect(isSystemSpec(value)).toBeFalse();
        expect(isAppSpec(value)).toBeFalse();
      }
    });
  });

  describe('toSystemSpecView', () => {
    it('returns a system spec unchanged', () => {
      expect(toSystemSpecView(systemSpec)).toBe(systemSpec);
    });

    it('wraps an app spec into a single-entity system spec', () => {
      const view = toSystemSpecView(appSpec);
      expect(view.system.displayName).toBe('Contrat');
      expect(view.system.description).toBe('Contrats clients');
      expect(view.entities.length).toBe(1);
      expect(view.entities[0].ref).toBe('entity');
      expect(view.entities[0].displayNamePlural).toBe('Contrats');
      expect(view.entities[0].fields).toBe(appSpec.fields);
      expect(view.entities[0].report).toBe(appSpec.report);
    });

    it('omits the report when the app spec has none', () => {
      const view = toSystemSpecView({ entity: { displayName: 'X', displayNamePlural: 'Xs' }, fields: [] });
      expect('report' in view.entities[0]).toBeFalse();
    });
  });

  describe('countSpec', () => {
    it('returns zeros for an empty spec', () => {
      expect(countSpec(null)).toEqual({ entities: 0, fields: 0, relations: 0, forms: 0, seedRecords: 0, reports: 0 });
      expect(countSpec(undefined)).toEqual({ entities: 0, fields: 0, relations: 0, forms: 0, seedRecords: 0, reports: 0 });
    });

    it('counts entities, fields, relations, forms, seed records and reports', () => {
      expect(countSpec(systemSpec)).toEqual({
        entities: 2,
        fields: 3,
        relations: 2,
        forms: 1,
        seedRecords: 2,
        reports: 1
      });
    });

    it('ignores empty form sections', () => {
      const spec: StudioSystemSpec = {
        system: { displayName: 'X' },
        entities: [{ ref: 'a', displayName: 'A', displayNamePlural: 'As', fields: [], form: { sections: [] } }]
      };
      expect(countSpec(spec).forms).toBe(0);
    });
  });

  describe('isSystemBuildResult', () => {
    it('detects a system result by its systemKey', () => {
      expect(isSystemBuildResult({
        success: true, systemKey: 'conges', systemUrl: '/studio/systems/conges', displayName: 'Congés',
        entityCount: 2, entities: [], warnings: [], message: 'ok'
      })).toBeTrue();
    });

    it('rejects an app result and nullish values', () => {
      expect(isSystemBuildResult({
        entityKey: 'contrats', displayName: 'Contrats', warnings: [], openUrl: '/studio/e/contrats', message: 'ok'
      })).toBeFalse();
      expect(isSystemBuildResult(null)).toBeFalse();
      expect(isSystemBuildResult(undefined)).toBeFalse();
    });
  });
});
