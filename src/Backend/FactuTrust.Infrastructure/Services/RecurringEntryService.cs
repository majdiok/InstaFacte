using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.Configuration;
using FactuTrust.Domain.Common;
using FactuTrust.Infrastructure.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FactuTrust.Infrastructure.Services;

/// <summary>« Générer maintenant » : exécute le générateur récurrent dans le tenant courant.</summary>
public sealed class RecurringEntryService : IRecurringEntryService
{
    private readonly ITenantDbContextFactory _contextFactory;
    private readonly RecurringEntryGenerator _generator;
    private readonly AccountingSettings _settings;

    public RecurringEntryService(
        ITenantDbContextFactory contextFactory,
        RecurringEntryGenerator generator,
        IOptions<AccountingSettings> settings)
    {
        _contextFactory = contextFactory;
        _generator = generator;
        _settings = settings.Value;
    }

    public async Task<Result<Guid>> GenerateNowAsync(Guid templateId, CancellationToken cancellationToken = default)
    {
        await using var ctx = _contextFactory.CreateContext();
        var template = await ctx.JournalEntryTemplates
            .Include(t => t.Lines)
            .FirstOrDefaultAsync(t => t.Id == templateId, cancellationToken);
        if (template is null)
            return Result.Failure<Guid>(Error.Validation("Id", "Modèle introuvable."));
        if (!template.IsActive)
            return Result.Failure<Guid>(Error.Validation("Recurrence", "Le modèle est désactivé."));
        if (!template.IsRecurring)
            return Result.Failure<Guid>(Error.Validation("Recurrence", "Ce modèle n'est pas planifié : configurez d'abord sa récurrence."));
        if (template.NextRunDate is null)
            return Result.Failure<Guid>(Error.Validation("Recurrence", "La récurrence de ce modèle est épuisée (date de fin atteinte)."));

        return await _generator.GenerateOccurrenceAsync(ctx, _settings, template, template.NextRunDate.Value, cancellationToken);
    }
}
