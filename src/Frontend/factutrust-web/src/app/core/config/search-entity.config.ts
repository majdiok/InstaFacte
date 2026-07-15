export type SearchEntityType =
  | 'invoice'
  | 'quote'
  | 'deliverynote'
  | 'client'
  | 'product'
  | 'supplier';

export interface SearchEntityConfig {
  type: SearchEntityType;
  label: string;
  labelPlural: string;
  icon: string;
  listRoute: string;
}

export const SEARCH_ENTITY_CONFIG: Record<SearchEntityType, SearchEntityConfig> = {
  invoice: { type: 'invoice', label: 'Facture', labelPlural: 'Factures', icon: 'fa-file-invoice', listRoute: '/invoices' },
  quote: { type: 'quote', label: 'Devis', labelPlural: 'Devis', icon: 'fa-file-lines', listRoute: '/quotes' },
  deliverynote: { type: 'deliverynote', label: 'Bon de livraison', labelPlural: 'Bons de livraison', icon: 'fa-truck', listRoute: '/delivery-notes' },
  client: { type: 'client', label: 'Client', labelPlural: 'Clients', icon: 'fa-users', listRoute: '/clients' },
  product: { type: 'product', label: 'Produit', labelPlural: 'Produits', icon: 'fa-cube', listRoute: '/products' },
  supplier: { type: 'supplier', label: 'Fournisseur', labelPlural: 'Fournisseurs', icon: 'fa-building', listRoute: '/suppliers' }
};

export function getEntityGroupLabel(entityType?: string): string {
  if (!entityType) return 'Documents';
  const key = entityType.toLowerCase() as SearchEntityType;
  return SEARCH_ENTITY_CONFIG[key]?.labelPlural ?? 'Documents';
}
