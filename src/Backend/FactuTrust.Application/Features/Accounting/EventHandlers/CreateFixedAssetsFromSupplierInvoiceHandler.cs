using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Configuration;
using FactuTrust.Application.Features.Accounting.Notifications;
using FactuTrust.Application.Features.FixedAssets;
using FactuTrust.Domain.Entities;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FactuTrust.Application.Features.Accounting.EventHandlers;

public sealed class CreateFixedAssetsFromSupplierInvoiceHandler
    : INotificationHandler<SupplierInvoiceCreatedForAccountingNotification>
{
    private readonly ISupplierInvoiceRepository _supplierInvoices;
    private readonly IFixedAssetRepository _fixedAssets;
    private readonly IDepreciationRateCategoryRepository _categories;
    private readonly ILogger<CreateFixedAssetsFromSupplierInvoiceHandler> _logger;
    private readonly FixedAssetsOptions _options;

    public CreateFixedAssetsFromSupplierInvoiceHandler(
        ISupplierInvoiceRepository supplierInvoices,
        IFixedAssetRepository fixedAssets,
        IDepreciationRateCategoryRepository categories,
        ILogger<CreateFixedAssetsFromSupplierInvoiceHandler> logger,
        IOptions<FixedAssetsOptions> options)
    {
        _supplierInvoices = supplierInvoices;
        _fixedAssets = fixedAssets;
        _categories = categories;
        _logger = logger;
        _options = options.Value;
    }

    public async Task Handle(SupplierInvoiceCreatedForAccountingNotification notification, CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
            return;

        var invoice = await _supplierInvoices.GetByIdWithLinesAsync(notification.SupplierInvoiceId, cancellationToken);
        if (invoice is null)
            return;

        var assetLines = invoice.Lines.Where(l => l.IsFixedAsset).ToList();
        if (assetLines.Count == 0)
            return;

        var year = invoice.InvoiceDate.Year;

        foreach (var line in assetLines)
        {
            var category = line.DepreciationRateCategoryId.HasValue
                ? await _categories.GetByIdAsync(line.DepreciationRateCategoryId.Value, cancellationToken)
                : await _categories.GetByCodeAsync("OTHER", cancellationToken);

            if (category is null)
            {
                _logger.LogWarning("No depreciation category for supplier line {Line}", line.LineNumber);
                continue;
            }

            var rate = category.IsNonDepreciable ? 0m : category.LegalRatePercent;
            var assetAccount = line.AssetAccountNumber ?? category.DefaultAssetAccount;
            if (!FixedAssetAccountRules.IsValidAssetAccount(assetAccount))
            {
                _logger.LogWarning(
                    "Invalid asset account {Account} on supplier line {Line}, falling back to category default {Default}",
                    assetAccount, line.LineNumber, category.DefaultAssetAccount);
                assetAccount = category.DefaultAssetAccount;
            }

            var depreciationAccount = category.DefaultDepreciationAccount;
            var expenseAccount = category.DefaultExpenseAccount;
            var vatCapitalized = FixedAssetVatRules.IsVatCapitalized(category.Code, assetAccount);

            // Garde défensive (B5/T8) : le triplet résolu doit rester cohérent même après repli sur
            // les défauts de catégorie (valides post-T1) — sinon la ligne est ignorée proprement,
            // sans lever d'exception dans ce notification handler.
            var accountsValidation = FixedAssetAccountRules.Validate(assetAccount, depreciationAccount, expenseAccount);
            if (accountsValidation.IsFailure)
            {
                _logger.LogWarning(
                    "Fixed asset draft skipped for supplier line {Line}: invalid account triplet {Asset}/{Depreciation}/{Expense} ({Error})",
                    line.LineNumber, assetAccount, depreciationAccount, expenseAccount, accountsValidation.Error.Description);
                continue;
            }

            var added = await _fixedAssets.AddWithGeneratedInventoryNumberAsync(
                inventoryNumber =>
                {
                    var create = FixedAsset.Create(
                        inventoryNumber,
                        $"{line.ProductName} ({invoice.InvoiceNumber})",
                        category.Id,
                        rate,
                        category?.UsefulLifeYears ?? (rate > 0 ? 100m / rate : 0m),
                        assetAccount,
                        depreciationAccount,
                        expenseAccount,
                        line.SubTotal.Amount,
                        0m,
                        0m,
                        invoice.InvoiceDate,
                        line.ProductDescription,
                        line.VatAmount.Amount,
                        supplierId: invoice.SupplierId,
                        vatCapitalized: vatCapitalized);

                    if (create.IsSuccess)
                        create.Value.LinkSupplierInvoiceSource(invoice.Id, line.Id);

                    return create;
                },
                year,
                cancellationToken);

            if (added.IsFailure)
            {
                _logger.LogWarning("Fixed asset draft skipped for supplier line {Line}: {Error}",
                    line.LineNumber, added.Error.Description);
            }
        }
    }
}