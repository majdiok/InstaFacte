using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Enums;
using FactuTrust.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FactuTrust.Infrastructure.Scripts;

/// <summary>
/// Balayage plateforme des doublons de numéros de facture de vente (Invoices.Number)
/// sur toutes les bases tenant. Préalable obligatoire à l'index UNIQUE exigé par la
/// réglementation fiscale tunisienne : une migration unique appliquée sur une base
/// contenant des doublons échouerait et bloquerait le tenant via TenantMigrationGuard.
/// Fail-closed : un tenant injoignable n'est JAMAIS compté comme propre.
/// </summary>
public static class InvoiceNumberIntegrityHelper
{
    // Détail des factures dont le numéro apparaît plus d'une fois.
    private const string DuplicateDetailsSql = @"
SELECT i.[Number],
       i.[Id],
       i.[Status],
       i.[IssueDate],
       i.[CreatedAt],
       i.[TotalAmount]
FROM [dbo].[Invoices] i
WHERE i.[Number] IN (
    SELECT [Number] FROM [dbo].[Invoices]
    GROUP BY [Number] HAVING COUNT(*) > 1)
ORDER BY i.[Number], i.[CreatedAt];";

    private const string InvoiceCountSql = "SELECT COUNT(*) FROM [dbo].[Invoices];";

    // Index UNIQUE dont la première colonne de clé est Number (indépendant du nom d'index).
    private const string UniqueIndexSql = @"
SELECT CASE WHEN EXISTS (
    SELECT 1
    FROM sys.indexes ix
    INNER JOIN sys.index_columns ic
        ON ic.object_id = ix.object_id AND ic.index_id = ix.index_id AND ic.key_ordinal = 1
    INNER JOIN sys.columns c
        ON c.object_id = ic.object_id AND c.column_id = ic.column_id
    WHERE ix.object_id = OBJECT_ID(N'[dbo].[Invoices]')
      AND ix.is_unique = 1
      AND c.[name] = N'Number') THEN 1 ELSE 0 END;";

    /// <summary>Balaye tous les tenants actifs et consolide le rapport d'unicité.</summary>
    public static async Task<InvoiceNumberIntegrityReportDto> ScanAllTenantsAsync(
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken = default)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        var logger = loggerFactory.CreateLogger(typeof(InvoiceNumberIntegrityHelper).FullName
            ?? nameof(InvoiceNumberIntegrityHelper));

        await using var scope = serviceProvider.CreateAsyncScope();
        var masterContext = scope.ServiceProvider.GetRequiredService<MasterDbContext>();
        var tenantService = scope.ServiceProvider.GetRequiredService<ITenantService>();

        var tenants = await masterContext.Tenants
            .AsNoTracking()
            .Where(t => t.IsActive)
            .OrderBy(t => t.CompanyName)
            .Select(t => new { t.Id, t.CompanyName })
            .ToListAsync(cancellationToken);

        logger.LogInformation(
            "Invoice number integrity scan started for {Count} active tenant(s)", tenants.Count);

        var results = new List<TenantInvoiceNumberIntegrityDto>(tenants.Count);

        foreach (var tenant in tenants)
        {
            results.Add(await ScanTenantAsync(tenantService, tenant.Id, tenant.CompanyName, logger, cancellationToken));
        }

        var clean = results.Count(r => r.Status == TenantInvoiceNumberIntegrityDto.StatusClean);
        var withDuplicates = results.Count(r => r.Status == TenantInvoiceNumberIntegrityDto.StatusDuplicatesFound);
        var unreachable = results.Count(r => r.Status == TenantInvoiceNumberIntegrityDto.StatusUnreachable);

        var report = new InvoiceNumberIntegrityReportDto
        {
            ScannedAtUtc = DateTime.UtcNow,
            TotalTenants = results.Count,
            CleanTenants = clean,
            TenantsWithDuplicates = withDuplicates,
            UnreachableTenants = unreachable,
            TotalDuplicateNumbers = results.Sum(r => r.Duplicates.Count),
            IsUniqueIndexSafe = results.Count > 0 && clean == results.Count,
            Tenants = results
        };

        logger.LogInformation(
            "Invoice number integrity scan completed. Total: {Total}, Clean: {Clean}, Duplicates: {Duplicates}, Unreachable: {Unreachable}",
            report.TotalTenants, report.CleanTenants, report.TenantsWithDuplicates, report.UnreachableTenants);

        return report;
    }

    private static async Task<TenantInvoiceNumberIntegrityDto> ScanTenantAsync(
        ITenantService tenantService,
        Guid tenantId,
        string tenantName,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        try
        {
            var connectionString = await tenantService.GetConnectionStringAsync(tenantId, cancellationToken);
            if (string.IsNullOrEmpty(connectionString))
            {
                return Unreachable(tenantId, tenantName, "Chaîne de connexion introuvable.");
            }

            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);

            int invoiceCount;
            await using (var countCommand = new SqlCommand(InvoiceCountSql, connection))
            {
                invoiceCount = (int)(await countCommand.ExecuteScalarAsync(cancellationToken) ?? 0);
            }

            bool hasUniqueIndex;
            await using (var indexCommand = new SqlCommand(UniqueIndexSql, connection))
            {
                hasUniqueIndex = (int)(await indexCommand.ExecuteScalarAsync(cancellationToken) ?? 0) == 1;
            }

            var duplicates = await ReadDuplicatesAsync(connection, cancellationToken);

            return new TenantInvoiceNumberIntegrityDto
            {
                TenantId = tenantId,
                TenantName = tenantName,
                Status = duplicates.Count == 0
                    ? TenantInvoiceNumberIntegrityDto.StatusClean
                    : TenantInvoiceNumberIntegrityDto.StatusDuplicatesFound,
                InvoiceCount = invoiceCount,
                HasUniqueIndex = hasUniqueIndex,
                Duplicates = duplicates
            };
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Invoice number integrity scan failed for tenant {TenantId} ({TenantName})",
                tenantId, tenantName);
            return Unreachable(tenantId, tenantName, ex.Message);
        }
    }

    private static async Task<List<DuplicateInvoiceNumberDto>> ReadDuplicatesAsync(
        SqlConnection connection,
        CancellationToken cancellationToken)
    {
        var byNumber = new Dictionary<string, List<DuplicateInvoiceDto>>();

        await using var command = new SqlCommand(DuplicateDetailsSql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var number = reader.GetString(0);
            var status = (InvoiceStatus)reader.GetInt32(2);

            if (!byNumber.TryGetValue(number, out var invoices))
            {
                invoices = new List<DuplicateInvoiceDto>();
                byNumber[number] = invoices;
            }

            invoices.Add(new DuplicateInvoiceDto
            {
                InvoiceId = reader.GetGuid(1),
                Status = status,
                StatusDisplay = status.ToDisplayString(),
                IssueDate = reader.GetDateTime(3),
                CreatedAt = reader.GetDateTime(4),
                TotalAmount = reader.GetDecimal(5)
            });
        }

        return byNumber
            .OrderBy(kvp => kvp.Key, StringComparer.Ordinal)
            .Select(kvp => new DuplicateInvoiceNumberDto { Number = kvp.Key, Invoices = kvp.Value })
            .ToList();
    }

    private static TenantInvoiceNumberIntegrityDto Unreachable(Guid tenantId, string tenantName, string error) =>
        new()
        {
            TenantId = tenantId,
            TenantName = tenantName,
            Status = TenantInvoiceNumberIntegrityDto.StatusUnreachable,
            Error = error
        };
}
