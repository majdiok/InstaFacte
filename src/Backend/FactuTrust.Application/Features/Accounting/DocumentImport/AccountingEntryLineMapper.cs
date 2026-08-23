using FactuTrust.Domain.Common;

namespace FactuTrust.Application.Features.Accounting.DocumentImport;

/// <summary>
/// Comptes retenus pour construire une proposition. Le service de proposition les résout
/// (profil comptable du tiers, ventilation par compte de produit) et les passe au mapper.
/// </summary>
public sealed record ProposalAccountPlan
{
    /// <summary>Compte collectif du tiers : 4111 en vente, 4011 en achat, ou le compte du profil.</summary>
    public required string ThirdPartyAccount { get; init; }

    /// <summary>
    /// Ventilation du HT par compte de produit/charge. Vide ⇒ une seule ligne sur
    /// <see cref="DefaultResultAccount"/> pour la totalité du HT.
    /// </summary>
    public IReadOnlyDictionary<string, decimal> HtByAccount { get; init; } =
        new Dictionary<string, decimal>(StringComparer.Ordinal);

    /// <summary>Compte de produit (vente) ou de charge (achat) par défaut.</summary>
    public required string DefaultResultAccount { get; init; }

    public required string VatAccount { get; init; }
    public required string FodecAccount { get; init; }
    public required string StampAccount { get; init; }
}

/// <summary>
/// Construit les lignes d'une écriture à partir d'une pièce extraite. Fonction pure, sans accès
/// base : c'est ce qui la rend testable en table sur les cas piégeux (plusieurs taux, remises,
/// avoirs, montants au millime).
///
/// Invariant central : la ligne de contrepartie (client/fournisseur) n'est JAMAIS lue sur la
/// pièce. Elle vaut la somme des autres lignes, déjà arrondies au millime. L'écriture proposée
/// est donc équilibrée par construction et ne peut pas être rejetée par
/// <c>JournalEntry.BuildLines</c>. Le TTC imprimé sert uniquement de contrôle.
/// </summary>
public static class AccountingEntryLineMapper
{
    public static IReadOnlyList<ProposedLineDto> Build(
        AccountingDocumentExtractionDto document,
        string direction,
        ProposalAccountPlan accounts,
        ProposedThirdPartyDto? thirdParty)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(accounts);

        var isSale = direction == DocumentDirections.Sale;
        var isCreditNote = document.DocumentType == DocumentTypes.CreditNote;

        // Un avoir inverse les sens. Les montants restent positifs : le sens est porté par la colonne.
        var resultOnCredit = isSale ^ isCreditNote;

        var reference = document.DocumentNumber ?? "sans numéro";
        var lines = new List<ProposedLineDto>();

        // --- Produits (vente) ou charges (achat), ventilés par compte -----------------------
        var htByAccount = accounts.HtByAccount.Count > 0
            ? accounts.HtByAccount
            : new Dictionary<string, decimal>(StringComparer.Ordinal)
            {
                [accounts.DefaultResultAccount] = document.TotalHt ?? 0m
            };

        foreach (var (account, rawAmount) in htByAccount.OrderBy(kv => kv.Key, StringComparer.Ordinal))
        {
            var amount = MillimeRounding.Round(Math.Abs(rawAmount));
            if (amount == 0m)
                continue;

            lines.Add(new ProposedLineDto
            {
                AccountNumber = account,
                Role = isSale ? ProposedLineRoles.Revenue : ProposedLineRoles.Expense,
                Label = BuildLabel(isSale, isCreditNote, resultLine: true, reference),
                Debit = resultOnCredit ? 0m : amount,
                Credit = resultOnCredit ? amount : 0m
            });
        }

        // --- TVA, une ligne par taux --------------------------------------------------------
        foreach (var bucket in document.VatBreakdown.OrderBy(b => b.RatePercent))
        {
            var amount = MillimeRounding.Round(Math.Abs(bucket.VatAmount));
            if (amount == 0m)
                continue;

            var vatLabel = isSale ? "TVA collectée" : "TVA déductible";
            lines.Add(new ProposedLineDto
            {
                AccountNumber = accounts.VatAccount,
                Role = ProposedLineRoles.Vat,
                Label = $"{vatLabel} {bucket.RatePercent}% — {reference}",
                Debit = resultOnCredit ? 0m : amount,
                Credit = resultOnCredit ? amount : 0m,
                VatRatePercent = bucket.RatePercent
            });
        }

        // --- FODEC ---------------------------------------------------------------------------
        var fodec = MillimeRounding.Round(Math.Abs(document.FodecAmount ?? 0m));
        if (fodec > 0m)
        {
            // À la vente le FODEC est collecté pour l'État (compte de dette). À l'achat il fait
            // partie du coût et reste sur le compte de charge.
            lines.Add(new ProposedLineDto
            {
                AccountNumber = isSale ? accounts.FodecAccount : accounts.DefaultResultAccount,
                Role = ProposedLineRoles.Fodec,
                Label = $"FODEC — {reference}",
                Debit = resultOnCredit ? 0m : fodec,
                Credit = resultOnCredit ? fodec : 0m
            });
        }

        // --- Timbre fiscal --------------------------------------------------------------------
        var stamp = MillimeRounding.Round(Math.Abs(document.FiscalStampAmount ?? 0m));
        if (stamp > 0m)
        {
            // Vente : collecté pour l'État → crédit d'un compte de dette (4371).
            // Achat : supporté par l'entreprise → débit d'un compte de charge (6654).
            lines.Add(new ProposedLineDto
            {
                AccountNumber = accounts.StampAccount,
                Role = ProposedLineRoles.Stamp,
                Label = $"Timbre fiscal — {reference}",
                Debit = resultOnCredit ? 0m : stamp,
                Credit = resultOnCredit ? stamp : 0m
            });
        }

        // --- Contrepartie : SOMME des lignes ci-dessus, jamais le TTC du document -------------
        var counterpart = MillimeRounding.Round(
            lines.Sum(l => resultOnCredit ? l.Credit : l.Debit));

        if (counterpart > 0m)
        {
            var counterpartLine = new ProposedLineDto
            {
                AccountNumber = accounts.ThirdPartyAccount,
                Role = ProposedLineRoles.ThirdParty,
                Label = BuildLabel(isSale, isCreditNote, resultLine: false, reference),
                Debit = resultOnCredit ? counterpart : 0m,
                Credit = resultOnCredit ? 0m : counterpart,
                ThirdPartyId = thirdParty?.MatchedId,
                ThirdPartyKind = thirdParty?.MatchedId is null ? null : thirdParty.Kind
            };

            // Convention de lecture : la contrepartie ouvre l'écriture en vente, la ferme en achat —
            // même ordre que la comptabilisation automatique existante.
            if (isSale)
                lines.Insert(0, counterpartLine);
            else
                lines.Add(counterpartLine);
        }

        return lines;
    }

    private static string BuildLabel(bool isSale, bool isCreditNote, bool resultLine, string reference)
    {
        if (resultLine)
        {
            return (isSale, isCreditNote) switch
            {
                (true, false) => $"Ventes {reference}",
                (true, true) => $"Ventes — Avoir {reference}",
                (false, false) => $"Achats — {reference}",
                (false, true) => $"Achats — Avoir {reference}"
            };
        }

        return (isSale, isCreditNote) switch
        {
            (true, false) => $"Client — {reference}",
            (true, true) => $"Avoir client — {reference}",
            (false, false) => $"Fournisseur — {reference}",
            (false, true) => $"Avoir fournisseur — {reference}"
        };
    }
}
