using System.Globalization;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.AI.DTOs;
using FactuTrust.Application.Features.CashDesk.Commands;
using FactuTrust.Application.Features.Invoices.Commands;
using FactuTrust.Application.Features.Studio.Ai;
using FactuTrust.Application.Features.Studio.Common;
using FactuTrust.Application.Features.Studio.Entities;
using FactuTrust.Application.Features.Studio.Fields;
using FactuTrust.Application.Features.Studio.Reports;
using FactuTrust.Infrastructure.Services.Studio;
using FactuTrust.Domain.Enums;
using MediatR;

namespace FactuTrust.Infrastructure.Services.AI;

/// <summary>
/// Studio "AI-native" tool handlers. Thin orchestrators over the existing Studio CQRS commands — they
/// add NO business logic; all validation/quotas/permissions/audit of the underlying commands apply.
/// </summary>
public sealed partial class AiToolExecutor
{
    private async Task<AiToolResult> HandleStudioGenerateApp(Dictionary<string, object?> args, CancellationToken ct)
    {
        var specJson = GetStringArg(args, "spec_json");
        if (!StudioAiAppSpec.TryParse(specJson, out var spec, out var parseError) || spec is null)
            return AiToolResult.Error(parseError ?? "Spécification d'application invalide.");

        // Unique entity key across the tenant (the create handler also enforces uniqueness).
        var existing = await _mediator.Send(new ListCustomEntitiesQuery(true), ct);
        var usedKeys = existing.IsSuccess
            ? existing.Value.Select(e => e.Key).ToHashSet(StringComparer.Ordinal)
            : new HashSet<string>(StringComparer.Ordinal);
        var entityKey = UniqueEntityKey(spec.EntityDisplayName, usedKeys);

        var entityResult = await _mediator.Send(new CreateCustomEntityCommand(
            new CreateCustomEntityRequest(entityKey, spec.EntityDisplayName, spec.EntityDisplayNamePlural, spec.Icon, spec.Description)), ct);
        if (!entityResult.IsSuccess)
            return AiToolResult.Error(entityResult.Error.Description);

        var entity = entityResult.Value;
        var created = new List<string>();
        var failed = new List<string>();

        foreach (var f in spec.Fields)
        {
            var req = new CreateCustomFieldRequest(f.Key, f.Label, f.FieldType, f.Required, f.Unique, null, f.Options, null, f.Config);
            var fr = await _mediator.Send(new CreateCustomFieldCommand(entity.Id, req), ct);
            if (fr.IsSuccess) created.Add(f.Label);
            else failed.Add($"{f.Label} : {fr.Error.Description}");
        }

        // Optional starter report — only if the user can also design reports (defense in depth beyond the tool gate).
        string? reportName = null;
        if (spec.Report is not null && _currentUser.HasPermission(Permissions.Studio.DesignReports))
        {
            var def = new ReportDefinition { Grouping = spec.Report.Grouping, Aggregations = spec.Report.Aggregations };
            var reportReq = new SaveCustomReportRequest(null, spec.Report.DisplayName, CustomReportDataSourceKind.CustomEntity, entity.Key, def);
            var rr = await _mediator.Send(new UpsertCustomReportCommand(null, reportReq), ct);
            if (rr.IsSuccess) reportName = rr.Value.DisplayName;
        }

        var payload = new
        {
            success = true,
            entityKey = entity.Key,
            displayName = entity.DisplayName,
            fieldsCreated = created.Count,
            fieldsFailed = failed,
            reportCreated = reportName,
            openUrl = $"/studio/d/{entity.Key}",
            message = $"Table « {entity.DisplayName} » créée avec {created.Count} champ(s)"
                + (reportName is not null ? $", plus le rapport « {reportName} »." : ".")
        };
        return AiToolResult.Ok(Serialize(payload));
    }

    private async Task<AiToolResult> HandleStudioGenerateSystem(Dictionary<string, object?> args, CancellationToken ct)
    {
        if (!_ollamaSettings.EnableStudioSystemGeneration)
            return AiToolResult.Error("La génération de systèmes multi-tables n'est pas activée.");

        var specJson = GetStringArg(args, "spec_json");
        if (!StudioAiSystemSpec.TryParse(specJson, out var spec, out var parseError) || spec is null)
            return AiToolResult.Error(parseError ?? "Spécification système invalide.");

        var orchestrator = new StudioAiSystemOrchestrator(_mediator, _currentUser, _studioQuota);
        var (success, error, payload) = await orchestrator.ExecuteAsync(spec, progress: null, ct);
        if (!success || payload is null)
            return AiToolResult.Error(error ?? "Échec de la création du système.");

        return AiToolResult.Ok(Serialize(payload));
    }

