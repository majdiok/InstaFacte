using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.Configuration;
using FactuTrust.Application.Features.Accounting;
using FactuTrust.Application.Features.Accounting.Services;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Entities.Payroll;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.Services.Payroll;
using FactuTrust.Domain.ValueObjects;
using FactuTrust.Infrastructure.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FactuTrust.Infrastructure.Services;

public sealed class AccountingService : IAccountingService
{
    public const string SourceInvoice = "Invoice";
    public const string SourceInvoiceCreditNote = "InvoiceCreditNote";
    public const string SourcePayment = "Payment";
    public const string SourceInvoiceCancelled = "InvoiceCancelled";
    public const string SourceSupplierInvoice = "SupplierInvoice";
    public const string SourceSupplierPayment = "SupplierPayment";
    public const string SourceSupplierInvoiceWithholding = "SupplierInvoiceWithholding";
    public const string SourceBankDeposit = "BankDeposit";
    public const string SourceCashOperation = "CashOperation";
    public const string SourceOpeningBalance = "OpeningBalance";
    public const string SourceFixedAssetAcquisition = "FixedAssetAcquisition";
    public const string SourceFixedAssetDepreciation = "FixedAssetDepreciation";
    public const string SourceFixedAssetDisposal = "FixedAssetDisposal";
    public const string SourceManualReversal = "ManualReversal";
    public const string SourcePayrollRun = "PayrollRun";
    public const string SourcePayrollRunCancelled = "PayrollRunCancelled";
    public const string SourcePayrollPayment = "PayrollPayment";
    public const string SourcePayrollPaymentCancelled = "PayrollPaymentCancelled";
    public const string SourceCnssContributionPayment = "CnssContributionPayment";
    public const string SourceCnssContributionPaymentCancelled = "CnssContributionPaymentCancelled";

    /// <summary>Clé d'idempotence du 2ᵉ volet d'un effet (encaissement/paiement à échéance), sur l'id du paiement.</summary>
    public const string SourceEffetSettlement = "EffetSettlement";

    public const string FixedAssetJournalCode = "JIM";

    /// <summary>Comptes d'effets de commerce (traites) — alignés sur le seed du plan comptable tenant.</summary>
    public const string ClientEffetAccountNumber = "413";   // Clients - effets à recevoir (NCT 01)
    public const string SupplierEffetAccountNumber = "403";  // Fournisseurs - effets à payer

    /// <summary>Compte FODEC collecté (dette envers l'État). Crédité à la vente, débité en avoir.</summary>
    public const string FodecAccountNumber = TunisianPostingAccounts.Fodec;

    /// <summary>Journal des Opérations Diverses : réception/acceptation d'effet et impayé (aucun mouvement de trésorerie).</summary>
    public const string MiscJournalCode = "JOD";
    /// <summary>Journal de Banque : encaissement/paiement effectif d'un effet à échéance.</summary>
    public const string BankJournalCode = "JB";

    /// <summary>
    /// Comptes de solde du résultat pour les écritures d'à-nouveau (classe 1).
    /// Alignés sur le seed du plan comptable tenant (<c>AddAccountingModule_Tenant</c>) : 131 bénéfice, 135 perte.
    /// </summary>
    public const string OpeningBalanceProfitAccountNumber = "131";

    public const string OpeningBalanceLossAccountNumber = "135";

    private readonly IChartOfAccountRepository _chartOfAccounts;
    private readonly IAccountingPeriodService _periodService;
    private readonly IJournalEntryRepository _journalEntries;
    private readonly IWithholdingTaxRepository _withholdingTaxTypes;
    private readonly ITenantDbContextFactory _contextFactory;
    private readonly ILogger<AccountingService> _logger;
    private readonly AccountingSettings _settings;

    public AccountingService(
        IChartOfAccountRepository chartOfAccounts,
        IAccountingPeriodService periodService,
        IJournalEntryRepository journalEntries,
        IWithholdingTaxRepository withholdingTaxTypes,
        ITenantDbContextFactory contextFactory,
        ILogger<AccountingService> logger,
        IOptions<AccountingSettings> settings)
    {
        _chartOfAccounts = chartOfAccounts;
        _periodService = periodService;
        _journalEntries = journalEntries;
        _withholdingTaxTypes = withholdingTaxTypes;
        _contextFactory = contextFactory;
        _logger = logger;
        _settings = settings.Value;
    }

    /// <summary>
    /// Statut des écritures générées automatiquement : brouillon quand le workflow brouillard est
    /// activé (validation par l'expert), sinon validé (comportement historique, aucun impact états).
    /// </summary>
    private JournalEntryStatus NewEntryStatus =>
        _settings.BrouillardEnabled ? JournalEntryStatus.Brouillon : JournalEntryStatus.Validee;

    /// <summary>
    /// Validates that all account numbers in the given lines exist in the Chart of Accounts.
    /// If a sub-account is missing but its parent account exists, the sub-account is
    /// auto-created to prevent data loss from a single missing configuration entry.
    /// Returns a failure result with the first missing account, or success if all accounts exist.
    /// </summary>
    private async Task<Result> ValidateAccountsExistAsync(
        IEnumerable<JournalLineInput> lines,
        CancellationToken cancellationToken)
    {
        var checkedAccounts = new HashSet<string>(StringComparer.Ordinal);
        foreach (var line in lines)
        {
            var acc = line.AccountNumber?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(acc) || !checkedAccounts.Add(acc))
                continue;

            var account = await _chartOfAccounts.GetByAccountNumberAsync(acc, cancellationToken);
            if (account is null)
            {
                // Attempt auto-creation: find nearest parent account in the chart
                var created = await TryAutoCreateSubAccountAsync(acc, cancellationToken);
                if (!created)
                {
                    _logger.LogError(
                        "Account {AccountNumber} not found in chart of accounts and no parent exists for auto-creation. " +
                        "Planned journal lines: {@PlannedLines}",
                        acc, lines.Select(l => new { l.AccountNumber, l.Label, l.Debit, l.Credit }));
                    return Result.Failure(Error.Validation("AccountNumber",
                        $"Le compte {acc} n'existe pas dans le plan comptable."));
                }
            }
            else if (!account.IsActive)
            {
                _logger.LogWarning("Account {AccountNumber} is deactivated", acc);
                return Result.Failure(Error.Validation("AccountNumber",
                    $"Le compte {acc} est désactivé."));
            }
        }

