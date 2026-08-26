import {
  FIRM_CHECKLIST_IDS,
  COMPANY_CHECKLIST_IDS,
  OnboardingChecklistItemDef,
  ProductTourStepDef
} from './product-onboarding.models';

export const COMPANY_TOUR_STEPS: readonly ProductTourStepDef[] = [
  {
    id: 'welcome',
    title: 'Bienvenue',
    description:
      'Bienvenue. Voici les fonctions essentielles — 2 minutes. Vous pourrez relancer la visite depuis votre profil.',
    compact: true
  },
  {
    id: 'nav-dashboard',
    title: 'Tableau de bord',
    description: 'Votre page d’accueil : indicateurs, factures récentes et actions rapides.',
    selector: '[data-tour="nav-dashboard"]',
    compact: true
  },
  {
    id: 'nav-ventes',
    title: 'Ventes',
    description: 'Devis, commandes, bons de livraison, factures et retours — tout le cycle commercial.',
    selector: '[data-tour="nav-ventes"]',
    expandNavTourId: 'ventes',
    compact: true
  },
  {
    id: 'nav-achats',
    title: 'Achats',
    description: 'Fournisseurs, commandes d’achat et factures fournisseurs.',
    selector: '[data-tour="nav-achats"]',
    expandNavTourId: 'achats'
  },
  {
    id: 'nav-fiches',
    title: 'Fiches',
    description: 'Clients et articles : le référentiel de votre société (pas un CRM séparé).',
    selector: '[data-tour="nav-fiches"]',
    expandNavTourId: 'fiches',
    compact: true
  },
  {
    id: 'nav-stock',
    title: 'Stock',
    description: 'Mouvements, transferts et inventaires par entrepôt.',
    selector: '[data-tour="nav-stock"]',
    expandNavTourId: 'stock'
  },
  {
    id: 'nav-tresorerie',
    title: 'Trésorerie',
    description: 'Caisses, banques, paiements et prévision de trésorerie.',
    selector: '[data-tour="nav-tresorerie"]',
    expandNavTourId: 'tresorerie'
  },
  {
    id: 'nav-comptabilite',
    title: 'Comptabilité',
    description: 'Journaux, grand livre, déclarations et clôture — selon vos modules activés.',
    selector: '[data-tour="nav-comptabilite"]',
    expandNavTourId: 'comptabilite'
  },
  {
    id: 'nav-ai',
    title: 'Assistant IA',
    description: 'Posez une question métier : l’assistant s’appuie sur vos données, dans le périmètre autorisé.',
    selector: '[data-tour="nav-ai-assistant"]'
  },
  {
    id: 'header-search',
    title: 'Recherche',
    description: 'Retrouvez une facture, un client ou un menu. Raccourci : Ctrl+K.',
    selector: '[data-tour="header-search"]'
  },
  {
    id: 'header-quick-access',
    title: 'Accès rapide',
    description: 'Raccourcis pour créer un document ou ouvrir les pages les plus utilisées.',
    selector: '[data-tour="header-quick-access"]'
  },
  {
    id: 'header-settings',
    title: 'Paramètres',
    description: 'Société, numérotation, taxes, utilisateurs et abonnement.',
    selector: '[data-tour="header-settings"]',
    compact: true
  },
  {
    id: 'header-notifications',
    title: 'Notifications',
    description: 'Alertes (stock, échéances, messages) regroupées ici.',
    selector: '[data-tour="header-notifications"]'
  },
  {
    id: 'header-profile',
    title: 'Votre profil',
    description: 'Compte, déconnexion, et relance de cette visite guidée.',
    selector: '[data-tour="header-profile"]'
  },
  {
    id: 'header-warehouse',
    title: 'Entrepôt actif',
    description: 'Les stocks et documents sont filtrés sur cet entrepôt.',
    selector: '[data-tour="header-warehouse"]'
  },
  {
    id: 'dash-kpi',
    title: 'Indicateurs',
    description: 'Chiffre d’affaires, impayés et alertes. Un clic ouvre la liste filtrée.',
    selector: '[data-tour="dash-kpi"]',
    compact: true
  },
  {
    id: 'dash-quick-actions',
    title: 'Actions rapides',
    description: 'Créez une facture, un devis ou un bon de livraison en un clic.',
    selector: '[data-tour="dash-quick-actions"]'
  },
  {
    id: 'dash-empty-invoice',
    title: 'Première facture',
    description: 'Quand la liste est vide, ce raccourci crée votre première facture.',
    selector: '[data-tour="dash-empty-invoice"]'
  },
  {
    id: 'ai-fab',
    title: 'Assistant IA',
    description: 'Le bouton en bas à droite ouvre l’assistant dans un onglet de travail, sans quitter votre écran.',
    selector: '[data-tour="ai-fab"]'
  },
  {
    id: 'checklist',
    title: 'Premiers pas',
    description: 'Cette liste reste affichée jusqu’à ce que vous ayez configuré l’essentiel. Vous pouvez la masquer.',
    selector: '[data-tour="onboarding-checklist"]',
    compact: true
  }
];

