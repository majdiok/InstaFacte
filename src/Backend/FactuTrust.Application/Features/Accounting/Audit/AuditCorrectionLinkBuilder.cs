using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.Accounting;

namespace FactuTrust.Application.Features.Accounting.Audit;

/// <summary>Construit les liens de correction contextualisés pour le module d'audit comptable.</summary>
public static class AuditCorrectionLinkBuilder
{
    public sealed record LineContext(
        Guid? JournalEntryId,
        DateTime? EntryDate,
        string? AccountNumber,
        string? Label,
        string? PieceRef);

    public static AccountingAuditCorrectionLinkDto Build(
        string ruleCode,
        string? deepLinkRoute,
        string? accountRef,
        DateOnly? periodFrom,
        DateOnly? periodTo,
        int fiscalYear,
        Guid? anomalyId,
        IReadOnlyList<LineContext> lines)
    {
        var (route, queryParams, label) = ruleCode switch
        {
            "drafts" => EntrySearch(
                fiscalYear, periodFrom, periodTo,
                status: "0",
                label: "Voir les écritures en brouillard"),

            "unbalanced" => EntrySearchFromFirstLine(
                fiscalYear, periodFrom, periodTo, lines,
                label: "Corriger l'écriture déséquilibrée"),

            "suspense" => Balance(
                accountRef ?? lines.FirstOrDefault()?.AccountNumber ?? "471",
                fiscalYear,
                label: "Consulter le compte d'attente"),

            "unlettered" => Lettering(accountRef, fiscalYear, lines, label: "Ouvrir le lettrage"),

            "depreciation" => (
                "/accounting/fixed-assets/depreciation-run",
                new Dictionary<string, string> { ["fiscalYear"] = fiscalYear.ToString() },
                "Lancer les dotations d'amortissement"),

            "vat" => VatDeclaration(fiscalYear, lines, label: "Ouvrir la déclaration TVA"),

            "open-periods" or "health-out-of-period" => (
                "/accounting/closing",
                new Dictionary<string, string> { ["fiscalYear"] = fiscalYear.ToString() },
                "Gérer les périodes comptables"),

            "sequence-gaps" => EntrySearchFromFirstLine(
                fiscalYear, periodFrom, periodTo, lines,
                label: "Rechercher les pièces du journal"),

            "health-piece-duplicates" => EntrySearchFromFirstLine(
                fiscalYear, periodFrom, periodTo, lines,
                label: "Voir les doublons de pièce"),

            "health-orphan-accounts" => (
                "/accounting/chart",
                new Dictionary<string, string>(),
                "Ouvrir le plan comptable"),

            "health-thirdparty-mislink" => EntrySearch(
                fiscalYear, periodFrom, periodTo,
                account: lines.FirstOrDefault()?.AccountNumber,
                label: "Corriger les lignes tiers"),

            "entry-missing-attachment" => Journal(
                fiscalYear, periodFrom, periodTo,
                missingAttachment: true,
                label: "Voir les écritures sans justificatif"),

            "vat-deductible-no-proof" => EntrySearch(
                fiscalYear, periodFrom, periodTo,
                account: accountRef ?? "4366",
                label: "Voir la TVA déductible"),

            "recon-bank-incomplete" => (
                "/accounting/bank-reconciliation",
                new Dictionary<string, string>
                {
                    ["fiscalYear"] = fiscalYear.ToString(),
                    ["unmatchedOnly"] = "1"
                },
                "Rapprocher les lignes bancaires"),

            // ── Réviseur : famille Comptable ───────────────────────────────────────────────

            "entry-vat-vs-document" => EntrySearchFromFirstLine(
                fiscalYear, periodFrom, periodTo, lines,
                label: "Ouvrir l'écriture de TVA en écart"),

            "thirdparty-account-mismatch" => EntrySearch(
                fiscalYear, periodFrom, periodTo,
                account: lines.FirstOrDefault()?.AccountNumber,
                label: "Corriger l'imputation du tiers"),

            "lettering-orphan" => Lettering(
                accountRef, fiscalYear, lines, label: "Reprendre le lettrage"),

            "reversal-missing" => EntrySearchFromFirstLine(
                fiscalYear, periodFrom, periodTo, lines,
                label: "Ouvrir l'écriture à extourner"),

            // ── Réviseur : famille Documentaire ────────────────────────────────────────────

            "supplier-invoice-no-proof" => SupplierInvoices(
                fiscalYear, missingAttachment: true, label: "Voir les factures sans pièce"),

            "supplier-invoice-duplicate" => SupplierInvoices(
                fiscalYear, missingAttachment: false, label: "Comparer les factures en doublon"),

            "purchase-price-drift" => SupplierInvoices(
                fiscalYear, missingAttachment: false, label: "Vérifier les prix facturés"),

            // ── Réviseur : famille Trésorerie ──────────────────────────────────────────────

            "cash-negative" => Ledger(
                accountRef ?? lines.FirstOrDefault()?.AccountNumber ?? "54",
                fiscalYear,
                label: "Ouvrir le grand livre de caisse"),

            "cash-in-without-invoice" => EntrySearchFromFirstLine(
                fiscalYear, periodFrom, periodTo, lines,
                label: "Voir les encaissements non justifiés"),

            // ── Réviseur : famille Fiscale ─────────────────────────────────────────────────

            "withholding-missing-on-fees" => (
                "/withholding-tax",
                new Dictionary<string, string> { ["fiscalYear"] = fiscalYear.ToString() },
                "Ouvrir la retenue à la source"),

            "fodec-missing" => EntrySearch(
                fiscalYear, periodFrom, periodTo,
                account: accountRef ?? TunisianPostingAccounts.Fodec,
                label: "Voir les écritures de vente"),

            "vat-period-not-closed" => VatDeclaration(
                fiscalYear, lines, label: "Verrouiller la déclaration"),

            // ── Réviseur : famille Paie ────────────────────────────────────────────────────

            "payroll-cnss-regime-mismatch" or "payroll-overtime-out-of-regime"
                or "payroll-below-smig" => (
                "/payroll/runs",
                new Dictionary<string, string> { ["year"] = fiscalYear.ToString() },
                "Ouvrir les cycles de paie"),

            "payroll-dependent-no-proof" => (
                "/payroll/employees",
                new Dictionary<string, string>(),
                "Ouvrir les dossiers salariés"),

            // ── Réviseur : famille Fraude douce ────────────────────────────────────────────

            "threshold-structuring" => SupplierInvoices(
                fiscalYear, missingAttachment: false, label: "Examiner les factures concernées"),

            "supplier-created-then-paid" => (
                "/suppliers",
                new Dictionary<string, string>(),
                "Ouvrir la fiche fournisseur"),

            "self-validation" or "off-hours-entry" or "backdated-entry" => EntrySearchFromFirstLine(
                fiscalYear, periodFrom, periodTo, lines,
                label: "Ouvrir l'écriture concernée"),

            _ => Fallback(deepLinkRoute, fiscalYear, periodFrom, periodTo)
        };

        if (anomalyId is { } id)
            queryParams["auditAnomalyId"] = id.ToString();

        return new AccountingAuditCorrectionLinkDto
        {
            Route = route,
            QueryParams = queryParams,
            Label = label
        };
    }

