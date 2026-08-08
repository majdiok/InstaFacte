using FactuTrust.Domain.Common;
using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;
using Xunit.Abstractions;

namespace FactuTrust.Infrastructure.Tests.Persistence;

/// <summary>
/// Diagnostic (temporaire) : imprime la <see cref="ValueGenerated"/> des clés des types owned
/// (Money via OwnsOne) qui partagent leur PK avec un propriétaire du domaine. Sert à comprendre
/// pourquoi la convention <c>ApplyClientGeneratedGuidKeys</c> perturbait la propagation de FK
/// d'InvoiceLine.Id vers InvoiceLine.UnitPrice#Money.InvoiceLineId.
/// </summary>
public sealed class OwnedKeyDiagnosticTests
{
    private readonly ITestOutputHelper _output;
    public OwnedKeyDiagnosticTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public void Print_ValueGenerated_ForOwnedTypeKeys_OnTenantContext()
    {
        var options = new DbContextOptionsBuilder<TenantDbContext>()
            .UseInMemoryDatabase($"DiagOwnedKeys_{Guid.NewGuid():N}")
            .Options;
        using var ctx = new TenantDbContext(options);

        var lines = new List<string>();
        foreach (var entityType in ctx.Model.GetEntityTypes()
                     .Where(t => t.IsOwned() && t.Name.Contains("InvoiceLine")))
        {
            var pk = entityType.FindPrimaryKey();
            if (pk is null) continue;
            foreach (var prop in pk.Properties)
            {
                var line = $"{entityType.Name}::{prop.Name} clr={prop.ClrType.Name} isFK={prop.IsForeignKey()} VG={prop.ValueGenerated}";
                _output.WriteLine(line);
                lines.Add(line);
            }
        }

        // Also print owner InvoiceLine
        var invoiceLine = ctx.Model.FindEntityType(typeof(FactuTrust.Domain.Entities.InvoiceLine));
        if (invoiceLine is not null)
        {
            foreach (var prop in invoiceLine.FindPrimaryKey()!.Properties)
            {
                var line = $"InvoiceLine::{prop.Name} clr={prop.ClrType.Name} isFK={prop.IsForeignKey()} VG={prop.ValueGenerated}";
                _output.WriteLine(line);
                lines.Add(line);
            }
        }

        // Persist for external inspection
        var path = Path.Combine(Path.GetTempPath(), "owned_keys_diag.txt");
        File.WriteAllLines(path, lines);
        _output.WriteLine($"Written {lines.Count} lines to {path}");
    }
}