        return Result.Success();
    }

    /// <summary>
    /// Attempts to auto-create a missing sub-account by finding its nearest parent
    /// in the chart of accounts hierarchy. This follows standard ERP practice for
    /// Tunisian SCE where sub-accounts are created as needed.
    /// </summary>
    /// <returns>True if the sub-account was successfully created, false otherwise.</returns>
    private async Task<bool> TryAutoCreateSubAccountAsync(string accountNumber, CancellationToken cancellationToken)
    {
        // Try progressively shorter prefixes to find a parent account
        // e.g., for "4371": try "437", then "43", then "4"
        for (var len = accountNumber.Length - 1; len >= 1; len--)
        {
            var parentNumber = accountNumber[..len];
            var parent = await _chartOfAccounts.GetByAccountNumberAsync(parentNumber, cancellationToken);
            if (parent is null || !parent.IsActive)
                continue;

            // Found a valid parent — create the sub-account
            var accountClass = int.Parse(accountNumber[..1].ToString());
            var label = $"{parent.Label} — {accountNumber}";
            var createResult = ChartOfAccount.Create(
                accountNumber,
                label,
                accountClass,
                parentNumber,
                parent.NatureType,
                isSystem: false);

            if (createResult.IsFailure)
            {
                _logger.LogWarning(
                    "Failed to auto-create sub-account {AccountNumber} under parent {ParentNumber}: {Error}",
                    accountNumber, parentNumber, createResult.Error.Description);
                return false;
            }

            var newAccount = createResult.Value;
            newAccount.SetAuditInfo("system");
            await _chartOfAccounts.AddAsync(newAccount, cancellationToken);

            _logger.LogInformation(
                "Auto-created missing sub-account {AccountNumber} under parent {ParentNumber} (class {AccountClass})",
                accountNumber, parentNumber, accountClass);

            return true;
        }

        return false;
    }


    public async Task<Result> GenerateInvoiceSaleEntryAsync(Invoice invoice, CancellationToken cancellationToken = default)
    {
        if (await _chartOfAccounts.CountAsync(cancellationToken) == 0)
        {
            _logger.LogDebug("Chart of accounts empty — skip accounting for tenant");
            return Result.Success();
        }

        var existing = await _journalEntries.GetBySourceAsync(SourceInvoice, invoice.Id, cancellationToken);
        if (existing is not null)
            return Result.Success();

        var periodResult = await _periodService.EnsureOpenPeriodAsync(invoice.IssueDate, cancellationToken);
        if (periodResult.IsFailure)
            return Result.Failure(periodResult.Error);

        var period = periodResult.Value;
        var currency = invoice.TotalAmount.Currency;
        var lines = new List<JournalLineInput>();

        lines.Add(new JournalLineInput(
            TunisianPostingAccounts.Client,
            $"Client — {invoice.Number.Value}",
            invoice.TotalAmount.Amount,
            0,
            invoice.ClientId,
            ThirdPartyKind.Client));

        var htByRevenue = new Dictionary<string, decimal>(StringComparer.Ordinal);
        foreach (var line in invoice.Lines)
        {
            var acc = RevenueAccountForLine(line);
            var ht = line.SubTotal.Amount;
            if (!htByRevenue.TryGetValue(acc, out var sum))
                sum = 0;
            htByRevenue[acc] = sum + ht;
        }

        foreach (var kv in htByRevenue.OrderBy(k => k.Key))
        {
            if (kv.Value <= 0)
                continue;
            lines.Add(new JournalLineInput(
                kv.Key,
                $"Ventes {invoice.Number.Value}",
                0,
                kv.Value,
                null,
                ThirdPartyKind.None));
        }

        var vatByRate = new Dictionary<int, decimal>();
        foreach (var line in invoice.Lines)
        {
            var rate = (int)line.VatRate;
            if (rate == 0)
                continue;
            if (!vatByRate.TryGetValue(rate, out var v))
                v = 0;
            vatByRate[rate] = v + line.VatAmount.Amount;
        }

        foreach (var kv in vatByRate.OrderBy(k => k.Key))
        {
            if (kv.Value <= 0)
                continue;
            lines.Add(new JournalLineInput(
                TunisianPostingAccounts.VatCollected,
                $"TVA collectée {kv.Key}% — {invoice.Number.Value}",
                0,
                kv.Value,
                null,
                ThirdPartyKind.None));
        }

        var fodec = invoice.FodecAmount.Amount;
        if (Math.Abs(fodec) > 0.0005m)
        {
            lines.Add(new JournalLineInput(
                FodecAccountNumber,
                $"FODEC — {invoice.Number.Value}",
                0,
                fodec,
                null,
                ThirdPartyKind.None));
        }

        var stamp = invoice.FiscalStampAmount.Amount;
        if (Math.Abs(stamp) > 0.0005m)
        {
            const string stampAccount = TunisianPostingAccounts.FiscalStampOnSale;
            if (stamp > 0)
            {
                lines.Add(new JournalLineInput(
                    stampAccount,
                    $"Timbre fiscal — {invoice.Number.Value}",
                    0,
                    stamp,
                    null,
                    ThirdPartyKind.None));
            }
            else
            {
                lines.Add(new JournalLineInput(
                    stampAccount,
                    $"Timbre fiscal — {invoice.Number.Value}",
                    Math.Abs(stamp),
                    0,
                    null,
                    ThirdPartyKind.None));
            }
        }

        var accountValidation = await ValidateAccountsExistAsync(lines, cancellationToken);
        if (accountValidation.IsFailure)
            return accountValidation;

        var n = await _journalEntries.ReserveNextEntryNumberAsync(
            TunisianPostingAccounts.SalesJournalCode, invoice.IssueDate.Year, cancellationToken);
        var create = JournalEntry.Create(
            n,
            TunisianPostingAccounts.SalesJournalCode,
            invoice.IssueDate,
            $"Facture vente {invoice.Number.Value}",
            period.Id,
            true,
            SourceInvoice,
            invoice.Id,
            lines,
            currency);

        if (create.IsFailure)
            return Result.Failure(create.Error);

        var entry = create.Value;
        entry.MarkInitialStatus(NewEntryStatus);
        entry.SetAuditInfo("system", false);
        await _journalEntries.AddAsync(entry, cancellationToken);
        return Result.Success();
    }

    /// <summary>
    /// Posts the credit-note journal entry. Mirrors <see cref="GenerateInvoiceSaleEntryAsync"/>
    /// with debit/credit reversed, and uses absolute magnitudes for journal-line scalars
    /// (debit and credit are always positive — direction is the column).
    /// </summary>
    public async Task<Result> GenerateInvoiceCreditNoteEntryAsync(Invoice invoice, CancellationToken cancellationToken = default)
    {
        if (await _chartOfAccounts.CountAsync(cancellationToken) == 0)
        {
            _logger.LogDebug("Chart of accounts empty — skip accounting for tenant");
            return Result.Success();
        }

        var existing = await _journalEntries.GetBySourceAsync(SourceInvoiceCreditNote, invoice.Id, cancellationToken);
        if (existing is not null)
            return Result.Success();

        var periodResult = await _periodService.EnsureOpenPeriodAsync(invoice.IssueDate, cancellationToken);
        if (periodResult.IsFailure)
            return Result.Failure(periodResult.Error);

        var period = periodResult.Value;
        var currency = invoice.TotalAmount.Currency;
        var lines = new List<JournalLineInput>();

        // CREDIT 4111 (Client) for the absolute total — we owe the client this amount.
        lines.Add(new JournalLineInput(
            TunisianPostingAccounts.Client,
            $"Avoir client — {invoice.Number.Value}",
            0,
            Math.Abs(invoice.TotalAmount.Amount),
            invoice.ClientId,
            ThirdPartyKind.Client));

        var htByRevenue = new Dictionary<string, decimal>(StringComparer.Ordinal);
        foreach (var line in invoice.Lines)
        {
            var acc = RevenueAccountForLine(line);
            var ht = line.SubTotal.Amount;
            if (!htByRevenue.TryGetValue(acc, out var sum))
                sum = 0;
            htByRevenue[acc] = sum + ht;
        }

        // DEBIT the original revenue accounts — we reverse the previously-recognised revenue.
        foreach (var kv in htByRevenue.OrderBy(k => k.Key))
        {
            if (kv.Value <= 0)
                continue;
            lines.Add(new JournalLineInput(
                kv.Key,
                $"Avoir ventes {invoice.Number.Value}",
                kv.Value,
                0,
                null,
                ThirdPartyKind.None));
        }

        var vatByRate = new Dictionary<int, decimal>();
        foreach (var line in invoice.Lines)
        {
            var rate = (int)line.VatRate;
            if (rate == 0)
                continue;
            if (!vatByRate.TryGetValue(rate, out var v))
                v = 0;
            vatByRate[rate] = v + line.VatAmount.Amount;
        }

        // DEBIT TVA collectée — we cancel the previously-collected VAT.
        foreach (var kv in vatByRate.OrderBy(k => k.Key))
        {
            if (kv.Value <= 0)
                continue;
            lines.Add(new JournalLineInput(
                TunisianPostingAccounts.VatCollected,
                $"TVA collectée {kv.Key}% — Avoir {invoice.Number.Value}",
                kv.Value,
                0,
                null,
                ThirdPartyKind.None));
        }

        var fodec = invoice.FodecAmount.Amount;
        if (Math.Abs(fodec) > 0.0005m)
        {
            lines.Add(new JournalLineInput(
                FodecAccountNumber,
                $"FODEC — Avoir {invoice.Number.Value}",
                Math.Abs(fodec),
                0,
                null,
                ThirdPartyKind.None));
        }

        // Fiscal stamp — for AVO it is stored as negative; reverse direction vs sale.
        var stamp = invoice.FiscalStampAmount.Amount;
        if (Math.Abs(stamp) > 0.0005m)
        {
            const string stampAccount = TunisianPostingAccounts.FiscalStampOnSale;
            if (stamp < 0)
            {
                // Negative stamp (typical for AVO) = we owe back the stamp = DEBIT 4371.
                lines.Add(new JournalLineInput(
                    stampAccount,
                    $"Timbre fiscal — Avoir {invoice.Number.Value}",
                    Math.Abs(stamp),
                    0,
                    null,
                    ThirdPartyKind.None));
            }
            else
            {
                lines.Add(new JournalLineInput(
                    stampAccount,
                    $"Timbre fiscal — Avoir {invoice.Number.Value}",
                    0,
                    stamp,
                    null,
                    ThirdPartyKind.None));
            }
        }

        var accountValidation = await ValidateAccountsExistAsync(lines, cancellationToken);
        if (accountValidation.IsFailure)
            return accountValidation;

        var n = await _journalEntries.ReserveNextEntryNumberAsync(
            TunisianPostingAccounts.SalesJournalCode, invoice.IssueDate.Year, cancellationToken);
        var create = JournalEntry.Create(
            n,
            TunisianPostingAccounts.SalesJournalCode,
            invoice.IssueDate,
            $"Avoir vente {invoice.Number.Value}",
            period.Id,
            true,
            SourceInvoiceCreditNote,
            invoice.Id,
            lines,
            currency);

        if (create.IsFailure)
            return Result.Failure(create.Error);

        var entry = create.Value;
        entry.MarkInitialStatus(NewEntryStatus);
        entry.SetAuditInfo("system", false);
        await _journalEntries.AddAsync(entry, cancellationToken);
        return Result.Success();
    }

    public async Task<Result> GenerateClientPaymentEntryAsync(Payment payment, CancellationToken cancellationToken = default)
    {
        if (await _chartOfAccounts.CountAsync(cancellationToken) == 0)
            return Result.Success();

        var existing = await _journalEntries.GetBySourceAsync(SourcePayment, payment.Id, cancellationToken);
        if (existing is not null)
            return Result.Success();

        var invoice = payment.Invoice;
        var date = payment.PaymentDate;
        var periodResult = await _periodService.EnsureOpenPeriodAsync(date, cancellationToken);
        if (periodResult.IsFailure)
            return Result.Failure(periodResult.Error);

        var period = periodResult.Value;
        var currency = payment.Amount.Currency;
        // Traite (effet de commerce) : la créance client n'est pas encaissée mais transformée en
        // effet à recevoir (413), sans mouvement de trésorerie → journal des opérations diverses.
        var isEffet = payment.Method == PaymentMethod.Traite;
        var debitAccount = isEffet ? ClientEffetAccountNumber : TreasuryAccount(payment.Method);
        var debitLabel = isEffet ? $"Effet à recevoir — {invoice.Number.Value}" : $"Encaissement — {invoice.Number.Value}";
        var netAmount = payment.Amount.Amount;
        var withholding = payment.ClientWithholdingAmount ?? 0m;
        var totalApplied = netAmount + withholding;

        var lines = new List<JournalLineInput>
        {
            new(debitAccount, debitLabel, netAmount, 0, null, ThirdPartyKind.None),
        };

        if (withholding > 0)
        {
            lines.Add(new JournalLineInput("4341", $"Retenue à la source subie — {invoice.Number.Value}", withholding, 0, null, ThirdPartyKind.None));
        }

        lines.Add(new JournalLineInput(TunisianPostingAccounts.Client, $"Client — {invoice.Number.Value}", 0, totalApplied, invoice.ClientId, ThirdPartyKind.Client));

        var accountValidation = await ValidateAccountsExistAsync(lines, cancellationToken);
        if (accountValidation.IsFailure)
            return accountValidation;

        var journal = isEffet ? MiscJournalCode : (payment.Method == PaymentMethod.Cash ? "JC" : "JB");
        var description = isEffet
            ? $"Effet à recevoir facture {invoice.Number.Value}"
            : $"Encaissement facture {invoice.Number.Value}";
        var n = await _journalEntries.ReserveNextEntryNumberAsync(journal, date.Year, cancellationToken);
        var create = JournalEntry.Create(
            n,
            journal,
            date,
            description,
            period.Id,
            true,
            SourcePayment,
            payment.Id,
            lines,
            currency);

        if (create.IsFailure)
            return Result.Failure(create.Error);

        var entry = create.Value;
        entry.MarkInitialStatus(NewEntryStatus);
        entry.SetAuditInfo("system", false);
        await _journalEntries.AddAsync(entry, cancellationToken);
        return Result.Success();
    }

    public async Task<Result> ReverseInvoiceSaleEntryAsync(Guid invoiceId, string invoiceNumber, CancellationToken cancellationToken = default)
    {
        if (await _chartOfAccounts.CountAsync(cancellationToken) == 0)
            return Result.Success();

        var reversalType = SourceInvoiceCancelled;
        var existingReversal = await _journalEntries.GetBySourceAsync(reversalType, invoiceId, cancellationToken);
        if (existingReversal is not null)
            return Result.Success();

        // The original entry can be either a regular sale (FAC → SourceInvoice)
        // or a credit note (AVO → SourceInvoiceCreditNote). Check both.
        var original = await _journalEntries.GetBySourceAsync(SourceInvoice, invoiceId, cancellationToken)
                    ?? await _journalEntries.GetBySourceAsync(SourceInvoiceCreditNote, invoiceId, cancellationToken);
        if (original is null)
            return Result.Success();

        // Brouillard (C5) : une écriture encore en brouillon se supprime au lieu de s'extourner
        // (même règle que l'extourne manuelle) — l'annulation de la facture retire la pièce
        // provisoire sans générer de contre-passation.
        if (original.IsDraft)
        {
            await _journalEntries.RemoveAsync(original, cancellationToken);
            _logger.LogInformation(
                "Invoice {InvoiceNumber} cancelled: draft journal entry {EntryId} deleted instead of reversed",
                invoiceNumber, original.Id);
            return Result.Success();
        }

        // Contre-passer dans la période de l'écriture d'origine si elle est encore ouverte ;
        // sinon, repli sur la date du jour (une période clôturée ne peut recevoir d'écriture).
        var reversalDate = original.EntryDate;
        var periodResult = await _periodService.EnsureOpenPeriodAsync(reversalDate, cancellationToken);
        if (periodResult.IsFailure)
        {
            reversalDate = DateTime.UtcNow.Date;
            periodResult = await _periodService.EnsureOpenPeriodAsync(reversalDate, cancellationToken);
        }
        if (periodResult.IsFailure)
            return Result.Failure(periodResult.Error);

        var period = periodResult.Value;
        var currency = original.Lines.First().DebitAmount.Amount > 0
            ? original.Lines.First().DebitAmount.Currency
            : original.Lines.First().CreditAmount.Currency;

        var revLines = new List<JournalLineInput>();
        foreach (var line in original.Lines.OrderBy(l => l.LineNumber))
        {
            revLines.Add(new JournalLineInput(
                line.AccountNumber,
                line.Label,
                line.CreditAmount.Amount,
                line.DebitAmount.Amount,
                line.ThirdPartyId,
                line.ThirdPartyKind));
        }

        var n = await _journalEntries.ReserveNextEntryNumberAsync(original.JournalCode, reversalDate.Year, cancellationToken);
        var create = JournalEntry.Create(
            n,
            original.JournalCode,
            reversalDate,
            $"Contre-passation facture {invoiceNumber}",
            period.Id,
            true,
            reversalType,
            invoiceId,
            revLines,
            currency,
            original.Id);

        if (create.IsFailure)
            return Result.Failure(create.Error);

        var reversal = create.Value;
        reversal.MarkInitialStatus(NewEntryStatus);
        reversal.SetAuditInfo("system", false);
        await _journalEntries.AddAsync(reversal, cancellationToken);

        original.MarkReversedBy(reversal.Id);
        await _journalEntries.UpdateAsync(original, cancellationToken);
        return Result.Success();
    }

    public async Task<Result<Guid>> ReverseJournalEntryAsync(Guid entryId, string reason, CancellationToken cancellationToken = default)
    {
        if (!_settings.ManualReversalEnabled)
            return Result.Failure<Guid>(Error.Validation("Extourne", "La contre-passation manuelle n'est pas activée."));

        var original = await _journalEntries.GetByIdAsync(entryId, cancellationToken);
        if (original is null)
            return Result.Failure<Guid>(Error.NotFound("JournalEntry", entryId));

        if (original.IsDraft)
            return Result.Failure<Guid>(Error.Validation("Extourne",
                "Une écriture en brouillon se corrige ou se supprime directement, sans extourne."));

        if (original.IsReversed)
            return Result.Failure<Guid>(Error.Validation("Extourne", "Cette écriture a déjà été extournée."));

        // Contre-passer dans la période de l'écriture d'origine si elle est encore ouverte ;
        // sinon, repli sur la date du jour (une période clôturée ne peut recevoir d'écriture).
        var reversalDate = original.EntryDate;
        var periodResult = await _periodService.EnsureOpenPeriodAsync(reversalDate, cancellationToken);
        if (periodResult.IsFailure)
        {
            reversalDate = DateTime.UtcNow.Date;
            periodResult = await _periodService.EnsureOpenPeriodAsync(reversalDate, cancellationToken);
        }
        if (periodResult.IsFailure)
            return Result.Failure<Guid>(periodResult.Error);

        var period = periodResult.Value;
        var firstLine = original.Lines.First();
        var currency = firstLine.DebitAmount.Amount > 0 ? firstLine.DebitAmount.Currency : firstLine.CreditAmount.Currency;

        var revLines = original.Lines
            .OrderBy(l => l.LineNumber)
            .Select(l => new JournalLineInput(
                l.AccountNumber,
                l.Label,
                l.CreditAmount.Amount,
                l.DebitAmount.Amount,
                l.ThirdPartyId,
                l.ThirdPartyKind))
            .ToList();

        var trimmedReason = string.IsNullOrWhiteSpace(reason) ? "correction" : reason.Trim();
        var label = $"Extourne {original.JournalCode}-{original.EntryNumber} : {trimmedReason}";
        if (label.Length > 500)
            label = label[..500];

        var n = await _journalEntries.ReserveNextEntryNumberAsync(original.JournalCode, reversalDate.Year, cancellationToken);
        var create = JournalEntry.Create(
            n,
            original.JournalCode,
            reversalDate,
            label,
            period.Id,
            false,
            SourceManualReversal,
            original.Id,
            revLines,
            currency,
            original.Id,
            NewEntryStatus);

        if (create.IsFailure)
            return Result.Failure<Guid>(create.Error);

        var reversal = create.Value;
        reversal.SetAuditInfo("system", false);
        await _journalEntries.AddAsync(reversal, cancellationToken);

        original.MarkReversedBy(reversal.Id);
        await _journalEntries.UpdateAsync(original, cancellationToken);

        return Result.Success(reversal.Id);
    }

    public async Task<Result> GenerateSupplierInvoiceEntryAsync(SupplierInvoice invoice, CancellationToken cancellationToken = default)
    {
        if (await _chartOfAccounts.CountAsync(cancellationToken) == 0)
            return Result.Success();

        var existing = await _journalEntries.GetBySourceAsync(SourceSupplierInvoice, invoice.Id, cancellationToken);
        if (existing is not null)
            return Result.Success();

        var periodResult = await _periodService.EnsureOpenPeriodAsync(invoice.InvoiceDate, cancellationToken);
        if (periodResult.IsFailure)
            return Result.Failure(periodResult.Error);

        var period = periodResult.Value;
        var currency = invoice.TotalAmount.Currency;
        var productTypes = await TryLoadStandaloneProductTypesAsync(invoice, cancellationToken);
        var (lines, _) = SupplierInvoiceJournalLineBuilder.Build(invoice, productTypes);

        var accountValidation = await ValidateAccountsExistAsync(lines, cancellationToken);
        if (accountValidation.IsFailure)
            return accountValidation;

        var n = await _journalEntries.ReserveNextEntryNumberAsync(
            TunisianPostingAccounts.PurchaseJournalCode, invoice.InvoiceDate.Year, cancellationToken);
        var create = JournalEntry.Create(
            n,
            TunisianPostingAccounts.PurchaseJournalCode,
            invoice.InvoiceDate,
            $"Facture fournisseur {invoice.InvoiceNumber}",
            period.Id,
            true,
            SourceSupplierInvoice,
            invoice.Id,
            lines,
            currency);

        if (create.IsFailure)
            return Result.Failure(create.Error);

        var entry = create.Value;
        entry.MarkInitialStatus(NewEntryStatus);
        entry.SetAuditInfo("system", false);
        await _journalEntries.AddAsync(entry, cancellationToken);
        return Result.Success();
    }

    /// <summary>
    /// Loads catalog product types only for standalone invoices (no PO / receipt).
    /// BC/BR invoices keep the historic 607 posting by returning null.
    /// </summary>
    private async Task<IReadOnlyDictionary<Guid, ProductType>?> TryLoadStandaloneProductTypesAsync(
        SupplierInvoice invoice,
        CancellationToken cancellationToken)
    {
        if (invoice.PurchaseOrderId is not null || invoice.SourcePurchaseReceiptId is not null)
            return null;

        var productIds = invoice.Lines.Select(l => l.ProductId).Distinct().ToList();
        if (productIds.Count == 0)
            return null;

        await using var context = _contextFactory.CreateContext();
        return await context.Products.AsNoTracking()
            .Where(p => productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.Type, cancellationToken);
    }

    public async Task<Result> GenerateSupplierPaymentEntryAsync(SupplierPayment payment, CancellationToken cancellationToken = default)
    {
        if (await _chartOfAccounts.CountAsync(cancellationToken) == 0)
            return Result.Success();

        var existing = await _journalEntries.GetBySourceAsync(SourceSupplierPayment, payment.Id, cancellationToken);
        if (existing is not null)
            return Result.Success();

        var inv = payment.SupplierInvoice;
        var date = payment.PaymentDate;
        var periodResult = await _periodService.EnsureOpenPeriodAsync(date, cancellationToken);
        if (periodResult.IsFailure)
            return Result.Failure(periodResult.Error);

        var period = periodResult.Value;
        var currency = payment.Amount.Currency;
        // Traite (effet de commerce) : la dette fournisseur n'est pas décaissée mais transformée en
        // effet à payer (403), sans mouvement de trésorerie → journal des opérations diverses.
        var isEffet = payment.Method == PaymentMethod.Traite;
        var creditAccount = isEffet ? SupplierEffetAccountNumber : TreasuryAccount(payment.Method);
        var creditLabel = isEffet ? $"Effet à payer — {inv.InvoiceNumber}" : $"Paiement fournisseur — {inv.InvoiceNumber}";
        var amount = payment.Amount.Amount;

        var lines = new List<JournalLineInput>
        {
            new(TunisianPostingAccounts.Supplier, $"Fournisseur — {inv.InvoiceNumber}", amount, 0, inv.SupplierId, ThirdPartyKind.Supplier),
            new(creditAccount, creditLabel, 0, amount, null, ThirdPartyKind.None)
        };

        var accountValidation = await ValidateAccountsExistAsync(lines, cancellationToken);
        if (accountValidation.IsFailure)
            return accountValidation;

        var journal = isEffet ? MiscJournalCode : (payment.Method == PaymentMethod.Cash ? "JC" : "JB");
        var description = isEffet
            ? $"Effet à payer fournisseur {inv.InvoiceNumber}"
            : $"Paiement fournisseur {inv.InvoiceNumber}";
        var n = await _journalEntries.ReserveNextEntryNumberAsync(journal, date.Year, cancellationToken);
        var create = JournalEntry.Create(
            n,
            journal,
            date,
            description,
            period.Id,
            true,
            SourceSupplierPayment,
            payment.Id,
            lines,
            currency);

        if (create.IsFailure)
            return Result.Failure(create.Error);

        var entry = create.Value;
        entry.MarkInitialStatus(NewEntryStatus);
        entry.SetAuditInfo("system", false);
        await _journalEntries.AddAsync(entry, cancellationToken);
        return Result.Success();
    }

    public async Task<Result> GenerateClientEffetSettlementEntryAsync(Payment payment, EffetStatus outcome, CancellationToken cancellationToken = default)
    {
        if (await _chartOfAccounts.CountAsync(cancellationToken) == 0)
            return Result.Success();

        if (payment.Method != PaymentMethod.Traite)
            return Result.Success();

        if (outcome != EffetStatus.Encaisse && outcome != EffetStatus.Impaye)
            return Result.Failure(Error.Validation("Effet", "L'issue de règlement de l'effet est invalide"));

        var existing = await _journalEntries.GetBySourceAsync(SourceEffetSettlement, payment.Id, cancellationToken);
        if (existing is not null)
            return Result.Success();

        var invoice = payment.Invoice;
        var date = (payment.EffetSettledAt ?? DateTime.UtcNow).Date;
        var periodResult = await _periodService.EnsureOpenPeriodAsync(date, cancellationToken);
        if (periodResult.IsFailure)
            return Result.Failure(periodResult.Error);

        var period = periodResult.Value;
        var currency = payment.Amount.Currency;
        // Le compte 413 porte le net reçu à la réception de l'effet ; c'est ce montant qui bouge à échéance.
        var net = payment.Amount.Amount;

        List<JournalLineInput> lines;
        string journal;
        string description;
        if (outcome == EffetStatus.Encaisse)
        {
            // Encaissement : la banque reçoit les fonds, l'effet à recevoir est soldé.
            lines = new List<JournalLineInput>
            {
                new("5321", $"Encaissement effet — {invoice.Number.Value}", net, 0, null, ThirdPartyKind.None),
                new(ClientEffetAccountNumber, $"Effet à recevoir soldé — {invoice.Number.Value}", 0, net, null, ThirdPartyKind.None)
            };
            journal = BankJournalCode;
            description = $"Encaissement effet facture {invoice.Number.Value}";
        }
        else
        {
            // Impayé : l'effet revient impayé, la créance client est réouverte.
            lines = new List<JournalLineInput>
            {
                new(TunisianPostingAccounts.Client, $"Effet impayé — {invoice.Number.Value}", net, 0, invoice.ClientId, ThirdPartyKind.Client),
                new(ClientEffetAccountNumber, $"Effet à recevoir impayé — {invoice.Number.Value}", 0, net, null, ThirdPartyKind.None)
            };
            journal = MiscJournalCode;
            description = $"Effet impayé facture {invoice.Number.Value}";
        }

        var accountValidation = await ValidateAccountsExistAsync(lines, cancellationToken);
        if (accountValidation.IsFailure)
            return accountValidation;

        var n = await _journalEntries.ReserveNextEntryNumberAsync(journal, date.Year, cancellationToken);
        var create = JournalEntry.Create(
            n,
            journal,
            date,
            description,
            period.Id,
            true,
            SourceEffetSettlement,
            payment.Id,
            lines,
            currency);

        if (create.IsFailure)
            return Result.Failure(create.Error);

        var entry = create.Value;
        entry.MarkInitialStatus(NewEntryStatus);
        entry.SetAuditInfo("system", false);
        await _journalEntries.AddAsync(entry, cancellationToken);
        return Result.Success();
    }

    public async Task<Result> GenerateSupplierEffetSettlementEntryAsync(SupplierPayment payment, CancellationToken cancellationToken = default)
    {
        if (await _chartOfAccounts.CountAsync(cancellationToken) == 0)
            return Result.Success();

        if (payment.Method != PaymentMethod.Traite)
            return Result.Success();

        var existing = await _journalEntries.GetBySourceAsync(SourceEffetSettlement, payment.Id, cancellationToken);
        if (existing is not null)
            return Result.Success();

        var inv = payment.SupplierInvoice;
        var date = (payment.EffetSettledAt ?? DateTime.UtcNow).Date;
        var periodResult = await _periodService.EnsureOpenPeriodAsync(date, cancellationToken);
        if (periodResult.IsFailure)
            return Result.Failure(periodResult.Error);

        var period = periodResult.Value;
        var currency = payment.Amount.Currency;
        var amount = payment.Amount.Amount;

        // Paiement de l'effet à échéance : l'effet à payer est soldé, la banque est décaissée.
        var lines = new List<JournalLineInput>
        {
            new(SupplierEffetAccountNumber, $"Effet à payer soldé — {inv.InvoiceNumber}", amount, 0, null, ThirdPartyKind.None),
            new("5321", $"Paiement effet — {inv.InvoiceNumber}", 0, amount, null, ThirdPartyKind.None)
        };

        var accountValidation = await ValidateAccountsExistAsync(lines, cancellationToken);
        if (accountValidation.IsFailure)
            return accountValidation;

        var n = await _journalEntries.ReserveNextEntryNumberAsync(BankJournalCode, date.Year, cancellationToken);
        var create = JournalEntry.Create(
            n,
            BankJournalCode,
            date,
            $"Paiement effet fournisseur {inv.InvoiceNumber}",
            period.Id,
            true,
            SourceEffetSettlement,
            payment.Id,
            lines,
            currency);

        if (create.IsFailure)
            return Result.Failure(create.Error);

        var entry = create.Value;
        entry.MarkInitialStatus(NewEntryStatus);
        entry.SetAuditInfo("system", false);
        await _journalEntries.AddAsync(entry, cancellationToken);
        return Result.Success();
    }

    public async Task<Result> GenerateBankDepositEntryAsync(BankDeposit deposit, CancellationToken cancellationToken = default)
    {
        if (await _chartOfAccounts.CountAsync(cancellationToken) == 0)
            return Result.Success();

        var existing = await _journalEntries.GetBySourceAsync(SourceBankDeposit, deposit.Id, cancellationToken);
        if (existing is not null)
            return Result.Success();

        var periodResult = await _periodService.EnsureOpenPeriodAsync(deposit.DepositDate, cancellationToken);
        if (periodResult.IsFailure)
            return Result.Failure(periodResult.Error);

        var period = periodResult.Value;
        var currency = deposit.Amount.Currency;
        var amount = deposit.Amount.Amount;

        var lines = new List<JournalLineInput>
        {
            new("5321", $"Remise en banque {deposit.Number.Value}", amount, 0, null, ThirdPartyKind.None),
            new("5411", $"Sortie caisse — remise {deposit.Number.Value}", 0, amount, null, ThirdPartyKind.None)
        };

        var accountValidation = await ValidateAccountsExistAsync(lines, cancellationToken);
        if (accountValidation.IsFailure)
            return accountValidation;

        var n = await _journalEntries.ReserveNextEntryNumberAsync("JB", deposit.DepositDate.Year, cancellationToken);
        var create = JournalEntry.Create(
            n,
            "JB",
            deposit.DepositDate,
            $"Remise en banque {deposit.Number.Value}",
            period.Id,
            true,
            SourceBankDeposit,
            deposit.Id,
            lines,
            currency);

        if (create.IsFailure)
            return Result.Failure(create.Error);

        var entry = create.Value;
        entry.MarkInitialStatus(NewEntryStatus);
        entry.SetAuditInfo("system", false);
        await _journalEntries.AddAsync(entry, cancellationToken);
        return Result.Success();
    }

    public async Task<Result> GenerateCashOperationEntryAsync(CashOperation operation, CancellationToken cancellationToken = default)
    {
        if (await _chartOfAccounts.CountAsync(cancellationToken) == 0)
            return Result.Success();

        if (operation.Category == CashExpenseCategory.BankDeposit)
            return Result.Success();

        var existing = await _journalEntries.GetBySourceAsync(SourceCashOperation, operation.Id, cancellationToken);
        if (existing is not null)
            return Result.Success();

        var periodResult = await _periodService.EnsureOpenPeriodAsync(operation.OperationDate, cancellationToken);
        if (periodResult.IsFailure)
            return Result.Failure(periodResult.Error);

        var period = periodResult.Value;
        var currency = operation.Amount.Currency;
        var amount = operation.Amount.Amount;
        var journalLabel = CashOperationJournalLabelBuilder.BuildJournalLabel(operation);
        var lines = new List<JournalLineInput>();

        if (operation.OperationType == CashOperationType.Debit)
        {
            var expense = ExpenseAccount(operation.Category!.Value);
            lines.Add(new JournalLineInput(expense, journalLabel, amount, 0, null, ThirdPartyKind.None));
            lines.Add(new JournalLineInput("5411", journalLabel, 0, amount, null, ThirdPartyKind.None));
        }
        else
        {
            var revenue = RevenueAccount(operation.RevenueCategory!.Value);
            lines.Add(new JournalLineInput("5411", journalLabel, amount, 0, null, ThirdPartyKind.None));
            lines.Add(new JournalLineInput(revenue, journalLabel, 0, amount, null, ThirdPartyKind.None));
        }

        var accountValidation = await ValidateAccountsExistAsync(lines, cancellationToken);
        if (accountValidation.IsFailure)
            return accountValidation;

        var n = await _journalEntries.ReserveNextEntryNumberAsync("JC", operation.OperationDate.Year, cancellationToken);
        var create = JournalEntry.Create(
            n,
            "JC",
            operation.OperationDate,
            journalLabel,
            period.Id,
            true,
            SourceCashOperation,
            operation.Id,
            lines,
            currency);

        if (create.IsFailure)
            return Result.Failure(create.Error);

        var entry = create.Value;
        entry.MarkInitialStatus(NewEntryStatus);
        entry.SetAuditInfo("system", false);
        await _journalEntries.AddAsync(entry, cancellationToken);
        return Result.Success();
    }

    public async Task<Result> GenerateSupplierInvoiceWithholdingEntryAsync(
        SupplierInvoice invoice,
        CancellationToken cancellationToken = default)
    {
        if (await _chartOfAccounts.CountAsync(cancellationToken) == 0)
            return Result.Success();

        if (!invoice.IsSubjectToWithholding || invoice.WithholdingAmount is not > 0)
            return Result.Success();

        // Une seule écriture RS par FF : si le montant change après coup, un ajustement manuel ou une écriture complémentaire est requis.
        // Le recalcul RS à la solde s’exécute avant la première génération (notification après paiement).
        var existing = await _journalEntries.GetBySourceAsync(SourceSupplierInvoiceWithholding, invoice.Id, cancellationToken);
        if (existing is not null)
            return Result.Success();

        var paymentDate = invoice.PaidAt?.Date ?? invoice.InvoiceDate;
        var periodResult = await _periodService.EnsureOpenPeriodAsync(paymentDate, cancellationToken);
        if (periodResult.IsFailure)
            return Result.Failure(periodResult.Error);

        var period = periodResult.Value;
        var totalWithheld = invoice.WithholdingAmount!.Value;

        var operationCode = "RS7_000001";
        if (invoice.WithholdingTaxTypeId.HasValue)
        {
            var wt = await _withholdingTaxTypes.GetTypeByIdAsync(invoice.WithholdingTaxTypeId.Value, cancellationToken);
            if (wt is not null)
                operationCode = wt.Code;
        }

        var withholdingAccount = WithholdingAccountForOperationCode(operationCode);
        var label = $"RS facture fournisseur {invoice.InvoiceNumber}";

        // L'écriture de paiement (GenerateSupplierPaymentEntryAsync) solde déjà le compte 4011
        // pour le TTC et crédite la trésorerie du TTC. Cette écriture RS reclasse la seule part
        // retenue : elle n'a pas été décaissée au fournisseur mais reste due à l'État.
        //   Débit 5321  : la retenue n'est pas sortie de la trésorerie ;
        //   Crédit 445x : dette de retenue à la source envers l'État.
        var lines = new List<JournalLineInput>
        {
            new(
                "5321",
                $"Retenue à la source non décaissée — {label}",
                totalWithheld,
                0,
                null,
                ThirdPartyKind.None),
            new(
                withholdingAccount,
                $"Retenue à la source à reverser à l'État — {label}",
                0,
                totalWithheld,
                null,
                ThirdPartyKind.None)
        };

        var accountValidation = await ValidateAccountsExistAsync(lines, cancellationToken);
        if (accountValidation.IsFailure)
            return accountValidation;

        var n = await _journalEntries.ReserveNextEntryNumberAsync("JOD", paymentDate.Year, cancellationToken);
        var create = JournalEntry.Create(
            n,
            "JOD",
            paymentDate,
            label,
            period.Id,
            true,
            SourceSupplierInvoiceWithholding,
            invoice.Id,
            lines);

        if (create.IsFailure)
            return Result.Failure(create.Error);

        var entry = create.Value;
        entry.MarkInitialStatus(NewEntryStatus);
        entry.SetAuditInfo("system", false);
        await _journalEntries.AddAsync(entry, cancellationToken);
        return Result.Success();
    }

    public async Task<Result> ReverseSupplierInvoiceEntryAsync(Guid supplierInvoiceId, string invoiceNumber, CancellationToken cancellationToken = default)
    {
        if (await _chartOfAccounts.CountAsync(cancellationToken) == 0)
            return Result.Success();

        const string reversalType = "SupplierInvoiceCancelled";
        var existingReversal = await _journalEntries.GetBySourceAsync(reversalType, supplierInvoiceId, cancellationToken);
        if (existingReversal is not null)
            return Result.Success();

        var original = await _journalEntries.GetBySourceAsync(SourceSupplierInvoice, supplierInvoiceId, cancellationToken);
        if (original is null)
            return Result.Success();

        // Contre-passer dans la période de l'écriture d'origine si elle est encore ouverte ;
        // sinon, repli sur la date du jour (une période clôturée ne peut recevoir d'écriture).
        var reversalDate = original.EntryDate;
        var periodResult = await _periodService.EnsureOpenPeriodAsync(reversalDate, cancellationToken);
        if (periodResult.IsFailure)
        {
            reversalDate = DateTime.UtcNow.Date;
            periodResult = await _periodService.EnsureOpenPeriodAsync(reversalDate, cancellationToken);
        }
        if (periodResult.IsFailure)
            return Result.Failure(periodResult.Error);

        var period = periodResult.Value;
        var currency = original.Lines.First().DebitAmount.Amount > 0
            ? original.Lines.First().DebitAmount.Currency
            : original.Lines.First().CreditAmount.Currency;

        var revLines = new List<JournalLineInput>();
        foreach (var line in original.Lines.OrderBy(l => l.LineNumber))
        {
            revLines.Add(new JournalLineInput(
                line.AccountNumber,
                line.Label,
                line.CreditAmount.Amount,
                line.DebitAmount.Amount,
                line.ThirdPartyId,
                line.ThirdPartyKind));
        }

        var n = await _journalEntries.ReserveNextEntryNumberAsync(original.JournalCode, reversalDate.Year, cancellationToken);
        var create = JournalEntry.Create(
            n,
            original.JournalCode,
            reversalDate,
            $"Contre-passation facture fournisseur {invoiceNumber}",
            period.Id,
            true,
            reversalType,
            supplierInvoiceId,
            revLines,
            currency,
            original.Id);

        if (create.IsFailure)
            return Result.Failure(create.Error);

        var reversal = create.Value;
        reversal.MarkInitialStatus(NewEntryStatus);
        reversal.SetAuditInfo("system", false);
        await _journalEntries.AddAsync(reversal, cancellationToken);

        original.MarkReversedBy(reversal.Id);
        await _journalEntries.UpdateAsync(original, cancellationToken);
        return Result.Success();
    }

    public async Task<Result<Guid>> GenerateOpeningEntriesAsync(int closedFiscalYear, CancellationToken cancellationToken = default)
    {
        if (await _chartOfAccounts.CountAsync(cancellationToken) == 0)
            return Result.Failure<Guid>(Error.Validation("ChartOfAccounts", "Le plan comptable est vide."));

        var newYear = closedFiscalYear + 1;

        // Idempotency: check if opening entries already exist for this year
        var existingSourceId = GuidFromFiscalYear(closedFiscalYear);
        var existing = await _journalEntries.GetBySourceAsync(SourceOpeningBalance, existingSourceId, cancellationToken);
        if (existing is not null)
            return Result.Failure<Guid>(Error.Conflict($"Les écritures d'à-nouveau pour l'exercice {closedFiscalYear} ont déjà été générées."));

        // Garde-fou légal : les à-nouveaux figent les soldes de l'exercice — aucun brouillon
        // ne doit subsister (même wording que la clôture annuelle). Sans effet quand le
        // workflow brouillard est désactivé.
        var drafts = await _journalEntries.CountDraftsByFiscalYearAsync(closedFiscalYear, cancellationToken);
        if (drafts > 0)
            return Result.Failure<Guid>(Error.Validation("Brouillard",
                $"{drafts} écriture(s) en brouillard sur l'exercice {closedFiscalYear}. Validez-les avant de générer les à-nouveaux."));

        // Ensure Jan 1st of the new year has an open period
        var jan1 = new DateTime(newYear, 1, 1);
        var periodResult = await _periodService.EnsureOpenPeriodAsync(jan1, cancellationToken);
        if (periodResult.IsFailure)
            return Result.Failure<Guid>(periodResult.Error);

        var period = periodResult.Value;

        // Compute closing balances for ALL accounts up to end of closed fiscal year.
        // Sortie légale : seules les écritures définitives entrent dans les soldes — un brouillon
        // (y compris antérieur à l'exercice clôturé) ne doit jamais alimenter l'à-nouveau.
        await using var ctx = _contextFactory.CreateContext();
        var endDate = new DateTime(closedFiscalYear, 12, 31);

        // Agrégation par compte ET par tiers : un à-nouveau qui perd le tiers n'est plus justifiable
        // dans la balance auxiliaire ni dans le grand livre tiers. Les lignes sans tiers (la très
        // grande majorité des comptes) forment un groupe unique par compte — comportement inchangé.
        var accountBalances = await ctx.JournalEntryLines
            .AsNoTracking()
            .Include(l => l.JournalEntry)
            .Where(l => l.JournalEntry.EntryDate <= endDate
                        && l.JournalEntry.Status != JournalEntryStatus.Brouillon)
            .GroupBy(l => new { l.AccountNumber, l.ThirdPartyId, l.ThirdPartyKind })
            .Select(g => new
            {
                g.Key.AccountNumber,
                g.Key.ThirdPartyId,
                g.Key.ThirdPartyKind,
                TotalDebit = g.Sum(l => l.DebitAmount.Amount),
                TotalCredit = g.Sum(l => l.CreditAmount.Amount)
            })
            .ToListAsync(cancellationToken);

        var lines = new List<JournalLineInput>();

        // Carry forward balance sheet accounts (classes 1-5)
        foreach (var ab in accountBalances
            .Where(a => a.AccountNumber.Length > 0 &&
                        a.AccountNumber[0] >= '1' && a.AccountNumber[0] <= '5')
            .OrderBy(a => a.AccountNumber, StringComparer.Ordinal)
            .ThenBy(a => a.ThirdPartyId))
        {
            var balance = Math.Round(ab.TotalDebit - ab.TotalCredit, 3);
            if (balance == 0) continue;

            var lineLabel = $"À-nouveau {closedFiscalYear} — {ab.AccountNumber}";
            if (balance > 0)
            {
                lines.Add(new JournalLineInput(
                    ab.AccountNumber, lineLabel,
                    balance, 0, ab.ThirdPartyId, ab.ThirdPartyKind));
            }
            else
            {
                lines.Add(new JournalLineInput(
                    ab.AccountNumber, lineLabel,
                    0, Math.Abs(balance), ab.ThirdPartyId, ab.ThirdPartyKind));
            }
        }

        // Compute net result from P&L accounts (classes 6 and 7)
        // Class 6 = expenses (debit nature), Class 7 = revenues (credit nature)
        var pnlBalance = accountBalances
            .Where(a => a.AccountNumber.Length > 0 &&
                        (a.AccountNumber[0] == '6' || a.AccountNumber[0] == '7'))
            .Sum(a => a.TotalCredit - a.TotalDebit);

        var netResult = Math.Round(pnlBalance, 3);

        if (netResult != 0)
        {
            var resultAccount = netResult > 0 ? OpeningBalanceProfitAccountNumber : OpeningBalanceLossAccountNumber;
            if (netResult > 0)
            {
                lines.Add(new JournalLineInput(
                    resultAccount,
                    $"Résultat net exercice {closedFiscalYear}",
                    0, netResult, null, ThirdPartyKind.None));
            }
            else
            {
                lines.Add(new JournalLineInput(
                    resultAccount,
                    $"Résultat net exercice {closedFiscalYear}",
                    Math.Abs(netResult), 0, null, ThirdPartyKind.None));
            }
        }

        if (lines.Count < 2)
        {
            _logger.LogInformation("No opening balance entries to generate for fiscal year {Year}", closedFiscalYear);
            return Result.Failure<Guid>(Error.Validation("OpeningBalance",
                "Aucune écriture d'à-nouveau à générer (pas de soldes sur les comptes de bilan)."));
        }

        // Validate all accounts exist
        var accountValidation = await ValidateAccountsExistAsync(lines, cancellationToken);
        if (accountValidation.IsFailure)
            return Result.Failure<Guid>(accountValidation.Error);

        var n = await _journalEntries.ReserveNextEntryNumberAsync("JAN", newYear, cancellationToken);
        var create = JournalEntry.Create(
            n,
            "JAN",
            jan1,
            $"Écritures d'à-nouveau — Exercice {closedFiscalYear}",
            period.Id,
            true,
            SourceOpeningBalance,
            existingSourceId,
            lines);

        if (create.IsFailure)
            return Result.Failure<Guid>(create.Error);

        var entry = create.Value;
        entry.MarkInitialStatus(NewEntryStatus);
        entry.SetAuditInfo("system", false);
        await _journalEntries.AddAsync(entry, cancellationToken);

        _logger.LogInformation("Generated {LineCount} opening balance lines for fiscal year {NewYear} from {ClosedYear}",
            lines.Count, newYear, closedFiscalYear);

        return Result.Success(entry.Id);
    }

    /// <summary>
    /// Generates a deterministic GUID from a fiscal year integer for idempotency checking.
    /// </summary>
    private static Guid GuidFromFiscalYear(int fiscalYear)
    {
        var bytes = new byte[16];
        BitConverter.GetBytes(fiscalYear).CopyTo(bytes, 0);
        // Marker bytes to make this identifiable as an opening balance source
        bytes[4] = 0xA0;
        bytes[5] = 0x0B; // "A-Nouveau Opening Balance"
        return new Guid(bytes);
    }

    private static string WithholdingAccountForOperationCode(string operationCode)
    {
        return operationCode switch
        {
            var c when c.StartsWith("RS1", StringComparison.OrdinalIgnoreCase) => "4321",
            var c when c.StartsWith("RS2", StringComparison.OrdinalIgnoreCase) => "4322",
            var c when c.StartsWith("RS3", StringComparison.OrdinalIgnoreCase) => "4323",
            var c when c.StartsWith("RS4", StringComparison.OrdinalIgnoreCase) => "4324",
            var c when c.StartsWith("RS5", StringComparison.OrdinalIgnoreCase) => "4325",
            var c when c.StartsWith("RS6", StringComparison.OrdinalIgnoreCase) => "4326",
            var c when c.StartsWith("RS7", StringComparison.OrdinalIgnoreCase) => "4327",
            var c when c.StartsWith("RS8", StringComparison.OrdinalIgnoreCase) => "4320",
            var c when c.StartsWith("RS9", StringComparison.OrdinalIgnoreCase) => "4320",
            _ => "4320"
        };
    }

    private static string TreasuryAccount(PaymentMethod method) =>
        method == PaymentMethod.Cash ? "5411" : "5321";

    private static string RevenueAccountForLine(InvoiceLine line)
    {
        if (!line.ProductId.HasValue || line.Product is null)
            return TunisianPostingAccounts.SalesOfGoods;
        return line.Product.Type == ProductType.Service
            ? TunisianPostingAccounts.SalesOfServices
            : TunisianPostingAccounts.SalesOfGoods;
    }

    private static string ExpenseAccount(CashExpenseCategory category) => category switch
    {
        CashExpenseCategory.SuppliesAndConsumables => "607",
        CashExpenseCategory.SupplierInvoicePayment => "607",
        CashExpenseCategory.TaxesAndDuties => "6654",
        // Dépense non catégorisée : charges diverses de gestion courante.
        _ => "658"
    };

    private static string RevenueAccount(CashRevenueCategory category) => category switch
    {
        CashRevenueCategory.CashSalesReceipt => "707",
        CashRevenueCategory.ClientReceivablesReceipt => "4111",
        CashRevenueCategory.PartnerContributionsReceipt => "101",
        CashRevenueCategory.BankCreditReceipt => "5321",
        _ => "707"
    };

    public async Task<Result> GenerateFixedAssetAcquisitionEntryAsync(FixedAsset asset, CancellationToken cancellationToken = default)
    {
        if (await _chartOfAccounts.CountAsync(cancellationToken) == 0)
            return Result.Success();

        var existing = await _journalEntries.GetBySourceAsync(SourceFixedAssetAcquisition, asset.Id, cancellationToken);
        if (existing is not null)
            return Result.Success();

        if (asset.InServiceDate is null || string.IsNullOrWhiteSpace(asset.CreditAccountNumber))
            return Result.Failure(Error.Validation("FixedAsset", "Mise en service incomplète pour l'écriture d'acquisition."));

        var entryDate = asset.InServiceDate.Value;
        var periodResult = await _periodService.EnsureOpenPeriodAsync(entryDate, cancellationToken);
        if (periodResult.IsFailure)
            return Result.Failure(periodResult.Error);

        var amount = asset.TotalCapitalizedCost;
        var lines = new List<JournalLineInput>
        {
            new(asset.AssetAccountNumber, $"Acquisition — {asset.InventoryNumber}", amount, 0, null, ThirdPartyKind.None),
            new(asset.CreditAccountNumber!, $"Acquisition — {asset.Label}", 0, amount, null, ThirdPartyKind.None)
        };

        var accountValidation = await ValidateAccountsExistAsync(lines, cancellationToken);
        if (accountValidation.IsFailure)
            return accountValidation;

        var n = await _journalEntries.ReserveNextEntryNumberAsync(FixedAssetJournalCode, entryDate.Year, cancellationToken);
        var create = JournalEntry.Create(
            n,
            FixedAssetJournalCode,
            entryDate,
            $"Immobilisation {asset.InventoryNumber}",
            periodResult.Value.Id,
            true,
            SourceFixedAssetAcquisition,
            asset.Id,
            lines);

        if (create.IsFailure)
            return Result.Failure(create.Error);

        var entry = create.Value;
        entry.MarkInitialStatus(NewEntryStatus);
        entry.SetAuditInfo("system", false);
        await _journalEntries.AddAsync(entry, cancellationToken);
        return Result.Success();
    }

    public async Task<Result> GenerateFixedAssetDepreciationEntryAsync(
        FixedAsset asset,
        DepreciationScheduleLine scheduleLine,
        CancellationToken cancellationToken = default)
    {
        if (await _chartOfAccounts.CountAsync(cancellationToken) == 0)
            return Result.Success();

        if (scheduleLine.IsPosted)
            return Result.Success();

        var existing = await _journalEntries.GetBySourceAsync(SourceFixedAssetDepreciation, scheduleLine.Id, cancellationToken);
        if (existing is not null)
            return Result.Success();

        if (scheduleLine.DepreciationAmount <= 0)
            return Result.Success();

        var entryDate = new DateTime(scheduleLine.FiscalYear, 12, 31);
        var periodResult = await _periodService.EnsureOpenPeriodAsync(entryDate, cancellationToken);
        if (periodResult.IsFailure)
            return Result.Failure(periodResult.Error);

        var amount = scheduleLine.DepreciationAmount;
        var lines = new List<JournalLineInput>
        {
            new(asset.ExpenseAccountNumber, $"Dotation {scheduleLine.FiscalYear} — {asset.InventoryNumber}", amount, 0, null, ThirdPartyKind.None),
            new(asset.DepreciationAccountNumber, $"Amort. {scheduleLine.FiscalYear} — {asset.InventoryNumber}", 0, amount, null, ThirdPartyKind.None)
        };

        var accountValidation = await ValidateAccountsExistAsync(lines, cancellationToken);
        if (accountValidation.IsFailure)
            return accountValidation;

        var n = await _journalEntries.ReserveNextEntryNumberAsync(FixedAssetJournalCode, scheduleLine.FiscalYear, cancellationToken);
        var create = JournalEntry.Create(
            n,
            FixedAssetJournalCode,
            entryDate,
            $"Dotation amortissement {asset.InventoryNumber} ({scheduleLine.FiscalYear})",
            periodResult.Value.Id,
            true,
            SourceFixedAssetDepreciation,
            scheduleLine.Id,
            lines);

        if (create.IsFailure)
            return Result.Failure(create.Error);

        var entry = create.Value;
        entry.MarkInitialStatus(NewEntryStatus);
        entry.SetAuditInfo("system", false);
        await _journalEntries.AddAsync(entry, cancellationToken);

        scheduleLine.MarkPosted(entry.Id, periodResult.Value.Id);
        asset.ApplyDepreciation(amount, scheduleLine.AccumulatedDepreciation, scheduleLine.ClosingNbv);

        return Result.Success();
    }

    public async Task<Result> GenerateFixedAssetDisposalEntryAsync(FixedAsset asset, CancellationToken cancellationToken = default)
    {
        if (await _chartOfAccounts.CountAsync(cancellationToken) == 0)
            return Result.Success();

        var existing = await _journalEntries.GetBySourceAsync(SourceFixedAssetDisposal, asset.Id, cancellationToken);
        if (existing is not null)
            return Result.Success();

        if (asset.DisposalDate is null || string.IsNullOrWhiteSpace(asset.DisposalTreasuryAccount))
            return Result.Failure(Error.Validation("FixedAsset", "Cession incomplète pour l'écriture comptable."));

        var entryDate = asset.DisposalDate.Value;
        var periodResult = await _periodService.EnsureOpenPeriodAsync(entryDate, cancellationToken);
        if (periodResult.IsFailure)
            return Result.Failure(periodResult.Error);

        var gross = asset.TotalCapitalizedCost;
        var accumulated = asset.AccumulatedDepreciation;
        var proceeds = asset.DisposalProceeds ?? 0m;
        var nbv = asset.NetBookValue;
        var gainOrLoss = proceeds - nbv;

        var lines = new List<JournalLineInput>();

        if (accumulated > 0)
        {
            lines.Add(new JournalLineInput(
                asset.DepreciationAccountNumber,
                $"Reprise amort. cession — {asset.InventoryNumber}",
                accumulated,
                0,
                null,
                ThirdPartyKind.None));
        }

        if (proceeds > 0)
        {
            lines.Add(new JournalLineInput(
                asset.DisposalTreasuryAccount!,
                $"Produit cession — {asset.InventoryNumber}",
                proceeds,
                0,
                null,
                ThirdPartyKind.None));
        }

        lines.Add(new JournalLineInput(
            asset.AssetAccountNumber,
            $"Sortie immobilisation — {asset.InventoryNumber}",
            0,
            gross,
            null,
            ThirdPartyKind.None));

        if (gainOrLoss > 0)
        {
            lines.Add(new JournalLineInput(
                "736",
                $"Plus-value cession — {asset.InventoryNumber}",
                0,
                gainOrLoss,
                null,
                ThirdPartyKind.None));
        }
        else if (gainOrLoss < 0)
        {
            lines.Add(new JournalLineInput(
                "636",
                $"Moins-value cession — {asset.InventoryNumber}",
                Math.Abs(gainOrLoss),
                0,
                null,
                ThirdPartyKind.None));
        }

        var accountValidation = await ValidateAccountsExistAsync(lines, cancellationToken);
        if (accountValidation.IsFailure)
            return accountValidation;

        var n = await _journalEntries.ReserveNextEntryNumberAsync(FixedAssetJournalCode, entryDate.Year, cancellationToken);
        var create = JournalEntry.Create(
            n,
            FixedAssetJournalCode,
            entryDate,
            $"Cession immobilisation {asset.InventoryNumber}",
            periodResult.Value.Id,
            true,
            SourceFixedAssetDisposal,
            asset.Id,
            lines);

        if (create.IsFailure)
            return Result.Failure(create.Error);

        var entry = create.Value;
        entry.MarkInitialStatus(NewEntryStatus);
        entry.SetAuditInfo("system", false);
        await _journalEntries.AddAsync(entry, cancellationToken);
        return Result.Success();
    }

    public async Task<int> CountReplaceableLinesAsync(string accountNumber, CancellationToken cancellationToken = default)
    {
        var acc = accountNumber?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(acc))
            return 0;

        await using var ctx = _contextFactory.CreateContext();
        return await ctx.JournalEntryLines
            .Where(l => l.AccountNumber == acc && !l.JournalEntry.AccountingPeriod!.IsClosed)
            .CountAsync(cancellationToken);
    }

    public async Task<Result<int>> ReplaceAccountAsync(string oldAccountNumber, string newAccountNumber, CancellationToken cancellationToken = default)
    {
        var oldAcc = oldAccountNumber?.Trim() ?? string.Empty;
        var newAcc = newAccountNumber?.Trim() ?? string.Empty;

        if (string.IsNullOrEmpty(oldAcc) || string.IsNullOrEmpty(newAcc))
            return Result.Failure<int>(Error.Validation("Account", "Les deux comptes sont obligatoires."));
        if (string.Equals(oldAcc, newAcc, StringComparison.Ordinal))
            return Result.Failure<int>(Error.Validation("Account", "Le nouveau compte doit être différent de l'ancien."));

        var oldAccount = await _chartOfAccounts.GetByAccountNumberAsync(oldAcc, cancellationToken);
        if (oldAccount is null)
            return Result.Failure<int>(Error.Validation("Account", $"Le compte {oldAcc} n'existe pas."));
        if (oldAccount.IsSystem)
            return Result.Failure<int>(Error.Validation("Account", $"Le compte système {oldAcc} ne peut pas être remplacé."));

        var newAccount = await _chartOfAccounts.GetByAccountNumberAsync(newAcc, cancellationToken);
        if (newAccount is null)
            return Result.Failure<int>(Error.Validation("Account", $"Le compte {newAcc} n'existe pas."));
        if (!newAccount.IsActive)
            return Result.Failure<int>(Error.Validation("Account", $"Le compte {newAcc} est désactivé."));

        await using var ctx = _contextFactory.CreateContext();
        // Périodes OUVERTES uniquement (une écriture clôturée ne peut pas être modifiée).
        var affected = await ctx.JournalEntryLines
            .Where(l => l.AccountNumber == oldAcc && !l.JournalEntry.AccountingPeriod!.IsClosed)
            .ExecuteUpdateAsync(s => s.SetProperty(l => l.AccountNumber, newAcc), cancellationToken);

        return Result.Success(affected);
    }

    public async Task<Result> GeneratePayrollRunEntryAsync(PayrollRun payrollRun, CancellationToken cancellationToken = default)
    {
        if (await _chartOfAccounts.CountAsync(cancellationToken) == 0)
            return Result.Success();

        // Idempotence : une écriture active suffit. Une écriture extournée (cycle rouvert) ne doit
        // pas bloquer la régénération, sinon la comptabilité resterait figée sur les anciens montants.
        var existing = await _journalEntries.GetActiveBySourceAsync(SourcePayrollRun, payrollRun.Id, cancellationToken);
        if (existing is not null)
            return Result.Success();

        var entryDate = new DateTime(payrollRun.Year, payrollRun.Month, 1).AddMonths(1).AddDays(-1);
        var periodResult = await _periodService.EnsureOpenPeriodAsync(entryDate, cancellationToken);
        if (periodResult.IsFailure)
            return Result.Failure(periodResult.Error);

        var period = periodResult.Value;
        var label = $"Paie {payrollRun.Month:D2}/{payrollRun.Year}";
        var currency = Money.DefaultCurrency;

        // Prefer payslip sum when loaded (covers cycles calculated before TotalOtherDeductions).
        var otherDeductions = payrollRun.Payslips.Count > 0
            ? Math.Round(payrollRun.Payslips.Sum(p => p.OtherDeductions), 3, MidpointRounding.AwayFromZero)
            : payrollRun.TotalOtherDeductions;

        var hasTypedDeductions = payrollRun.Payslips.Any(p =>
            p.Lines.Any(l => l.Kind == PayslipLineKind.Deduction && l.DeductionKind.HasValue));

        var accountMap = new PayrollJournalEntryAccountMap
        {
            LoansAccount = _settings.PayrollEmployeeLoansAccount,
            GarnishmentsAccount = _settings.PayrollGarnishmentsAccount,
            MutuelleEmployeeAccount = _settings.PayrollMutuelleEmployeeAccount,
            MealVoucherEmployeeAccount = _settings.PayrollMealVoucherEmployeeAccount
        };

        var auxiliaryCredits = _settings.PayrollEmployeeAuxiliaryEnabled && payrollRun.Payslips.Count > 0
            ? payrollRun.Payslips
                .Where(p => p.NetSalary > 0)
                .Select(p => new PayrollJournalEntryBuilder.EmployeeAuxiliaryCredit(
                    p.EmployeeId,
                    p.EmployeeName,
                    p.EmployeeAuxiliaryAccount
                        ?? PayrollEmployeeAuxiliaryAccountResolver.Resolve(p.EmployeeNumber),
                    p.NetSalary))
                .ToList()
            : null;

        var linesResult = hasTypedDeductions
            ? PayrollJournalEntryBuilder.BuildLinesFromRun(payrollRun, label, accountMap, auxiliaryCredits)
            : _settings.PayrollEmployeeAuxiliaryEnabled && auxiliaryCredits is { Count: > 0 }
                ? PayrollJournalEntryBuilder.BuildLines(
                    payrollRun.TotalGross,
                    payrollRun.TotalNet,
                    payrollRun.TotalCnssEmployee,
                    payrollRun.TotalCnssEmployer,
                    payrollRun.TotalIrpp,
                    payrollRun.TotalCss,
                    payrollRun.TotalTfp,
                    payrollRun.TotalFoprolos,
                    payrollRun.TotalWorkAccident,
                    otherDeductions,
                    label,
                    auxiliaryCredits,
                    payrollRun.TotalIrppRegularization,
                    payrollRun.TotalCssRegularization,
                    payrollRun.TotalCssEmployer)
                : PayrollJournalEntryBuilder.BuildLines(
                    payrollRun.TotalGross,
                    payrollRun.TotalNet,
                    payrollRun.TotalCnssEmployee,
                    payrollRun.TotalCnssEmployer,
                    payrollRun.TotalIrpp,
                    payrollRun.TotalCss,
                    payrollRun.TotalTfp,
                    payrollRun.TotalFoprolos,
                    payrollRun.TotalWorkAccident,
                    otherDeductions,
                    label,
                    payrollRun.TotalIrppRegularization,
                    payrollRun.TotalCssRegularization,
                    payrollRun.TotalCssEmployer);
        if (linesResult.IsFailure)
            return Result.Failure(linesResult.Error);

        var lines = linesResult.Value;

        var accountValidation = await ValidateAccountsExistAsync(lines, cancellationToken);
        if (accountValidation.IsFailure)
            return accountValidation;

        var n = await _journalEntries.ReserveNextEntryNumberAsync("JOD", payrollRun.Year, cancellationToken);
        var create = JournalEntry.Create(
            n,
            "JOD",
            entryDate,
            label,
            period.Id,
            true,
            SourcePayrollRun,
            payrollRun.Id,
            lines,
            currency);

        if (create.IsFailure)
            return Result.Failure(create.Error);

        var entry = create.Value;
        entry.MarkInitialStatus(NewEntryStatus);
        entry.SetAuditInfo("system", false);
        await _journalEntries.AddAsync(entry, cancellationToken);
        return Result.Success();
    }

    public async Task<Result> ReversePayrollRunEntryAsync(
        Guid payrollRunId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        if (await _chartOfAccounts.CountAsync(cancellationToken) == 0)
            return Result.Success();

        var original = await _journalEntries.GetActiveBySourceAsync(SourcePayrollRun, payrollRunId, cancellationToken);
        if (original is null)
            return Result.Success();

        // Brouillon : suppression pure, la revalidation régénérera une écriture propre.
        if (original.IsDraft)
        {
            await _journalEntries.RemoveAsync(original, cancellationToken);
            return Result.Success();
        }

        var reversalDate = original.EntryDate;
        var periodResult = await _periodService.EnsureOpenPeriodAsync(reversalDate, cancellationToken);
        if (periodResult.IsFailure)
        {
            reversalDate = DateTime.UtcNow.Date;
            periodResult = await _periodService.EnsureOpenPeriodAsync(reversalDate, cancellationToken);
        }

        if (periodResult.IsFailure)
            return Result.Failure(periodResult.Error);

        var period = periodResult.Value;
        var firstLine = original.Lines.First();
        var currency = firstLine.DebitAmount.Amount > 0
            ? firstLine.DebitAmount.Currency
            : firstLine.CreditAmount.Currency;

        var revLines = original.Lines
            .OrderBy(l => l.LineNumber)
            .Select(l => new JournalLineInput(
                l.AccountNumber,
                l.Label,
                l.CreditAmount.Amount,
                l.DebitAmount.Amount,
                l.ThirdPartyId,
                l.ThirdPartyKind))
            .ToList();

        var journal = original.JournalCode;
        var n = await _journalEntries.ReserveNextEntryNumberAsync(journal, reversalDate.Year, cancellationToken);
        var create = JournalEntry.Create(
            n,
            journal,
            reversalDate,
            $"Annulation écriture de paie — {reason.Trim()}",
            period.Id,
            true,
            SourcePayrollRunCancelled,
            payrollRunId,
            revLines,
            currency);

        if (create.IsFailure)
            return Result.Failure(create.Error);

        var reversal = create.Value;
        reversal.MarkInitialStatus(NewEntryStatus);
        reversal.SetAuditInfo("system", false);
        await _journalEntries.AddAsync(reversal, cancellationToken);
        original.MarkReversedBy(reversal.Id);
        await _journalEntries.UpdateAsync(original, cancellationToken);
        return Result.Success();
    }

    public async Task<Result> GeneratePayrollPaymentEntryAsync(
        PayrollPayment payment,
        PayrollRun payrollRun,
        BankAccount? bankAccount,
        CancellationToken cancellationToken = default)
    {
        if (await _chartOfAccounts.CountAsync(cancellationToken) == 0)
            return Result.Success();

        var existing = await _journalEntries.GetBySourceAsync(SourcePayrollPayment, payment.Id, cancellationToken);
        if (existing is not null)
            return Result.Success();

        var date = payment.PaymentDate;
        var periodResult = await _periodService.EnsureOpenPeriodAsync(date, cancellationToken);
        if (periodResult.IsFailure)
            return Result.Failure(periodResult.Error);

        var period = periodResult.Value;
        var currency = payment.Amount.Currency;
        var creditAccount = payment.Method == PaymentMethod.Cash
            ? TreasuryAccount(PaymentMethod.Cash)
            : (bankAccount?.ChartOfAccountNumber ?? TreasuryAccount(payment.Method));
        var label = $"Paiement paie {payrollRun.Month:D2}/{payrollRun.Year}";
        var total = payment.Amount.Amount;

        // Le règlement doit débiter le compte sur lequel la dette a été constatée. Les cycles
        // validés avant les comptes auxiliaires portent un crédit 421 agrégé (sans tiers) : y
        // opposer des débits 421xxxx rendrait le lettrage impossible (groupe multi-comptes).
        var runEntry = await _journalEntries.GetActiveBySourceAsync(SourcePayrollRun, payrollRun.Id, cancellationToken);

        var hasAuxiliaryCredits = runEntry?.Lines.Any(l =>
            l.CreditAmount.Amount > 0 && l.ThirdPartyKind == ThirdPartyKind.Employee) == true;

        var aggregatedCredit = hasAuxiliaryCredits
            ? null
            : runEntry?.Lines.FirstOrDefault(l =>
                l.AccountNumber.StartsWith(PayrollJournalEntryBuilder.PersonnelPayableAccount, StringComparison.Ordinal)
                && l.CreditAmount.Amount > 0
                && l.ThirdPartyKind == ThirdPartyKind.None);

        var lines = new List<JournalLineInput>();

        if (aggregatedCredit is not null)
        {
            // Miroir du format historique : une seule ligne de débit, sur le compte exact de l'OD.
            lines.Add(new JournalLineInput(
                aggregatedCredit.AccountNumber,
                label,
                total,
                0,
                null,
                ThirdPartyKind.None));
        }
        else
        {
            foreach (var line in payment.Lines)
            {
                lines.Add(new JournalLineInput(
                    line.EmployeeAuxiliaryAccount,
                    $"{label} — {line.EmployeeAuxiliaryAccount}",
                    line.Amount.Amount,
                    0,
                    line.EmployeeId,
                    ThirdPartyKind.Employee));
            }
        }

        lines.Add(new JournalLineInput(
            creditAccount,
            label,
            0,
            total,
            null,
            ThirdPartyKind.None));

        var accountValidation = await ValidateAccountsExistAsync(lines, cancellationToken);
        if (accountValidation.IsFailure)
            return accountValidation;

        var journal = payment.Method == PaymentMethod.Cash ? "JC" : BankJournalCode;
        var n = await _journalEntries.ReserveNextEntryNumberAsync(journal, date.Year, cancellationToken);
        var create = JournalEntry.Create(
            n,
            journal,
            date,
            label,
            period.Id,
            true,
            SourcePayrollPayment,
            payment.Id,
            lines,
            currency);

        if (create.IsFailure)
            return Result.Failure(create.Error);

        var entry = create.Value;
        entry.MarkInitialStatus(NewEntryStatus);
        entry.SetAuditInfo("system", false);
        await _journalEntries.AddAsync(entry, cancellationToken);
        return Result.Success();
    }

    public async Task<Result> ReversePayrollPaymentEntryAsync(
        Guid payrollPaymentId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        if (await _chartOfAccounts.CountAsync(cancellationToken) == 0)
            return Result.Success();

        var existingReversal = await _journalEntries.GetBySourceAsync(
            SourcePayrollPaymentCancelled, payrollPaymentId, cancellationToken);
        if (existingReversal is not null)
            return Result.Success();

        var original = await _journalEntries.GetBySourceAsync(SourcePayrollPayment, payrollPaymentId, cancellationToken);
        if (original is null)
            return Result.Success();

        if (original.IsDraft)
        {
            await _journalEntries.RemoveAsync(original, cancellationToken);
            return Result.Success();
        }

        var reversalDate = original.EntryDate;
        var periodResult = await _periodService.EnsureOpenPeriodAsync(reversalDate, cancellationToken);
        if (periodResult.IsFailure)
        {
            reversalDate = DateTime.UtcNow.Date;
            periodResult = await _periodService.EnsureOpenPeriodAsync(reversalDate, cancellationToken);
        }

        if (periodResult.IsFailure)
            return Result.Failure(periodResult.Error);

        var period = periodResult.Value;
        var firstLine = original.Lines.First();
        var currency = firstLine.DebitAmount.Amount > 0
            ? firstLine.DebitAmount.Currency
            : firstLine.CreditAmount.Currency;

        var revLines = original.Lines
            .OrderBy(l => l.LineNumber)
            .Select(l => new JournalLineInput(
                l.AccountNumber,
                l.Label,
                l.CreditAmount.Amount,
                l.DebitAmount.Amount,
                l.ThirdPartyId,
                l.ThirdPartyKind))
            .ToList();

        var journal = original.JournalCode;
        var n = await _journalEntries.ReserveNextEntryNumberAsync(journal, reversalDate.Year, cancellationToken);
        var create = JournalEntry.Create(
            n,
            journal,
            reversalDate,
            $"Annulation paiement paie — {reason.Trim()}",
            period.Id,
            true,
            SourcePayrollPaymentCancelled,
            payrollPaymentId,
            revLines,
            currency);

        if (create.IsFailure)
            return Result.Failure(create.Error);

        var reversal = create.Value;
        reversal.MarkInitialStatus(NewEntryStatus);
        reversal.SetAuditInfo("system", false);
        await _journalEntries.AddAsync(reversal, cancellationToken);
        original.MarkReversedBy(reversal.Id);
        await _journalEntries.UpdateAsync(original, cancellationToken);
        return Result.Success();
    }

    public async Task<Result> GenerateCnssContributionPaymentEntryAsync(
        CnssContributionPayment payment,
        BankAccount? bankAccount,
        CancellationToken cancellationToken = default)
    {
        if (await _chartOfAccounts.CountAsync(cancellationToken) == 0)
            return Result.Success();

        var existing = await _journalEntries.GetBySourceAsync(
            SourceCnssContributionPayment, payment.Id, cancellationToken);
        if (existing is not null)
            return Result.Success();

        var date = payment.PaymentDate;
        var periodResult = await _periodService.EnsureOpenPeriodAsync(date, cancellationToken);
        if (periodResult.IsFailure)
            return Result.Failure(periodResult.Error);

        var period = periodResult.Value;
        var currency = payment.Amount.Currency;
        var creditAccount = payment.Method == PaymentMethod.Cash
            ? TreasuryAccount(PaymentMethod.Cash)
            : (bankAccount?.ChartOfAccountNumber ?? TreasuryAccount(payment.Method));
        var label = $"Versement CNSS {payment.Month:D2}/{payment.Year}";

        var linesResult = CnssContributionPaymentJournalBuilder.BuildPaymentLines(
            payment.Amount.Amount, label, creditAccount);
        if (linesResult.IsFailure)
            return Result.Failure(linesResult.Error);

        var accountValidation = await ValidateAccountsExistAsync(linesResult.Value, cancellationToken);
        if (accountValidation.IsFailure)
            return accountValidation;

        var journal = payment.Method == PaymentMethod.Cash ? "JC" : BankJournalCode;
        var n = await _journalEntries.ReserveNextEntryNumberAsync(journal, date.Year, cancellationToken);
        var create = JournalEntry.Create(
            n,
            journal,
            date,
            label,
            period.Id,
            true,
            SourceCnssContributionPayment,
            payment.Id,
            linesResult.Value,
            currency);

        if (create.IsFailure)
            return Result.Failure(create.Error);

        var entry = create.Value;
        entry.MarkInitialStatus(NewEntryStatus);
        entry.SetAuditInfo("system", false);
        await _journalEntries.AddAsync(entry, cancellationToken);
        return Result.Success();
    }

    public async Task<Result> ReverseCnssContributionPaymentEntryAsync(
        Guid cnssContributionPaymentId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        if (await _chartOfAccounts.CountAsync(cancellationToken) == 0)
            return Result.Success();

        var existingReversal = await _journalEntries.GetBySourceAsync(
            SourceCnssContributionPaymentCancelled, cnssContributionPaymentId, cancellationToken);
        if (existingReversal is not null)
            return Result.Success();

        var original = await _journalEntries.GetBySourceAsync(
            SourceCnssContributionPayment, cnssContributionPaymentId, cancellationToken);
        if (original is null)
            return Result.Success();

        if (original.IsDraft)
        {
            await _journalEntries.RemoveAsync(original, cancellationToken);
            return Result.Success();
        }

        var reversalDate = original.EntryDate;
        var periodResult = await _periodService.EnsureOpenPeriodAsync(reversalDate, cancellationToken);
        if (periodResult.IsFailure)
        {
            reversalDate = DateTime.UtcNow.Date;
            periodResult = await _periodService.EnsureOpenPeriodAsync(reversalDate, cancellationToken);
        }

        if (periodResult.IsFailure)
            return Result.Failure(periodResult.Error);

        var period = periodResult.Value;
        var firstLine = original.Lines.First();
        var currency = firstLine.DebitAmount.Amount > 0
            ? firstLine.DebitAmount.Currency
            : firstLine.CreditAmount.Currency;

        var revLines = original.Lines
            .OrderBy(l => l.LineNumber)
            .Select(l => new JournalLineInput(
                l.AccountNumber,
                l.Label,
                l.CreditAmount.Amount,
                l.DebitAmount.Amount,
                l.ThirdPartyId,
                l.ThirdPartyKind))
            .ToList();

        var journal = original.JournalCode;
        var n = await _journalEntries.ReserveNextEntryNumberAsync(journal, reversalDate.Year, cancellationToken);
        var create = JournalEntry.Create(
            n,
            journal,
            reversalDate,
            $"Annulation versement CNSS — {reason.Trim()}",
            period.Id,
            true,
            SourceCnssContributionPaymentCancelled,
            cnssContributionPaymentId,
            revLines,
            currency);

        if (create.IsFailure)
            return Result.Failure(create.Error);

        var reversal = create.Value;
        reversal.MarkInitialStatus(NewEntryStatus);
        reversal.SetAuditInfo("system", false);
        await _journalEntries.AddAsync(reversal, cancellationToken);
        original.MarkReversedBy(reversal.Id);
        await _journalEntries.UpdateAsync(original, cancellationToken);
        return Result.Success();
    }
}
