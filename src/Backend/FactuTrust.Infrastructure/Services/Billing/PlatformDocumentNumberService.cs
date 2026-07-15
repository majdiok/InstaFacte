using System.Data;
using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Domain.Billing;
using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace FactuTrust.Infrastructure.Services.Billing;

/// <summary>
/// Lot C4 — Réservation atomique de numéros de documents fiscaux.
///
/// Garantit la continuité <b>stricte</b> imposée par le DGI tunisien :
/// <list type="bullet">
///   <item>Transaction <c>Serializable</c> (les autres lecteurs attendent).</item>
///   <item><see cref="PlatformInvoiceSequence.RowVersion"/> détecte la concurrence
///         (<c>DbUpdateConcurrencyException</c> → retry borné).</item>
///   <item>Crée la séquence à la volée si absente pour l'année courante.</item>
/// </list>
/// </summary>
public sealed class PlatformDocumentNumberService : IPlatformDocumentNumberService
{
    private const int MaxRetries = 5;

    private readonly MasterDbContext _db;

    public PlatformDocumentNumberService(MasterDbContext db)
    {
        _db = db;
    }

    public async Task<(string Number, int Year)> ReserveAsync(string documentType, string prefix, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(documentType)) throw new ArgumentException("Type document requis", nameof(documentType));
        if (string.IsNullOrWhiteSpace(prefix)) prefix = "FT";

        var year = DateTime.UtcNow.Year;
        var attempt = 0;
        while (true)
        {
            attempt++;
            try
            {
                using IDbContextTransaction trx = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

                var sequence = await _db.PlatformInvoiceSequences
                    .FirstOrDefaultAsync(s => s.DocumentType == documentType && s.Year == year, cancellationToken);

                if (sequence is null)
                {
                    sequence = PlatformInvoiceSequence.Create(documentType, year, startAt: 1);
                    _db.PlatformInvoiceSequences.Add(sequence);
                }

                var nextNumber = sequence.Reserve();
                await _db.SaveChangesAsync(cancellationToken);
                await trx.CommitAsync(cancellationToken);

                var formatted = $"{prefix.Trim().ToUpperInvariant()}-{year:D4}-{nextNumber:D6}";
                return (formatted, year);
            }
            catch (DbUpdateConcurrencyException) when (attempt < MaxRetries)
            {
                // Concurrence détectée par RowVersion → on relit et on réessaie.
                foreach (var entry in _db.ChangeTracker.Entries().ToList())
                {
                    entry.State = EntityState.Detached;
                }
                await Task.Delay(20 * attempt, cancellationToken);
            }
        }
    }
}