    private static string UniqueEntityKey(string displayName, HashSet<string> used)
    {
        var baseKey = StudioAiAppSpec.SlugKey(displayName);
        if (string.IsNullOrEmpty(baseKey) || !StudioKey.IsValidShape(baseKey)) baseKey = "table";
        var key = baseKey;
        var i = 1;
        while (used.Contains(key)) key = $"{baseKey}_{++i}";
        return key;
    }

    // ───────── ERP bridge actions (also usable from the chat assistant) ─────────

    private async Task<AiToolResult> HandleGenerateInvoice(Dictionary<string, object?> args, CancellationToken ct)
    {
        var clientId = ParseGuidOrNull(args, "client_id");
        if (clientId is null) return AiToolResult.Error("client_id (GUID du client) est requis.");
        var productId = ParseGuidOrNull(args, "product_id");
        if (productId is null) return AiToolResult.Error("product_id (GUID du produit) est requis.");

        var quantity = TryParseDecimalArg(args, "quantity") ?? 1m;
        if (quantity <= 0) return AiToolResult.Error("quantity doit être supérieure à zéro.");

        var dto = new CreateInvoiceDto
        {
            ClientId = clientId.Value,
            IssueDate = ResolveDateOrToday(args, "issue_date"),
            DueDate = ResolveOptionalDate(args, "due_date"),
            Reference = OptionalString(args, "reference"),
            Lines = new[]
            {
                new CreateInvoiceLineDto
                {
                    ProductId = productId.Value,
                    Quantity = quantity,
                    CustomUnitPrice = TryParseDecimalArg(args, "unit_price"),
                    DiscountPercent = TryParseDecimalArg(args, "discount_percent")
                }
            }
        };

        var result = await _mediator.Send(new CreateInvoiceCommand(dto), ct);
        return result.IsSuccess
            ? AiToolResult.Ok(Serialize(new { invoiceId = result.Value, message = "Facture générée." }))
            : AiToolResult.Error(result.Error.Description);
    }

    private async Task<AiToolResult> HandleCreateCashExpense(Dictionary<string, object?> args, CancellationToken ct)
    {
        var amount = TryParseDecimalArg(args, "amount") ?? 0m;
        if (amount <= 0) return AiToolResult.Error("amount doit être supérieur à zéro.");
        var label = GetStringArg(args, "label");
        if (string.IsNullOrWhiteSpace(label)) return AiToolResult.Error("label est requis.");

        var method = ParseEnumStrict(args, "method", PaymentMethod.Cash, out var methodErr);
        if (methodErr is not null) return AiToolResult.Error(methodErr);

        CashExpenseCategory? category = null;
        if (ArgProvided(args, "category"))
        {
            var cat = ParseEnumStrict(args, "category", CashExpenseCategory.SuppliesAndConsumables, out var catErr);
            if (catErr is not null) return AiToolResult.Error(catErr);
            category = cat;
        }

        var request = new CreateCashOperationRequest
        {
            OperationType = CashOperationType.Debit,
            OperationDate = ResolveDateOrToday(args, "expense_date"),
            Amount = amount,
            Method = method,
            Label = label,
            Category = category,
            Reference = OptionalString(args, "reference"),
            Notes = OptionalString(args, "notes")
        };

        var result = await _mediator.Send(new CreateCashOperationCommand(request), ct);
        return result.IsSuccess
            ? AiToolResult.Ok(Serialize(new { message = "Dépense enregistrée.", operation = result.Value }))
            : AiToolResult.Error(result.Error.Description);
    }

    private DateTime ResolveDateOrToday(Dictionary<string, object?> args, string key)
        => DateTime.TryParse(OptionalString(args, key), CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
            ? d.Date
            : _timeProvider.GetUtcNow().UtcDateTime.Date;

    private DateTime? ResolveOptionalDate(Dictionary<string, object?> args, string key)
        => DateTime.TryParse(OptionalString(args, key), CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
            ? d.Date
            : null;
}
