using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Features.AI.Export.PowerPoint.DTOs;
using FactuTrust.Application.Features.AI.Export.PowerPoint.Models;
using FactuTrust.Application.Features.AI.Export.PowerPoint.Services;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities.AI;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace FactuTrust.Application.Features.AI.Export.PowerPoint.Commands;

/// <summary>
/// Orchestrates a full PowerPoint export round-trip:
/// validation → tenant/user ownership check → generation → storage → audit.
/// </summary>
/// <remarks>
/// The command always persists the export to disk (via <see cref="IExportStorageService"/>) and
/// returns a signed download URL. The controller may also choose to stream the bytes inline for
/// small decks, in which case the persisted copy still serves as a recoverable audit artefact.
/// </remarks>
public sealed record GeneratePowerPointCommand(
    PowerPointExportRequestDto Request,
    Guid UserId,
    Guid TenantId,
    string? DownloadUrlTemplate)
    : IRequest<Result<GeneratePowerPointCommandResult>>;

/// <summary>
/// Successful output of <see cref="GeneratePowerPointCommand"/>. The bytes are retained for inline
/// streaming if desired; the storage metadata exposes the signed download URL otherwise.
/// </summary>
public sealed record GeneratePowerPointCommandResult(
    byte[] Content,
    string FileName,
    int SlideCount,
    long SizeBytes,
    TimeSpan Duration,
    Guid ExportId,
    string DownloadUrl,
    DateTime ExpiresAt,
    DateTime GeneratedAt);

public sealed class GeneratePowerPointCommandValidator : AbstractValidator<GeneratePowerPointCommand>
{
    public GeneratePowerPointCommandValidator()
    {
        RuleFor(c => c.UserId).NotEmpty();
        RuleFor(c => c.TenantId).NotEmpty();
        RuleFor(c => c.Request).NotNull().SetValidator(new PowerPointExportRequestValidator());
    }
}

public sealed class PowerPointExportRequestValidator : AbstractValidator<PowerPointExportRequestDto>
{
    public PowerPointExportRequestValidator()
    {
        RuleFor(r => r.Title)
            .NotEmpty().WithMessage("Le titre de la présentation est obligatoire.")
            .MaximumLength(PowerPointDeckLimits.TitleMaxLength)
            .WithMessage($"Le titre ne peut pas dépasser {PowerPointDeckLimits.TitleMaxLength} caractères.");

        RuleFor(r => r.Subtitle)
            .MaximumLength(PowerPointDeckLimits.SubtitleMaxLength)
            .WithMessage($"Le sous-titre ne peut pas dépasser {PowerPointDeckLimits.SubtitleMaxLength} caractères.")
            .When(r => r.Subtitle is not null);

        RuleFor(r => r.AuthorName)
            .MaximumLength(PowerPointDeckLimits.AuthorNameMaxLength)
            .WithMessage($"Le nom de l'auteur ne peut pas dépasser {PowerPointDeckLimits.AuthorNameMaxLength} caractères.")
            .When(r => r.AuthorName is not null);

        RuleFor(r => r.Locale)
            .MaximumLength(PowerPointDeckLimits.LocaleMaxLength)
            .Matches("^[A-Za-z]{2,3}(-[A-Za-z0-9]{2,8})?$")
            .WithMessage("Format de locale invalide. Exemple attendu : fr-TN.")
            .When(r => !string.IsNullOrWhiteSpace(r.Locale));

        RuleFor(r => r.Template).IsInEnum();
        RuleFor(r => r.Orientation).IsInEnum();

        RuleFor(r => r.Responses)
            .NotNull()
            .Must(list => list.Count >= PowerPointDeckLimits.MinResponses)
            .WithMessage($"Sélectionnez au moins {PowerPointDeckLimits.MinResponses} réponse.")
            .Must(list => list.Count <= PowerPointDeckLimits.MaxResponses)
            .WithMessage($"Vous ne pouvez pas inclure plus de {PowerPointDeckLimits.MaxResponses} réponses dans une seule présentation.");

        RuleForEach(r => r.Responses).SetValidator(new ResponseSelectionValidator());

        RuleFor(r => r.Responses)
            .Must(list => list.Select(x => x.MessageId).Distinct().Count() == list.Count)
            .WithMessage("Une même réponse ne peut pas être ajoutée plusieurs fois.")
            .When(r => r.Responses is { Count: > 0 });
    }
}

public sealed class ResponseSelectionValidator : AbstractValidator<ResponseSelectionDto>
{
    public ResponseSelectionValidator()
    {
        RuleFor(r => r.ConversationId).NotEmpty();
        RuleFor(r => r.MessageId).NotEmpty();
        RuleFor(r => r.CustomTitle)
            .MaximumLength(PowerPointDeckLimits.CustomTitleMaxLength)
            .When(r => r.CustomTitle is not null);
    }
}

