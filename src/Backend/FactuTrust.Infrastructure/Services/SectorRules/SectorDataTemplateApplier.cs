using System.Text.Json;
using FactuTrust.Application.Common;
using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.SectorConfiguration;
using FactuTrust.Infrastructure.MultiTenancy;
using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FactuTrust.Infrastructure.Services.SectorRules;

/// <summary>
/// Applies matching <see cref="DataTemplateSnapshot"/> rows to a tenant database (plan §WP-B6).
/// Every item handler checks-before-insert and never updates/deletes an existing tenant row —
/// same idempotency idiom as <c>WithholdingChartAccountsInitializer.EnsureAccountsAsync</c>.
/// </summary>
public sealed class SectorDataTemplateApplier : ISectorDataTemplateApplier
{
    private readonly ISectorCatalogProvider _catalogProvider;
    private readonly ITenantDbContextFactory _contextFactory;
    private readonly ILogger<SectorDataTemplateApplier> _logger;

    public SectorDataTemplateApplier(
        ISectorCatalogProvider catalogProvider,
        ITenantDbContextFactory contextFactory,
        ILogger<SectorDataTemplateApplier> logger)
    {
        _catalogProvider = catalogProvider;
        _contextFactory = contextFactory;
        _logger = logger;
    }

    public async Task<SectorTemplateApplyResult> ApplyAsync(
        Guid tenantId,
        string connectionString,
        string? segmentCode,
        string? domainCode,
        bool dryRun,
        CancellationToken cancellationToken = default)
    {
        var snapshot = _catalogProvider.GetSnapshot();

        var matching = snapshot.DataTemplates
            .Where(t => (t.SegmentCode is null || string.Equals(t.SegmentCode, segmentCode, StringComparison.Ordinal))
                     && (t.DomainCode is null || string.Equals(t.DomainCode, domainCode, StringComparison.Ordinal)))
            .OrderBy(t => t.SortOrder)
            .ToList();

        if (matching.Count == 0)
            return SectorTemplateApplyResult.Empty;

        await using var context = _contextFactory.CreateIsolatedContext(connectionString);

        var applied = new List<AppliedTemplateInfo>();
        var skipped = new List<AppliedTemplateInfo>();
        var itemOutcomes = new List<TemplateItemOutcome>();
        var warnings = new List<string>();

        foreach (var template in matching)
        {
            var alreadyApplied = await context.AppliedSectorTemplates
                .AsNoTracking()
                .AnyAsync(a => a.TemplateCode == template.Code && a.Version == template.Version, cancellationToken);

            if (alreadyApplied)
            {
                skipped.Add(new AppliedTemplateInfo(template.Code, template.Version));
                continue;
            }

            foreach (var item in template.Items.OrderBy(i => i.SortOrder))
            {
                var outcome = await ApplyItemAsync(context, tenantId, template.Code, item, dryRun, warnings, cancellationToken);
                itemOutcomes.Add(outcome);
            }

            applied.Add(new AppliedTemplateInfo(template.Code, template.Version));

            if (!dryRun)
                context.AppliedSectorTemplates.Add(AppliedSectorTemplate.Create(template.Code, template.Version));
        }

        if (!dryRun && applied.Count > 0)
        {
            await context.SaveChangesAsync(cancellationToken);
            _logger.LogInformation(
                "SectorTemplates.Applied TenantId={TenantId} Count={Count}",
                tenantId,
                applied.Count);
        }

        return new SectorTemplateApplyResult
        {
            Applied = applied,
            Skipped = skipped,
            ItemOutcomes = itemOutcomes,
            Warnings = warnings
        };
    }

    private static async Task<TemplateItemOutcome> ApplyItemAsync(
        TenantDbContext context,
        Guid tenantId,
        string templateCode,
        DataTemplateItemSnapshot item,
        bool dryRun,
        List<string> warnings,
        CancellationToken cancellationToken)
    {
        switch (item.ItemKind)
        {
            case "chart-account":
                return await ApplyChartAccountAsync(context, templateCode, item, dryRun, warnings, cancellationToken);
            case "document-numbering-scheme":
                return await ApplyDocumentNumberingSchemeAsync(context, tenantId, templateCode, item, dryRun, warnings, cancellationToken);
            case "setting":
                return await ApplySettingAsync(context, templateCode, item, dryRun, warnings, cancellationToken);
            default:
                warnings.Add($"Modèle « {templateCode} » : type d'élément inconnu ignoré ({item.ItemKind}).");
                return new TemplateItemOutcome(templateCode, item.ItemKind, "skipped");
        }
    }