    private static (string Route, Dictionary<string, string> Params, string Label) Fallback(
        string? deepLinkRoute,
        int fiscalYear,
        DateOnly? periodFrom,
        DateOnly? periodTo)
    {
        var route = string.IsNullOrWhiteSpace(deepLinkRoute) ? "/accounting/health" : deepLinkRoute;
        var p = YearRange(fiscalYear, periodFrom, periodTo);
        return (route, p, "Ouvrir l'écran de correction");
    }

    private static (string Route, Dictionary<string, string> Params, string Label) EntrySearch(
        int fiscalYear,
        DateOnly? periodFrom,
        DateOnly? periodTo,
        string? account = null,
        string? journalCode = null,
        string? entryNumber = null,
        string? status = null,
        string label = "Rechercher les écritures")
    {
        var p = YearRange(fiscalYear, periodFrom, periodTo);
        p["autoSearch"] = "1";
        if (!string.IsNullOrWhiteSpace(account)) p["account"] = account.Trim();
        if (!string.IsNullOrWhiteSpace(journalCode)) p["journalCode"] = journalCode.Trim();
        if (!string.IsNullOrWhiteSpace(entryNumber)) p["entryNumber"] = entryNumber.Trim();
        if (!string.IsNullOrWhiteSpace(status)) p["status"] = status;
        return ("/accounting/entry-search", p, label);
    }

    private static (string Route, Dictionary<string, string> Params, string Label) EntrySearchFromFirstLine(
        int fiscalYear,
        DateOnly? periodFrom,
        DateOnly? periodTo,
        IReadOnlyList<LineContext> lines,
        string label)
    {
        var line = lines.FirstOrDefault();
        ParseJournalPiece(line?.PieceRef ?? line?.Label, out var journalCode, out var entryNumber);
        if (string.IsNullOrEmpty(journalCode) && !string.IsNullOrWhiteSpace(line?.Label))
            ParseJournalPiece(line.Label, out journalCode, out entryNumber);

        return EntrySearch(fiscalYear, periodFrom, periodTo,
            journalCode: journalCode,
            entryNumber: entryNumber,
            account: line?.AccountNumber,
            label: label);
    }

    private static (string Route, Dictionary<string, string> Params, string Label) Balance(
        string account,
        int fiscalYear,
        string label)
    {
        var from = new DateOnly(fiscalYear, 1, 1);
        var to = new DateOnly(fiscalYear, 12, 31);
        return (
            "/accounting/balance",
            new Dictionary<string, string>
            {
                ["account"] = account,
                ["from"] = from.ToString("yyyy-MM-dd"),
                ["to"] = to.ToString("yyyy-MM-dd")
            },
            label);
    }

