using FactuTrust.Application.Accounting;
using FactuTrust.Application.DTOs;

namespace FactuTrust.Infrastructure.Services;

/// <summary>
/// Assemble les notes annexes détaillées (compte / intitulé / N / N-1) à partir des soldes nets.
/// Indépendant de <see cref="NctStatementBuilder"/> pour éviter toute régression sur les notes agrégées.
/// </summary>
public static class NctDetailedNotesBuilder
{
    public static IReadOnlyList<NctDetailedNoteDto> Build(
        IReadOnlyDictionary<string, decimal> currentNet,
        IReadOnlyDictionary<string, decimal> previousNet,
        IReadOnlyDictionary<string, string>? accountLabels = null)
    {
        var labels = accountLabels ?? new Dictionary<string, string>();
        var accounts = new HashSet<string>(currentNet.Keys, StringComparer.Ordinal);
        foreach (var key in previousNet.Keys)
            accounts.Add(key);

        var result = new List<NctDetailedNoteDto>();
        foreach (var def in NctDetailedNoteCatalog.All)
        {
            var lines = new List<NctDetailedNoteLineDto>();
            foreach (var account in accounts.OrderBy(a => a, StringComparer.Ordinal))
            {
                if (!def.AccountPredicate(account))
                    continue;

                currentNet.TryGetValue(account, out var curNet);
                previousNet.TryGetValue(account, out var prevNet);
                if (curNet == 0m && prevNet == 0m)
                    continue;

                var amount = ApplySign(curNet, def.AmountSign);
                var previous = ApplySign(prevNet, def.AmountSign);
                var label = labels.TryGetValue(account, out var lbl) && !string.IsNullOrWhiteSpace(lbl)
                    ? lbl
                    : account;

                lines.Add(new NctDetailedNoteLineDto
                {
                    AccountNumber = account,
                    Label = label,
                    Amount = amount,
                    PreviousAmount = previous
                });
            }

            if (lines.Count == 0)
                continue;

            result.Add(new NctDetailedNoteDto
            {
                Number = def.Number,
                Title = def.Title,
                Family = def.Family,
                Lines = lines,
                Total = lines.Sum(l => l.Amount),
                PreviousTotal = lines.Sum(l => l.PreviousAmount)
            });
        }

        return result;
    }

    private static decimal ApplySign(decimal net, NctNoteAmountSign sign) =>
        sign == NctNoteAmountSign.NegateNet ? -net : net;
}
