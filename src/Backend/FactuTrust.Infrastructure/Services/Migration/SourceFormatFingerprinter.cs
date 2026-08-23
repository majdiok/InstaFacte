using FactuTrust.Application.DTOs;

namespace FactuTrust.Infrastructure.Services.Migration;

/// <summary>Résultat interne du fingerprint (avant mise en forme en DTO).</summary>
internal sealed record MigrationFingerprint(
    MigrationSourceSystem System,
    double Confidence,
    ReferenceImportTarget Target,
    double TargetConfidence,
    bool KnownFormatMatched,
    KnownFormatSignature? Signature,
    IReadOnlyList<string> Warnings);

/// <summary>
/// Fingerprint DÉTERMINISTE d'un fichier source de migration : confrontation des en-têtes
/// normalisés au <see cref="KnownFormatCatalog"/>, puis heuristiques génériques de cible.
/// Aucun appel LLM ici : un format inconnu bascule simplement sur le mapping assisté (M1),
/// conformément à la doctrine « abstention plutôt que classification forcée ».
/// </summary>
internal static class SourceFormatFingerprinter
{
    /// <summary>Score minimal du catalogue pour déclarer un progiciel reconnu.</summary>
    private const int CatalogMatchMinScore = 3;

    /// <summary>Confiance publiée quand une signature du catalogue correspond (déterministe).</summary>
    private const double CatalogMatchConfidence = 0.95;

    public static MigrationFingerprint Fingerprint(MigrationFileShape shape)
    {
        var warnings = new List<string>();
        var normalizedHeaders = shape.Headers
            .Select(TabularFileParsing.Normalize)
            .Where(h => h.Length > 0)
            .ToHashSet(StringComparer.Ordinal);

        if (normalizedHeaders.Count == 0)
        {
            warnings.Add("Aucun en-tête exploitable : le fichier semble vide ou sans ligne d'en-tête.");
            return new MigrationFingerprint(
                MigrationSourceSystem.Inconnu, 0, ReferenceImportTarget.ChartOfAccounts, 0,
                false, null, warnings);
        }

        if (shape.SheetCount > 1)
            warnings.Add($"Classeur de {shape.SheetCount} feuilles : seule la première feuille est analysée (comme l'import standard).");

        // 1. Catalogue de formats connus : strong headers (×2) + colonnes canoniques résolues (×1).
        KnownFormatSignature? best = null;
        var bestScore = 0;
        foreach (var sig in KnownFormatCatalog.All)
        {
            var score = 0;
            foreach (var strong in sig.StrongHeaders)
                if (normalizedHeaders.Contains(strong))
                    score += 2;
            foreach (var (_, synonyms) in sig.Synonyms)
                if (synonyms.Any(normalizedHeaders.Contains))
                    score += 1;

            if (score > bestScore) { bestScore = score; best = sig; }
        }

        if (best is not null && bestScore >= CatalogMatchMinScore)
        {
            return new MigrationFingerprint(
                best.System, CatalogMatchConfidence, best.Target, 0.9,
                true, best, warnings);
        }

        // 2. Heuristiques génériques de cible (le progiciel reste inconnu).
        var (target, targetConfidence) = GuessTarget(normalizedHeaders);
        if (targetConfidence < 0.5)
            warnings.Add("Cible d'import incertaine : vérifiez le type de fichier proposé avant de continuer.");

        return new MigrationFingerprint(
            MigrationSourceSystem.Inconnu, 0, target, targetConfidence,
            false, null, warnings);
    }

    /// <summary>Heuristique de cible pour un fichier non reconnu par le catalogue.</summary>
    private static (ReferenceImportTarget, double) GuessTarget(ISet<string> headers)
    {
        bool HasAny(params string[] keys) => keys.Any(headers.Contains);

        var hasAccount = HasAny("compte", "numero", "numerocompte", "account", "num", "numcompte", "codecompte");
        var hasDebit = HasAny("debit", "debiteur", "d");
        var hasCredit = HasAny("credit", "crediteur", "c");
        var hasLibelle = HasAny("libelle", "intitule", "label", "nom", "designation");
        var hasClasse = HasAny("classe", "class");
        var hasEmail = HasAny("email", "mail", "courriel", "mel");
        var hasCity = HasAny("ville", "city");
        var hasName = HasAny("nom", "name", "raisonsociale");
        var hasType = HasAny("type", "categorie", "sens");

        // Balance d'ouverture : compte + montants débit/crédit.
        if (hasAccount && hasDebit && hasCredit)
            return (ReferenceImportTarget.OpeningBalance, 0.85);

        // Plan tiers : nom + coordonnées.
        if (hasName && (hasEmail || hasCity) && !hasAccount)
            return (ReferenceImportTarget.ThirdParties, 0.85);

        // Plan comptable : compte + libellé (+ classe éventuelle).
        if (hasAccount && hasLibelle)
            return (ReferenceImportTarget.ChartOfAccounts, hasClasse ? 0.85 : 0.6);

        // Tiers faiblement typé : nom + type seuls.
        if (hasName && hasType)
            return (ReferenceImportTarget.ThirdParties, 0.5);

        return (ReferenceImportTarget.ChartOfAccounts, 0.3);
    }
}