    /// <summary>Payload : {"accountNumber","label","accountClass","parentAccountNumber"?,"natureType","isSystem"?,"accountType"?}.</summary>
    private static async Task<TemplateItemOutcome> ApplyChartAccountAsync(
        TenantDbContext context,
        string templateCode,
        DataTemplateItemSnapshot item,
        bool dryRun,
        List<string> warnings,
        CancellationToken cancellationToken)
    {
        JsonElement payload;
        try
        {
            payload = JsonDocument.Parse(item.PayloadJson).RootElement;
        }
        catch (JsonException)
        {
            warnings.Add($"Modèle « {templateCode} » : élément chart-account avec un JSON invalide, ignoré.");
            return new TemplateItemOutcome(templateCode, item.ItemKind, "skipped");
        }

        var accountNumber = payload.TryGetProperty("accountNumber", out var accountNumberProp) ? accountNumberProp.GetString() : null;
        if (string.IsNullOrWhiteSpace(accountNumber))
        {
            warnings.Add($"Modèle « {templateCode} » : élément chart-account sans numéro de compte, ignoré.");
            return new TemplateItemOutcome(templateCode, item.ItemKind, "skipped");
        }

        var exists = await context.ChartOfAccounts
            .AsNoTracking()
            .AnyAsync(a => a.AccountNumber == accountNumber, cancellationToken);

        if (exists)
            return new TemplateItemOutcome(templateCode, item.ItemKind, "existing");

        if (dryRun)
            return new TemplateItemOutcome(templateCode, item.ItemKind, "created");

        var label = payload.TryGetProperty("label", out var labelProp) ? labelProp.GetString() ?? accountNumber : accountNumber;
        var accountClass = payload.TryGetProperty("accountClass", out var classProp) && classProp.TryGetInt32(out var cls) ? cls : accountNumber[0] - '0';
        var parentAccountNumber = payload.TryGetProperty("parentAccountNumber", out var parentProp) ? parentProp.GetString() : null;
        var isSystem = payload.TryGetProperty("isSystem", out var isSystemProp) && isSystemProp.ValueKind is JsonValueKind.True or JsonValueKind.False && isSystemProp.GetBoolean();

        var natureType = AccountNatureType.Debit;
        if (payload.TryGetProperty("natureType", out var natureProp) && Enum.TryParse<AccountNatureType>(natureProp.GetString(), ignoreCase: true, out var parsedNature))
            natureType = parsedNature;

        var accountType = AccountType.General;
        if (payload.TryGetProperty("accountType", out var accountTypeProp) && Enum.TryParse<AccountType>(accountTypeProp.GetString(), ignoreCase: true, out var parsedAccountType))
            accountType = parsedAccountType;

        var result = ChartOfAccount.Create(
            accountNumber,
            label,
            accountClass,
            parentAccountNumber,
            natureType,
            isSystem: isSystem,
            accountType: accountType);

        if (result.IsFailure)
        {
            warnings.Add($"Modèle « {templateCode} » : compte {accountNumber} invalide ({result.Error.Description}), ignoré.");
            return new TemplateItemOutcome(templateCode, item.ItemKind, "skipped");
        }

        context.ChartOfAccounts.Add(result.Value);
        return new TemplateItemOutcome(templateCode, item.ItemKind, "created");
    }

