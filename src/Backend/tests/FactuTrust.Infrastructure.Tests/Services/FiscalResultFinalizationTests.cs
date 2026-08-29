using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.Configuration;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.Accounting.Fiscal;
using FactuTrust.Domain.Authorization;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities.Fiscal;
using FactuTrust.Domain.Enums;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Services;

/// <summary>
/// T10 — finalisation atomique de la liasse fiscale : écriture d'impôt 691/6912/4343, alignement
/// transactionnel de la feuille (résultat net après impôt + ligne R-IS), réconciliation en deux
/// passes, porte de concurrence (FinalizeAsync conditionnel) et blocages Option 2 (FIFO/différés).
/// L'unité de travail est factice (passthrough) : on valide l'orchestration du handler sans SQL Server.
/// </summary>
public sealed class FiscalResultFinalizationTests
{
    private const int Year = 2026;

    private static IncomeTaxYearParameter Params() => IncomeTaxYearParameterDefaults.Create(Year);

    /// <summary>Construit une feuille brouillon avec un résultat comptable et des acomptes.</summary>
    private static FiscalResultDeclaration DraftDeclaration(decimal accountingResult, decimal acomptes = 0m)
    {
        var d = FiscalResultDeclaration.Create(Year, TaxpayerKind.CorporateIS);
        d.UpdateInputs(TaxpayerKind.CorporateIS, accountingResult, 0.15m, 0m, acomptes, 0m, 0m);
        return d;
    }

    private static FiscalCarryForwardItem Deficit(int originYear, decimal initial, decimal imputed, int? expiry = null) =>
        FiscalCarryForwardItem.Create(FiscalCarryForwardKind.Deficit, originYear, initial, imputed, expiry);

    /// <summary>État de résultat NCT factice : ResultBeforeTax (avant impôt) et NetResult (après impôt).</summary>
    private static NctFinancialStatementsDto Nct(decimal resultBeforeTax, decimal netResult) => new()
    {
        FiscalYear = Year,
        IncomeStatement = new NctIncomeStatementDto
        {
            ResultBeforeTax = resultBeforeTax,
            NetResult = netResult
        }
    };

    /// <summary>
    /// Montage complet du handler de finalisation avec toutes les dépendances factices.
    /// <paramref name="taxEntryResult"/> contrôle le succès/échec de l'écriture d'impôt ;
    /// <paramref name="finalizeOutcome"/> contrôle la porte de concurrence.
    /// </summary>
    private sealed class Harness
    {
        public Mock<IFiscalResultDeclarationRepository> Declarations { get; } = new();
        public Mock<IIncomeTaxYearParameterRepository> Parameters { get; } = new();
        public Mock<IAccountingService> Accounting { get; } = new();
        public Mock<IAccountingReportingService> Reporting { get; } = new();
        public Mock<IFiscalYearLockService> Locks { get; } = new();
        public Mock<ITenantUnitOfWork> Uow { get; } = new();
        public Mock<ICurrentUser> User { get; } = new();

        public int TaxYear { get; set; }
        public decimal TaxDue { get; set; }
        public decimal CssDue { get; set; }
        public Guid DeclarationId { get; set; }
        public FiscalResultDeclaration? Upserted { get; private set; }
        public List<FiscalAdjustmentLine> AlignedAdjustments { get; } = new();

