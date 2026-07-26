using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.Configuration;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.ValueObjects;
using FactuTrust.Infrastructure.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FactuTrust.Infrastructure.Services;

/// <summary>
/// Reprise de dossier : valide (dry-run) puis importe des écritures depuis un fichier CSV/Excel/FEC.
/// Les écritures importées sont créées EN BROUILLON (revue avant validation). Le commit est
/// tout-ou-rien (un seul <c>SaveChanges</c>). Piloté par <c>Accounting.DossierImportEnabled</c>.
/// </summary>
public sealed class JournalImportService : IJournalImportService
{
    public const string SourceImport = "Import";
    private const int SampleSize = 20;

    private readonly ITenantDbContextFactory _contextFactory;
    private readonly IAccountingPeriodService _periodService;
    private readonly AccountingSettings _settings;
    private readonly JournalImportParser _parser = new();

    public JournalImportService(
        ITenantDbContextFactory contextFactory,
        IAccountingPeriodService periodService,
        IOptions<AccountingSettings> settings)
    {
        _contextFactory = contextFactory;
        _periodService = periodService;
        _settings = settings.Value;
    }

    // Surcharges historiques : délèguent sans table de correspondance (comportement strictement inchangé).
    public Task<Result<JournalImportPreviewDto>> PreviewAsync(byte[] content, JournalImportFormat format, CancellationToken cancellationToken = default)
        => PreviewAsync(content, format, null, cancellationToken);

    public Task<Result<JournalImportCommitResultDto>> CommitAsync(byte[] content, JournalImportFormat format, CancellationToken cancellationToken = default)
        => CommitAsync(content, format, null, cancellationToken);

    public async Task<Result<JournalImportPreviewDto>> PreviewAsync(
        byte[] content, JournalImportFormat format,
        byte[]? accountMappingContent, CancellationToken cancellationToken = default)
    {
        if (!_settings.DossierImportEnabled)
            return Result.Failure<JournalImportPreviewDto>(Error.Validation("Import", "La reprise de dossier par import n'est pas activée."));

        var (entries, parseIssues) = _parser.Parse(content, format);
        var issues = new List<ImportIssueDto>(parseIssues);

        var (mappedEntries, mapping) = await ApplyMappingAsync(entries, accountMappingContent, format, issues, cancellationToken);
        entries = mappedEntries;
        issues.AddRange(await ValidateAsync(entries, cancellationToken));

        return Result.Success(BuildPreview(entries, issues, mapping));
    }

