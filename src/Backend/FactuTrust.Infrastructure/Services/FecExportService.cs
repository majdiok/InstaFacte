using System.Globalization;
using System.Text;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Domain.Common;
using FactuTrust.Infrastructure.MultiTenancy;
using Microsoft.EntityFrameworkCore;

namespace FactuTrust.Infrastructure.Services;

public sealed class FecExportService : IFecExportService
{
    private const char Sep = '\t';

    private static readonly string[] HeaderFields =
    [
        "JournalCode", "JournalLib", "EcritureNum", "EcritureDate",
        "CompteNum", "CompteLib", "CompAuxNum", "CompAuxLib",
        "PieceRef", "PieceDate", "EcritureLib",
        "Debit", "Credit", "EcritureLet", "DateLet",
        "ValidDate", "Montantdevise", "Idevise"
    ];

    private static readonly Dictionary<string, string> JournalLabels = new(StringComparer.OrdinalIgnoreCase)
    {
        ["JV"] = "Journal des Ventes",
        ["JA"] = "Journal des Achats",
        ["JC"] = "Journal de Caisse",
        ["JB"] = "Journal de Banque",
        ["JOD"] = "Journal des Opérations Diverses",
        ["JAN"] = "Journal des À-Nouveaux",
        ["JIM"] = "Journal des Immobilisations"
    };

    private readonly ITenantDbContextFactory _contextFactory;

    public FecExportService(ITenantDbContextFactory contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<Result<byte[]>> ExportFecAsync(int fiscalYear, CancellationToken cancellationToken = default)
    {
        await using var ctx = _contextFactory.CreateContext();

        var startDate = new DateTime(fiscalYear, 1, 1);
        var endDate = new DateTime(fiscalYear, 12, 31);

        // Sortie légale : uniquement les écritures définitives (validées ou clôturées),
        // jamais les brouillons (cf. AccountingSettings.IncludeBrouillardInReports).
        var entries = await ctx.JournalEntries
            .AsNoTracking()
            .Include(j => j.Lines)
            .Where(j => j.EntryDate >= startDate && j.EntryDate <= endDate
                        && j.Status != Domain.Enums.JournalEntryStatus.Brouillon)
            .OrderBy(j => j.JournalCode)
            .ThenBy(j => j.EntryDate)
            .ThenBy(j => j.EntryNumber)
            .ToListAsync(cancellationToken);

        var accountLabels = await ctx.ChartOfAccounts
            .AsNoTracking()
            .ToDictionaryAsync(c => c.AccountNumber, c => c.Label, cancellationToken);

        // Load client and supplier names for CompAuxLib
        var clientNames = await ctx.Clients
            .AsNoTracking()
            .ToDictionaryAsync(c => c.Id, c => c.Name, cancellationToken);

        var supplierNames = await ctx.Suppliers
            .AsNoTracking()
            .ToDictionaryAsync(s => s.Id, s => s.Name, cancellationToken);

        // Codes auxiliaires lisibles du plan tiers (CompAuxNum) — fallback GUID si absent.
        var auxiliaryCodes = await ctx.ThirdPartyAccountingProfiles
            .AsNoTracking()
            .ToDictionaryAsync(p => (p.Kind, p.ThirdPartyId), p => p.AuxiliaryCode, cancellationToken);

        // Load lettering group dates for DateLet
        var letteringDates = await ctx.LetteringGroups
            .AsNoTracking()
            .Include(lg => lg.Members)
            .SelectMany(lg => lg.Members.Select(m => new { m.JournalEntryLineId, lg.LetteredAt }))
            .ToDictionaryAsync(x => x.JournalEntryLineId, x => x.LetteredAt, cancellationToken);

        var sb = new StringBuilder();

        sb.AppendLine(string.Join(Sep, HeaderFields));

        foreach (var entry in entries)
        {
            var journalLib = JournalLabels.TryGetValue(entry.JournalCode, out var jl) ? jl : entry.JournalCode;
            var ecritureDate = entry.EntryDate.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
            // PieceRef/PieceDate FEC : pièce externe quand renseignée, sinon repli historique
            // (n° d'écriture / date comptable).
            var pieceRef = string.IsNullOrEmpty(entry.PieceRef)
                ? entry.EntryNumber.ToString(CultureInfo.InvariantCulture)
                : entry.PieceRef;
            var pieceDate = (entry.PieceDate ?? entry.EntryDate).ToString("yyyyMMdd", CultureInfo.InvariantCulture);

            foreach (var line in entry.Lines.OrderBy(l => l.LineNumber))
            {
                var compteLib = accountLabels.TryGetValue(line.AccountNumber, out var cl) ? cl : string.Empty;
                var debit = line.DebitAmount.Amount;
                var credit = line.CreditAmount.Amount;

                // Populate CompAuxNum and CompAuxLib from third-party references
                var compAuxNum = string.Empty;
                var compAuxLib = string.Empty;
                if (line.ThirdPartyId.HasValue && line.ThirdPartyKind != Domain.Enums.ThirdPartyKind.None)
                {
                    // Code auxiliaire lisible (plan tiers) quand la fiche existe ; sinon GUID historique.
                    compAuxNum = auxiliaryCodes.TryGetValue((line.ThirdPartyKind, line.ThirdPartyId.Value), out var auxCode)
                        ? auxCode
                        : line.ThirdPartyId.Value.ToString("N");
                    if (line.ThirdPartyKind == Domain.Enums.ThirdPartyKind.Client)
                        clientNames.TryGetValue(line.ThirdPartyId.Value, out compAuxLib);
                    else if (line.ThirdPartyKind == Domain.Enums.ThirdPartyKind.Supplier)
                        supplierNames.TryGetValue(line.ThirdPartyId.Value, out compAuxLib);
                    compAuxLib ??= string.Empty;
                }

                // Populate DateLet from lettering group creation date
                var dateLet = string.Empty;
                if (!string.IsNullOrEmpty(line.LetteringCode) && letteringDates.TryGetValue(line.Id, out var letDate))
                    dateLet = letDate.ToString("yyyyMMdd", CultureInfo.InvariantCulture);

                var fields = new[]
                {
                    entry.JournalCode,
                    journalLib,
                    entry.EntryNumber.ToString(CultureInfo.InvariantCulture),
                    ecritureDate,
                    line.AccountNumber,
                    compteLib,
                    compAuxNum,
                    compAuxLib,
                    pieceRef,
                    pieceDate,
                    line.Label,
                    FormatAmount(debit),
                    FormatAmount(credit),
                    line.LetteringCode ?? string.Empty,
                    dateLet,
                    ecritureDate, // ValidDate
                    string.Empty, // Montantdevise (requires multi-currency support)
                    string.Empty  // Idevise (requires multi-currency support)
                };

                sb.AppendLine(string.Join(Sep, fields));
            }
        }

        var preamble = Encoding.UTF8.GetPreamble();
        var content = Encoding.UTF8.GetBytes(sb.ToString());
        var result = new byte[preamble.Length + content.Length];
        Buffer.BlockCopy(preamble, 0, result, 0, preamble.Length);
        Buffer.BlockCopy(content, 0, result, preamble.Length, content.Length);

        return Result.Success(result);
    }

    private static string FormatAmount(decimal amount) =>
        amount.ToString("F2", CultureInfo.InvariantCulture);
}