        public FinalizeFiscalResultDeclarationCommandHandler BuildHandler()
        {
            Parameters.Setup(x => x.GetOrDefaultAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Params());

            // UoW passthrough : exécute l'action inline (pas de transaction réelle).
            Uow.Setup(x => x.ExecuteAsync(It.IsAny<Func<CancellationToken, Task<Result>>>(), It.IsAny<CancellationToken>()))
                .Returns((Func<CancellationToken, Task<Result>> action, CancellationToken ct) => action(ct));

            Locks.Setup(x => x.GetLocksAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result.Success<IReadOnlyList<FiscalYearLockDto>>(Array.Empty<FiscalYearLockDto>()));

            User.SetupGet(u => u.IsAccountingFirmDelegatedContext).Returns(true);
            User.SetupGet(u => u.Email).Returns("expert@cabinet.tn");

            // Écriture d'impôt : capture les montants et réussit (rend un Guid).
            Accounting.Setup(x => x.GenerateFiscalTaxEntryAsync(
                    It.IsAny<int>(), It.IsAny<decimal>(), It.IsAny<decimal>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .Returns((int year, decimal tax, decimal css, Guid declId, CancellationToken _) =>
                {
                    TaxYear = year; TaxDue = tax; CssDue = css; DeclarationId = declId;
                    return Task.FromResult(Result.Success(Guid.NewGuid()));
                });

            // Upsert : renvoie l'entité alignée (AccountingResult + ajustements passés).
            Declarations.Setup(x => x.UpsertAsync(It.IsAny<FiscalDeclarationUpsert>(), It.IsAny<CancellationToken>()))
                .Returns((FiscalDeclarationUpsert u, CancellationToken _) =>
                {
                    var d = FiscalResultDeclaration.Create(Year, u.TaxpayerKind);
                    d.UpdateInputs(u.TaxpayerKind, u.AccountingResult, u.AppliedIsRate, u.LocalTurnoverTtc,
                        u.AcomptesPaid, u.WithholdingSuffered, u.PriorTaxCredit, u.MinimumTaxRegime);
                    d.ReplaceAdjustments(u.Adjustments);
                    d.ReplaceCarryForwards(u.CarryForwards);
                    Upserted = d;
                    AlignedAdjustments.AddRange(u.Adjustments);
                    return Task.FromResult(d);
                });

            var settings = Options.Create(new AccountingSettings { FiscalLiasseEnabled = true });
            return new FinalizeFiscalResultDeclarationCommandHandler(
                Declarations.Object, Parameters.Object, Accounting.Object, Reporting.Object,
                Locks.Object, Uow.Object, User.Object, settings);
        }
    }

    // ── Cas nominal : écriture + alignement + finalisation ──────────────────────────────────

    [Fact]
    public async Task Finalize_BooksAtTenThousand_PostsTaxEntry_AndAlignsSheetToNetAfterTax()
    {
        // Livres à 10 000 avant impôt. La feuille brouillon porte AccountingResult = 10 000 (avant impôt,
        // à aligner). IS 15 % × 10 000 = 1 500 ; CSS 1 % × 10 000 = 100 ; impôt total = 1 600.
        // Après l'écriture d'impôt, le résultat net NCT = 10 000 − 1 600 = 8 400.
        var declaration = DraftDeclaration(accountingResult: 10_000m);
        var harness = new Harness();
        harness.Declarations.Setup(x => x.GetByYearAsync(Year, It.IsAny<CancellationToken>()))
            .ReturnsAsync(declaration);
        harness.Reporting.Setup(x => x.GetNctStatementsAsync(Year, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(Nct(resultBeforeTax: 10_000m, netResult: 8_400m)));
        harness.Declarations.Setup(x => x.FinalizeAsync(Year, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(FiscalFinalizeOutcome.Finalized);
        var handler = harness.BuildHandler();

        var result = await handler.Handle(new FinalizeFiscalResultDeclarationCommand(Year), CancellationToken.None);

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Description : null);

        // 6. Écriture d'impôt : 691 = 1 500, 6912 = 100, 4343 = 1 600 (montants autoritaires serveur).
        Assert.Equal(Year, harness.TaxYear);
        Assert.Equal(1_500m, harness.TaxDue);
        Assert.Equal(100m, harness.CssDue);
        Assert.Equal(declaration.Id, harness.DeclarationId);

        // 8. Alignement : AccountingResult = 8 400 (net après impôt) + ligne R-IS = 1 600.
        Assert.NotNull(harness.Upserted);
        Assert.Equal(8_400m, harness.Upserted!.AccountingResult);
        var ris = Assert.Single(harness.AlignedAdjustments,
            a => a.CatalogCode == FiscalFinalizationCatalogCodes.IncomeTaxReintegration);
        Assert.Equal(1_600m, ris.Amount); // 1 500 (IS) + 100 (CSS) = charge de classe 69 totale
        Assert.True(ris.IsAutoSuggested);

        // 10. Finalisation conditionnelle appelée.
        harness.Declarations.Verify(
            x => x.FinalizeAsync(Year, It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── Invariance en deux passes : base imposable et impôt inchangés ──────────────────────

    [Fact]
    public async Task Finalize_TwoPassInvariance_TaxableResultAndTaxDueUnchanged()
    {
        // La feuille n'a pas de réintégration manuelle : le passage R-IS (réintégration de l'IS) compense
        // exactement la baisse du résultat comptable (net après impôt) → base imposable inchangée.
        var declaration = DraftDeclaration(accountingResult: 10_000m);
        var harness = new Harness();
        harness.Declarations.Setup(x => x.GetByYearAsync(Year, It.IsAny<CancellationToken>()))
            .ReturnsAsync(declaration);
        harness.Reporting.Setup(x => x.GetNctStatementsAsync(Year, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(Nct(resultBeforeTax: 10_000m, netResult: 8_400m)));
        harness.Declarations.Setup(x => x.FinalizeAsync(Year, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(FiscalFinalizeOutcome.Finalized);
        var handler = harness.BuildHandler();

        var result = await handler.Handle(new FinalizeFiscalResultDeclarationCommand(Year), CancellationToken.None);

        // La réconciliation en deux passes passe : pas d'écart (409 réconciliation).
        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Description : null);
        // Base imposable recalculée sur la feuille alignée : (8 400 net) + (1 600 R-IS) = 10 000.
        Assert.Equal(10_000m, harness.Upserted!.AccountingResult + harness.AlignedAdjustments
            .Where(a => a.CatalogCode == FiscalFinalizationCatalogCodes.IncomeTaxReintegration)
            .Sum(a => a.Amount));
    }

    [Fact]
    public async Task Finalize_InvarianceViolation_ReturnsConflict_RollsBack()
    {
        // Cas pathologique : le résultat comptable saisi sur la feuille (10 000) ne correspond PAS au
        // résultat avant impôt des livres (12 000). La feuille ignore donc une charge/différence de 2 000.
        // Passe 1 (sur la feuille brute) : base = 10 000. Passe 2 (après alignement sur les livres) :
        // AccountingResult → net 8 000, R-IS → charge 69 = 12 000 − 8 000 = 4 000, base = 8 000 + 4 000 = 12 000.
        // |12 000 − 10 000| = 2 000 ≥ tolérance → écart de réconciliation → 409 + rollback intégral.
        var declaration = DraftDeclaration(accountingResult: 10_000m);

        var harness = new Harness();
        harness.Declarations.Setup(x => x.GetByYearAsync(Year, It.IsAny<CancellationToken>()))
            .ReturnsAsync(declaration);
        // Les livres : résultat avant impôt 12 000, net après impôt 8 000 (charge 69 = 4 000).
        // 12 000 ≠ 10 000 (feuille) → l'alignement déplace la base imposable (passe 1 ≠ passe 2).
        harness.Reporting.Setup(x => x.GetNctStatementsAsync(Year, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(Nct(resultBeforeTax: 12_000m, netResult: 8_000m)));
        harness.Declarations.Setup(x => x.FinalizeAsync(Year, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(FiscalFinalizeOutcome.Finalized);
        var handler = harness.BuildHandler();

        var result = await handler.Handle(new FinalizeFiscalResultDeclarationCommand(Year), CancellationToken.None);

        // Écart de réconciliation → 409 Conflict, rollback (la finalisation ne doit pas être appelée).
        Assert.True(result.IsFailure);
        Assert.Equal("Conflict", result.Error.Code);
        harness.Declarations.Verify(
            x => x.FinalizeAsync(Year, It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── Porte de concurrence : feuille déjà finalisée par un autre → 409 ───────────────────

    [Fact]
    public async Task Finalize_AlreadyFinalizedByConcurrent_ReturnsConflict()
    {
        var declaration = DraftDeclaration(accountingResult: 10_000m);
        var harness = new Harness();
        harness.Declarations.Setup(x => x.GetByYearAsync(Year, It.IsAny<CancellationToken>()))
            .ReturnsAsync(declaration);
        harness.Reporting.Setup(x => x.GetNctStatementsAsync(Year, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(Nct(resultBeforeTax: 10_000m, netResult: 8_400m)));
        // La porte de concurrence : un autre a finalisé entre l'alignement et la finalisation.
        harness.Declarations.Setup(x => x.FinalizeAsync(Year, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(FiscalFinalizeOutcome.AlreadyFinalized);
        var handler = harness.BuildHandler();

        var result = await handler.Handle(new FinalizeFiscalResultDeclarationCommand(Year), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Conflict", result.Error.Code);
    }

    [Fact]
    public async Task Finalize_NoSheet_ReturnsValidation()
    {
        var harness = new Harness();
        harness.Declarations.Setup(x => x.GetByYearAsync(Year, It.IsAny<CancellationToken>()))
            .ReturnsAsync((FiscalResultDeclaration?)null);
        var handler = harness.BuildHandler();

        var result = await handler.Handle(new FinalizeFiscalResultDeclarationCommand(Year), CancellationToken.None);

        Assert.True(result.IsFailure);
        // Absente → 400 (Validation), pas 409.
        Assert.NotEqual("Conflict", result.Error.Code);
        harness.Accounting.Verify(
            x => x.GenerateFiscalTaxEntryAsync(
                It.IsAny<int>(), It.IsAny<decimal>(), It.IsAny<decimal>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Finalize_AlreadyFinalizedSheet_ReturnsConflict()
    {
        var declaration = DraftDeclaration(accountingResult: 10_000m);
        declaration.Finalize("someone@cabinet.tn");
        var harness = new Harness();
        harness.Declarations.Setup(x => x.GetByYearAsync(Year, It.IsAny<CancellationToken>()))
            .ReturnsAsync(declaration);
        var handler = harness.BuildHandler();

        var result = await handler.Handle(new FinalizeFiscalResultDeclarationCommand(Year), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Conflict", result.Error.Code);
        // Aucune écriture d'impôt (court-circuit avant l'écriture).
        harness.Accounting.Verify(
            x => x.GenerateFiscalTaxEntryAsync(
                It.IsAny<int>(), It.IsAny<decimal>(), It.IsAny<decimal>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ── Exercice verrouillé → 409 ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Finalize_LockedYear_ReturnsConflict()
    {
        var declaration = DraftDeclaration(accountingResult: 10_000m);
        var harness = new Harness();
        harness.Declarations.Setup(x => x.GetByYearAsync(Year, It.IsAny<CancellationToken>()))
            .ReturnsAsync(declaration);
        var handler = harness.BuildHandler();
        // Override APRÈS BuildHandler (Moq : le dernier Setup gagne ; BuildHandler force un verrou vide).
        harness.Locks.Setup(x => x.GetLocksAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success<IReadOnlyList<FiscalYearLockDto>>(
                new[] { new FiscalYearLockDto { FiscalYear = Year } }));

        var result = await handler.Handle(new FinalizeFiscalResultDeclarationCommand(Year), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Conflict", result.Error.Code);
        harness.Accounting.Verify(
            x => x.GenerateFiscalTaxEntryAsync(
                It.IsAny<int>(), It.IsAny<decimal>(), It.IsAny<decimal>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ── Blocages Option 2 à la finalisation (art. 8 code IRPP/IS) ────────────────────────────

    [Fact]
    public async Task Finalize_ExpiredDeficitImputed_ReturnsValidation()
    {
        // Déficit 2018, report 5 ans → prescrit en 2024, imputé sur 2026 → blocage.
        var declaration = DraftDeclaration(accountingResult: 50_000m);
        declaration.ReplaceCarryForwards(new[] { Deficit(2018, 10_000m, 10_000m, expiry: 2023) });

        var harness = new Harness();
        harness.Declarations.Setup(x => x.GetByYearAsync(Year, It.IsAny<CancellationToken>()))
            .ReturnsAsync(declaration);
        var handler = harness.BuildHandler();

        var result = await handler.Handle(new FinalizeFiscalResultDeclarationCommand(Year), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.NotEqual("Conflict", result.Error.Code); // Validation (400), pas 409
        harness.Accounting.Verify(
            x => x.GenerateFiscalTaxEntryAsync(
                It.IsAny<int>(), It.IsAny<decimal>(), It.IsAny<decimal>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Finalize_NonFifoOrder_ReturnsValidation()
    {
        // Déficit 2020 (stock 10 000) non imputé + déficit 2023 (stock 5 000) imputé → non-FIFO.
        // Le déficit 2020 doit être NON prescrit (échéance 2030 ≥ 2026) pour que le contrôle FIFO
        // s'applique : un déficit périmé est ignoré par l'ordre d'imputation.
        var declaration = DraftDeclaration(accountingResult: 50_000m);
        declaration.ReplaceCarryForwards(new[]
        {
            Deficit(2020, 10_000m, 0m, expiry: 2030),       // plus ancien, non imputé, encore imputable
            Deficit(2023, 5_000m, 5_000m, expiry: 2028)      // plus récent, imputé → FIFO violé
        });

        var harness = new Harness();
        harness.Declarations.Setup(x => x.GetByYearAsync(Year, It.IsAny<CancellationToken>()))
            .ReturnsAsync(declaration);
        var handler = harness.BuildHandler();

        var result = await handler.Handle(new FinalizeFiscalResultDeclarationCommand(Year), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.NotEqual("Conflict", result.Error.Code);
        harness.Accounting.Verify(
            x => x.GenerateFiscalTaxEntryAsync(
                It.IsAny<int>(), It.IsAny<decimal>(), It.IsAny<decimal>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Finalize_DeferredBeforeOrdinaryDeficit_ReturnsValidation()
    {
        // Déficit ordinaire 2024 (stock 8 000) non imputé + amortissement différé imputé → blocage.
        var declaration = DraftDeclaration(accountingResult: 50_000m);
        declaration.ReplaceCarryForwards(new[]
        {
            Deficit(2024, 8_000m, 0m, expiry: 2029),
            FiscalCarryForwardItem.Create(FiscalCarryForwardKind.DeferredDepreciation, 2022, 6_000m, 6_000m, null)
        });

        var harness = new Harness();
        harness.Declarations.Setup(x => x.GetByYearAsync(Year, It.IsAny<CancellationToken>()))
            .ReturnsAsync(declaration);
        var handler = harness.BuildHandler();

        var result = await handler.Handle(new FinalizeFiscalResultDeclarationCommand(Year), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.NotEqual("Conflict", result.Error.Code);
        harness.Accounting.Verify(
            x => x.GenerateFiscalTaxEntryAsync(
                It.IsAny<int>(), It.IsAny<decimal>(), It.IsAny<decimal>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ── Défense en profondeur : cabinet non délégué → 403 ───────────────────────────────────

    [Fact]
    public async Task Finalize_NotDelegatedContext_ReturnsForbidden()
    {
        var harness = new Harness();
        var handler = harness.BuildHandler();
        // Override APRÈS BuildHandler (Moq : le dernier Setup gagne ; BuildHandler force true).
        harness.User.SetupGet(u => u.IsAccountingFirmDelegatedContext).Returns(false);

        var result = await handler.Handle(new FinalizeFiscalResultDeclarationCommand(Year), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Forbidden", result.Error.Code);
    }

    // ── Idempotence de l'écriture : rejeu sûr (l'écriture existe déjà) ─────────────────────

    [Fact]
    public async Task Finalize_TaxEntryIdempotent_DoesNotBlockFinalization()
    {
        var declaration = DraftDeclaration(accountingResult: 10_000m);
        var harness = new Harness();
        harness.Declarations.Setup(x => x.GetByYearAsync(Year, It.IsAny<CancellationToken>()))
            .ReturnsAsync(declaration);
        harness.Reporting.Setup(x => x.GetNctStatementsAsync(Year, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(Nct(resultBeforeTax: 10_000m, netResult: 8_400m)));
        harness.Declarations.Setup(x => x.FinalizeAsync(Year, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(FiscalFinalizeOutcome.Finalized);
        // L'écriture d'impôt existe déjà (rejeu) : l'IAccountingService factice rend quand même un Guid
        // (comme le ferait le vrai GenerateFiscalTaxEntryAsync via GetBySourceAsync).
        var handler = harness.BuildHandler();

        var result = await handler.Handle(new FinalizeFiscalResultDeclarationCommand(Year), CancellationToken.None);

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Description : null);
    }
}
