using FactuTrust.Domain.Authorization;
using FactuTrust.Domain.Enums;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Authorization;

/// <summary>
/// Le filtre qui fait porter à la permission l'état du module multi-devises.
///
/// <para>
/// Sans lui, <c>accounting:currencies_manage</c> entre dans le JWT même module éteint :
/// l'autorisation HTTP passe, l'interface affiche le bouton, et le handler refuse en 400. Ces tests
/// verrouillent les deux propriétés qui comptent — la clé disparaît quand le module est éteint, et
/// <b>aucune autre permission n'est jamais touchée</b>.
/// </para>
/// </summary>
public sealed class AccountingCurrencyAccessTests
{
    private static readonly string[] Untouched =
    {
        Permissions.Accounting.Read,
        Permissions.Accounting.Create,
        Permissions.Accounting.Close,
        Permissions.Accounting.Validate,
        Permissions.Payroll.RunPayroll,
        Permissions.Users.Read
    };

    [Fact]
    public void WhenDisabled_BothCurrencyPermissionsAreRemoved()
    {
        var permissions = new[]
        {
            Permissions.Accounting.Read,
            Permissions.Accounting.CurrenciesManage,
            Permissions.Accounting.ExchangeRateOverride
        };

        var result = AccountingCurrencyAccess.FilterWhenDisabled(permissions, multiCurrencyEnabled: false);

        Assert.DoesNotContain(Permissions.Accounting.CurrenciesManage, result);
        Assert.DoesNotContain(Permissions.Accounting.ExchangeRateOverride, result);
    }

    [Fact]
    public void WhenEnabled_TheListIsReturnedIntact()
    {
        var permissions = new[]
        {
            Permissions.Accounting.Read,
            Permissions.Accounting.CurrenciesManage,
            Permissions.Accounting.ExchangeRateOverride
        };

        var result = AccountingCurrencyAccess.FilterWhenDisabled(permissions, multiCurrencyEnabled: true);

        Assert.Equal(permissions, result);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void OtherPermissionsAreNeverTouched(bool enabled)
    {
        // Le point le plus important : ce filtre ne doit jamais retirer autre chose que ses deux clés.
        var permissions = Untouched
            .Append(Permissions.Accounting.CurrenciesManage)
            .Append(Permissions.Accounting.ExchangeRateOverride)
            .ToArray();

        var result = AccountingCurrencyAccess.FilterWhenDisabled(permissions, enabled);

        Assert.All(Untouched, p => Assert.Contains(p, result));
    }

    [Fact]
    public void WhenDisabled_TheOrderOfSurvivingPermissionsIsPreserved()
    {
        var permissions = new[]
        {
            Permissions.Accounting.Read,
            Permissions.Accounting.CurrenciesManage,
            Permissions.Accounting.Close,
            Permissions.Accounting.ExchangeRateOverride,
            Permissions.Users.Read
        };

        var result = AccountingCurrencyAccess.FilterWhenDisabled(permissions, multiCurrencyEnabled: false);

        Assert.Equal(
            new[] { Permissions.Accounting.Read, Permissions.Accounting.Close, Permissions.Users.Read },
            result);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AnEmptyListIsHandled(bool enabled)
    {
        var result = AccountingCurrencyAccess.FilterWhenDisabled(Array.Empty<string>(), enabled);

        Assert.Empty(result);
    }

    [Fact]
    public void AListWithoutCurrencyPermissionsIsUnchanged()
    {
        var result = AccountingCurrencyAccess.FilterWhenDisabled(Untouched, multiCurrencyEnabled: false);

        Assert.Equal(Untouched, result);
    }

    [Theory]
    [InlineData("accounting:currencies_manage", true)]
    [InlineData("accounting:exchange_rate_override", true)]
    [InlineData("accounting:read", false)]
    [InlineData("accounting:close", false)]
    [InlineData("", false)]
    public void IsMultiCurrencyPermission_RecognisesExactlyTheTwoKeys(string permission, bool expected)
    {
        Assert.Equal(expected, AccountingCurrencyAccess.IsMultiCurrencyPermission(permission));
    }

    /// <summary>
    /// Le filtre gouverne exactement deux clés : si quelqu'un en ajoute une troisième sans y penser,
    /// ce test le signale plutôt que de laisser une permission disparaître silencieusement.
    /// </summary>
    [Fact]
    public void TheGatedSetIsExactlyTheTwoDocumentedKeys()
    {
        Assert.Equal(
            new[] { Permissions.Accounting.CurrenciesManage, Permissions.Accounting.ExchangeRateOverride }.OrderBy(x => x),
            AccountingCurrencyAccess.FlagGatedPermissionList.OrderBy(x => x));
    }
}
