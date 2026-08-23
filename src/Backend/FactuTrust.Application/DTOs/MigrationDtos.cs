namespace FactuTrust.Application.DTOs;

/// <summary>Progiciel comptable source détecté lors d'une migration assistée.</summary>
public enum MigrationSourceSystem
{
    /// <summary>Format non reconnu — le mapping de colonnes assisté reste possible.</summary>
    Inconnu = 0,

    /// <summary>Sage Ligne 100 (en-têtes CG_NUM / CT_NUM…).</summary>
    SageLigne100 = 1,

    /// <summary>EBP Comptabilité.</summary>
    Ebp = 2,

    /// <summary>Cegid.</summary>
    Cegid = 3,

    /// <summary>Quadra.</summary>
    Quadra = 4,

    /// <summary>Tableur générique (Excel/CSV « à la main ») aux en-têtes non propriétaires.</summary>
    TableurGenerique = 5
}

/// <summary>Résultat de l'analyse d'un fichier source (fingerprint) — aucune donnée n'est écrite.</summary>
public sealed record MigrationAnalysisDto
{
    public required string FileName { get; init; }

    /// <summary>Progiciel source détecté (<see cref="MigrationSourceSystem.Inconnu"/> si non reconnu).</summary>
    public MigrationSourceSystem DetectedSource { get; init; }

    /// <summary>Confiance de la détection du progiciel (0..1). 0 si non reconnu.</summary>
    public double Confidence { get; init; }

    /// <summary>Format tabulaire détecté (CSV ou Excel).</summary>
    public JournalImportFormat Format { get; init; }

    /// <summary>Délimiteur détecté pour un CSV, null pour un classeur Excel.</summary>
    public string? Delimiter { get; init; }

    /// <summary>Cible d'import proposée (plan comptable, plan tiers, balance d'ouverture).</summary>
    public ReferenceImportTarget SuggestedTarget { get; init; }

    /// <summary>Confiance de la cible proposée (0..1).</summary>
    public double TargetConfidence { get; init; }

    /// <summary>En-têtes lus dans le fichier (première ligne / première feuille).</summary>
    public IReadOnlyList<string> Headers { get; init; } = Array.Empty<string>();

    /// <summary>Nombre de lignes de données détectées sous l'en-tête.</summary>
    public int SampleRowCount { get; init; }

    /// <summary>Vrai si le catalogue déterministe de formats connus a reconnu le fichier.</summary>
    public bool KnownFormatMatched { get; init; }

    /// <summary>Avertissements lisibles (format incertain, classeur multi-feuilles…).</summary>
    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();
}

/// <summary>Origine d'une suggestion (jamais appliquée silencieusement — toujours éditable).</summary>
public enum MigrationSuggestionOrigin
{
    /// <summary>Correspondance par les synonymes historiques du pipeline d'import.</summary>
    Synonyme = 0,

    /// <summary>Correspondance issue du catalogue de formats connus (Sage, EBP…).</summary>
    Catalogue = 1,

    /// <summary>Correspondance proposée par le modèle de langage (bornée et contrôlée).</summary>
    Ia = 2,

    /// <summary>Compte identique déjà présent dans le plan local.</summary>
    Identite = 3,

    /// <summary>Rapprochement par préfixe de codification (ex. 613200 → 6132).</summary>
    Prefixe = 4
}

/// <summary>Suggestion de correspondance d'une colonne source vers le schéma canonique.</summary>
public sealed record ColumnMappingSuggestionItemDto
{
    /// <summary>En-tête source tel que lu dans le fichier.</summary>
    public required string SourceColumn { get; init; }

    /// <summary>Colonne canonique proposée (<c>compte</c>, <c>libelle</c>…), null = non mappée.</summary>
    public string? CanonicalColumn { get; init; }

    public double Confidence { get; init; }

    public MigrationSuggestionOrigin Origin { get; init; }
}

/// <summary>Résultat du mapping de colonnes assisté pour une cible donnée.</summary>
public sealed record ColumnMappingSuggestionDto
{
    public ReferenceImportTarget Target { get; init; }

    /// <summary>Colonnes canoniques obligatoires de la cible (rappel pour l'UI).</summary>
    public IReadOnlyList<string> RequiredCanonical { get; init; } = Array.Empty<string>();

    /// <summary>Une suggestion par colonne source du fichier.</summary>
    public IReadOnlyList<ColumnMappingSuggestionItemDto> Items { get; init; }
        = Array.Empty<ColumnMappingSuggestionItemDto>();

    /// <summary>Vrai si toutes les colonnes obligatoires sont couvertes par une suggestion.</summary>
    public bool Complete { get; init; }

    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();
}

/// <summary>Suggestion de correspondance d'un compte source vers le plan comptable local.</summary>
public sealed record AccountMappingSuggestionItemDto
{
    public required string SourceAccount { get; init; }

    /// <summary>Intitulé source (première occurrence rencontrée), informatif.</summary>
    public string? SourceLabel { get; init; }

    /// <summary>Compte cible du plan local ; null = abstention (à mapper manuellement).</summary>
    public string? TargetAccount { get; init; }

    public double Confidence { get; init; }

    public MigrationSuggestionOrigin Origin { get; init; }

    /// <summary>Justification courte (obligatoire pour les propositions IA).</summary>
    public string? Justification { get; init; }
}

/// <summary>Résultat du mapping comptable assisté.</summary>
public sealed record AccountMappingSuggestionDto
{
    /// <summary>Nombre de comptes source distincts détectés dans le fichier.</summary>
    public int TotalAccounts { get; init; }

    /// <summary>Comptes résolus sans appel au modèle (identité ou préfixe).</summary>
    public int ResolvedWithoutModel { get; init; }

    /// <summary>Comptes pour lesquels aucune proposition fiable n'a été trouvée (abstention).</summary>
    public int Abstained { get; init; }

    public IReadOnlyList<AccountMappingSuggestionItemDto> Items { get; init; }
        = Array.Empty<AccountMappingSuggestionItemDto>();

    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();
}

/// <summary>Paire candidate de tiers en doublon (ligne importée ↔ tiers existant ou autre ligne).</summary>
public sealed record ThirdPartyDuplicatePairDto
{
    /// <summary>Nom du tiers tel qu'importé (ligne du fichier).</summary>
    public required string ImportedName { get; init; }

    /// <summary>Référence lisible de la ligne importée (ex. « ligne 12 »).</summary>
    public required string ImportedRef { get; init; }

    public string? ImportedNif { get; init; }

    /// <summary>Origine du tiers en face : « client », « fournisseur » ou « fichier » (doublon interne).</summary>
    public required string ExistingOrigin { get; init; }

    public required string ExistingName { get; init; }

    public string? ExistingNif { get; init; }

    /// <summary>Score de la paire (0..1), déterministe et explicable.</summary>
    public double Score { get; init; }

    /// <summary>Motif lisible (ex. « Même matricule fiscal », « Noms identiques après normalisation »).</summary>
    public required string Reason { get; init; }
}

/// <summary>Résultat de la détection de doublons sur un plan tiers à importer.</summary>
public sealed record ThirdPartyDuplicateDetectionDto
{
    public int ImportedCount { get; init; }

    public int ExistingCount { get; init; }

    public int CandidatePairs { get; init; }

    public IReadOnlyList<ThirdPartyDuplicatePairDto> Pairs { get; init; }
        = Array.Empty<ThirdPartyDuplicatePairDto>();

    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();
}