    /// <summary>Payload : {"documentType","currentSequence"?}. La fiscalYear utilisée est l'année civile courante.</summary>
    private static async Task<TemplateItemOutcome> ApplyDocumentNumberingSchemeAsync(
        TenantDbContext context,
        Guid tenantId,
        string templateCode,
        DataTemplateItemSnapshot item,
        bool dryRun,
        List<string> warnings,
        CancellationToken cancellationToken)
    {
        JsonElement payload;
        try
        {
            payload = JsonDocument.Parse(item.PayloadJson).RootElement;
        }
        catch (JsonException)
        {
            warnings.Add($"Modèle « {templateCode} » : élément document-numbering-scheme avec un JSON invalide, ignoré.");
            return new TemplateItemOutcome(templateCode, item.ItemKind, "skipped");
        }

        var documentTypeStr = payload.TryGetProperty("documentType", out var docTypeProp) ? docTypeProp.GetString() : null;
        if (!Enum.TryParse<NumberingDocumentType>(documentTypeStr, ignoreCase: true, out var documentType))
        {
            warnings.Add($"Modèle « {templateCode} » : type de document invalide ({documentTypeStr}), ignoré.");
            return new TemplateItemOutcome(templateCode, item.ItemKind, "skipped");
        }

        var fiscalYear = DateTime.UtcNow.Year;

        var exists = await context.DocumentNumberingSchemes
            .AsNoTracking()
            .AnyAsync(s => s.TenantId == tenantId && s.DocumentType == documentType && s.FiscalYear == fiscalYear, cancellationToken);

        if (exists)
            return new TemplateItemOutcome(templateCode, item.ItemKind, "existing");

        if (dryRun)
            return new TemplateItemOutcome(templateCode, item.ItemKind, "created");

        var currentSequence = payload.TryGetProperty("currentSequence", out var seqProp) && seqProp.TryGetInt32(out var seq) ? seq : 0;

        var scheme = DocumentNumberingScheme.CreateDefault(tenantId, documentType, fiscalYear, currentSequence);
        context.DocumentNumberingSchemes.Add(scheme);
        return new TemplateItemOutcome(templateCode, item.ItemKind, "created");
    }

    /// <summary>
    /// Réservé aux lignes singleton (ex. FixedAssetSettings). Payload : {"table":"FixedAssetSettings", ...}.
    /// N'insère que si aucune ligne n'existe déjà ; ne modifie jamais une ligne existante.
    /// </summary>
    private static async Task<TemplateItemOutcome> ApplySettingAsync(
        TenantDbContext context,
        string templateCode,
        DataTemplateItemSnapshot item,
        bool dryRun,
        List<string> warnings,
        CancellationToken cancellationToken)
    {
        JsonElement payload;
        try
        {
            payload = JsonDocument.Parse(item.PayloadJson).RootElement;
        }
        catch (JsonException)
        {
            warnings.Add($"Modèle « {templateCode} » : élément setting avec un JSON invalide, ignoré.");
            return new TemplateItemOutcome(templateCode, item.ItemKind, "skipped");
        }

        var table = payload.TryGetProperty("table", out var tableProp) ? tableProp.GetString() : null;

        if (!string.Equals(table, "FixedAssetSettings", StringComparison.Ordinal))
        {
            warnings.Add($"Modèle « {templateCode} » : table de paramètre non prise en charge ({table}), ignorée.");
            return new TemplateItemOutcome(templateCode, item.ItemKind, "skipped");
        }

        var exists = await context.FixedAssetSettings.AsNoTracking().AnyAsync(cancellationToken);
        if (exists)
            return new TemplateItemOutcome(templateCode, item.ItemKind, "existing");

        if (dryRun)
            return new TemplateItemOutcome(templateCode, item.ItemKind, "created");

        var fiscalYearStartMonth = payload.TryGetProperty("fiscalYearStartMonth", out var monthProp) && monthProp.TryGetInt32(out var month)
            ? month
            : FixedAssetSettings.DefaultFiscalYearStartMonth;
        var fiscalYearLabelFormat = payload.TryGetProperty("fiscalYearLabelFormat", out var formatProp)
            ? formatProp.GetString() ?? FixedAssetSettings.LabelFormatNn1
            : FixedAssetSettings.LabelFormatNn1;

        var result = FixedAssetSettings.Create(fiscalYearStartMonth, fiscalYearLabelFormat);
        if (result.IsFailure)
        {
            warnings.Add($"Modèle « {templateCode} » : paramètres FixedAssetSettings invalides ({result.Error.Description}), ignorés.");
            return new TemplateItemOutcome(templateCode, item.ItemKind, "skipped");
        }

        context.FixedAssetSettings.Add(result.Value);
        return new TemplateItemOutcome(templateCode, item.ItemKind, "created");
    }
}
