using System.Text.Json;
using FactuTrust.Application.Common;
using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.SectorConfiguration;
using FactuTrust.Domain.ValueObjects;
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
            case "product-category":
                return await ApplyProductCategoryAsync(context, templateCode, item, dryRun, warnings, cancellationToken);
            case "warehouse":
                return await ApplyWarehouseAsync(context, templateCode, item, dryRun, warnings, cancellationToken);
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

    /// <summary>
    /// Payload : {"documentType","currentSequence"?,"prefix"?}. La fiscalYear utilisée est l'année civile
    /// courante. Lorsqu'un <c>prefix</c> non vide est fourni, le bloc de texte libre par défaut du schéma
    /// est remplacé par ce préfixe (via <see cref="DocumentNumberingScheme.UpdateFormat"/>) — uniquement
    /// pour un schéma nouvellement créé (jamais verrouillé à l'inscription, currentSequence=0).
    /// </summary>
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

        // Optional custom free-text prefix (plan §3.4 — "default document numbering prefixes/mentions").
        // Only applied to a freshly created (unlocked) scheme, so the check-before-insert idempotency
        // contract is preserved: an existing scheme is never touched.
        var prefix = payload.TryGetProperty("prefix", out var prefixProp) ? prefixProp.GetString() : null;
        if (!string.IsNullOrWhiteSpace(prefix))
        {
            var prefixBlocks = BuildPrefixBlocks(documentType, prefix);
            var formatResult = scheme.UpdateFormat(prefixBlocks);
            if (formatResult.IsFailure)
                warnings.Add($"Modèle « {templateCode} » : préfixe de numérotation {prefix} refusé ({formatResult.Error.Description}), préfixe par défaut conservé.");
        }

        context.DocumentNumberingSchemes.Add(scheme);
        return new TemplateItemOutcome(templateCode, item.ItemKind, "created");
    }

    /// <summary>
    /// Construit les blocs de format équivalents à <see cref="NumberingSchemeDefaults.GetDefaultBlocks"/>
    /// mais en remplaçant le bloc de texte libre par <paramref name="prefix"/>. Reproduit la même
    /// structure (séparateur / année / numéro) que les schémas par défaut afin de rester homogène.
    /// </summary>
    private static IReadOnlyList<NumberingFormatBlock> BuildPrefixBlocks(NumberingDocumentType documentType, string prefix)
    {
        if (documentType == NumberingDocumentType.PhysicalInventory)
        {
            return new List<NumberingFormatBlock>
            {
                new(NumberingBlockType.FreeText, prefix, 0),
                new(NumberingBlockType.Separator, "-", 1),
                new(NumberingBlockType.DocumentNumberPadded6, null, 2)
            };
        }

        return new List<NumberingFormatBlock>
        {
            new(NumberingBlockType.FreeText, prefix, 0),
            new(NumberingBlockType.Separator, "-", 1),
            new(NumberingBlockType.Year4, null, 2),
            new(NumberingBlockType.Separator, "-", 3),
            new(NumberingBlockType.DocumentNumberPadded6, null, 4)
        };
    }

    /// <summary>Payload : {"code","name","displayOrder"?}. Insère la catégorie si elle n'existe pas déjà (par code normalisé).</summary>
    private static async Task<TemplateItemOutcome> ApplyProductCategoryAsync(
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
            warnings.Add($"Modèle « {templateCode} » : élément product-category avec un JSON invalide, ignoré.");
            return new TemplateItemOutcome(templateCode, item.ItemKind, "skipped");
        }

        var code = payload.TryGetProperty("code", out var codeProp) ? codeProp.GetString() : null;
        if (string.IsNullOrWhiteSpace(code))
        {
            warnings.Add($"Modèle « {templateCode} » : élément product-category sans code, ignoré.");
            return new TemplateItemOutcome(templateCode, item.ItemKind, "skipped");
        }

        var normalizedCode = code!.Trim().ToUpperInvariant();

        var exists = await context.ProductCategories
            .AsNoTracking()
            .AnyAsync(c => c.Code == normalizedCode, cancellationToken);

        if (exists)
            return new TemplateItemOutcome(templateCode, item.ItemKind, "existing");

        if (dryRun)
            return new TemplateItemOutcome(templateCode, item.ItemKind, "created");

        var name = payload.TryGetProperty("name", out var nameProp) ? nameProp.GetString() ?? normalizedCode : normalizedCode;
        var displayOrder = payload.TryGetProperty("displayOrder", out var orderProp) && orderProp.TryGetInt32(out var order) ? order : 0;

        var result = ProductCategory.Create(code, name, displayOrder);
        if (result.IsFailure)
        {
            warnings.Add($"Modèle « {templateCode} » : catégorie {normalizedCode} invalide ({result.Error.Description}), ignorée.");
            return new TemplateItemOutcome(templateCode, item.ItemKind, "skipped");
        }

        context.ProductCategories.Add(result.Value);
        return new TemplateItemOutcome(templateCode, item.ItemKind, "created");
    }

    /// <summary>Payload : {"code","name","address"?,"isDefault"?}. Insère l'entrepôt s'il n'existe pas déjà (par code normalisé).</summary>
    private static async Task<TemplateItemOutcome> ApplyWarehouseAsync(
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
            warnings.Add($"Modèle « {templateCode} » : élément warehouse avec un JSON invalide, ignoré.");
            return new TemplateItemOutcome(templateCode, item.ItemKind, "skipped");
        }

        var code = payload.TryGetProperty("code", out var codeProp) ? codeProp.GetString() : null;
        if (string.IsNullOrWhiteSpace(code))
        {
            warnings.Add($"Modèle « {templateCode} » : élément warehouse sans code, ignoré.");
            return new TemplateItemOutcome(templateCode, item.ItemKind, "skipped");
        }

        var normalizedCode = code!.Trim().ToUpperInvariant();

        var exists = await context.Warehouses
            .AsNoTracking()
            .AnyAsync(w => w.Code == normalizedCode, cancellationToken);

        if (exists)
            return new TemplateItemOutcome(templateCode, item.ItemKind, "existing");

        if (dryRun)
            return new TemplateItemOutcome(templateCode, item.ItemKind, "created");

        var name = payload.TryGetProperty("name", out var nameProp) ? nameProp.GetString() ?? normalizedCode : normalizedCode;
        var address = payload.TryGetProperty("address", out var addressProp) ? addressProp.GetString() : null;
        var isDefault = payload.TryGetProperty("isDefault", out var defaultProp) && defaultProp.ValueKind is JsonValueKind.True or JsonValueKind.False && defaultProp.GetBoolean();

        var result = Warehouse.Create(code, name, address, isDefault);
        if (result.IsFailure)
        {
            warnings.Add($"Modèle « {templateCode} » : entrepôt {normalizedCode} invalide ({result.Error.Description}), ignoré.");
            return new TemplateItemOutcome(templateCode, item.ItemKind, "skipped");
        }

        context.Warehouses.Add(result.Value);
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
