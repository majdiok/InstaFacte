/**
 * Métadonnées des chapitres de la documentation utilisateur.
 * Utilisé pour la navigation et le chargement des fichiers Markdown.
 */
export interface DocChapter {
  id: string;
  title: string;
  file: string;
  icon: string;
  description?: string;
}

export const DOC_CHAPTERS: DocChapter[] = [
  { id: 'premiers-pas', title: 'Premiers pas', file: '01-premiers-pas.md', icon: 'fa-sign-in', description: 'Créer un compte, se connecter' },
  { id: 'tableau-de-bord', title: 'Tableau de bord', file: '02-tableau-de-bord.md', icon: 'fa-th-large', description: 'Vue d\'ensemble de votre activité' },
  { id: 'ventes', title: 'Ventes', file: '03-ventes.md', icon: 'fa-shopping-bag', description: 'Devis, bons de livraison, factures' },
  { id: 'achats', title: 'Achats', file: '04-achats.md', icon: 'fa-shopping-cart', description: 'Fournisseurs, bons de commande, factures fournisseurs' },
  { id: 'fiches', title: 'Fiches', file: '05-fiches.md', icon: 'fa-folder-open-o', description: 'Clients et produits' },
  { id: 'stock', title: 'Stock', file: '06-stock.md', icon: 'fa-cubes', description: 'Gestion du stock et inventaire' },
  { id: 'paiements', title: 'Trésorerie', file: '07-paiements.md', icon: 'fa-credit-card', description: 'Suivre et enregistrer les paiements' },
  { id: 'rapports', title: 'Rapports', file: '08-rapports.md', icon: 'fa-bar-chart-o', description: 'Statistiques et analyses' },
  { id: 'parametres', title: 'Paramètres', file: '09-parametres.md', icon: 'fa-cog', description: 'Profil, entreprise, abonnement' },
  { id: 'glossaire', title: 'Glossaire', file: 'glossaire.md', icon: 'fa-list-alt', description: 'Définitions des termes utilisés' }
];

export function getChapterById(id: string): DocChapter | undefined {
  return DOC_CHAPTERS.find(c => c.id === id);
}
