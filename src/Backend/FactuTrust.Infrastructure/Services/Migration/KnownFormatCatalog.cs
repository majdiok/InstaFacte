using FactuTrust.Application.DTOs;

namespace FactuTrust.Infrastructure.Services.Migration;

/// <summary>
/// Signature d'un format d'export connu : en-têtes normalisés caractéristiques (<see cref="StrongHeaders"/>)
/// et synonymes de colonnes spécifiques au progiciel (canonique → en-têtes source normalisés).
/// Les en-têtes sont comparés après <c>TabularFileParsing.Normalize</c> (minuscules, sans accents,
/// lettres/chiffres uniquement).
/// </summary>
internal sealed record KnownFormatSignature(
    string Name,
    MigrationSourceSystem System,
    ReferenceImportTarget Target,
    IReadOnlyDictionary<string, string[]> Synonyms,
    string[] StrongHeaders);

/// <summary>
/// Catalogue DÉTERMINISTE des formats d'export rencontrés en migration (Tunisie / francophonie).
/// Premier étage du fingerprint : si le fichier correspond à une signature, ni appel LLM ni
/// saisie manuelle ne sont nécessaires pour le mapping de colonnes.
/// <para>
/// V1 : signatures constituées à partir des spécifications publiques des formats et des exports
/// observés. Le corpus de tests (<c>tests/Fixtures/Migration</c>) est synthétique — aucune donnée
/// client ne transite par le dépôt. Le catalogue est prévu pour être enrichi à chaque migration
/// réelle (via le profil validé, boucle d'apprentissage).
/// </para>
/// </summary>
internal static class KnownFormatCatalog
{
    public static IReadOnlyList<KnownFormatSignature> All { get; } = new[]
    {
        // ── Sage Ligne 100 ────────────────────────────────────────────────────
        new KnownFormatSignature(
            "Sage Ligne 100 — Plan comptable",
            MigrationSourceSystem.SageLigne100,
            ReferenceImportTarget.ChartOfAccounts,
            new Dictionary<string, string[]>
            {
                ["compte"] = new[] { "cgnum", "cgnumcompte" },
                ["libelle"] = new[] { "cgintitc", "cgintitule", "cglibelle" },
                ["classe"] = new[] { "cgclasse", "classe" },
                ["nature"] = new[] { "cgnature", "cgsens" }
            },
            new[] { "cgnum", "cgintitc" }),

        new KnownFormatSignature(
            "Sage Ligne 100 — Plan tiers",
            MigrationSourceSystem.SageLigne100,
            ReferenceImportTarget.ThirdParties,
            new Dictionary<string, string[]>
            {
                ["type"] = new[] { "cttype", "ctcategorie" },
                ["nom"] = new[] { "ctintitule", "ctintitc" },
                ["email"] = new[] { "ctemail", "ctmail" },
                ["rue"] = new[] { "ctadresse", "ctadresse1" },
                ["ville"] = new[] { "ctville" },
                ["gouvernorat"] = new[] { "ctregion", "ctgouvernorat" },
                ["nif"] = new[] { "ctidentifiant", "ctnif", "ctmatricule" },
                ["telephone"] = new[] { "cttelephone", "cttel" }
            },
            new[] { "ctnum", "ctintitule" }),

        new KnownFormatSignature(
            "Sage Ligne 100 — Balance",
            MigrationSourceSystem.SageLigne100,
            ReferenceImportTarget.OpeningBalance,
            new Dictionary<string, string[]>
            {
                ["compte"] = new[] { "cgnum", "cgnumcompte" },
                ["debit"] = new[] { "soldedebit", "cgdebit", "debit" },
                ["credit"] = new[] { "soldecredit", "cgcredit", "credit" }
            },
            new[] { "cgnum", "soldedebit" }),

        // ── EBP ───────────────────────────────────────────────────────────────
        new KnownFormatSignature(
            "EBP — Plan comptable",
            MigrationSourceSystem.Ebp,
            ReferenceImportTarget.ChartOfAccounts,
            new Dictionary<string, string[]>
            {
                ["compte"] = new[] { "numerocompte", "ncompte" },
                ["libelle"] = new[] { "intitulecompte", "intitule" },
                ["classe"] = new[] { "classecompte", "classe" },
                ["nature"] = new[] { "naturecompte", "sens" }
            },
            new[] { "numerocompte", "intitulecompte" }),

        new KnownFormatSignature(
            "EBP — Balance",
            MigrationSourceSystem.Ebp,
            ReferenceImportTarget.OpeningBalance,
            new Dictionary<string, string[]>
            {
                ["compte"] = new[] { "numerocompte", "ncompte" },
                ["debit"] = new[] { "totaldebit", "mouvementdebit", "debit" },
                ["credit"] = new[] { "totalcredit", "mouvementcredit", "credit" }
            },
            new[] { "numerocompte", "totaldebit" }),

        // ── Cegid ─────────────────────────────────────────────────────────────
        new KnownFormatSignature(
            "Cegid — Plan comptable",
            MigrationSourceSystem.Cegid,
            ReferenceImportTarget.ChartOfAccounts,
            new Dictionary<string, string[]>
            {
                ["compte"] = new[] { "codecompte", "numcompte" },
                ["libelle"] = new[] { "libellecompte", "intitule" },
                ["classe"] = new[] { "classecompte", "classe" },
                ["nature"] = new[] { "senscompte", "sens" }
            },
            new[] { "codecompte", "libellecompte" }),

        // ── Quadra ────────────────────────────────────────────────────────────
        new KnownFormatSignature(
            "Quadra — Plan comptable",
            MigrationSourceSystem.Quadra,
            ReferenceImportTarget.ChartOfAccounts,
            new Dictionary<string, string[]>
            {
                ["compte"] = new[] { "comptegeneral", "numero" },
                ["libelle"] = new[] { "libelle", "intitule" },
                ["classe"] = new[] { "classe" },
                ["nature"] = new[] { "nature" }
            },
            new[] { "comptegeneral" }),
    };

    /// <summary>Signatures candidates pour une cible donnée.</summary>
    public static IEnumerable<KnownFormatSignature> ForTarget(ReferenceImportTarget target)
        => All.Where(s => s.Target == target);
}