    public async Task<Result<JournalImportCommitResultDto>> CommitAsync(
        byte[] content, JournalImportFormat format,
        byte[]? accountMappingContent, CancellationToken cancellationToken = default)
    {
        if (!_settings.DossierImportEnabled)
            return Result.Failure<JournalImportCommitResultDto>(Error.Validation("Import", "La reprise de dossier par import n'est pas activée."));

        var (entries, parseIssues) = _parser.Parse(content, format);
        var issues = new List<ImportIssueDto>(parseIssues);

        var (mappedEntries, _) = await ApplyMappingAsync(entries, accountMappingContent, format, issues, cancellationToken);
        entries = mappedEntries;
        issues.AddRange(await ValidateAsync(entries, cancellationToken));

        if (issues.Any(i => i.IsBlocking) || entries.Count == 0)
        {
            var first = issues.FirstOrDefault(i => i.IsBlocking);
            return Result.Failure<JournalImportCommitResultDto>(Error.Validation("Import",
                first is not null
                    ? $"Import refusé : {first.Ref} — {first.Message}"
                    : "Import refusé : aucune écriture exploitable."));
        }

        // Pré-création idempotente des périodes (hors transaction d'écriture : une période vide est inoffensive).
        var periodByMonth = new Dictionary<(int Year, int Month), Guid>();
        foreach (var e in entries)
        {
            var key = (e.EntryDate.Year, e.EntryDate.Month);
            if (periodByMonth.ContainsKey(key)) continue;
            var period = await _periodService.EnsureOpenPeriodAsync(e.EntryDate, cancellationToken);
            if (period.IsFailure)
                return Result.Failure<JournalImportCommitResultDto>(Error.Validation("Import",
                    $"Période clôturée ou indisponible pour la date {e.EntryDate:yyyy-MM-dd} — impossible d'importer."));
            periodByMonth[key] = period.Value.Id;
        }

        await using var ctx = _contextFactory.CreateContext();

        // Séquences de numérotation par (journal, année), chargées/complétées dans le MÊME contexte
        // pour rester atomiques avec les écritures.
        var seqKeys = entries.Select(e => (Code: e.JournalCode, Year: e.EntryDate.Year)).Distinct().ToList();
        var sequences = new Dictionary<(string Code, int Year), JournalEntrySequence>();
        foreach (var (code, year) in seqKeys)
        {
            var seq = await ctx.JournalEntrySequences.FirstOrDefaultAsync(s => s.JournalCode == code && s.FiscalYear == year, cancellationToken);
            if (seq is null)
            {
                seq = JournalEntrySequence.Create(code, year);
                seq.SetAuditInfo("system", isUpdate: false);
                ctx.JournalEntrySequences.Add(seq);
            }
            sequences[(code, year)] = seq;
        }

        var importedEntries = 0;
        var importedLines = 0;
        foreach (var e in entries)
        {
            var seq = sequences[(e.JournalCode, e.EntryDate.Year)];
            var number = seq.Next();

            var lines = e.Lines.Select(l => new JournalLineInput(
                l.AccountNumber, l.Label, l.Debit, l.Credit, null, ThirdPartyKind.None)).ToList();

            // La pièce du fichier source est conservée comme référence externe (tronquée à 50
            // caractères — la limite du domaine — plutôt que de refuser toute la reprise).
            var pieceRef = e.Piece.Length > 50 ? e.Piece[..50] : e.Piece;
            var create = JournalEntry.Create(
                number,
                e.JournalCode,
                e.EntryDate,
                e.Label,
                periodByMonth[(e.EntryDate.Year, e.EntryDate.Month)],
                isAutoGenerated: false,
                SourceImport,
                sourceEntityId: null,
                lines,
                Money.DefaultCurrency,
                reversesEntryId: null,
                initialStatus: JournalEntryStatus.Brouillon,
                pieceRef: pieceRef);

            if (create.IsFailure)
                return Result.Failure<JournalImportCommitResultDto>(Error.Validation("Import",
                    $"Écriture {e.Ref} invalide : {create.Error.Description}"));

            var entry = create.Value;
            entry.SetAuditInfo("system", false);
            ctx.JournalEntries.Add(entry);
            importedEntries++;
            importedLines += lines.Count;
        }

        // Un seul SaveChanges = tout-ou-rien (transaction implicite EF).
        await ctx.SaveChangesAsync(cancellationToken);

        return Result.Success(new JournalImportCommitResultDto
        {
            ImportedEntries = importedEntries,
            ImportedLines = importedLines
        });
    }

    // ── Table de correspondance de comptes (facultative) ──────────────────────

    /// <summary>
    /// Applique la table de correspondance aux comptes des lignes, AVANT toute validation.
    /// Sans table fournie, renvoie les écritures telles quelles — comportement historique strict.
    /// Une table dont une CIBLE est absente du plan comptable est une anomalie bloquante (erreur de
    /// table, pas de données).
    /// </summary>
    private async Task<(IReadOnlyList<ImportEntryDto> Entries, AccountMappingTable? Mapping)> ApplyMappingAsync(
        IReadOnlyList<ImportEntryDto> entries,
        byte[]? mappingContent,
        JournalImportFormat format,
        List<ImportIssueDto> issues,
        CancellationToken cancellationToken)
    {
        if (mappingContent is not { Length: > 0 })
            return (entries, null);

        var (table, mappingIssues) = AccountMappingTable.Parse(mappingContent, format);
        issues.AddRange(mappingIssues);
        if (table is null)
            return (entries, null);

        // Les cibles doivent exister au plan comptable, sinon la traduction produirait des comptes
        // introuvables et l'anomalie serait attribuée à tort au fichier de données.
        await using (var ctx = _contextFactory.CreateContext())
        {
            var known = await ctx.ChartOfAccounts.AsNoTracking()
                .Select(c => c.AccountNumber)
                .ToListAsync(cancellationToken);
            var knownSet = new HashSet<string>(known, StringComparer.Ordinal);
            foreach (var target in table.TargetAccounts.Where(t => !knownSet.Contains(t)))
            {
                issues.Add(new ImportIssueDto
                {
                    Ref = "correspondance",
                    Message = $"Le compte cible {target} de la table de correspondance est absent du plan comptable.",
                    IsBlocking = true
                });
            }
        }

        var translated = entries
            .Select(e => e with
            {
                Lines = e.Lines
                    .Select(l => l with { AccountNumber = table.Translate(l.AccountNumber) })
                    .ToList()
            })
            .ToList();

        return (translated, table);
    }

