using System.Text.Json;
using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Billing;
using FactuTrust.Domain.Common;
using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FactuTrust.Infrastructure.Services.Billing;

/// <summary>
/// Lot C6 — CRUD campagnes dunning. Sérialise les steps en JSON dans la colonne
/// <c>StepsJson</c>. Garantit qu'au plus une seule campagne <c>IsActive=true</c> existe
/// (déactivation transparente des autres lors d'un Activate).
/// </summary>
public sealed class DunningCampaignService : IDunningCampaignService
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    private readonly MasterDbContext _db;

    public DunningCampaignService(MasterDbContext db)
    {
        _db = db;
    }

    public async Task<DunningCampaignsListDto> ListAsync(CancellationToken cancellationToken = default)
    {
        var rows = await _db.DunningCampaigns.AsNoTracking()
            .OrderByDescending(c => c.IsActive)
            .ThenByDescending(c => c.CreatedAt)
            .ToListAsync(cancellationToken);
        var items = rows.Select(Map).ToList();
        return new DunningCampaignsListDto
        {
            Items = items,
            ActiveCount = items.Count(i => i.IsActive)
        };
    }

    public async Task<Result<DunningCampaignDto>> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _db.DunningCampaigns.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (entity is null) return Result.Failure<DunningCampaignDto>(Error.NotFound(nameof(DunningCampaign), id));
        return Result.Success(Map(entity));
    }

    public async Task<Result<DunningCampaignDto>> CreateAsync(
        CreateDunningCampaignRequest request,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        if (request is null) return Result.Failure<DunningCampaignDto>(Error.Validation("Request", "Requête invalide"));
        if (request.Steps.Count == 0)
            return Result.Failure<DunningCampaignDto>(Error.Validation("Steps", "Au moins une étape requise"));

        var json = JsonSerializer.Serialize(request.Steps.Select(s => new DunningStep
        {
            DaysAfterDueDate = s.DaysAfterDueDate,
            Action = s.Action,
            EmailTemplateCode = s.EmailTemplateCode,
            Label = s.Label
        }).OrderBy(s => s.DaysAfterDueDate).ToList(), JsonOpts);

        DunningCampaign campaign;
        try
        {
            campaign = DunningCampaign.Create(request.Name, request.Description, json, actorUserId, isActive: false);
        }
        catch (ArgumentException ex)
        {
            return Result.Failure<DunningCampaignDto>(Error.Validation("Campaign", ex.Message));
        }
        campaign.SetAuditInfo(actorUserId.ToString());
        _db.DunningCampaigns.Add(campaign);
        await _db.SaveChangesAsync(cancellationToken);

        if (request.ActivateImmediately)
        {
            await ActivateInternalAsync(campaign, cancellationToken);
        }

        return Result.Success(Map(campaign));
    }

    public async Task<Result<DunningCampaignDto>> UpdateAsync(
        Guid id,
        UpdateDunningCampaignRequest request,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        if (request is null) return Result.Failure<DunningCampaignDto>(Error.Validation("Request", "Requête invalide"));

        var entity = await _db.DunningCampaigns.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (entity is null) return Result.Failure<DunningCampaignDto>(Error.NotFound(nameof(DunningCampaign), id));

        var json = JsonSerializer.Serialize(request.Steps.Select(s => new DunningStep
        {
            DaysAfterDueDate = s.DaysAfterDueDate,
            Action = s.Action,
            EmailTemplateCode = s.EmailTemplateCode,
            Label = s.Label
        }).OrderBy(s => s.DaysAfterDueDate).ToList(), JsonOpts);

        try
        {
            entity.Update(request.Name, request.Description, json);
        }
        catch (ArgumentException ex)
        {
            return Result.Failure<DunningCampaignDto>(Error.Validation("Campaign", ex.Message));
        }
        entity.SetAuditInfo(actorUserId.ToString(), isUpdate: true);
        await _db.SaveChangesAsync(cancellationToken);
        return Result.Success(Map(entity));
    }

    public async Task<Result> ActivateAsync(Guid id, Guid actorUserId, CancellationToken cancellationToken = default)
    {
        var entity = await _db.DunningCampaigns.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (entity is null) return Result.Failure(Error.NotFound(nameof(DunningCampaign), id));
        await ActivateInternalAsync(entity, cancellationToken);
        entity.SetAuditInfo(actorUserId.ToString(), isUpdate: true);
        await _db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> DeactivateAsync(Guid id, Guid actorUserId, CancellationToken cancellationToken = default)
    {
        var entity = await _db.DunningCampaigns.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (entity is null) return Result.Failure(Error.NotFound(nameof(DunningCampaign), id));
        entity.Deactivate();
        entity.SetAuditInfo(actorUserId.ToString(), isUpdate: true);
        await _db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> EnsureDefaultExistsAsync(CancellationToken cancellationToken = default)
    {
        var anyActive = await _db.DunningCampaigns.AnyAsync(c => c.IsActive, cancellationToken);
        if (anyActive) return Result.Success();

        // S'il existe une campagne nommée "default" inactive, on l'active.
        var existingDefault = await _db.DunningCampaigns.FirstOrDefaultAsync(c => c.Name == "Campagne par défaut", cancellationToken);
        if (existingDefault is not null)
        {
            existingDefault.Activate();
            await _db.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }

        var campaign = DunningCampaign.Create(
            name: "Campagne par défaut",
            description: "4 étapes : J+1 rappel doux, J+3 second rappel + PastDue, J+7 avertissement, J+14 suspension.",
            stepsJson: DunningCampaign.DefaultStepsJson(),
            createdByUserId: Guid.Empty,
            isActive: true);
        _db.DunningCampaigns.Add(campaign);
        await _db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    private async Task ActivateInternalAsync(DunningCampaign target, CancellationToken cancellationToken)
    {
        var others = await _db.DunningCampaigns.Where(c => c.Id != target.Id && c.IsActive).ToListAsync(cancellationToken);
        foreach (var c in others) c.Deactivate();
        target.Activate();
    }

    internal static DunningCampaignDto Map(DunningCampaign c)
    {
        var steps = ParseSteps(c.StepsJson);
        return new DunningCampaignDto
        {
            Id = c.Id,
            Name = c.Name,
            Description = c.Description,
            Steps = steps.Select(s => new DunningStepDto
            {
                DaysAfterDueDate = s.DaysAfterDueDate,
                Action = s.Action,
                ActionDisplay = s.Action.ToDisplayString(),
                EmailTemplateCode = s.EmailTemplateCode,
                Label = s.Label
            }).ToList(),
            IsActive = c.IsActive,
            CreatedAt = c.CreatedAt,
            UpdatedAt = c.UpdatedAt
        };
    }

    public static List<DunningStep> ParseSteps(string stepsJson)
    {
        if (string.IsNullOrWhiteSpace(stepsJson)) return new List<DunningStep>();
        try
        {
            return JsonSerializer.Deserialize<List<DunningStep>>(stepsJson, JsonOpts) ?? new List<DunningStep>();
        }
        catch
        {
            return new List<DunningStep>();
        }
    }
}
