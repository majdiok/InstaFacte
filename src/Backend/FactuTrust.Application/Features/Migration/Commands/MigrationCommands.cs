using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;
using MediatR;

namespace FactuTrust.Application.Features.Migration.Commands;

/// <summary>
/// Commandes de la migration assistée (N1). Handlers volontairement minces, calqués sur
/// <c>ReferenceImportCommands</c> : toute la logique est dans <see cref="IMigrationAssistantService"/>
/// (Infrastructure), ce qui préserve la séparation des couches et la testabilité.
/// Aucune de ces commandes n'écrit en base.
/// </summary>
public sealed record AnalyzeMigrationSourceCommand(string FileName, byte[] Content)
    : IRequest<Result<MigrationAnalysisDto>>;

public sealed class AnalyzeMigrationSourceCommandHandler
    : IRequestHandler<AnalyzeMigrationSourceCommand, Result<MigrationAnalysisDto>>
{
    private readonly IMigrationAssistantService _assistant;

    public AnalyzeMigrationSourceCommandHandler(IMigrationAssistantService assistant) => _assistant = assistant;

    public Task<Result<MigrationAnalysisDto>> Handle(AnalyzeMigrationSourceCommand request, CancellationToken cancellationToken)
        => _assistant.AnalyzeAsync(request.FileName, request.Content, cancellationToken);
}

/// <summary>Suggestion de correspondance de colonnes pour une cible d'import donnée.</summary>
public sealed record SuggestMigrationColumnMappingCommand(
    byte[] Content, JournalImportFormat Format, ReferenceImportTarget Target)
    : IRequest<Result<ColumnMappingSuggestionDto>>;

public sealed class SuggestMigrationColumnMappingCommandHandler
    : IRequestHandler<SuggestMigrationColumnMappingCommand, Result<ColumnMappingSuggestionDto>>
{
    private readonly IMigrationAssistantService _assistant;

    public SuggestMigrationColumnMappingCommandHandler(IMigrationAssistantService assistant) => _assistant = assistant;

    public Task<Result<ColumnMappingSuggestionDto>> Handle(SuggestMigrationColumnMappingCommand request, CancellationToken cancellationToken)
        => _assistant.SuggestColumnMappingAsync(request.Content, request.Format, request.Target, cancellationToken);
}

/// <summary>
/// Suggestion de correspondance de comptes source → plan local. Le mapping de colonnes validé
/// par l'utilisateur (en-tête source → canonique) peut être fourni pour chaîner les étapes.
/// </summary>
public sealed record SuggestMigrationAccountMappingCommand(
    byte[] Content, JournalImportFormat Format,
    IReadOnlyDictionary<string, string>? ManualColumnMapping)
    : IRequest<Result<AccountMappingSuggestionDto>>;

public sealed class SuggestMigrationAccountMappingCommandHandler
    : IRequestHandler<SuggestMigrationAccountMappingCommand, Result<AccountMappingSuggestionDto>>
{
    private readonly IMigrationAssistantService _assistant;

    public SuggestMigrationAccountMappingCommandHandler(IMigrationAssistantService assistant) => _assistant = assistant;

    public Task<Result<AccountMappingSuggestionDto>> Handle(SuggestMigrationAccountMappingCommand request, CancellationToken cancellationToken)
        => _assistant.SuggestAccountMappingAsync(
            request.Content, request.Format, request.ManualColumnMapping, cancellationToken);
}

/// <summary>Détection de doublons de tiers sur un plan tiers à importer (signalisation uniquement).</summary>
public sealed record DetectMigrationDuplicatesCommand(byte[] Content, JournalImportFormat Format)
    : IRequest<Result<ThirdPartyDuplicateDetectionDto>>;

public sealed class DetectMigrationDuplicatesCommandHandler
    : IRequestHandler<DetectMigrationDuplicatesCommand, Result<ThirdPartyDuplicateDetectionDto>>
{
    private readonly IMigrationAssistantService _assistant;

    public DetectMigrationDuplicatesCommandHandler(IMigrationAssistantService assistant) => _assistant = assistant;

    public Task<Result<ThirdPartyDuplicateDetectionDto>> Handle(DetectMigrationDuplicatesCommand request, CancellationToken cancellationToken)
        => _assistant.DetectThirdPartyDuplicatesAsync(request.Content, request.Format, cancellationToken);
}

/// <summary>
/// Réécrit le fichier source en CSV à en-têtes canoniques selon le mapping de colonnes validé.
/// Le résultat est destiné à être soumis aux endpoints d'import existants (preview/commit).
/// </summary>
public sealed record ApplyMigrationColumnMappingCommand(
    byte[] Content, JournalImportFormat Format, ReferenceImportTarget Target,
    IReadOnlyDictionary<string, string> ColumnMapping)
    : IRequest<Result<byte[]>>;

public sealed class ApplyMigrationColumnMappingCommandHandler
    : IRequestHandler<ApplyMigrationColumnMappingCommand, Result<byte[]>>
{
    private readonly IMigrationAssistantService _assistant;

    public ApplyMigrationColumnMappingCommandHandler(IMigrationAssistantService assistant) => _assistant = assistant;

    public Task<Result<byte[]>> Handle(ApplyMigrationColumnMappingCommand request, CancellationToken cancellationToken)
        => _assistant.ApplyColumnMappingAsync(
            request.Content, request.Format, request.Target, request.ColumnMapping, cancellationToken);
}
