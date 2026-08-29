using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.Configuration;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.Accounting.Fiscal;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities.Fiscal;
using FactuTrust.Domain.Enums;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Services;

/// <summary>
/// T17 — lecture des pénalités (6712 / 668) pour la suggestion R-PENALITES.
/// Le compte 6712 n'existe pas au catalogue livré (sous-compte tenant potentiel) : un tenant sans 6712
/// ne lève JAMAIS d'exception — la suggestion se réduit aux débits 668 seuls.
/// </summary>
public sealed class FiscalResultPenaltiesTests
{
    private const int Year = 2026;

    private static BalanceRowDto Row(string account, decimal debit, decimal credit = 0m) => new()
    {
        AccountNumber = account,
        Label = $"Compte {account}",
        MovementDebit = debit,
        MovementCredit = credit
    };

    /// <summary>
    /// Construit le handler de requête avec un reporting factice : la balance ne contient QUE des
    /// débits 668 (aucune ligne 6712) et aucun état NCT n'existe (retour succès vide → aperçu initial).
    /// </summary>
    private static (GetFiscalResultDeclarationQueryHandler handler, Mock<IAccountingReportingService> reporting) BuildHandler(
        IReadOnlyList<BalanceRowDto> balanceRows)
    {
        var declarations = new Mock<IFiscalResultDeclarationRepository>();
        declarations.Setup(x => x.GetByYearAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((FiscalResultDeclaration?)null);

        var parameters = new Mock<IIncomeTaxYearParameterRepository>();
        parameters.Setup(x => x.GetOrDefaultAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(IncomeTaxYearParameterDefaults.Create(Year));

        var reporting = new Mock<IAccountingReportingService>();
        reporting.Setup(x => x.GetBalanceAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success<IReadOnlyList<BalanceRowDto>>(balanceRows));
        // Pas de feuille existante → le handler appelle GetNctStatementsAsync (aperçu initial).
        reporting.Setup(x => x.GetNctStatementsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(new NctFinancialStatementsDto { FiscalYear = Year }));

        var settings = Options.Create(new AccountingSettings { FiscalLiasseEnabled = true });

        var handler = new GetFiscalResultDeclarationQueryHandler(
            declarations.Object, parameters.Object, reporting.Object, settings);
        return (handler, reporting);
    }

    [Fact]
    public async Task Penalties_TenantWithout6712_Suggests668DebitsOnly_NoException()
    {
        // Aucun sous-compte 6712 au plan comptable du tenant ; seuls des débits 668 existent.
        var rows = new[]
        {
            Row("6681", debit: 750m),
            Row("411", debit: 10_000m) // hors périmètre pénalités
        };

        var (handler, _) = BuildHandler(rows);
        var result = await handler.Handle(new GetFiscalResultDeclarationQuery(Year), CancellationToken.None);

        // Aucune exception levée (le tenant n'a pas de 6712) : succès pur.
        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Description : null);
        var penalties = Assert.Single(result.Value.Adjustments,
            a => a.CatalogCode == "R-PENALITES");
        // La suggestion ne retient que les débits 668 (750), sans aucune contribution 6712.
        Assert.Equal(750m, penalties.Amount);
        Assert.True(penalties.IsAutoSuggested);
    }

    [Fact]
    public async Task Penalties_WithBoth6712And668_SumsBothDebitRoots()
    {
        // Tenant ayant créé le sous-compte 6712 : les deux racines se cumulent.
        var rows = new[]
        {
            Row("6712", debit: 300m),
            Row("6681", debit: 750m)
        };

        var (handler, _) = BuildHandler(rows);
        var result = await handler.Handle(new GetFiscalResultDeclarationQuery(Year), CancellationToken.None);

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Description : null);
        var penalties = Assert.Single(result.Value.Adjustments,
            a => a.CatalogCode == "R-PENALITES");
        Assert.Equal(1_050m, penalties.Amount);
    }

    [Fact]
    public async Task Penalties_NoPenaltyAccounts_NoSuggestion_NoException()
    {
        // Aucun débit sur 668 ni 6712 : pas de suggestion R-PENALITES, aucune exception.
        var rows = new[]
        {
            Row("411", debit: 10_000m),
            Row("707", debit: 0m, credit: 10_000m)
        };

        var (handler, _) = BuildHandler(rows);
        var result = await handler.Handle(new GetFiscalResultDeclarationQuery(Year), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.DoesNotContain(result.Value.Adjustments, a => a.CatalogCode == "R-PENALITES");
    }
}
