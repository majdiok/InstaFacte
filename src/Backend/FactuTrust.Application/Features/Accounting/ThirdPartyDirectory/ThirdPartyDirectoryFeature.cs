using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.Configuration;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;
using AuditActions = FactuTrust.Domain.Entities.AuditActions;
using MediatR;
using Microsoft.Extensions.Options;

namespace FactuTrust.Application.Features.Accounting.ThirdPartyDirectory;

// Plan tiers unifié : répertoire clients+fournisseurs, fiche comptable par tiers et génération
// des codes auxiliaires. Toute la feature est gardée par AccountingSettings.ThirdPartyDirectoryEnabled.

internal static class ThirdPartyDirectoryGuard
{
    public static Error Disabled { get; } =
        Error.Validation("ThirdPartyDirectory", "Le plan tiers n'est pas activé.");

    public static Result<ThirdPartyKind> ParseKind(int kind) =>
        kind is (int)ThirdPartyKind.Client or (int)ThirdPartyKind.Supplier
            ? Result.Success((ThirdPartyKind)kind)
            : Result.Failure<ThirdPartyKind>(Error.Validation("Kind", "Type de tiers invalide (1 = client, 2 = fournisseur)."));
}

// ── Queries ────────────────────────────────────────────────────────────────

public sealed record GetThirdPartyDirectoryQuery(int? Kind, string? Search, bool IncludeInactive, int Page, int PageSize)
    : IRequest<Result<ThirdPartyDirectoryResultDto>>;

public sealed class GetThirdPartyDirectoryQueryHandler
    : IRequestHandler<GetThirdPartyDirectoryQuery, Result<ThirdPartyDirectoryResultDto>>
{
    private readonly IThirdPartyDirectoryService _directory;
    private readonly AccountingSettings _settings;

    public GetThirdPartyDirectoryQueryHandler(IThirdPartyDirectoryService directory, IOptions<AccountingSettings> settings)
    {
        _directory = directory;
        _settings = settings.Value;
    }

    public Task<Result<ThirdPartyDirectoryResultDto>> Handle(GetThirdPartyDirectoryQuery request, CancellationToken cancellationToken)
    {
        if (!_settings.ThirdPartyDirectoryEnabled)
            return Task.FromResult(Result.Failure<ThirdPartyDirectoryResultDto>(ThirdPartyDirectoryGuard.Disabled));

        ThirdPartyKind? kind = null;
        if (request.Kind.HasValue)
        {
            var parsed = ThirdPartyDirectoryGuard.ParseKind(request.Kind.Value);
            if (parsed.IsFailure)
                return Task.FromResult(Result.Failure<ThirdPartyDirectoryResultDto>(parsed.Error));
            kind = parsed.Value;
        }

        return _directory.GetDirectoryAsync(kind, request.Search, request.IncludeInactive, request.Page, request.PageSize, cancellationToken);
    }
}

public sealed record GetThirdPartyProfileQuery(int Kind, Guid ThirdPartyId) : IRequest<Result<ThirdPartyProfileDto>>;

public sealed class GetThirdPartyProfileQueryHandler : IRequestHandler<GetThirdPartyProfileQuery, Result<ThirdPartyProfileDto>>
{
    private readonly IThirdPartyDirectoryService _directory;
    private readonly AccountingSettings _settings;

    public GetThirdPartyProfileQueryHandler(IThirdPartyDirectoryService directory, IOptions<AccountingSettings> settings)
    {
        _directory = directory;
        _settings = settings.Value;
    }

    public Task<Result<ThirdPartyProfileDto>> Handle(GetThirdPartyProfileQuery request, CancellationToken cancellationToken)
    {
        if (!_settings.ThirdPartyDirectoryEnabled)
            return Task.FromResult(Result.Failure<ThirdPartyProfileDto>(ThirdPartyDirectoryGuard.Disabled));

        var kind = ThirdPartyDirectoryGuard.ParseKind(request.Kind);
        if (kind.IsFailure)
            return Task.FromResult(Result.Failure<ThirdPartyProfileDto>(kind.Error));

        return _directory.GetProfileAsync(kind.Value, request.ThirdPartyId, cancellationToken);
    }
}

// ── Commands ───────────────────────────────────────────────────────────────

public sealed record UpsertThirdPartyProfileCommand(int Kind, Guid ThirdPartyId, UpsertThirdPartyProfileRequest Request)
    : IRequest<Result>;

public sealed class UpsertThirdPartyProfileCommandHandler : IRequestHandler<UpsertThirdPartyProfileCommand, Result>
{
    private readonly IThirdPartyDirectoryService _directory;
    private readonly IAuditService _auditService;
    private readonly AccountingSettings _settings;

    public UpsertThirdPartyProfileCommandHandler(
        IThirdPartyDirectoryService directory, IAuditService auditService, IOptions<AccountingSettings> settings)
    {
        _directory = directory;
        _auditService = auditService;
        _settings = settings.Value;
    }

    public async Task<Result> Handle(UpsertThirdPartyProfileCommand request, CancellationToken cancellationToken)
    {
        if (!_settings.ThirdPartyDirectoryEnabled)
            return Result.Failure(ThirdPartyDirectoryGuard.Disabled);

        var kind = ThirdPartyDirectoryGuard.ParseKind(request.Kind);
        if (kind.IsFailure)
            return Result.Failure(kind.Error);

        var result = await _directory.UpsertProfileAsync(kind.Value, request.ThirdPartyId, request.Request, cancellationToken);
        if (result.IsFailure)
            return result;

        await _auditService.LogAsync(AuditActions.Accounting.ThirdPartyProfileSaved, "ThirdPartyAccountingProfile",
            request.ThirdPartyId,
            newValues: new { request.Kind, request.Request.AuxiliaryCode, request.Request.CollectiveAccountNumber, request.Request.PaymentTermDays },
            cancellationToken: cancellationToken);

        return Result.Success();
    }
}

public sealed record EnsureAuxiliaryCodesCommand : IRequest<Result<int>>;

public sealed class EnsureAuxiliaryCodesCommandHandler : IRequestHandler<EnsureAuxiliaryCodesCommand, Result<int>>
{
    private readonly IThirdPartyDirectoryService _directory;
    private readonly IAuditService _auditService;
    private readonly AccountingSettings _settings;

    public EnsureAuxiliaryCodesCommandHandler(
        IThirdPartyDirectoryService directory, IAuditService auditService, IOptions<AccountingSettings> settings)
    {
        _directory = directory;
        _auditService = auditService;
        _settings = settings.Value;
    }

    public async Task<Result<int>> Handle(EnsureAuxiliaryCodesCommand request, CancellationToken cancellationToken)
    {
        if (!_settings.ThirdPartyDirectoryEnabled)
            return Result.Failure<int>(ThirdPartyDirectoryGuard.Disabled);

        var result = await _directory.EnsureAuxiliaryCodesAsync(cancellationToken);
        if (result.IsFailure)
            return result;

        if (result.Value > 0)
            await _auditService.LogAsync(AuditActions.Accounting.ThirdPartyCodesGenerated, "ThirdPartyAccountingProfile", null,
                newValues: new { Created = result.Value }, cancellationToken: cancellationToken);

        return result;
    }
}
