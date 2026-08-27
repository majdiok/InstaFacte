using FactuTrust.Application.Configuration;
using FactuTrust.Application.Features.CashDesk.Queries;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Application;

/// <summary>
/// Plan §6.4/§9.6 : le endpoint feature-flags caisse restitue fidèlement
/// <c>AccountingSettings.CashDeskVatEnabled</c> (configuration applicative globale, cf. modèle
/// <c>GetPayrollFeatureFlagsQuery</c>).
/// </summary>
public sealed class GetCashDeskFeatureFlagsQueryTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Handle_ReturnsVatEnabled_MatchingSettings(bool vatEnabled)
    {
        var handler = new GetCashDeskFeatureFlagsQueryHandler(
            Options.Create(new AccountingSettings { CashDeskVatEnabled = vatEnabled }));

        var dto = await handler.Handle(new GetCashDeskFeatureFlagsQuery(), CancellationToken.None);

        dto.VatEnabled.Should().Be(vatEnabled);
    }
}
