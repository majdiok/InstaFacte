using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Configuration;
using FactuTrust.Application.Features.Accounting.Fiscal;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities.Fiscal;
using FactuTrust.Domain.Enums;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Services;

/// <summary>
/// T27 — l'upsert sur une feuille déjà finalisée doit rendre <c>Error.Conflict</c> (→ HTTP 409) et non
/// <c>Error.Validation</c> (→ 400). La feuille finalisée lève <c>InvalidOperationException</c> via
/// <c>FiscalResultDeclaration.EnsureEditable</c> (appelée par <c>UpsertAsync</c> / <c>UpdateInputs</c>) ;
/// le <c>catch</c> du handler <c>UpsertFiscalResultDeclarationCommandHandler</c> la traduit en conflit
/// d'état. Test compagnon de <c>LiasseHttpSemanticsTests</c> (mapping contrôleur 409) et de
/// <c>FiscalResultFinalizationTests</c> (409 finalisation/verrou/réconciliation).
/// </summary>
public sealed class FiscalResultUpsertConflictTests
{
    private const int Year = 2026;
    private const string FinalizedMessage =
        "La feuille de détermination du résultat fiscal est finalisée et non modifiable.";

    private static UpsertFiscalResultRequest ValidRequest() => new(
        TaxpayerKind: (int)TaxpayerKind.CorporateIS,
        AccountingResult: 10_000m,
        AppliedIsRate: 0.15m,
        LocalTurnoverTtc: 0m,
        AcomptesPaid: 0m,
        WithholdingSuffered: 0m,
        PriorTaxCredit: 0m,
        Adjustments: Array.Empty<FiscalAdjustmentLineInput>(),
        CarryForwards: Array.Empty<FiscalCarryForwardInput>());

    [Fact]
    public async Task Upsert_OnFinalizedSheet_ReturnsConflict_NotValidation()
    {
        var declarations = new Mock<IFiscalResultDeclarationRepository>();
        // UpsertAsync jette InvalidOperationException (feuille finalisée → EnsureEditable).
        declarations
            .Setup(x => x.UpsertAsync(It.IsAny<FiscalDeclarationUpsert>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException(FinalizedMessage));

        var parameters = new Mock<IIncomeTaxYearParameterRepository>();
        parameters.Setup(x => x.GetOrDefaultAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(IncomeTaxYearParameterDefaults.Create(Year));

        var settings = Options.Create(new AccountingSettings { FiscalLiasseEnabled = true });
        var handler = new UpsertFiscalResultDeclarationCommandHandler(
            declarations.Object, parameters.Object, settings);

        var result = await handler.Handle(
            new UpsertFiscalResultDeclarationCommand(Year, ValidRequest()), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Conflict", result.Error.Code);
        Assert.Contains("finalisée", result.Error.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Upsert_InvalidTaxpayerKind_ReturnsValidation_NotConflict()
    {
        // Une validation de contenu (TaxpayerKind hors enum) passe AVANT UpsertAsync → 400, pas 409.
        var declarations = new Mock<IFiscalResultDeclarationRepository>();
        var parameters = new Mock<IIncomeTaxYearParameterRepository>();
        parameters.Setup(x => x.GetOrDefaultAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(IncomeTaxYearParameterDefaults.Create(Year));

        var settings = Options.Create(new AccountingSettings { FiscalLiasseEnabled = true });
        var handler = new UpsertFiscalResultDeclarationCommandHandler(
            declarations.Object, parameters.Object, settings);

        var invalid = new UpsertFiscalResultRequest(
            TaxpayerKind: 999, AccountingResult: 10_000m, AppliedIsRate: 0.15m, LocalTurnoverTtc: 0m,
            AcomptesPaid: 0m, WithholdingSuffered: 0m, PriorTaxCredit: 0m,
            Adjustments: Array.Empty<FiscalAdjustmentLineInput>(),
            CarryForwards: Array.Empty<FiscalCarryForwardInput>());

        var result = await handler.Handle(
            new UpsertFiscalResultDeclarationCommand(Year, invalid), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.NotEqual("Conflict", result.Error.Code); // Validation (400)
        // Aucune persistance tentée.
        declarations.Verify(
            x => x.UpsertAsync(It.IsAny<FiscalDeclarationUpsert>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
