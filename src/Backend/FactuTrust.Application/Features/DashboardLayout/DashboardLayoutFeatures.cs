using System.Text.Json;
using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;
using MediatR;

namespace FactuTrust.Application.Features.DashboardLayout;

/// <summary>
/// Sérialisation / nettoyage de l'ordre des blocs du tableau de bord.
/// Le backend reste agnostique de la taxonomie des blocs (gérée côté frontend) :
/// il se contente de normaliser (trim, dédoublonnage, bornes) et de persister.
/// </summary>
internal static class DashboardLayoutSerializer
{
    private const int MaxBlocks = 50;
    private const int MaxIdLength = 64;

    public static IReadOnlyList<string> Sanitize(IEnumerable<string>? input)
    {
        if (input is null) return Array.Empty<string>();

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var result = new List<string>();
        foreach (var raw in input)
        {
            var id = raw?.Trim();
            if (string.IsNullOrEmpty(id) || id.Length > MaxIdLength) continue;
            if (seen.Add(id)) result.Add(id);
            if (result.Count >= MaxBlocks) break;
        }
        return result;
    }

    public static string Serialize(IReadOnlyList<string> order) => JsonSerializer.Serialize(order);

    public static IReadOnlyList<string> Deserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return Array.Empty<string>();
        try
        {
            return JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
        }
        catch (JsonException)
        {
            return Array.Empty<string>();
        }
    }
}

// --- Lecture de la disposition de l'utilisateur courant ---

public sealed record GetMyDashboardLayoutQuery : IRequest<DashboardLayoutDto>;

public sealed class GetMyDashboardLayoutQueryHandler
    : IRequestHandler<GetMyDashboardLayoutQuery, DashboardLayoutDto>
{
    private readonly IUserDashboardLayoutRepository _repository;
    private readonly ICurrentUser _currentUser;

    public GetMyDashboardLayoutQueryHandler(
        IUserDashboardLayoutRepository repository,
        ICurrentUser currentUser)
    {
        _repository = repository;
        _currentUser = currentUser;
    }

    public async Task<DashboardLayoutDto> Handle(GetMyDashboardLayoutQuery request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId;
        var tenantId = _currentUser.TenantId;
        if (userId is null || userId == Guid.Empty || tenantId is null || tenantId == Guid.Empty)
        {
            // Sans contexte utilisateur, renvoyer une disposition vide : le frontend
            // appliquera l'ordre par défaut via reconcileOrder.
            return new DashboardLayoutDto(Array.Empty<string>());
        }

        var layout = await _repository.GetAsync(tenantId.Value, userId.Value, cancellationToken);
        return new DashboardLayoutDto(DashboardLayoutSerializer.Deserialize(layout?.LayoutJson));
    }
}

// --- Enregistrement de la disposition de l'utilisateur courant (upsert) ---

public sealed record SaveMyDashboardLayoutCommand(SaveDashboardLayoutRequest Request)
    : IRequest<Result<DashboardLayoutDto>>;

public sealed class SaveMyDashboardLayoutCommandHandler
    : IRequestHandler<SaveMyDashboardLayoutCommand, Result<DashboardLayoutDto>>
{
    private readonly IUserDashboardLayoutRepository _repository;
    private readonly ICurrentUser _currentUser;

    public SaveMyDashboardLayoutCommandHandler(
        IUserDashboardLayoutRepository repository,
        ICurrentUser currentUser)
    {
        _repository = repository;
        _currentUser = currentUser;
    }

    public async Task<Result<DashboardLayoutDto>> Handle(SaveMyDashboardLayoutCommand command, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId;
        var tenantId = _currentUser.TenantId;
        if (userId is null || userId == Guid.Empty || tenantId is null || tenantId == Guid.Empty)
            return Result.Failure<DashboardLayoutDto>(Error.Unauthorized("Aucun utilisateur authentifié."));

        var order = DashboardLayoutSerializer.Sanitize(command.Request.BlockOrder);
        var json = DashboardLayoutSerializer.Serialize(order);

        var existing = await _repository.GetAsync(tenantId.Value, userId.Value, cancellationToken);
        if (existing is null)
        {
            existing = UserDashboardLayout.Create(tenantId.Value, userId.Value, json);
            await _repository.AddAsync(existing, cancellationToken);
        }
        else
        {
            existing.SetLayout(json);
            await _repository.UpdateAsync(existing, cancellationToken);
        }

        return Result.Success(new DashboardLayoutDto(order));
    }
}