export const FIRM_TOUR_STEPS: readonly ProductTourStepDef[] = [
  {
    id: 'welcome',
    title: 'Espace cabinet',
    description:
      'Bienvenue dans votre espace cabinet. Voici les fonctions essentielles — 2 minutes.',
    compact: true
  },
  {
    id: 'nav-dashboard',
    title: 'Tableau de bord',
    description: 'Alertes fiscales, dossiers actifs et invitations en attente.',
    selector: '[data-tour="nav-dashboard"]',
    compact: true
  },
  {
    id: 'nav-chef',
    title: 'Chef de mission',
    description: 'Assistant IA du cabinet : synthèse de portefeuille et aide à la mission.',
    selector: '[data-tour="nav-chef-de-mission"]'
  },
  {
    id: 'nav-clients',
    title: 'Dossiers clients',
    description: 'Ouvrez la comptabilité d’un dossier ou créez une société gérée.',
    selector: '[data-tour="nav-clients"]',
    compact: true
  },
  {
    id: 'nav-fiscal',
    title: 'Échéancier fiscal',
    description: 'Échéances TVA, IS et déclarations de vos dossiers.',
    selector: '[data-tour="nav-fiscal-schedule"]',
    compact: true
  },
  {
    id: 'nav-invitations',
    title: 'Invitations',
    description: 'Acceptez ou refusez les demandes d’affectation des sociétés.',
    selector: '[data-tour="nav-invitations"]'
  },
  {
    id: 'nav-cabinet',
    title: 'Mon cabinet',
    description: 'Collaborateurs, paramètres et votre profil cabinet.',
    selector: '[data-tour="nav-cabinet"]',
    expandNavTourId: 'cabinet',
    compact: true
  },
  {
    id: 'header-settings',
    title: 'Paramètres cabinet',
    description: 'Identité du cabinet, visibilité annuaire et préférences.',
    selector: '[data-tour="header-settings"]'
  },
  {
    id: 'header-profile',
    title: 'Votre profil',
    description: 'Compte personnel et relance de cette visite.',
    selector: '[data-tour="header-profile"]'
  },
  {
    id: 'firm-empty',
    title: 'Dossiers',
    description: 'Tant qu’aucun dossier n’est actif, acceptez une invitation ou créez un client géré.',
    selector: '[data-tour="firm-empty-clients"]'
  },
  {
    id: 'checklist',
    title: 'Premiers pas',
    description: 'Cochez les étapes restantes ici. La liste disparaît une fois terminée ou masquée.',
    selector: '[data-tour="onboarding-checklist"]',
    compact: true
  }
];

export const COMPANY_CHECKLIST_ITEMS: readonly OnboardingChecklistItemDef[] = [
  {
    id: COMPANY_CHECKLIST_IDS.companyProfile,
    label: 'Compléter la fiche entreprise',
    description: 'Logo, RIB ou registre de commerce pour vos documents.',
    route: '/settings/company',
    permission: 'settings:read'
  },
  {
    id: COMPANY_CHECKLIST_IDS.createClient,
    label: 'Créer un client',
    description: 'Au-delà du client passager, ajoutez votre premier client réel.',
    route: '/clients/new',
    permission: 'clients:create'
  },
  {
    id: COMPANY_CHECKLIST_IDS.createProduct,
    label: 'Créer un article',
    description: 'Un produit ou service à facturer.',
    route: '/products/new',
    permission: 'products:create'
  },
  {
    id: COMPANY_CHECKLIST_IDS.createInvoice,
    label: 'Créer une facture',
    description: 'Votre première facture de vente.',
    route: '/invoices/new',
    permission: 'invoices:create'
  },
  {
    id: COMPANY_CHECKLIST_IDS.numbering,
    label: 'Vérifier la numérotation',
    description: 'Préfixes et compteurs des documents (FAC, DEV…).',
    route: '/settings/numbering',
    permission: 'settings:read'
  },
  {
    id: COMPANY_CHECKLIST_IDS.inviteUser,
    label: 'Inviter un utilisateur',
    description: 'Ajoutez un collaborateur avec les bons modules.',
    route: '/settings/users',
    permission: 'users:create',
    adminOnly: true
  }
];

export const FIRM_CHECKLIST_ITEMS: readonly OnboardingChecklistItemDef[] = [
  {
    id: FIRM_CHECKLIST_IDS.firmSettings,
    label: 'Paramètres du cabinet',
    description: 'Complétez la fiche (description, inscription professionnelle).',
    route: '/firm/settings'
  },
  {
    id: FIRM_CHECKLIST_IDS.addCollaborator,
    label: 'Ajouter un collaborateur',
    description: 'Invitez un associé ou un collaborateur.',
    route: '/firm/collaborateurs',
    managerOnly: true
  },
  {
    id: FIRM_CHECKLIST_IDS.clientDossier,
    label: 'Créer ou accepter un dossier',
    description: 'Un dossier client actif pour travailler.',
    route: '/firm/clients'
  },
  {
    id: FIRM_CHECKLIST_IDS.fiscalSchedule,
    label: 'Consulter l’échéancier fiscal',
    description: 'Les prochaines échéances de vos dossiers.',
    route: '/firm/fiscal-schedule'
  },
  {
    id: FIRM_CHECKLIST_IDS.chefDeMission,
    label: 'Ouvrir Chef de mission',
    description: 'Découvrez l’assistant IA du cabinet.',
    route: '/firm/assistant',
    permission: 'firm:ai:chat'
  }
];

export function filterTourSteps(
  steps: readonly ProductTourStepDef[],
  options: { compact: boolean; elementExists: (selector: string) => boolean }
): ProductTourStepDef[] {
  return steps.filter(step => {
    if (options.compact && !step.compact) {
      return false;
    }
    if (!step.selector) {
      return true;
    }
    return options.elementExists(step.selector);
  });
}

export function mergeChecklistDone(
  manualIds: readonly string[] | undefined,
  autoIds: readonly string[] | undefined
): Set<string> {
  return new Set([...(manualIds ?? []), ...(autoIds ?? [])]);
}
