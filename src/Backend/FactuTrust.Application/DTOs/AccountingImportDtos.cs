namespace FactuTrust.Application.DTOs;

/// <summary>Format du fichier de reprise de dossier importé.</summary>
public enum JournalImportFormat
{
    /// <summary>CSV délimité (colonnes : journal, numero, date, compte, libelle, debit, credit).</summary>
    Csv = 0,

    /// <summary>Classeur Excel (.xlsx), mêmes colonnes que le CSV, première ligne = en-têtes.</summary>
    Excel = 1,

    /// <summary>Fichier des Écritures Comptables (FEC) — format tabulé produit par l'export FEC.</summary>
    Fec = 2
}

public sealed record ImportEntryLineDto
{
    public string AccountNumber { get; init; } = null!;
    public string Label { get; init; } = null!;
    public decimal Debit { get; init; }
    public decimal Credit { get; init; }
}

/// <summary>Une écriture reconstituée à partir du fichier (lignes regroupées par journal + numéro de pièce).</summary>
public sealed record ImportEntryDto
{
    /// <summary>Référence lisible de la pièce (ex. « JV/12 ») pour le rapport et l'aperçu.</summary>
    public string Ref { get; init; } = null!;
    /// <summary>Numéro de pièce brut du fichier (colonne « numero ») — repris comme PieceRef de l'écriture.</summary>
    public string Piece { get; init; } = null!;
    public string JournalCode { get; init; } = null!;
    public DateTime EntryDate { get; init; }
    public string Label { get; init; } = null!;
    public decimal TotalDebit { get; init; }
    public decimal TotalCredit { get; init; }
    public bool IsBalanced { get; init; }
    public IReadOnlyList<ImportEntryLineDto> Lines { get; init; } = Array.Empty<ImportEntryLineDto>();
}

/// <summary>Anomalie détectée lors de l'analyse ou de la validation (bloquante = empêche le commit).</summary>
public sealed record ImportIssueDto
{
    /// <summary>Référence de la pièce ou de la ligne concernée (ex. « JV/12 » ou « ligne 42 »).</summary>
    public string Ref { get; init; } = null!;
    public string Message { get; init; } = null!;
    public bool IsBlocking { get; init; } = true;
}

public sealed record JournalImportPreviewDto
{
    public int TotalEntries { get; init; }
    public int TotalLines { get; init; }
    public int ValidEntries { get; init; }
    public int EntriesWithErrors { get; init; }
    public decimal TotalDebit { get; init; }
    public decimal TotalCredit { get; init; }
    /// <summary>Vrai si aucune anomalie bloquante : le commit est alors autorisé.</summary>
    public bool CanCommit { get; init; }
    public IReadOnlyList<ImportIssueDto> Issues { get; init; } = Array.Empty<ImportIssueDto>();
    /// <summary>Échantillon des premières écritures (aperçu), limité côté serveur.</summary>
    public IReadOnlyList<ImportEntryDto> Sample { get; init; } = Array.Empty<ImportEntryDto>();
}

public sealed record JournalImportCommitResultDto
{
    public int ImportedEntries { get; init; }
    public int ImportedLines { get; init; }
}

/// <summary>Nature du référentiel importé par la reprise étendue (au-delà des écritures).</summary>
public enum ReferenceImportTarget
{
    /// <summary>Plan comptable : colonnes compte, libelle, classe, nature.</summary>
    ChartOfAccounts = 0,

    /// <summary>Plan tiers : colonnes type, nom, email, rue, ville, gouvernorat (nif, code optionnels).</summary>
    ThirdParties = 1,

    /// <summary>Balance d'ouverture : colonnes compte, debit, credit → une écriture d'à-nouveau en brouillon.</summary>
    OpeningBalance = 2
}

/// <summary>Ligne d'aperçu d'un import de référentiel (résumé lisible + statut).</summary>
public sealed record ReferenceImportRowDto
{
    public string Ref { get; init; } = null!;
    public string Summary { get; init; } = null!;
    /// <summary>Vrai si la ligne existe déjà et sera ignorée (jamais écrasée).</summary>
    public bool AlreadyExists { get; init; }
}

/// <summary>Aperçu (dry-run) d'un import de référentiel : mêmes règles que le commit, sans persistance.</summary>
public sealed record ReferenceImportPreviewDto
{
    public ReferenceImportTarget Target { get; init; }
    public int TotalRows { get; init; }
    public int ValidRows { get; init; }
    public int RowsWithErrors { get; init; }
    /// <summary>Lignes déjà présentes qui seront ignorées au commit (additif, jamais d'écrasement).</summary>
    public int ExistingRows { get; init; }
    /// <summary>Vrai si aucune anomalie bloquante.</summary>
    public bool CanCommit { get; init; }
    public IReadOnlyList<ImportIssueDto> Issues { get; init; } = Array.Empty<ImportIssueDto>();
    public IReadOnlyList<ReferenceImportRowDto> Sample { get; init; } = Array.Empty<ReferenceImportRowDto>();
}

public sealed record ReferenceImportCommitResultDto
{
    /// <summary>Éléments effectivement créés (comptes, tiers, ou lignes d'à-nouveau).</summary>
    public int CreatedCount { get; init; }
    /// <summary>Éléments ignorés car déjà présents.</summary>
    public int SkippedCount { get; init; }
}
