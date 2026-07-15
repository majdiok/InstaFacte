using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Configuration;
using FactuTrust.Application.Features.Accounting.Notifications;
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
        var seq = await _fixedAssets.CountByYearPrefixAsync(year, cancellationToken);

        foreach (var line in assetLines)
        {
            seq++;
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
            var depreciationAccount = category.DefaultDepreciationAccount;
            var expenseAccount = category.DefaultExpenseAccount;

            var create = FixedAsset.Create(
                $"IMMO-{year}-{seq:D4}",
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
                supplierId: invoice.SupplierId);

            if (create.IsFailure)
            {
                _logger.LogWarning("Fixed asset draft skipped for supplier line {Line}: {Error}",
                    line.LineNumber, create.Error.Description);
                continue;
            }

            var asset = create.Value;
            asset.LinkSupplierInvoiceSource(invoice.Id, line.Id);
            await _fixedAssets.AddAsync(asset, cancellationToken);
        }
    }
}