using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.Configuration;
using FactuTrust.Application.Features.Accounting.Commands;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Application;

/// <summary>
/// Multi-devises, lot 3 : contrôles d'entrée de l'apurement d'écart de change.
///
/// <para>
/// Le compte d'imputation est saisi librement par l'utilisateur. Sans borne serveur, il pourrait
/// loger l'écart dans un compte de trésorerie ou chez un autre tiers : le déséquilibre disparaîtrait
/// du bilan sans jamais être constaté en résultat. C'est ce que verrouillent ces tests.
/// </para>
/// </summary>
public sealed class SettleExchangeDifferenceValidationTests
{
    private static readonly Guid LineA = Guid.NewGuid();
    private static readonly Guid LineB = Guid.NewGuid();

    private static ChartOfAccount Account(string number, bool active = true)
    {
        var accountClass = number[0] - '0';
        var account = ChartOfAccount.Create(number, $"Compte {number}", accountClass, null, AccountNatureType.Debit).Value;
        if (!active)
            account.ToggleActive();
        return account;
    }

    private static SettleExchangeDifferenceCommandHandler Build(
        ChartOfAccount? account,
        bool multiCurrencyEnabled = true)
    {
        var chart = new Mock<IChartOfAccountRepository>();
        chart.Setup(c => c.GetByAccountNumberAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(account);

        // Le vrai dépôt ne rend jamais null ; Moq, si, tant qu'on ne le configure pas.
        var journalEntries = new Mock<IJournalEntryRepository>();
        journalEntries
            .Setup(j => j.GetLinesByIdsAsync(It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<JournalEntryLine>());

        var settings = Options.Create(new AccountingSettings { MultiCurrencyEnabled = multiCurrencyEnabled });

        return new SettleExchangeDifferenceCommandHandler(
            journalEntries.Object,
            chart.Object,
            new Mock<IAccountingPeriodService>().Object,
            new Mock<ILetteringService>().Object,
            new Mock<ITenantUnitOfWork>().Object,
            new Mock<IAuditService>().Object,
            new Mock<ICurrentUser>().Object,
            settings);
    }

    private static SettleExchangeDifferenceCommand Command(string accountNumber) =>
        new(new[] { LineA, LineB }, accountNumber);

    [Fact]
    public async Task IsRefusedWhenMultiCurrencyIsDisabled()
    {
        var handler = Build(Account("655"), multiCurrencyEnabled: false);

        var result = await handler.Handle(Command("655"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("multi-devises", result.Error.Description);
    }

    [Fact]
    public async Task IsRefusedWithFewerThanTwoLines()
    {
        var handler = Build(Account("655"));

        var result = await handler.Handle(new SettleExchangeDifferenceCommand(new[] { LineA }, "655"), CancellationToken.None);

        Assert.True(result.IsFailure);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task IsRefusedWithoutAccount(string accountNumber)
    {
        var handler = Build(Account("655"));

        var result = await handler.Handle(Command(accountNumber), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("obligatoire", result.Error.Description);
    }

    [Fact]
    public async Task IsRefusedWhenAccountDoesNotExist()
    {
        var handler = Build(account: null);

        var result = await handler.Handle(Command("999"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("n'existe pas", result.Error.Description);
    }

    [Fact]
    public async Task IsRefusedWhenAccountIsInactive()
    {
        var handler = Build(Account("655", active: false));

        var result = await handler.Handle(Command("655"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("désactivé", result.Error.Description);
    }

    /// <summary>
    /// Cœur du garde-fou : un écart de change s'impute en charge ou en produit, jamais sur un tiers
    /// ni sur la trésorerie — sinon le déséquilibre est déplacé au lieu d'être constaté.
    /// </summary>
    [Theory]
    [InlineData("4011")] // fournisseur
    [InlineData("4111")] // client
    [InlineData("5321")] // banque
    [InlineData("2154")] // immobilisation
    [InlineData("3011")] // stock
    public async Task IsRefusedOnAForbiddenAccountClass(string accountNumber)
    {
        var handler = Build(Account(accountNumber));

        var result = await handler.Handle(Command(accountNumber), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("classe 6 ou 7", result.Error.Description);
    }

    /// <summary>
    /// Les comptes admis : charges et produits (dont 655 / 756 en NCT 01, et 6613 pour un dossier
    /// non migré), plus les écarts de conversion 185 / 275 utilisés par la réévaluation.
    /// </summary>
    [Theory]
    [InlineData("655")]  // pertes de change NCT 01
    [InlineData("756")]  // gains de change NCT 01
    [InlineData("6613")] // dossier non migré
    [InlineData("185")]  // écarts de conversion passif
    [InlineData("275")]  // écarts de conversion actif
    public async Task PassesTheAccountCheckOnAnAllowedClass(string accountNumber)
    {
        var handler = Build(Account(accountNumber));

        var result = await handler.Handle(Command(accountNumber), CancellationToken.None);

        // Le contrôle de compte est franchi : l'échec suivant porte sur les lignes, introuvables
        // avec un dépôt simulé — c'est bien la preuve que le compte a été accepté.
        Assert.True(result.IsFailure);
        Assert.DoesNotContain("classe 6 ou 7", result.Error.Description);
        Assert.DoesNotContain("n'existe pas", result.Error.Description);
    }
}
