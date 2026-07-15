namespace FactuTrust.Application.Common;

/// <summary>
/// Référentiel métier (Tunisie) : correspondance nature de paiement ↔ codes TEJ ↔ règles de calcul.
/// Les taux et seuils effectifs sont dans le service <c>IWithholdingTaxService</c>,
/// le catalogue système des types RS et la table <c>WithholdingFiscalYearParameters</c> (seuil RS7 par exercice).
/// </summary>
/// <remarks>
/// <para><b>RS1 — Loyers</b> (Art. 52-I IRPP) : retenue sur HT ; retenue sur TVA (19 bis) si TVA — taux 25 % résident / 100 % non-résident.</para>
/// <para><b>RS2 — Honoraires, commissions</b> (Art. 52-II) : idem TVA 19 bis pour résidents.</para>
/// <para><b>RS3 — Revenus de capitaux mobiliers</b> (Art. 52-III) : retenue sur HT, pas de retenue TVA dans le moteur actuel.</para>
/// <para><b>RS4 — Dividendes</b>, <b>RS5 — Plus-values</b>, <b>RS6 — Fonds de commerce</b> : retenue sur HT.</para>
/// <para><b>RS7 — Achats</b> (Art. 52-g IS) : si TTC &lt; seuil (paramètre fiscal annuel, défaut 1000 TND) → pas de RS7 ; sinon taux selon sous-code (régime IS du fournisseur).</para>
/// <para><b>RS8 — Jeux</b> ; <b>RS9 — Non-résidents</b> (Art. 53) : retenue sur HT ; retenue TVA 100 % ; CNPC peut réduire le taux HT (traités).</para>
/// <para><b>RS10 / RS11</b> : selon catalogue seed.</para>
/// <para><b>Prise en charge</b> : taux effectif = taux / (100 − taux) appliqué sur HT.</para>
/// </remarks>
public static class WithholdingTaxLegislativeReference
{
    /// <summary>Codes RS7 officiels (seed) pour le guidage « régime IS » fournisseur.</summary>
    public const string Rs7CodeNormal25 = "RS7_000001";
    public const string Rs7CodeReduced15 = "RS7_000002";
    public const string Rs7CodeReduced10 = "RS7_000003";

    public static string TejCodeForRs7Bracket(Domain.Enums.SupplierRs7IsBracket bracket) => bracket switch
    {
        Domain.Enums.SupplierRs7IsBracket.Normal25 => Rs7CodeNormal25,
        Domain.Enums.SupplierRs7IsBracket.Reduced15 => Rs7CodeReduced15,
        Domain.Enums.SupplierRs7IsBracket.Reduced10 => Rs7CodeReduced10,
        _ => Rs7CodeNormal25
    };
}