    // ── Validation métier partagée (aperçu + commit) ──────────────────────────

    private async Task<List<ImportIssueDto>> ValidateAsync(IReadOnlyList<ImportEntryDto> entries, CancellationToken cancellationToken)
    {
        var issues = new List<ImportIssueDto>();
        if (entries.Count == 0)
            return issues;

        await using var ctx = _contextFactory.CreateContext();

        var accounts = await ctx.ChartOfAccounts.AsNoTracking()
            .ToDictionaryAsync(c => c.AccountNumber, c => c.IsActive, cancellationToken);

        // Mois clôturés (l'import ne peut pas alimenter une période clôturée).
        var closedMonths = await ctx.AccountingPeriods.AsNoTracking()
            .Where(p => p.IsClosed)
            .Select(p => new { p.FiscalYear, p.Month })
            .ToListAsync(cancellationToken);
        var closedSet = new HashSet<(int, int)>(closedMonths.Select(m => (m.FiscalYear, m.Month)));

        foreach (var e in entries)
        {
            if (e.Lines.Count < 2)
                issues.Add(Blocking(e.Ref, "Une écriture doit comporter au moins deux lignes."));

            if (!e.IsBalanced)
                issues.Add(Blocking(e.Ref,
                    $"Écriture déséquilibrée (débit {e.TotalDebit:0.###} != crédit {e.TotalCredit:0.###})."));

            if (closedSet.Contains((e.EntryDate.Year, e.EntryDate.Month)))
                issues.Add(Blocking(e.Ref, $"Période {e.EntryDate:MM/yyyy} clôturée — rouvrez-la avant l'import."));

            foreach (var line in e.Lines)
            {
                if (!accounts.TryGetValue(line.AccountNumber, out var isActive))
                    issues.Add(Blocking(e.Ref, $"Compte {line.AccountNumber} absent du plan comptable."));
                else if (!isActive)
                    issues.Add(Blocking(e.Ref, $"Compte {line.AccountNumber} désactivé."));
            }
        }

        return issues;
    }

    private static JournalImportPreviewDto BuildPreview(
        IReadOnlyList<ImportEntryDto> entries, List<ImportIssueDto> issues, AccountMappingTable? mapping = null)
    {
        var blocking = issues.Count(i => i.IsBlocking);
        var refsWithIssues = new HashSet<string>(issues.Where(i => i.IsBlocking).Select(i => i.Ref));
        var entriesWithErrors = entries.Count(e => refsWithIssues.Contains(e.Ref));

        return new JournalImportPreviewDto
        {
            TotalEntries = entries.Count,
            TotalLines = entries.Sum(e => e.Lines.Count),
            ValidEntries = entries.Count - entriesWithErrors,
            EntriesWithErrors = entriesWithErrors,
            TotalDebit = entries.Sum(e => e.TotalDebit),
            TotalCredit = entries.Sum(e => e.TotalCredit),
            CanCommit = blocking == 0 && entries.Count > 0,
            Issues = issues,
            Sample = entries.Take(SampleSize).ToList(),
            MappedAccountCount = mapping?.AppliedCount ?? 0,
            UnusedMappings = mapping?.UnusedSources ?? Array.Empty<string>()
        };
    }

    private static ImportIssueDto Blocking(string reference, string message) =>
        new() { Ref = reference, Message = message, IsBlocking = true };
}
