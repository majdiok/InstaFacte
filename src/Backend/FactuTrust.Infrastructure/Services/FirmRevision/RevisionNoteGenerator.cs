using System.Text.Json;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.Configuration;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.AI;
using FactuTrust.Application.Features.Accounting.Audit;
using FactuTrust.Application.Features.Accounting.Audit.Narrative;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities.AccountingAudit;
using FactuTrust.Domain.Enums;
using FactuTrust.Infrastructure.MultiTenancy;
using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FactuTrust.Infrastructure.Services.FirmRevision;

/// <summary>
/// Produit et persiste le dossier de révision d'un contrôle.
///
/// <para><b>La rédaction est facultative par conception.</b> Le dossier déterministe est produit
/// d'abord, complet et exploitable ; la rédaction ne fait que remplacer de la prose. Si le modèle
/// est éteint, indisponible, trop lent ou incompréhensible, le dossier sort quand même — avec
/// <c>AiGenerated = false</c> et le motif du repli.</para>
/// </summary>
public interface IRevisionNoteGenerator
{
    Task<Result<FirmRevisionNoteDto>> GenerateAsync(
        string connectionString,
        string companyName,
        int fiscalYear,
        Guid? userId,
        string? userName,
        CancellationToken cancellationToken = default);
}

/// <inheritdoc cref="IRevisionNoteGenerator"/>
public sealed class RevisionNoteGenerator : IRevisionNoteGenerator
{
    private readonly ITenantDbContextFactory _contextFactory;
    private readonly IAiNarrativeCompletionService _narrative;
    private readonly AccountingFirmsOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<RevisionNoteGenerator> _logger;

