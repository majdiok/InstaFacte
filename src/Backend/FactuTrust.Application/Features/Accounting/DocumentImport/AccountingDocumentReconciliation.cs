using FactuTrust.Domain.Common;

namespace FactuTrust.Application.Features.Accounting.DocumentImport;

/// <summary>
/// Contrôles de cohérence arithmétique d'une pièce extraite, au millime.
///
/// Sert deux usages :
/// <list type="bullet">
/// <item>porte de sortie du parseur natif — il ne rend un résultat que s'il est parfaitement
/// réconcilié, sinon on bascule sur l'IA. C'est ce qui rend la 1re passe incapable de produire
/// un résultat faux silencieusement ;</item>
/// <item>diagnostic affiché à l'utilisateur lorsque l'extraction vient du LLM.</item>
/// </list>
/// </summary>
public static class AccountingDocumentReconciliation
{
    /// <summary>Deux montants sont identiques s'ils le sont une fois arrondis au millime.</summary>
    public static bool AmountsMatch(decimal? a, decimal? b) =>
        MillimeRounding.Round(a ?? 0m) == MillimeRounding.Round(b ?? 0m);

    public sealed record Report(
        bool VatBaseMatchesTotalHt,
        bool VatAmountMatchesTotalVat,
        bool GrandTotalMatches,
        decimal? GrandTotalDelta)
    {
        /// <summary>Vrai lorsque tous les contrôles arithmétiques passent.</summary>
        public bool IsFullyConsistent =>
            VatBaseMatchesTotalHt && VatAmountMatchesTotalVat && GrandTotalMatches;
    }

    /// <summary>
    /// Vérifie que la ventilation TVA recoupe les totaux, et que
    /// <c>HT + TVA + FODEC + timbre == TTC</c>.
    /// </summary>
    public static Report Check(AccountingDocumentExtractionDto document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var totalHt = document.TotalHt;
        var totalVat = document.TotalVat;

        // Une pièce sans ventilation imprimée (ou entièrement exonérée) ne peut pas être recoupée
        // par ce biais : le contrôle est alors neutre, pas en échec.
        var hasBreakdown = document.VatBreakdown.Count > 0;
        var baseMatches = !hasBreakdown
            || totalHt is null
            || AmountsMatch(document.VatBreakdown.Sum(b => b.BaseAmount), totalHt);
        var vatMatches = !hasBreakdown
            || totalVat is null
            || AmountsMatch(document.VatBreakdown.Sum(b => b.VatAmount), totalVat);

        decimal? delta = null;
        var grandTotalMatches = true;
        if (document.TotalTtc is { } ttc && totalHt is { } ht)
        {
            var computed = ht
                + (totalVat ?? 0m)
                + (document.FodecAmount ?? 0m)
                + (document.FiscalStampAmount ?? 0m);
            delta = MillimeRounding.Round(ttc - computed);
            grandTotalMatches = delta == 0m;
        }

        return new Report(baseMatches, vatMatches, grandTotalMatches, delta);
    }

    /// <summary>
    /// Une pièce est exploitable telle quelle par le parseur natif si elle porte un numéro, une
    /// date, un total HT et un total TTC, et que tous ses contrôles arithmétiques passent.
    /// </summary>
    public static bool IsUsableWithoutFallback(AccountingDocumentExtractionDto document)
    {
        ArgumentNullException.ThrowIfNull(document);

        if (string.IsNullOrWhiteSpace(document.DocumentNumber))
            return false;
        if (document.IssueDate is null)
            return false;
        if (document.TotalHt is null || document.TotalTtc is null)
            return false;

        return Check(document).IsFullyConsistent;
    }
}