    private static (string Route, Dictionary<string, string> Params, string Label) Lettering(
        string? accountRef,
        int fiscalYear,
        IReadOnlyList<LineContext> lines,
        string label)
    {
        var lineAccount = lines.FirstOrDefault()?.AccountNumber;
        var account = ResolveLetteringAccount(accountRef, lineAccount);
        var from = new DateOnly(fiscalYear, 1, 1);
        var to = new DateOnly(fiscalYear, 12, 31);
        var p = new Dictionary<string, string>
        {
            ["account"] = account,
            ["from"] = from.ToString("yyyy-MM-dd"),
            ["to"] = to.ToString("yyyy-MM-dd"),
            ["unletteredOnly"] = "1",
            ["autoLoad"] = "1"
        };
        if (accountRef?.Contains("4011", StringComparison.Ordinal) == true
            && accountRef.Contains("4111", StringComparison.Ordinal))
            p["accountHint"] = "4011";
        return ("/accounting/lettering", p, label);
    }

    private static (string Route, Dictionary<string, string> Params, string Label) Journal(
        int fiscalYear,
        DateOnly? periodFrom,
        DateOnly? periodTo,
        bool missingAttachment,
        string label)
    {
        var p = YearRange(fiscalYear, periodFrom, periodTo);
        p["autoLoad"] = "1";
        if (missingAttachment) p["missingAttachment"] = "1";
        return ("/accounting/journal", p, label);
    }

    /// <summary>Grand livre d'un compte sur l'exercice — la vue qui montre un solde jour par jour.</summary>
    private static (string Route, Dictionary<string, string> Params, string Label) Ledger(
        string account,
        int fiscalYear,
        string label) =>
        ("/accounting/ledger",
            new Dictionary<string, string>
            {
                ["account"] = account,
                ["from"] = new DateOnly(fiscalYear, 1, 1).ToString("yyyy-MM-dd"),
                ["to"] = new DateOnly(fiscalYear, 12, 31).ToString("yyyy-MM-dd"),
                ["autoLoad"] = "1"
            },
            label);

    private static (string Route, Dictionary<string, string> Params, string Label) SupplierInvoices(
        int fiscalYear,
        bool missingAttachment,
        string label)
    {
        var p = new Dictionary<string, string>
        {
            ["from"] = new DateOnly(fiscalYear, 1, 1).ToString("yyyy-MM-dd"),
            ["to"] = new DateOnly(fiscalYear, 12, 31).ToString("yyyy-MM-dd"),
            ["autoLoad"] = "1"
        };
        if (missingAttachment) p["missingAttachment"] = "1";
        return ("/supplier-invoices", p, label);
    }

    private static (string Route, Dictionary<string, string> Params, string Label) VatDeclaration(
        int fiscalYear,
        IReadOnlyList<LineContext> lines,
        string label)
    {
        var p = new Dictionary<string, string> { ["fiscalYear"] = fiscalYear.ToString() };
        var firstMonth = lines
            .Select(l => l.EntryDate?.Month)
            .FirstOrDefault(m => m is > 0);
        if (firstMonth is > 0)
            p["periodMonth"] = firstMonth.Value.ToString();
        return ("/accounting/vat-declaration", p, label);
    }

    private static Dictionary<string, string> YearRange(int fiscalYear, DateOnly? periodFrom, DateOnly? periodTo)
    {
        var from = periodFrom ?? new DateOnly(fiscalYear, 1, 1);
        var to = periodTo ?? new DateOnly(fiscalYear, 12, 31);
        return new Dictionary<string, string>
        {
            ["from"] = from.ToString("yyyy-MM-dd"),
            ["to"] = to.ToString("yyyy-MM-dd")
        };
    }

    private static string ResolveLetteringAccount(string? accountRef, string? lineAccount)
    {
        if (!string.IsNullOrWhiteSpace(lineAccount))
        {
            if (lineAccount.StartsWith("4111", StringComparison.Ordinal)) return "4111";
            if (lineAccount.StartsWith("4011", StringComparison.Ordinal)) return "4011";
        }
        if (!string.IsNullOrWhiteSpace(accountRef))
        {
            if (accountRef.Contains("4111", StringComparison.Ordinal)) return "4111";
            if (accountRef.Contains("4011", StringComparison.Ordinal)) return "4011";
        }
        return "4111";
    }

    /// <summary>Parse « JC-21 » ou libellé similaire en journal + numéro de pièce.</summary>
    public static void ParseJournalPiece(string? value, out string? journalCode, out string? entryNumber)
    {
        journalCode = null;
        entryNumber = null;
        if (string.IsNullOrWhiteSpace(value)) return;

        var s = value.Trim();
        var dash = s.LastIndexOf('-');
        if (dash > 0 && dash < s.Length - 1)
        {
            journalCode = s[..dash].Trim();
            entryNumber = s[(dash + 1)..].Trim();
            return;
        }

        if (s.StartsWith("Journal ", StringComparison.OrdinalIgnoreCase))
            journalCode = s["Journal ".Length..].Trim();
    }
}
