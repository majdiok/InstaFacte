using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Entities;
using FactuTrust.Infrastructure.MultiTenancy;
using Microsoft.EntityFrameworkCore;

namespace FactuTrust.Infrastructure.Services;

public sealed record BankAccountMatchResult(
    Guid? BankAccountId,
    string? ChartOfAccountNumber,
    string? BankName,
    string? Iban,
    bool IsAmbiguous);

/// <summary>Rapproche un RIB détecté dans un relevé avec les comptes bancaires paramétrés.</summary>
public sealed class BankAccountMatcher
{
    private readonly ITenantDbContextFactory _contextFactory;

    public BankAccountMatcher(ITenantDbContextFactory contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<(BankAccountMatchResult Match, IReadOnlyList<ImportIssueDto> Issues)> MatchByRibAsync(
        string? ribDigits, CancellationToken cancellationToken = default)
    {
        var issues = new List<ImportIssueDto>();
        if (string.IsNullOrWhiteSpace(ribDigits) || ribDigits.Length != 20)
        {
            issues.Add(NonBlocking("rib", "RIB non détecté dans le relevé — sélectionnez le compte bancaire manuellement."));
            return (new BankAccountMatchResult(null, null, null, null, false), issues);
        }

        await using var ctx = _contextFactory.CreateContext();
        var accounts = await ctx.BankAccounts.AsNoTracking()
            .Where(a => a.IsActive && a.Rib == ribDigits)
            .ToListAsync(cancellationToken);

        if (accounts.Count == 0)
        {
            issues.Add(NonBlocking("rib",
                $"Aucun compte bancaire paramétré pour le RIB {FormatRib(ribDigits)} — créez-le ou sélectionnez-le manuellement."));
            return (new BankAccountMatchResult(null, null, null, null, false), issues);
        }

        if (accounts.Count > 1)
        {
            issues.Add(Blocking("rib",
                $"Plusieurs comptes bancaires correspondent au RIB {FormatRib(ribDigits)} — sélectionnez le compte manuellement."));
            return (new BankAccountMatchResult(null, null, null, null, true), issues);
        }

        var acc = accounts[0];
        if (string.IsNullOrWhiteSpace(acc.ChartOfAccountNumber))
        {
            issues.Add(NonBlocking("compte-comptable",
                "Le compte bancaire n'est pas lié à un compte 532x — associez-le dans Trésorerie > Comptes bancaires."));
        }

        return (new BankAccountMatchResult(acc.Id, acc.ChartOfAccountNumber, acc.BankName, acc.Iban, false), issues);
    }

    private static string FormatRib(string rib) =>
        rib.Length == 20
            ? $"{rib[..2]} {rib[2..5]} {rib[5..10]} {rib[10..12]} {rib[12..17]} {rib[17..18]} {rib[18..]}"
            : rib;

    private static ImportIssueDto Blocking(string reference, string message) =>
        new() { Ref = reference, Message = message, IsBlocking = true };

    private static ImportIssueDto NonBlocking(string reference, string message) =>
        new() { Ref = reference, Message = message, IsBlocking = false };
}
