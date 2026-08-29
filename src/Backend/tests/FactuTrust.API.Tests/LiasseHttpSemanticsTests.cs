using FactuTrust.API.Controllers;
using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.Accounting.Fiscal;
using FactuTrust.Domain.Common;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

// AccountingController emploie FactuTrust.API.Controllers.ApiResponse<T> (présente dans le namespace
// du contrôleur) dont Fail(error, code) reporte le message dans Error (le code est dropped) ; on l'alias
// pour lever l'ambiguïté avec FactuTrust.Application.DTOs.ApiResponse<T>.
using LiasseResponse = FactuTrust.API.Controllers.ApiResponse<object>;

namespace FactuTrust.API.Tests;

/// <summary>
/// T27 — sémantique HTTP 409 sur les mutations interdites (feuille finalisée / exercice verrouillé /
/// re-finalisation / réconciliation), 400 sur les autres validations de contenu.
/// Les échecs <c>Result</c> ne transitent pas par <c>ExceptionHandlingMiddleware</c> : chaque endpoint
/// liasse mappe localement le code d'erreur (patron <c>FinalizeFiscalResult</c> /
/// <c>StorefrontTenantController.MapFailure</c>). Ces tests valident le mapping au niveau contrôleur —
/// la décision « finalisée → <c>Error.Conflict</c> » est acquise côté handler (T10 + T27, catch
/// <c>InvalidOperationException</c> de <c>UpsertAsync</c>, voir <c>FiscalResultUpsertConflictTests</c>).
///
/// NB : <c>AccountingController</c> emploie <c>FactuTrust.API.Controllers.ApiResponse&lt;T&gt;</c>
/// (alias <c>LiasseResponse</c>) dont <c>Fail(error, code)</c> reporte le message dans le champ
/// <c>Error</c> ; la distinction 409/400 se lit donc sur le code de statut HTTP
/// (<c>ConflictObjectResult</c> / <c>BadRequestObjectResult</c>).
/// </summary>
public sealed class LiasseHttpSemanticsTests
{
    private const int Year = 2026;
    private const string FinalizedMessage =
        "La feuille de détermination du résultat fiscal est finalisée et non modifiable.";

    private static UpsertFiscalResultRequest ValidRequest() => new(
        TaxpayerKind: 0,
        AccountingResult: 10_000m,
        AppliedIsRate: 0.15m,
        LocalTurnoverTtc: 0m,
        AcomptesPaid: 0m,
        WithholdingSuffered: 0m,
        PriorTaxCredit: 0m,
        Adjustments: Array.Empty<FiscalAdjustmentLineInput>(),
        CarryForwards: Array.Empty<FiscalCarryForwardInput>());

    /// <summary>Construit un AccountingController avec un médiateur factice et un contexte HTTP minimal.</summary>
    private static AccountingController NewController(Mock<IMediator> mediator)
    {
        var currentUser = new Mock<ICurrentUser>();
        var controller = new AccountingController(mediator.Object, currentUser.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
        return controller;
    }

    // ── Upsert (POST fiscal-result) : feuille finalisée → 409 + message ─────────────────────

    [Fact]
    public async Task Upsert_OnFinalizedSheet_Returns409_WithMessage()
    {
        // Le handler lève Error.Conflict lorsqu'UpsertAsync jette InvalidOperationException (feuille
        // finalisée — EnsureEditable). Le contrôleur doit le traduire en 409 (T27), pas 400.
        var mediator = new Mock<IMediator>();
        mediator
            .Setup(m => m.Send(It.IsAny<UpsertFiscalResultDeclarationCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<FiscalResultDeclarationDto>(Error.Conflict(FinalizedMessage)));

        var controller = NewController(mediator);

        var action = await controller.UpsertFiscalResult(Year, ValidRequest(), CancellationToken.None);

        var conflict = Assert.IsType<ConflictObjectResult>(action);
        Assert.Equal(StatusCodes.Status409Conflict, conflict.StatusCode);
        var body = Assert.IsType<LiasseResponse>(conflict.Value);
        Assert.False(body.Success);
        Assert.Contains("finalisée", body.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Upsert_ValidationFailure_Returns400()
    {
        // Les validations de contenu (TaxpayerKind invalide, reports…) restent Error.Validation → 400.
        var mediator = new Mock<IMediator>();
        mediator
            .Setup(m => m.Send(It.IsAny<UpsertFiscalResultDeclarationCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<FiscalResultDeclarationDto>(
                Error.Validation("TaxpayerKind", "Type de contribuable invalide.")));

        var controller = NewController(mediator);

        var action = await controller.UpsertFiscalResult(Year, ValidRequest(), CancellationToken.None);

        var bad = Assert.IsType<BadRequestObjectResult>(action);
        Assert.Equal(StatusCodes.Status400BadRequest, bad.StatusCode);
        var body = Assert.IsType<LiasseResponse>(bad.Value);
        Assert.False(body.Success);
        Assert.Contains("contribuable", body.Error, StringComparison.OrdinalIgnoreCase);
    }

    // ── Finalize (POST fiscal-result/finalize) : 409 sur finalisé / verrouillé / réconciliation ──

    [Theory]
    [InlineData("La feuille de détermination du résultat fiscal de l'exercice 2026 est déjà finalisée.",
        "re-finalisation")]
    [InlineData("L'exercice 2026 est verrouillé : finalisation impossible.", "exercice verrouillé")]
    [InlineData("Le résultat comptable de la feuille ne correspond plus aux livres après comptabilisation " +
                "de l'impôt : reprendre le résultat suggéré et re-vérifier les réintégrations.",
        "réconciliation feuille/livres")]
    public async Task Finalize_ConflictStates_Return409(string message, string _)
    {
        var mediator = new Mock<IMediator>();
        mediator
            .Setup(m => m.Send(It.IsAny<FinalizeFiscalResultDeclarationCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure(Error.Conflict(message)));

        var controller = NewController(mediator);

        var action = await controller.FinalizeFiscalResult(Year, CancellationToken.None);

        var conflict = Assert.IsType<ConflictObjectResult>(action);
        Assert.Equal(StatusCodes.Status409Conflict, conflict.StatusCode);
        var body = Assert.IsType<LiasseResponse>(conflict.Value);
        Assert.False(body.Success);
        Assert.Equal(message, body.Error);
    }

    [Fact]
    public async Task Finalize_NoSheet_Returns400()
    {
        // Absence de feuille = validation (400), pas un conflit d'état (409).
        var mediator = new Mock<IMediator>();
        mediator
            .Setup(m => m.Send(It.IsAny<FinalizeFiscalResultDeclarationCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure(Error.Validation("Fiscal",
                $"Aucune feuille de détermination du résultat fiscal à finaliser pour l'exercice {Year}.")));

        var controller = NewController(mediator);

        var action = await controller.FinalizeFiscalResult(Year, CancellationToken.None);

        var bad = Assert.IsType<BadRequestObjectResult>(action);
        Assert.Equal(StatusCodes.Status400BadRequest, bad.StatusCode);
        var body = Assert.IsType<LiasseResponse>(bad.Value);
        Assert.False(body.Success);
        Assert.Contains("Aucune feuille", body.Error, StringComparison.OrdinalIgnoreCase);
    }
}
