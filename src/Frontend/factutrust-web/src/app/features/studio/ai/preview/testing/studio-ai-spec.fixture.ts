import { StudioSystemSpec } from '../../studio-ai.models';

/**
 * Spec canonique de référence pour les specs de l'aperçu (forme réellement émise par le backend P0).
 *
 * Contenu : 2 entités, une relation entre entités (`demandes.employe_id` → `employes`), une relation
 * ERP (`demandes.client_id` → `clients`), un formulaire à 2 sections, des données de référence et un
 * rapport. Chaque appel renvoie une copie fraîche : les tests peuvent muter le résultat sans se
 * contaminer entre eux.
 */
export function studioAiSpecFixture(): StudioSystemSpec {
  return {
    system: {
      displayName: 'Gestion des congés',
      icon: 'fa-solid fa-umbrella-beach',
      description: 'Suivi des demandes de congés et de leurs validations.',
      onboarding: ['Créer les types de congés', 'Saisir une première demande']
    },
    entities: [
      {
        ref: 'employes',
        displayName: 'Employé',
        displayNamePlural: 'Employés',
        icon: 'fa-solid fa-user',
        description: 'Personnel concerné par les congés.',
        fields: [
          { key: 'matricule', label: 'Matricule', type: 'text', required: true, unique: true },
          { key: 'nom', label: 'Nom', type: 'text', required: true, unique: false },
          {
            key: 'statut',
            label: 'Statut',
            type: 'select',
            required: false,
            unique: false,
            options: [
              { value: 'actif', label: 'Actif' },
              { value: 'inactif', label: 'Inactif' }
            ]
          }
        ]
      },
      {
        ref: 'demandes',
        displayName: 'Demande de congé',
        displayNamePlural: 'Demandes de congés',
        icon: 'fa-solid fa-calendar-days',
        fields: [
          { key: 'employe_id', label: 'Employé', type: 'relation', required: true, unique: false, relationTo: 'employes' },
          { key: 'client_id', label: 'Client concerné', type: 'relation', required: false, unique: false, relationTo: 'clients' },
          { key: 'date_debut', label: 'Date de début', type: 'date', required: true, unique: false },
          { key: 'nb_jours', label: 'Nombre de jours', type: 'number', required: true, unique: false },
          { key: 'montant', label: 'Indemnité', type: 'money', required: false, unique: false, config: { currency: 'EUR' } }
        ],
        form: {
          sections: [
            {
              title: 'Demandeur',
              fields: [
                { field: 'employe_id', width: 'half' },
                { field: 'client_id', width: 'half' }
              ]
            },
            {
              title: 'Période',
              fields: [
                { field: 'date_debut', width: 'half' },
                { field: 'nb_jours', width: 'half' },
                { field: 'montant', width: 'full' }
              ]
            }
          ]
        },
        report: {
          displayName: 'État des congés',
          groupBy: ['employe_id'],
          measures: [{ fn: 'sum', field: 'nb_jours' }, { fn: 'count' }],
          columns: ['employe_id', 'date_debut', 'nb_jours'],
          filters: [{ field: 'nb_jours', op: 'gte', value: 1 }],
          sort: [{ field: 'date_debut', dir: 'desc' }]
        }
      }
    ],
    seed: [
      {
        entityRef: 'employes',
        records: [
          { matricule: 'E-001', nom: 'Dupont', statut: 'actif' },
          { matricule: 'E-002', nom: 'Martin', statut: 'inactif' }
        ]
      }
    ]
  };
}