    public RevisionNoteGenerator(
        ITenantDbContextFactory contextFactory,
        IAiNarrativeCompletionService narrative,
        IOptions<AccountingFirmsOptions> options,
        TimeProvider timeProvider,
        ILogger<RevisionNoteGenerator> logger)
    {
        _contextFactory = contextFactory;
        _narrative = narrative;
        _options = options.Value;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<Result<FirmRevisionNoteDto>> GenerateAsync(
        string connectionString,
        string companyName,
        int fiscalYear,
        Guid? userId,
        string? userName,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            return Result.Failure<FirmRevisionNoteDto>(
                Error.Validation("Dossier", "Base du dossier inaccessible."));

        await using var ctx = _contextFactory.CreateIsolatedContext(connectionString);

        var run = await ctx.Set<AccountingControlRun>()
            .Where(r => r.FiscalYear == fiscalYear && r.Status == ControlRunStatus.Completed)
            .OrderByDescending(r => r.CompletedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (run is null)
            return Result.Failure<FirmRevisionNoteDto>(Error.Validation(
                "Contrôle", "Aucun contrôle terminé sur cet exercice : lancez un balayage d'abord."));

        var anomalies = await ctx.Set<AccountingAnomaly>().AsNoTracking()
            .Where(a => a.Run.FiscalYear == fiscalYear
                        && a.Status != AnomalyStatus.Corrected
                        && a.Status != AnomalyStatus.Ignored)
            .OrderByDescending(a => a.Severity)
            .ThenByDescending(a => a.Amount)
            .Select(a => new
            {
                a.Id, a.RuleCode, a.ModuleCode, a.Severity, a.Title, a.Description,
                a.Amount, a.AccountRef, a.RecommendationsJson
            })
            .ToListAsync(cancellationToken);

        var facts = anomalies.Select(a => new RevisionAnomalyFacts(
            a.Id, a.RuleCode, a.ModuleCode, a.Severity, a.Title, a.Description ?? string.Empty,
            a.Amount, a.AccountRef, PieceRef: null,
            Recommendations: DeserializeRecommendations(a.RecommendationsJson))).ToList();

        // 1. Le dossier déterministe, toujours produit.
        var assembly = RevisionDossierGuard.BuildDeterministic(facts);
        string? modelUsed = null;

        // 2. La rédaction, si elle est activée et qu'il y a matière.
        if (_options.FirmRevisionAiEnabled && facts.Count > 0)
            (assembly, modelUsed) = await TryWriteAsync(companyName, fiscalYear, facts, cancellationToken);

        var note = await UpsertAsync(ctx, run, fiscalYear, assembly, modelUsed, userId, userName, cancellationToken);
        return Result.Success(RevisionNoteMapper.ToDto(note));
    }

    /// <summary>
    /// Tente la rédaction. <b>Ne lève jamais</b> : toute panne rend l'assemblage déterministe assorti
    /// du motif. Le réviseur préfère une note factuelle à une page d'erreur.
    /// </summary>
    private async Task<(RevisionDossierAssembly Assembly, string? ModelUsed)> TryWriteAsync(
        string companyName,
        int fiscalYear,
        IReadOnlyList<RevisionAnomalyFacts> facts,
        CancellationToken cancellationToken)
    {
        // Au-delà du plafond, les anomalies restantes gardent leur description déterministe :
        // mieux vaut une note partiellement rédigée qu'un prompt tronqué au milieu d'une liste.
        var cap = Math.Clamp(_options.FirmRevisionMaxAnomaliesInPrompt, 1, 200);
        var submitted = facts.Take(cap).ToList();

        try
        {
            var timeout = TimeSpan.FromSeconds(Math.Clamp(_options.FirmRevisionAiTimeoutSeconds, 10, 600));

            var completion = await _narrative.CompleteAsync(
                new AiNarrativeCompletionRequest
                {
                    SystemPrompt = RevisionDossierPrompt.System,
                    UserPrompt = RevisionDossierPrompt.BuildUserPrompt(companyName, fiscalYear, submitted),
                    OutputSchema = RevisionDossierSchema.Instance,
                    ErrorCode = "RevisionDossier",
                    MaxOutputTokens = 4096,
                    Timeout = timeout
                },
                cancellationToken);

            if (completion.IsFailure)
                return (RevisionDossierGuard.BuildDeterministic(facts, completion.Error.Description), null);

            var parsed = RevisionDossierGuard.TryParse(completion.Value.RawContent, out var parseFailure);
            if (parsed is null)
                return (RevisionDossierGuard.BuildDeterministic(facts, parseFailure), completion.Value.ModelUsed);

            // L'assemblage porte sur TOUTES les anomalies, pas seulement celles soumises :
            // les non soumises gardent leur description déterministe (garde-fou n°5).
            return (RevisionDossierGuard.Assemble(facts, parsed), completion.Value.ModelUsed);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Dossier de révision : rédaction impossible, repli déterministe.");
            return (RevisionDossierGuard.BuildDeterministic(facts, "Rédaction indisponible."), null);
        }
    }

    /// <summary>Une note par contrôle : régénérer remplace, on n'empile pas les versions.</summary>
    private async Task<AccountingRevisionNote> UpsertAsync(
        TenantDbContext ctx,
        AccountingControlRun run,
        int fiscalYear,
        RevisionDossierAssembly assembly,
        string? modelUsed,
        Guid? userId,
        string? userName,
        CancellationToken cancellationToken)
    {
        var note = await ctx.Set<AccountingRevisionNote>()
            .FirstOrDefaultAsync(n => n.RunId == run.Id, cancellationToken);

        if (note is null)
        {
            note = new AccountingRevisionNote { RunId = run.Id };
            ctx.Set<AccountingRevisionNote>().Add(note);
        }

        note.FiscalYear = fiscalYear;
        note.PeriodFrom = run.PeriodFrom;
        note.PeriodTo = run.PeriodTo;
        note.GeneratedAt = _timeProvider.GetUtcNow().UtcDateTime;
        note.GeneratedByUserId = userId;
        note.GeneratedByUserName = userName;
        note.AiGenerated = assembly.AiGenerated;
        note.ModelRef = modelUsed;
        note.FallbackReason = assembly.FallbackReason;
        note.ExecutiveSummary = assembly.ExecutiveSummary;
        note.ItemsJson = RevisionNoteMapper.SerializeItems(assembly.Items);
        note.TotalImpactAmount = assembly.TotalImpactAmount;
        note.AnomalyCount = assembly.AnomalyCount;
        note.BlockingCount = assembly.BlockingCount;
        note.SetAuditInfo(userName ?? "system", note.CreatedAt != default);

        await ctx.SaveChangesAsync(cancellationToken);
        return note;
    }

    /// <summary>
    /// Les recommandations sont stockées en JSON par le moteur. Une valeur illisible ne doit pas
    /// empêcher la génération : elle n'est qu'une piste offerte au rédacteur.
    /// </summary>
    private static IReadOnlyList<string> DeserializeRecommendations(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return Array.Empty<string>();

        try
        {
            return JsonSerializer.Deserialize<List<string>>(json) ?? [];
        }
        catch (JsonException)
        {
            return Array.Empty<string>();
        }
    }
}
