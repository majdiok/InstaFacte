using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.Configuration;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.Accounting.Commands;
using FactuTrust.Application.Features.Accounting.FiscalSchedule;
using FactuTrust.Application.Features.Accounting.Queries;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.ValueObjects;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Application;

/// <summary>
/// SaveVatDeclarationCommandHandler : un brouillon existant est re-enregistrable/soumettable EN PLACE
/// (sans rectificative) ; une déclaration soumise n'accepte qu'une rectificative V2 (sinon Conflict
/// avec message clair) ; la création reste inchangée.
/// </summary>
public sealed class SaveVatDeclarationCommandTests
{
    private const string Tnd = "TND";
    private readonly Mock<IVatDeclarationRepository> _repo = new();
    private readonly Mock<IMediator> _mediator = new();
    private readonly Mock<IAuditService> _audit = new();
    private readonly Mock<ICurrentUser> _currentUser = new();

    public SaveVatDeclarationCommandTests()
    {
        _currentUser.SetupGet(u => u.Email).Returns("comptable@cabinet.tn");
    }

    private static VatDeclarationDto Dto(decimal vatDue = 800m) => new()
    {
        Year = 2026,
        Month = 7,
        CollectedVat19 = 1000m,
        CollectedVat13 = 0m,
        CollectedVat7 = 0m,
        DeductibleVatGoods = 200m,
        DeductibleVatAssets = 0m,
        PreviousCredit = 0m,
        VatDue = vatDue,
        Currency = Tnd
    };

    private static VatDeclaration Draft() => VatDeclaration.CreateDraft(
        2026, 7,
        Money.Create(1000m, Tnd), Money.Zero(Tnd), Money.Zero(Tnd),
        Money.Create(200m, Tnd), Money.Zero(Tnd), Money.Zero(Tnd), Tnd);

    private SaveVatDeclarationCommandHandler BuildHandler(bool v2 = true)
    {
        _mediator
            .Setup(m => m.Send(It.IsAny<GetVatDeclarationQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(Dto()));

        var settings = Options.Create(new AccountingSettings
        {
            MonthlyDeclarationV2Enabled = v2,
            DeclarationScheduleSyncEnabled = false // synchro échéancier no-op (couverte ailleurs)
        });

        var sync = new DeclarationScheduleSynchronizer(
            Mock.Of<IFiscalScheduleRepository>(),
            Mock.Of<ITunisianFiscalDeadlineService>(),
            settings,
            NullLogger<DeclarationScheduleSynchronizer>.Instance);

        return new SaveVatDeclarationCommandHandler(
            _repo.Object, _mediator.Object, _audit.Object, _currentUser.Object,
            settings, sync, NullLogger<SaveVatDeclarationCommandHandler>.Instance);
    }

    private static SaveVatDeclarationCommand Command(bool submit = false, bool rectificative = false) =>
        new(new SaveVatDeclarationRequest { Year = 2026, Month = 7, Submit = submit, IsRectificative = rectificative });

    // ── Création (non-régression) ──────────────────────────────────────────

    [Fact]
    public async Task NoExisting_Submit_CreatesSubmittedDeclaration()
    {
        _repo.Setup(r => r.GetByYearMonthAsync(2026, 7, It.IsAny<CancellationToken>()))
            .ReturnsAsync((VatDeclaration?)null);
        VatDeclaration? added = null;
        _repo.Setup(r => r.AddAsync(It.IsAny<VatDeclaration>(), It.IsAny<CancellationToken>()))
            .Callback<VatDeclaration, CancellationToken>((d, _) => added = d)
            .ReturnsAsync((VatDeclaration d, CancellationToken _) => d);

        var result = await BuildHandler().Handle(Command(submit: true), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(added);
        Assert.Equal(VatDeclarationStatus.Submitted, added!.Status);
        _repo.Verify(r => r.UpdateAsync(It.IsAny<VatDeclaration>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── Brouillon existant : le bug de départ ──────────────────────────────

    [Fact]
    public async Task ExistingDraft_ReSave_Succeeds_UpdatesInPlace_NoRectificative()
    {
        var existing = Draft();
        _repo.Setup(r => r.GetByYearMonthAsync(2026, 7, It.IsAny<CancellationToken>())).ReturnsAsync(existing);

        var result = await BuildHandler().Handle(Command(submit: false), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, existing.RevisionNumber);           // pas de bump
        Assert.False(existing.IsRectificative);
        Assert.Equal(VatDeclarationStatus.Draft, existing.Status);
        _repo.Verify(r => r.UpdateAsync(existing, It.IsAny<CancellationToken>()), Times.Once);
        _repo.Verify(r => r.AddAsync(It.IsAny<VatDeclaration>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExistingDraft_Submit_Succeeds_MarksSubmitted_NoVersionBump()
    {
        var existing = Draft();
        _repo.Setup(r => r.GetByYearMonthAsync(2026, 7, It.IsAny<CancellationToken>())).ReturnsAsync(existing);

        var result = await BuildHandler().Handle(Command(submit: true), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(VatDeclarationStatus.Submitted, existing.Status);
        Assert.Equal(1, existing.RevisionNumber);
        Assert.False(existing.IsRectificative);
    }

    [Fact]
    public async Task ExistingDraft_V1FlagOff_StillSaves()
    {
        var existing = Draft();
        _repo.Setup(r => r.GetByYearMonthAsync(2026, 7, It.IsAny<CancellationToken>())).ReturnsAsync(existing);

        var result = await BuildHandler(v2: false).Handle(Command(submit: true), CancellationToken.None);

        Assert.True(result.IsSuccess); // le !v2 ne bloque plus un brouillon
        Assert.Equal(VatDeclarationStatus.Submitted, existing.Status);
    }

    // ── Déclaration déjà soumise ───────────────────────────────────────────

    [Fact]
    public async Task ExistingSubmitted_ReSubmit_WithoutRectificative_Conflicts()
    {
        var existing = Draft();
        existing.Submit();
        _repo.Setup(r => r.GetByYearMonthAsync(2026, 7, It.IsAny<CancellationToken>())).ReturnsAsync(existing);

        var result = await BuildHandler().Handle(Command(submit: true, rectificative: false), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("déjà soumise", result.Error.Description);
        _repo.Verify(r => r.UpdateAsync(It.IsAny<VatDeclaration>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExistingSubmitted_Rectificative_BumpsVersion_AndResubmits()
    {
        var existing = Draft();
        existing.Submit();
        _repo.Setup(r => r.GetByYearMonthAsync(2026, 7, It.IsAny<CancellationToken>())).ReturnsAsync(existing);

        var result = await BuildHandler().Handle(Command(submit: true, rectificative: true), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, existing.RevisionNumber);
        Assert.True(existing.IsRectificative);
        Assert.Equal(VatDeclarationStatus.Submitted, existing.Status); // ApplyRevision → Draft puis Submit
        _repo.Verify(r => r.UpdateAsync(existing, It.IsAny<CancellationToken>()), Times.Once);
    }
}
