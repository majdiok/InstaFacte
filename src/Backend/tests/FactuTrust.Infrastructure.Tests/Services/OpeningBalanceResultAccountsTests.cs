using FactuTrust.Infrastructure.Services;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Services;

/// <summary>
/// Verrouille la cohérence avec le seed <c>ChartOfAccounts</c> (migration tenant AddAccountingModule).
/// </summary>
public sealed class OpeningBalanceResultAccountsTests
{
    [Fact]
    public void Opening_balance_profit_and_loss_accounts_match_tenant_seed()
    {
        Assert.Equal("131", AccountingService.OpeningBalanceProfitAccountNumber);
        Assert.Equal("135", AccountingService.OpeningBalanceLossAccountNumber);
    }
}
