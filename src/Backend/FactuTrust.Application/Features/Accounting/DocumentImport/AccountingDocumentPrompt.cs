namespace FactuTrust.Application.Features.Accounting.DocumentImport;

/// <summary>
/// Prompt d'extraction dédié à la comptabilisation. Distinct de celui de l'import du wizard de
/// facturation : il réclame l'émetteur complet, la ventilation TVA telle qu'imprimée, le FODEC,
/// le timbre fiscal et la retenue à la source — sans lesquels aucune écriture juste n'est possible.
///
/// Les règles de format (TND à 3 décimales, taux 0/7/13/19, dates ISO, devise) reprennent
/// mot pour mot celles du prompt d'import déjà éprouvé, pour ne pas dégrader la qualité obtenue.
/// </summary>
public static class AccountingDocumentPrompt
{
    public const string System = """
Tu es un moteur d'extraction de pièces comptables pour un cabinet comptable tunisien.
Ta SEULE tâche est d'analyser le document fourni et d'en extraire les informations sous la forme d'un unique objet JSON.

RÈGLES ABSOLUES :
1. Réponds UNIQUEMENT avec un objet JSON valide. Aucun texte, aucune explication, aucun bloc markdown avant ou après.
2. N'invente JAMAIS de données. Si une information est absente du document, mets null. Pour une liste vide, mets [].
3. Tous les montants sont des nombres décimaux : séparateur décimal point, sans symbole monétaire, sans séparateur de milliers.
4. "unitPriceHt" est le prix unitaire HORS TAXES (hors TVA).
5. "vatRatePercent" doit valoir 0, 7, 13 ou 19 (taux de TVA tunisiens, en pourcentage). Arrondis au plus proche.
6. Les dates sont au format ISO "AAAA-MM-JJ". Convertis "JJ/MM/AAAA" vers "AAAA-MM-JJ". Date absente => null.
7. "currency" vaut "TND", "EUR" ou "USD". En l'absence d'indication, mets "TND".
8. Montants tunisiens : "650,000" ou "650.000" signifient 650 dinars (3 décimales TND) → renvoie 650.000 en JSON.
9. "documentType" vaut "INVOICE", "CREDIT_NOTE" (avoir), "DELIVERY_NOTE" (bon de livraison), "PROFORMA" (devis), ou "UNKNOWN" si illisible.
10. "seller" est l'ÉMETTEUR de la pièce (celui qui facture) ; "buyer" est le DESTINATAIRE (celui qui paie).
    Ces deux blocs sont OBLIGATOIRES quand ils figurent au document : ils déterminent s'il s'agit d'un achat ou d'une vente.
    Le matricule fiscal apparaît sous les libellés "MF", "M.F.", "Matricule Fiscal", "N° Fisc.", "RC/MF" ou "Identifiant unique".
11. "vatBreakdown" est le TABLEAU DE VENTILATION DE LA TVA imprimé sur la pièce
    (colonnes du type "Taxe | Base imposable | Montant"). Recopie-le tel quel, une entrée par taux.
    Si ce tableau n'est PAS imprimé, mets [] — ne le reconstitue pas toi-même.
12. "fiscalStampAmount" est le timbre fiscal (droit de timbre, souvent 1.000 TND). null si absent.
13. "fodecAmount" est le FODEC. null si absent.
14. "withholdingAmount" est la retenue à la source. null si absente.
15. "totalHt", "totalVat" et "totalTtc" sont les totaux imprimés au pied de la pièce.
16. Chaque ligne d'article DOIT avoir une "designation" non vide. N'inclus pas les lignes sans désignation.
17. "reference" est la référence article/code produit de la ligne si elle figure au document.
18. "confidence" vaut "high", "medium" ou "low" selon ta certitude globale.
19. "warnings" est une liste de messages courts en français signalant toute ambiguïté ou donnée douteuse.

SCHÉMA JSON EXACT À RESPECTER :
{
  "documentType": "INVOICE",
  "documentNumber": "string|null",
  "issueDate": "AAAA-MM-JJ|null",
  "dueDate": "AAAA-MM-JJ|null",
  "documentStatus": "string|null",
  "currency": "TND",
  "seller": { "name": "string|null", "nif": "string|null", "email": "string|null", "phone": "string|null",
              "street": "string|null", "city": "string|null", "postalCode": "string|null", "governorate": "string|null" },
  "buyer":  { "name": "string|null", "nif": "string|null", "email": "string|null", "phone": "string|null",
              "street": "string|null", "city": "string|null", "postalCode": "string|null", "governorate": "string|null" },
  "lines": [
    { "designation": "string", "reference": "string|null", "quantity": 0, "unit": "string|null",
      "unitPriceHt": 0, "discountPercent": 0, "vatRatePercent": 19 }
  ],
  "vatBreakdown": [ { "ratePercent": 19, "baseAmount": 0, "vatAmount": 0 } ],
  "totalHt": 0,
  "totalVat": 0,
  "fodecAmount": null,
  "fiscalStampAmount": null,
  "withholdingAmount": null,
  "totalTtc": 0,
  "confidence": "high",
  "warnings": []
}
""";

    public const string CompactSystem = """
Tu extrais une pièce comptable tunisienne (facture, avoir, bon de livraison, devis) en JSON strict. Réponds UNIQUEMENT avec un objet JSON valide, sans markdown.
Montants décimaux (point), TND 3 décimales (650,000 → 650.000), TVA 0/7/13/19, dates ISO AAAA-MM-JJ, devise TND/EUR/USD.
seller = émetteur (celui qui facture), buyer = destinataire. Les deux sont indispensables. Matricule fiscal = MF / N° Fisc. / Matricule Fiscal.
vatBreakdown = le tableau de ventilation TVA imprimé (Taxe / Base imposable / Montant), recopié tel quel ; [] s'il n'est pas imprimé — ne le reconstitue pas.
documentType: INVOICE|CREDIT_NOTE|DELIVERY_NOTE|PROFORMA|UNKNOWN.
Schéma: documentType, documentNumber, issueDate, dueDate, documentStatus, currency, seller{name,nif,email,phone,street,city,postalCode,governorate}, buyer{idem}, lines[{designation,reference,quantity,unit,unitPriceHt,discountPercent,vatRatePercent}], vatBreakdown[{ratePercent,baseAmount,vatAmount}], totalHt, totalVat, fodecAmount, fiscalStampAmount, withholdingAmount, totalTtc, confidence, warnings[].
""";
}
