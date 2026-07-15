export type PrintableDocumentType =
  | 'SalesInvoice'
  | 'CreditNote'
  | 'Quote'
  | 'PurchaseOrder'
  | 'DeliveryNote'
  | 'SupplierInvoice';

export interface DocumentTemplatePreferenceDto {
  documentType: PrintableDocumentType;
  documentTypeValue: number;
  documentTypeLabel: string;
  templateKey: string;
  templateName: string;
}

export interface DocumentTemplateCatalogItemDto {
  key: string;
  name: string;
  description: string;
  thumbnail: string;
  supportedDocumentTypes: string[];
}

export interface SaveDocumentTemplateRequest {
  templateKey: string;
}

export interface DocumentTypeTab {
  type: PrintableDocumentType;
  label: string;
  icon: string;
}

export const DOCUMENT_TYPE_TABS: DocumentTypeTab[] = [
  { type: 'SalesInvoice', label: 'Factures de vente', icon: 'pi pi-file' },
  { type: 'CreditNote', label: 'Avoirs', icon: 'pi pi-file-excel' },
  { type: 'Quote', label: 'Devis', icon: 'pi pi-file-edit' },
  { type: 'PurchaseOrder', label: 'Bons de commande', icon: 'pi pi-shopping-cart' },
  { type: 'DeliveryNote', label: 'Bons de livraison', icon: 'pi pi-truck' },
  { type: 'SupplierInvoice', label: "Factures d'achat", icon: 'pi pi-receipt' }
];
