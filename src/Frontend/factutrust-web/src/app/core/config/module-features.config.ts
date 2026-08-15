import { AppModule } from '@core/models/app-module';
import type { UserModuleAccessItem } from '@core/services/tenant-users.service';

export interface ModuleFeatureOption {
  key: string;
  label: string;
}

/** Mirrors backend <c>ModuleFeatureCatalog</c> keys + French labels for the UI. */
const MODULE_FEATURE_OPTIONS: Record<AppModule, ModuleFeatureOption[]> = {
  [AppModule.Clients]: [
    { key: 'read', label: 'Consultation' },
    { key: 'manage', label: 'Création / modification / suppression' }
  ],
  [AppModule.Products]: [
    { key: 'read', label: 'Consultation' },
    { key: 'manage', label: 'Création / modification / suppression' }
  ],
  [AppModule.Sales]: [
    { key: 'quotes', label: 'Devis' },
    { key: 'delivery_notes', label: 'Bons de livraison' },
    { key: 'invoices', label: 'Factures' }
  ],
  [AppModule.Treasury]: [
    { key: 'read', label: 'Consultation' },
    { key: 'manage', label: 'Saisie et mise à jour' },
    { key: 'forecast_read', label: 'Trésorerie prévisionnelle — consultation' },
    { key: 'forecast_manage', label: 'Trésorerie prévisionnelle — recalcul et engagements' }
  ],
  [AppModule.Reports]: [
    { key: 'sales', label: 'Ventes' },
    { key: 'purchases', label: 'Achats' },
    { key: 'stock', label: 'Stock' },
    { key: 'fiches', label: 'Fiches' },
    { key: 'payments', label: 'Paiements' }
  ],
  [AppModule.Administration]: [
    { key: 'users', label: 'Utilisateurs' },
    { key: 'settings', label: 'Paramètres' }
  ],
  [AppModule.Purchases]: [
    { key: 'suppliers', label: 'Fournisseurs' },
    { key: 'purchase_orders', label: 'Bons de commande' },
    { key: 'purchase_receipts', label: 'Bons de réception' },
    { key: 'supplier_invoices', label: 'Factures fournisseurs' }
  ],
  [AppModule.Stock]: [
    { key: 'stock', label: 'Mouvements de stock' },
    { key: 'stock_transfers', label: 'Transferts' },
    { key: 'inventory', label: 'Inventaires' }
  ],
  [AppModule.Accounting]: [
    { key: 'journal', label: 'Journal / écritures' },
    { key: 'ledger', label: 'Grand livre' },
    { key: 'aging', label: 'Balance âgée' },
    { key: 'vat_declaration', label: 'Déclaration TVA' },
    { key: 'closing', label: 'Clôture' },
    { key: 'audit_log', label: 'Journal d\'audit' },
    { key: 'fixed_assets', label: 'Immobilisations' }
  ],
  [AppModule.CRM]: [
    { key: 'opportunities', label: 'Opportunités' },
    { key: 'activities', label: 'Activités' },
    { key: 'targets', label: 'Objectifs commerciaux' },
    { key: 'quote_templates', label: 'Modèles de devis' }
  ],
  [AppModule.Fiscal]: [],
  [AppModule.AI]: [],
  [AppModule.Forecasting]: [
    { key: 'view', label: 'Consultation des prévisions' },
    { key: 'manage', label: 'Approbation / dismiss / recalcul' }
  ],
  [AppModule.Studio]: [],
  [AppModule.Payroll]: [],
  [AppModule.Honoraires]: [
    { key: 'invoices', label: 'Factures' },
    { key: 'quotes', label: 'Devis' },
    { key: 'payments', label: 'Encaissements' }
  ]
};

export function getModuleFeatureOptions(module: AppModule): ModuleFeatureOption[] {
  return MODULE_FEATURE_OPTIONS[module] ?? [];
}

export function allFeatureKeysForModule(module: AppModule): string[] {
  return getModuleFeatureOptions(module).map(o => o.key);
}

export function isSubFeatureOn(item: UserModuleAccessItem, featureKey: string): boolean {
  if (!item.enabled) return false;
  const all = allFeatureKeysForModule(item.module);
  if (all.length === 0) return false;
  if (item.enabledFeatureKeys == null) return true;
  if (item.enabledFeatureKeys.length === 0) return false;
  return item.enabledFeatureKeys.includes(featureKey);
}

/** Tout coché → null ; aucune case → [] ; sous-ensemble explicite → liste. */
export function setSubFeatureChecked(item: UserModuleAccessItem, featureKey: string, checked: boolean): void {
  const next = withSubFeatureToggled(item, featureKey, checked);
  item.enabledFeatureKeys = next.enabledFeatureKeys;
}

/** Immutable — à utiliser avec signal.update (p-checkbox OnPush + NgModel). */
export function withSubFeatureToggled(
  item: UserModuleAccessItem,
  featureKey: string,
  checked: boolean
): UserModuleAccessItem {
  const all = allFeatureKeysForModule(item.module);
  if (all.length === 0) return { ...item };
  const current =
    item.enabledFeatureKeys == null
      ? [...all]
      : item.enabledFeatureKeys.length === 0
        ? []
        : [...item.enabledFeatureKeys];
  const set = new Set(current);
  if (checked) set.add(featureKey);
  else set.delete(featureKey);
  const next = all.filter(k => set.has(k));
  const keys =
    next.length === 0 ? [] : next.length === all.length ? null : next;
  return { ...item, enabledFeatureKeys: keys };
}

/**
 * null/undefined keys = toutes les sous-fonctions (omit). Tableau vide = aucune sous-fonction explicite.
 */
export function toModuleAccessApiPayload(
  items: UserModuleAccessItem[]
): Array<{ module: AppModule; enabled: boolean; enabledFeatureKeys?: string[] | null }> {
  return items.map(x => {
    const row: { module: AppModule; enabled: boolean; enabledFeatureKeys?: string[] | null } = {
      module: x.module,
      enabled: x.enabled
    };
    if (!x.enabled) return row;
    const keys = x.enabledFeatureKeys;
    if (keys === null || keys === undefined) return row;
    row.enabledFeatureKeys = keys;
    return row;
  });
}