public sealed class GeneratePowerPointCommandHandler
    : IRequestHandler<GeneratePowerPointCommand, Result<GeneratePowerPointCommandResult>>
{
    private readonly IPowerPointGenerator _generator;
    private readonly IConversationRepository _conversationRepository;
    private readonly IExportStorageService _storage;
    private readonly IAiExportAuditRepository _auditRepository;
    private readonly ILogger<GeneratePowerPointCommandHandler> _logger;

    public GeneratePowerPointCommandHandler(
        IPowerPointGenerator generator,
        IConversationRepository conversationRepository,
        IExportStorageService storage,
        IAiExportAuditRepository auditRepository,
        ILogger<GeneratePowerPointCommandHandler> logger)
    {
        _generator = generator;
        _conversationRepository = conversationRepository;
        _storage = storage;
        _auditRepository = auditRepository;
        _logger = logger;
    }

    public async Task<Result<GeneratePowerPointCommandResult>> Handle(
        GeneratePowerPointCommand command,
        CancellationToken cancellationToken)
    {
        var startedAt = DateTime.UtcNow;

        var ownershipCheck = await EnsureOwnershipAsync(command, cancellationToken);
        if (ownershipCheck.IsFailure)
        {
            await PersistAuditAsync(
                command,
                slideCount: 0,
                sizeBytes: 0,
                durationMs: 0,
                storagePath: null,
                success: false,
                failureReason: ownershipCheck.Error.Description,
                cancellationToken);
            return Result.Failure<GeneratePowerPointCommandResult>(ownershipCheck.Error);
        }

        PowerPointGenerationResult generationResult;
        try
        {
            generationResult = await _generator.GenerateAsync(command.Request, command.UserId, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "PowerPoint generation failed. UserId={UserId} TenantId={TenantId} ResponseCount={ResponseCount}",
                command.UserId,
                command.TenantId,
                command.Request.Responses.Count);
            await PersistAuditAsync(
                command,
                slideCount: 0,
                sizeBytes: 0,
                durationMs: (int)(DateTime.UtcNow - startedAt).TotalMilliseconds,
                storagePath: null,
                success: false,
                failureReason: "Generation engine error",
                cancellationToken);
            return Result.Failure<GeneratePowerPointCommandResult>(
                new Error("PowerPoint.GenerationFailed", "Impossible de générer la présentation. Réessayez dans un instant."));
        }

        var stored = await _storage.StoreAsync(
            command.TenantId,
            command.UserId,
            "pptx",
            generationResult.FileName,
            generationResult.Content,
            cancellationToken);

        var downloadUrl = BuildDownloadUrl(command.DownloadUrlTemplate, stored.ExportId, stored.Token);

        var duration = DateTime.UtcNow - startedAt;
        await PersistAuditAsync(
            command,
            slideCount: generationResult.SlideCount,
            sizeBytes: generationResult.SizeBytes,
            durationMs: (int)duration.TotalMilliseconds,
            storagePath: stored.StoragePath,
            success: true,
            failureReason: null,
            cancellationToken);

        _logger.LogInformation(
            "PowerPoint export generated. ExportId={ExportId} UserId={UserId} TenantId={TenantId} " +
            "Template={Template} Slides={SlideCount} SizeBytes={SizeBytes} DurationMs={DurationMs}",
            stored.ExportId,
            command.UserId,
            command.TenantId,
            command.Request.Template,
            generationResult.SlideCount,
            generationResult.SizeBytes,
            (int)duration.TotalMilliseconds);

        return Result.Success(new GeneratePowerPointCommandResult(
            Content: generationResult.Content,
            FileName: generationResult.FileName,
            SlideCount: generationResult.SlideCount,
            SizeBytes: generationResult.SizeBytes,
            Duration: duration,
            ExportId: stored.ExportId,
            DownloadUrl: downloadUrl,
            ExpiresAt: stored.ExpiresAt,
            GeneratedAt: startedAt));
    }

    private async Task<Result> EnsureOwnershipAsync(GeneratePowerPointCommand command, CancellationToken cancellationToken)
    {
        var conversationIds = command.Request.Responses
            .Select(r => r.ConversationId)
            .Distinct()
            .ToArray();

        var requestedMessages = command.Request.Responses
            .Select(r => (r.ConversationId, r.MessageId))
            .ToHashSet();

        foreach (var conversationId in conversationIds)
        {
            var conversation = await _conversationRepository.GetByIdAsync(conversationId, cancellationToken);
            if (conversation is null)
                return Result.Failure(new Error(
                    "PowerPoint.ConversationNotFound",
                    "Une des conversations sélectionnées est introuvable."));

            if (conversation.UserId != command.UserId)
                return Result.Failure(Error.Forbidden(
                    "Vous n'êtes pas autorisé à exporter une conversation d'un autre utilisateur."));

            var existingMessageIds = conversation.Messages.Select(m => m.Id).ToHashSet();
            var requested = requestedMessages
                .Where(t => t.ConversationId == conversationId)
                .Select(t => t.MessageId);

            foreach (var messageId in requested)
            {
                if (!existingMessageIds.Contains(messageId))
                    return Result.Failure(new Error(
                        "PowerPoint.MessageNotFound",
                        "Une des réponses sélectionnées n'existe plus."));
            }
        }

        return Result.Success();
    }

    private async Task PersistAuditAsync(
        GeneratePowerPointCommand command,
        int slideCount,
        long sizeBytes,
        int durationMs,
        string? storagePath,
        bool success,
        string? failureReason,
        CancellationToken cancellationToken)
    {
        try
        {
            var audit = AiExportAudit.Create(
                userId: command.UserId,
                format: "pptx",
                template: command.Request.Template.ToString(),
                title: command.Request.Title,
                responseCount: command.Request.Responses.Count,
                slideCount: slideCount,
                sizeBytes: sizeBytes,
                durationMs: durationMs,
                storagePath: storagePath,
                success: success,
                conversationIds: command.Request.Responses.Select(r => r.ConversationId),
                messageIds: command.Request.Responses.Select(r => r.MessageId),
                failureReason: failureReason);

            await _auditRepository.AddAsync(audit, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Failed to persist AI export audit row. UserId={UserId} TenantId={TenantId}",
                command.UserId,
                command.TenantId);
        }
    }

    private static string BuildDownloadUrl(string? template, Guid exportId, string token)
    {
        if (string.IsNullOrWhiteSpace(template))
            return $"/api/ai/exports/powerpoint/{exportId:D}?token={token}";

        return template
            .Replace("{exportId}", exportId.ToString("D"), StringComparison.Ordinal)
            .Replace("{token}", Uri.EscapeDataString(token), StringComparison.Ordinal);
    }
}
