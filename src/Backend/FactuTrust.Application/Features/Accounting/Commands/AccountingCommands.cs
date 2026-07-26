using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.Configuration;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.Accounting.FiscalSchedule;
using FactuTrust.Application.Features.Accounting.Queries;
using FactuTrust.Domain.Authorization;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.ValueObjects;
using AuditActions = FactuTrust.Domain.Entities.AuditActions;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FactuTrust.Application.Features.Accounting.Commands;

public sealed record CreateSubAccountCommand(CreateSubAccountRequest Request) : IRequest<Result<Guid>>;

public sealed class CreateSubAccountCommandHandler : IRequestHandler<CreateSubAccountCommand, Result<Guid>>
{
    private readonly IChartOfAccountRepository _chartOfAccounts;
    private readonly IAuditService _auditService;
    private readonly ICurrentUser _currentUser;

    public CreateSubAccountCommandHandler(
        IChartOfAccountRepository chartOfAccounts,
        IAuditService auditService,
        ICurrentUser currentUser)
    {
        _chartOfAccounts = chartOfAccounts;
        _auditService = auditService;
        _currentUser = currentUser;
    }

    public async Task<Result<Guid>> Handle(CreateSubAccountCommand request, CancellationToken cancellationToken)
    {
        var r = request.Request;
        var nature = (AccountNatureType)r.NatureType;
        var create = ChartOfAccount.Create(
            r.AccountNumber,
            r.Label,
            r.AccountClass,
            r.ParentAccountNumber,
            nature,
            isSystem: false,
            accountType: (AccountType)r.AccountType,
            isAuxiliary: r.IsAuxiliary,
            affectationAccountNumber: r.AffectationAccountNumber);
        if (create.IsFailure)
            return Result.Failure<Guid>(create.Error);

        var entity = create.Value;
        entity.SetAuditInfo(_currentUser.Email ?? "system", false);
        await _chartOfAccounts.AddAsync(entity, cancellationToken);

        await _auditService.LogAsync(
            AuditActions.Accounting.SubAccountCreated,
            "ChartOfAccount",
            entity.Id,
            newValues: new { r.AccountNumber, r.Label, r.AccountClass },
            cancellationToken: cancellationToken);

        return Result.Success(entity.Id);
    }
}

public sealed record CreateManualJournalEntryCommand(CreateManualJournalEntryRequest Request) : IRequest<Result<Guid>>;

public sealed class CreateManualJournalEntryCommandHandler : IRequestHandler<CreateManualJournalEntryCommand, Result<Guid>>
{
    private readonly IAccountingPeriodService _periodService;
    private readonly IJournalEntryRepository _journalEntries;
    private readonly IChartOfAccountRepository _chartOfAccounts;
    private readonly IAuditService _auditService;
    private readonly ICurrentUser _currentUser;
    private readonly AccountingSettings _settings;

    public CreateManualJournalEntryCommandHandler(
        IAccountingPeriodService periodService,
        IJournalEntryRepository journalEntries,
        IChartOfAccountRepository chartOfAccounts,
        IAuditService auditService,
        ICurrentUser currentUser,
        IOptions<AccountingSettings> settings)
    {
        _periodService = periodService;
        _journalEntries = journalEntries;
        _chartOfAccounts = chartOfAccounts;
        _auditService = auditService;
        _currentUser = currentUser;
        _settings = settings.Value;
    }

    public async Task<Result<Guid>> Handle(CreateManualJournalEntryCommand request, CancellationToken cancellationToken)
    {
        var r = request.Request;

        // Validate that all account numbers exist in the chart of accounts
        var checkedAccounts = new HashSet<string>(StringComparer.Ordinal);
        foreach (var line in r.Lines)
        {
            var acc = line.AccountNumber?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(acc) || !checkedAccounts.Add(acc))
                continue;

            var account = await _chartOfAccounts.GetByAccountNumberAsync(acc, cancellationToken);
            if (account is null)
                return Result.Failure<Guid>(Error.Validation("AccountNumber",
                    $"Le compte {acc} n'existe pas dans le plan comptable."));

            if (!account.IsActive)
                return Result.Failure<Guid>(Error.Validation("AccountNumber",
                    $"Le compte {acc} est désactivé."));
        }

        var period = await _periodService.EnsureOpenPeriodAsync(r.EntryDate, cancellationToken);
        if (period.IsFailure)
            return Result.Failure<Guid>(period.Error);

        var mapped = ManualJournalLineMapper.Map(r.Lines);
        if (mapped.IsFailure)
            return Result.Failure<Guid>(mapped.Error);
        var lines = mapped.Value;

        var n = await _journalEntries.ReserveNextEntryNumberAsync(r.JournalCode, r.EntryDate.Year, cancellationToken);
        var create = JournalEntry.Create(
            n,
            r.JournalCode,
            r.EntryDate,
            r.Label,
            period.Value.Id,
            false,
            "Manual",
            null,
            lines,
            pieceRef: r.PieceRef,
            pieceDate: r.PieceDate);

        if (create.IsFailure)
            return Result.Failure<Guid>(create.Error);

        var entry = create.Value;
        entry.MarkInitialStatus(_settings.BrouillardEnabled ? JournalEntryStatus.Brouillon : JournalEntryStatus.Validee);
        entry.SetAuditInfo(_currentUser.Email ?? "system", false);
        await _journalEntries.AddAsync(entry, cancellationToken);

        await _auditService.LogAsync(
            AuditActions.Accounting.ManualEntryCreated,
            "JournalEntry",
            entry.Id,
            newValues: new { r.JournalCode, r.EntryDate, r.Label, LineCount = r.Lines.Count },
            cancellationToken: cancellationToken);

        return Result.Success(entry.Id);
    }
}

public sealed record UpdateAccountLabelCommand(Guid Id, string Label) : IRequest<Result>;

public sealed class UpdateAccountLabelCommandHandler : IRequestHandler<UpdateAccountLabelCommand, Result>
{
    private readonly IChartOfAccountRepository _chartOfAccounts;
    private readonly IAuditService _auditService;

    public UpdateAccountLabelCommandHandler(IChartOfAccountRepository chartOfAccounts, IAuditService auditService)
    {
        _chartOfAccounts = chartOfAccounts;
        _auditService = auditService;
    }

    public async Task<Result> Handle(UpdateAccountLabelCommand request, CancellationToken cancellationToken)
    {
        var account = await _chartOfAccounts.GetByIdAsync(request.Id, cancellationToken);
        if (account is null)
            return Result.Failure(Error.NotFound("ChartOfAccount", request.Id));

        var oldLabel = account.Label;
        var result = account.UpdateLabel(request.Label);
        if (result.IsFailure)
            return result;

        await _chartOfAccounts.UpdateAsync(account, cancellationToken);

        await _auditService.LogAsync(
            AuditActions.Accounting.AccountLabelUpdated,
            "ChartOfAccount",
            account.Id,
            oldValues: new { Label = oldLabel },
            newValues: new { account.AccountNumber, Label = request.Label },
            cancellationToken: cancellationToken);

        return Result.Success();
    }
}

public sealed record ToggleAccountActiveCommand(Guid Id) : IRequest<Result>;

public sealed class ToggleAccountActiveCommandHandler : IRequestHandler<ToggleAccountActiveCommand, Result>
{
    private readonly IChartOfAccountRepository _chartOfAccounts;
    private readonly IAuditService _auditService;

    public ToggleAccountActiveCommandHandler(IChartOfAccountRepository chartOfAccounts, IAuditService auditService)
    {
        _chartOfAccounts = chartOfAccounts;
        _auditService = auditService;
    }

    public async Task<Result> Handle(ToggleAccountActiveCommand request, CancellationToken cancellationToken)
    {
        var account = await _chartOfAccounts.GetByIdAsync(request.Id, cancellationToken);
        if (account is null)
            return Result.Failure(Error.NotFound("ChartOfAccount", request.Id));

        if (account.IsSystem)
            return Result.Failure(Error.Validation("ChartOfAccount", "Impossible de modifier un compte système."));

        var wasActive = account.IsActive;
        account.ToggleActive();
        await _chartOfAccounts.UpdateAsync(account, cancellationToken);

        await _auditService.LogAsync(
            AuditActions.Accounting.AccountToggled,
            "ChartOfAccount",
            account.Id,
            oldValues: new { IsActive = wasActive },
            newValues: new { account.AccountNumber, account.IsActive },
            cancellationToken: cancellationToken);

        return Result.Success();
    }
}

public sealed record ClosePeriodCommand(Guid PeriodId) : IRequest<Result>;

public sealed class ClosePeriodCommandHandler : IRequestHandler<ClosePeriodCommand, Result>
{
    private readonly IAccountingPeriodRepository _periods;
    private readonly IAccountingPeriodService _periodService;
    private readonly IAuditService _auditService;
    private readonly ICurrentUser _currentUser;

    public ClosePeriodCommandHandler(
        IAccountingPeriodRepository periods,
        IAccountingPeriodService periodService,
        IAuditService auditService,
        ICurrentUser currentUser)
    {
        _periods = periods;
        _periodService = periodService;
        _auditService = auditService;
        _currentUser = currentUser;
    }

    public async Task<Result> Handle(ClosePeriodCommand request, CancellationToken cancellationToken)
    {
        var closedBy = _currentUser.Email ?? "system";

        // Use serializable transaction to prevent concurrent entry creation during close
        var result = await _periodService.ClosePeriodWithLockAsync(request.PeriodId, closedBy, cancellationToken);
        if (result.IsFailure)
            return result;

        await _auditService.LogAsync(
            AuditActions.Accounting.PeriodClosed,
            "AccountingPeriod",
            request.PeriodId,
            newValues: new { ClosedBy = closedBy },
            cancellationToken: cancellationToken);

        return Result.Success();
    }
}

public sealed record ReopenPeriodCommand(Guid PeriodId) : IRequest<Result>;

public sealed class ReopenPeriodCommandHandler : IRequestHandler<ReopenPeriodCommand, Result>
{
    private readonly IAccountingPeriodService _periodService;
    private readonly IAuditService _auditService;

    public ReopenPeriodCommandHandler(IAccountingPeriodService periodService, IAuditService auditService)
    {
        _periodService = periodService;
        _auditService = auditService;
    }

    public async Task<Result> Handle(ReopenPeriodCommand request, CancellationToken cancellationToken)
    {
        // La réouverture rebascule aussi les écritures Cloturee → Validee (symétrie de la clôture).
        var result = await _periodService.ReopenPeriodAsync(request.PeriodId, cancellationToken);
        if (result.IsFailure)
            return Result.Failure(result.Error);

        var p = result.Value;
        await _auditService.LogAsync(
            AuditActions.Accounting.PeriodReopened,
            "AccountingPeriod",
            p.Id,
            newValues: new { p.FiscalYear, p.Month },
            cancellationToken: cancellationToken);

        return Result.Success();
    }
}

public sealed record SaveVatDeclarationCommand(SaveVatDeclarationRequest Request) : IRequest<Result<Guid>>;

public sealed class SaveVatDeclarationCommandHandler : IRequestHandler<SaveVatDeclarationCommand, Result<Guid>>
{
    private readonly IVatDeclarationRepository _vatDecl;
    private readonly IMediator _mediator;
    private readonly IAuditService _auditService;
    private readonly ICurrentUser _currentUser;
    private readonly AccountingSettings _settings;
    private readonly DeclarationScheduleSynchronizer _scheduleSync;
    private readonly ILogger<SaveVatDeclarationCommandHandler> _logger;

    public SaveVatDeclarationCommandHandler(
        IVatDeclarationRepository vatDecl,
        IMediator mediator,
        IAuditService auditService,
        ICurrentUser currentUser,
        IOptions<AccountingSettings> settings,
        DeclarationScheduleSynchronizer scheduleSync,
        ILogger<SaveVatDeclarationCommandHandler> logger)
    {
        _vatDecl = vatDecl;
        _mediator = mediator;
        _auditService = auditService;
        _currentUser = currentUser;
        _settings = settings.Value;
        _scheduleSync = scheduleSync;
        _logger = logger;
    }

    /// <summary>
    /// Total à payer reflétant les valeurs EFFECTIVEMENT sauvegardées (les taxes de la requête,
    /// pas celles du DTO recalculé qui lit l'état antérieur) — même formule que la query V2.
    /// </summary>
    private decimal ComputeSavedTotalToPay(VatDeclarationDto dto, SaveVatDeclarationRequest r) =>
        _settings.MonthlyDeclarationV2Enabled
            ? Math.Max(0m, dto.VatDue + r.Fodec + r.DroitTimbre + r.Tcl + r.Tfp + r.Foprolos + r.WithholdingTax - r.Acomptes)
            : dto.VatDue;

    /// <summary>Synchro échéancier best-effort : ne fait JAMAIS échouer la sauvegarde.</summary>
    private async Task TrySyncScheduleAsync(VatDeclaration declaration, decimal totalToPay, CancellationToken ct)
    {
        try
        {
            await _scheduleSync.SyncAsync(declaration, totalToPay, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Declaration→schedule sync failed for {Year}-{Month}",
                declaration.Year, declaration.Month);
        }
    }

    public async Task<Result<Guid>> Handle(SaveVatDeclarationCommand request, CancellationToken cancellationToken)
    {
        var r = request.Request;
        var computed = await _mediator.Send(new GetVatDeclarationQuery(r.Year, r.Month), cancellationToken);
        if (computed.IsFailure)
            return Result.Failure<Guid>(computed.Error);

        var dto = computed.Value;
        var currency = dto.Currency;
        var v2 = _settings.MonthlyDeclarationV2Enabled;
        var user = _currentUser.Email ?? "system";

        var existing = await _vatDecl.GetByYearMonthAsync(r.Year, r.Month, cancellationToken);
        if (existing is not null)
        {
            // Brouillon existant : mise à jour EN PLACE (TVA recalculée depuis les écritures) puis
            // soumission éventuelle — ce n'est PAS une rectificative (RevisionNumber inchangé).
            if (existing.Status == VatDeclarationStatus.Draft)
            {
                var upd = existing.UpdateDraft(
                    Money.Create(dto.CollectedVat19, currency),
                    Money.Create(dto.CollectedVat13, currency),
                    Money.Create(dto.CollectedVat7, currency),
                    Money.Create(dto.DeductibleVatGoods, currency),
                    Money.Create(dto.DeductibleVatAssets, currency),
                    Money.Create(dto.PreviousCredit, currency));
                if (upd.IsFailure)
                    return Result.Failure<Guid>(upd.Error);

                if (v2)
                    existing.SetAdditionalTaxes(r.Fodec, r.DroitTimbre, r.Tcl, r.Tfp, r.Foprolos, r.WithholdingTax, r.Acomptes);
                if (r.Submit)
                    existing.Submit();
                existing.SetAuditInfo(user, isUpdate: true);
                await _vatDecl.UpdateAsync(existing, cancellationToken);

                await _auditService.LogAsync(
                    r.Submit ? AuditActions.Accounting.VatDeclarationSubmitted : AuditActions.Accounting.VatDeclarationSaved,
                    "VatDeclaration",
                    existing.Id,
                    newValues: new { r.Year, r.Month, dto.VatDue, r.Submit },
                    cancellationToken: cancellationToken);

                await TrySyncScheduleAsync(existing, ComputeSavedTotalToPay(dto, r), cancellationToken);

                return Result.Success(existing.Id);
            }

            // Déclaration déjà soumise / verrouillée : seule une rectificative (V2) est autorisée.
            if (!v2 || !r.IsRectificative)
                return Result.Failure<Guid>(Error.Conflict(
                    "La déclaration de cette période est déjà soumise. Utilisez « Rectificative » pour la corriger."));

            existing.ApplyRevision(
                Money.Create(dto.CollectedVat19, currency),
                Money.Create(dto.CollectedVat13, currency),
                Money.Create(dto.CollectedVat7, currency),
                Money.Create(dto.DeductibleVatGoods, currency),
                Money.Create(dto.DeductibleVatAssets, currency),
                Money.Create(dto.PreviousCredit, currency),
                r.Fodec, r.DroitTimbre, r.Tcl, r.Tfp, r.Foprolos, r.WithholdingTax, r.Acomptes);
            if (r.Submit)
                existing.Submit();
            existing.SetAuditInfo(user, isUpdate: true);
            await _vatDecl.UpdateAsync(existing, cancellationToken);

            await _auditService.LogAsync(
                AuditActions.Accounting.VatDeclarationRectified,
                "VatDeclaration",
                existing.Id,
                newValues: new { r.Year, r.Month, existing.RevisionNumber, dto.VatDue, r.Submit },
                cancellationToken: cancellationToken);

            await TrySyncScheduleAsync(existing, ComputeSavedTotalToPay(dto, r), cancellationToken);

            return Result.Success(existing.Id);
        }

        var draft = VatDeclaration.CreateDraft(
            r.Year,
            r.Month,
            Money.Create(dto.CollectedVat19, currency),
            Money.Create(dto.CollectedVat13, currency),
            Money.Create(dto.CollectedVat7, currency),
            Money.Create(dto.DeductibleVatGoods, currency),
            Money.Create(dto.DeductibleVatAssets, currency),
            Money.Create(dto.PreviousCredit, currency),
            currency);

        if (v2)
            draft.SetAdditionalTaxes(r.Fodec, r.DroitTimbre, r.Tcl, r.Tfp, r.Foprolos, r.WithholdingTax, r.Acomptes);

        draft.SetAuditInfo(user, false);
        if (r.Submit)
            draft.Submit();

        await _vatDecl.AddAsync(draft, cancellationToken);

        await _auditService.LogAsync(
            r.Submit ? AuditActions.Accounting.VatDeclarationSubmitted : AuditActions.Accounting.VatDeclarationSaved,
            "VatDeclaration",
            draft.Id,
            newValues: new { r.Year, r.Month, dto.VatDue, r.Submit },
            cancellationToken: cancellationToken);

        await TrySyncScheduleAsync(draft, ComputeSavedTotalToPay(dto, r), cancellationToken);

        return Result.Success(draft.Id);
    }
}

public sealed record LetterAccountEntriesCommand(IReadOnlyList<Guid> JournalEntryLineIds, bool AllowPartial = false) : IRequest<Result>;

public sealed class LetterAccountEntriesCommandHandler : IRequestHandler<LetterAccountEntriesCommand, Result>
{
    private readonly ILetteringService _lettering;
    private readonly IAuditService _auditService;

    public LetterAccountEntriesCommandHandler(ILetteringService lettering, IAuditService auditService)
    {
        _lettering = lettering;
        _auditService = auditService;
    }

    public async Task<Result> Handle(LetterAccountEntriesCommand request, CancellationToken cancellationToken)
    {
        var result = await _lettering.ManualLetterAsync(request.JournalEntryLineIds, request.AllowPartial, cancellationToken);
        if (result.IsSuccess)
        {
            await _auditService.LogAsync(
                AuditActions.Accounting.Lettered,
                "LetteringGroup",
                newValues: new { LineCount = request.JournalEntryLineIds.Count, request.AllowPartial },
                cancellationToken: cancellationToken);
        }

        return result;
    }
}

public sealed record UnletterAccountEntriesCommand(string Code) : IRequest<Result>;

public sealed class UnletterAccountEntriesCommandHandler : IRequestHandler<UnletterAccountEntriesCommand, Result>
{
    private readonly ILetteringService _lettering;
    private readonly IAuditService _auditService;

    public UnletterAccountEntriesCommandHandler(ILetteringService lettering, IAuditService auditService)
    {
        _lettering = lettering;
        _auditService = auditService;
    }

    public async Task<Result> Handle(UnletterAccountEntriesCommand request, CancellationToken cancellationToken)
    {
        var result = await _lettering.UnletterAsync(request.Code, cancellationToken);
        if (result.IsSuccess)
        {
            await _auditService.LogAsync(
                AuditActions.Accounting.Unlettered,
                "LetteringGroup",
                newValues: new { request.Code },
                cancellationToken: cancellationToken);
        }

        return result;
    }
}

public sealed record CloseAnnualPeriodCommand(int FiscalYear) : IRequest<Result>;

public sealed class CloseAnnualPeriodCommandHandler : IRequestHandler<CloseAnnualPeriodCommand, Result>
{
    private readonly IAccountingPeriodRepository _periods;
    private readonly IAccountingPeriodService _periodService;
    private readonly IJournalEntryRepository _journalEntries;
    private readonly IPreClosingControlService _preClosing;
    private readonly IAuditService _auditService;
    private readonly ICurrentUser _currentUser;
    private readonly AccountingSettings _settings;

    public CloseAnnualPeriodCommandHandler(
        IAccountingPeriodRepository periods,
        IAccountingPeriodService periodService,
        IJournalEntryRepository journalEntries,
        IPreClosingControlService preClosing,
        IAuditService auditService,
        ICurrentUser currentUser,
        IOptions<AccountingSettings> settings)
    {
        _periods = periods;
        _periodService = periodService;
        _journalEntries = journalEntries;
        _preClosing = preClosing;
        _auditService = auditService;
        _currentUser = currentUser;
        _settings = settings.Value;
    }

    public async Task<Result> Handle(CloseAnnualPeriodCommand request, CancellationToken cancellationToken)
    {
        var list = await _periods.GetByFiscalYearAsync(request.FiscalYear, cancellationToken);
        if (list.Count == 0)
            return Result.Failure(Error.Validation("FiscalYear", "Aucune période comptable pour cet exercice."));

        // Garde-fou légal : une clôture ne peut pas laisser des écritures en brouillard non validées.
        // Sans effet quand le workflow brouillard est désactivé (aucune écriture en brouillon).
        var drafts = await _journalEntries.CountDraftsByFiscalYearAsync(request.FiscalYear, cancellationToken);
        if (drafts > 0)
            return Result.Failure(Error.Validation("Brouillard",
                $"{drafts} écriture(s) en brouillard sur l'exercice {request.FiscalYear}. Validez-les avant la clôture annuelle."));

        // Contrôles de pré-clôture (Lot F) : refus si un contrôle BLOQUANT subsiste. Additif et
        // conditionné au flag — comportement historique préservé quand désactivé.
        if (_settings.PreClosingControlsEnabled)
        {
            var checklist = await _preClosing.RunAsync(request.FiscalYear, cancellationToken);
            if (checklist.IsFailure)
                return Result.Failure(checklist.Error);
            if (checklist.Value.HasBlocking)
            {
                var blocking = checklist.Value.Checks
                    .Where(c => c.Severity == (int)PreClosingSeverity.Blocking && c.Count > 0)
                    .Select(c => c.Title);
                return Result.Failure(Error.Validation("PreClosing",
                    $"Contrôles de pré-clôture bloquants : {string.Join(" ; ", blocking)}. Résolvez-les avant la clôture."));
            }
        }

        // Chaque période passe par la clôture verrouillée du service : mêmes gardes que la
        // clôture mensuelle + bascule des écritures Validee → Cloturee.
        var closedBy = _currentUser.Email ?? "system";
        foreach (var p in list)
        {
            if (p.IsClosed)
                continue;
            var close = await _periodService.ClosePeriodWithLockAsync(p.Id, closedBy, cancellationToken);
            if (close.IsFailure)
                return close;
        }

        await _auditService.LogAsync(
            AuditActions.Accounting.FiscalYearClosed,
            "AccountingPeriod",
            newValues: new { request.FiscalYear, PeriodsCount = list.Count },
            cancellationToken: cancellationToken);

        return Result.Success();
    }
}

public sealed record GenerateOpeningEntriesCommand(int ClosedFiscalYear) : IRequest<Result<Guid>>;

public sealed class GenerateOpeningEntriesCommandHandler : IRequestHandler<GenerateOpeningEntriesCommand, Result<Guid>>
{
    private readonly IAccountingService _accountingService;
    private readonly IAuditService _auditService;

    public GenerateOpeningEntriesCommandHandler(IAccountingService accountingService, IAuditService auditService)
    {
        _accountingService = accountingService;
        _auditService = auditService;
    }

    public async Task<Result<Guid>> Handle(GenerateOpeningEntriesCommand request, CancellationToken cancellationToken)
    {
        var result = await _accountingService.GenerateOpeningEntriesAsync(request.ClosedFiscalYear, cancellationToken);
        if (result.IsSuccess)
        {
            await _auditService.LogAsync(
                AuditActions.Accounting.FiscalYearClosed,
                "JournalEntry",
                result.Value,
                newValues: new { request.ClosedFiscalYear, NewFiscalYear = request.ClosedFiscalYear + 1 },
                cancellationToken: cancellationToken);
        }

        return result;
    }
}

// ── Phase 1 : brouillard & validation ─────────────────────────────────────────────

public sealed record ValidateJournalEntryCommand(Guid Id) : IRequest<Result>;

public sealed class ValidateJournalEntryCommandHandler : IRequestHandler<ValidateJournalEntryCommand, Result>
{
    private readonly IJournalEntryRepository _journalEntries;
    private readonly IAuditService _auditService;
    private readonly ICurrentUser _currentUser;

    public ValidateJournalEntryCommandHandler(
        IJournalEntryRepository journalEntries,
        IAuditService auditService,
        ICurrentUser currentUser)
    {
        _journalEntries = journalEntries;
        _auditService = auditService;
        _currentUser = currentUser;
    }

    public async Task<Result> Handle(ValidateJournalEntryCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAccountingFirmDelegatedContext)
            return Result.Failure(Error.Forbidden(AccountingValidationAccess.DeniedMessage));

        var entry = await _journalEntries.GetByIdAsync(request.Id, cancellationToken);
        if (entry is null)
            return Result.Failure(Error.NotFound("JournalEntry", request.Id));

        if (entry.AccountingPeriod?.IsClosed == true)
            return Result.Failure(Error.Validation("Period", "La période de cette écriture est clôturée."));

        var result = entry.Validate(_currentUser.Email ?? "system");
        if (result.IsFailure)
            return result;

        await _journalEntries.UpdateAsync(entry, cancellationToken);

        await _auditService.LogAsync(
            AuditActions.Accounting.EntryValidated,
            "JournalEntry",
            entry.Id,
            newValues: new { entry.JournalCode, entry.EntryNumber },
            cancellationToken: cancellationToken);

        return Result.Success();
    }
}

public sealed record ValidateJournalEntriesBatchCommand(ValidateJournalEntriesBatchRequest Request) : IRequest<Result<int>>;

public sealed class ValidateJournalEntriesBatchCommandHandler : IRequestHandler<ValidateJournalEntriesBatchCommand, Result<int>>
{
    private readonly IJournalEntryRepository _journalEntries;
    private readonly IAuditService _auditService;
    private readonly ICurrentUser _currentUser;

    public ValidateJournalEntriesBatchCommandHandler(
        IJournalEntryRepository journalEntries,
        IAuditService auditService,
        ICurrentUser currentUser)
    {
        _journalEntries = journalEntries;
        _auditService = auditService;
        _currentUser = currentUser;
    }

    public async Task<Result<int>> Handle(ValidateJournalEntriesBatchCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAccountingFirmDelegatedContext)
            return Result.Failure<int>(Error.Forbidden(AccountingValidationAccess.DeniedMessage));

        var drafts = await _journalEntries.GetDraftsByPeriodAsync(
            request.Request.PeriodId, request.Request.JournalCode, cancellationToken);

        var user = _currentUser.Email ?? "system";
        var count = 0;
        foreach (var entry in drafts)
        {
            var result = entry.Validate(user);
            if (result.IsFailure)
                continue;
            await _journalEntries.UpdateAsync(entry, cancellationToken);
            count++;
        }

        if (count > 0)
        {
            await _auditService.LogAsync(
                AuditActions.Accounting.EntriesBatchValidated,
                "AccountingPeriod",
                request.Request.PeriodId,
                newValues: new { request.Request.PeriodId, request.Request.JournalCode, Count = count },
                cancellationToken: cancellationToken);
        }

        return Result.Success(count);
    }
}

public sealed record UpdateDraftJournalEntryCommand(Guid Id, UpdateDraftJournalEntryRequest Request) : IRequest<Result>;

public sealed class UpdateDraftJournalEntryCommandHandler : IRequestHandler<UpdateDraftJournalEntryCommand, Result>
{
    private readonly IJournalEntryRepository _journalEntries;
    private readonly IChartOfAccountRepository _chartOfAccounts;
    private readonly IAuditService _auditService;

    public UpdateDraftJournalEntryCommandHandler(
        IJournalEntryRepository journalEntries,
        IChartOfAccountRepository chartOfAccounts,
        IAuditService auditService)
    {
        _journalEntries = journalEntries;
        _chartOfAccounts = chartOfAccounts;
        _auditService = auditService;
    }

    public async Task<Result> Handle(UpdateDraftJournalEntryCommand request, CancellationToken cancellationToken)
    {
        // Contrôle des comptes (asynchrone) AVANT la mutation suivie — même contrôle que la saisie manuelle.
        var checkedAccounts = new HashSet<string>(StringComparer.Ordinal);
        foreach (var line in request.Request.Lines)
        {
            var acc = line.AccountNumber?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(acc) || !checkedAccounts.Add(acc))
                continue;

            var account = await _chartOfAccounts.GetByAccountNumberAsync(acc, cancellationToken);
            if (account is null)
                return Result.Failure(Error.Validation("AccountNumber",
                    $"Le compte {acc} n'existe pas dans le plan comptable."));
            if (!account.IsActive)
                return Result.Failure(Error.Validation("AccountNumber", $"Le compte {acc} est désactivé."));
        }

        var mappedLines = ManualJournalLineMapper.Map(request.Request.Lines);
        if (mappedLines.IsFailure)
            return Result.Failure(mappedLines.Error);
        var lines = mappedLines.Value;

        // Mutation sur l'entité SUIVIE : le remplacement des lignes est reconcilié proprement
        // (anciennes lignes supprimées, nouvelles insérées) — pas de lignes orphelines.
        var result = await _journalEntries.MutateAsync(request.Id, entry =>
        {
            if (!entry.IsDraft)
                return Result.Failure(Error.Validation("Status", "Seule une écriture en brouillon peut être modifiée."));
            // C4 : un brouillon résiduel d'une période close (données antérieures au garde-fou
            // de clôture) ne doit plus pouvoir modifier les chiffres de la période.
            if (entry.AccountingPeriod?.IsClosed == true)
                return Result.Failure(Error.Validation("Period", "La période de cette écriture est clôturée."));
            // C5 : une extourne est générée comme miroir exact de l'écriture d'origine — l'éditer
            // casserait silencieusement cette symétrie. Elle se valide ou se supprime.
            if (entry.ReversesEntryId is not null)
                return Result.Failure(Error.Validation("Extourne",
                    "Une extourne doit rester le miroir exact de l'écriture d'origine : validez-la ou supprimez-la."));
            return entry.UpdateDraftLines(
                request.Request.Label,
                lines,
                pieceRef: request.Request.PieceRef,
                pieceDate: request.Request.PieceDate);
        }, cancellationToken);

        if (result.IsFailure)
            return result;

        await _auditService.LogAsync(
            AuditActions.Accounting.DraftUpdated,
            "JournalEntry",
            request.Id,
            newValues: new { LineCount = lines.Count },
            cancellationToken: cancellationToken);

        return Result.Success();
    }
}

public sealed record DeleteDraftJournalEntryCommand(Guid Id) : IRequest<Result>;

public sealed class DeleteDraftJournalEntryCommandHandler : IRequestHandler<DeleteDraftJournalEntryCommand, Result>
{
    private readonly IJournalEntryRepository _journalEntries;
    private readonly IAuditService _auditService;

    public DeleteDraftJournalEntryCommandHandler(
        IJournalEntryRepository journalEntries,
        IAuditService auditService)
    {
        _journalEntries = journalEntries;
        _auditService = auditService;
    }

    public async Task<Result> Handle(DeleteDraftJournalEntryCommand request, CancellationToken cancellationToken)
    {
        var entry = await _journalEntries.GetByIdAsync(request.Id, cancellationToken);
        if (entry is null)
            return Result.Failure(Error.NotFound("JournalEntry", request.Id));

        if (!entry.IsDraft)
            return Result.Failure(Error.Validation("Status", "Seule une écriture en brouillon peut être supprimée."));

        // C4 : même verrou que la modification — la période close fige aussi ses brouillons résiduels.
        if (entry.AccountingPeriod?.IsClosed == true)
            return Result.Failure(Error.Validation("Period", "La période de cette écriture est clôturée."));

        // C5 : la suppression d'un brouillon d'extourne doit restaurer l'écriture d'origine
        // (marquée IsReversed dès la création de l'extourne), sinon elle resterait verrouillée
        // avec une référence d'extourne orpheline.
        JournalEntry? reversedOriginal = null;
        if (entry.ReversesEntryId is Guid originalId)
        {
            reversedOriginal = await _journalEntries.GetByIdAsync(originalId, cancellationToken);
            if (reversedOriginal is not null && reversedOriginal.ReversedByEntryId == entry.Id)
            {
                reversedOriginal.ClearReversalMark();
                await _journalEntries.UpdateAsync(reversedOriginal, cancellationToken);
            }
        }

        await _journalEntries.RemoveAsync(entry, cancellationToken);

        await _auditService.LogAsync(
            AuditActions.Accounting.DraftDeleted,
            "JournalEntry",
            entry.Id,
            oldValues: new { entry.JournalCode, entry.EntryNumber, entry.ReversesEntryId, RestoredOriginalId = reversedOriginal?.Id },
            cancellationToken: cancellationToken);

        return Result.Success();
    }
}

// ── Phase 2 : extourne manuelle ───────────────────────────────────────────────────

public sealed record ReverseJournalEntryCommand(Guid Id, string Reason) : IRequest<Result<Guid>>;

public sealed class ReverseJournalEntryCommandHandler : IRequestHandler<ReverseJournalEntryCommand, Result<Guid>>
{
    private readonly IAccountingService _accountingService;
    private readonly IAuditService _auditService;

    public ReverseJournalEntryCommandHandler(
        IAccountingService accountingService,
        IAuditService auditService)
    {
        _accountingService = accountingService;
        _auditService = auditService;
    }

    public async Task<Result<Guid>> Handle(ReverseJournalEntryCommand request, CancellationToken cancellationToken)
    {
        var result = await _accountingService.ReverseJournalEntryAsync(request.Id, request.Reason, cancellationToken);
        if (result.IsSuccess)
        {
            await _auditService.LogAsync(
                AuditActions.Accounting.EntryReversed,
                "JournalEntry",
                request.Id,
                newValues: new { ReversalEntryId = result.Value, request.Reason },
                cancellationToken: cancellationToken);
        }

        return result;
    }
}

// ── Phase 3 : reprise de dossier par import ────────────────────────────────────────

public sealed record PreviewJournalImportCommand(
    byte[] Content, JournalImportFormat Format, byte[]? AccountMappingContent = null)
    : IRequest<Result<JournalImportPreviewDto>>;

public sealed class PreviewJournalImportCommandHandler : IRequestHandler<PreviewJournalImportCommand, Result<JournalImportPreviewDto>>
{
    private readonly IJournalImportService _importService;

    public PreviewJournalImportCommandHandler(IJournalImportService importService)
    {
        _importService = importService;
    }

    public Task<Result<JournalImportPreviewDto>> Handle(PreviewJournalImportCommand request, CancellationToken cancellationToken)
        => _importService.PreviewAsync(request.Content, request.Format, request.AccountMappingContent, cancellationToken);
}

public sealed record CommitJournalImportCommand(
    byte[] Content, JournalImportFormat Format, byte[]? AccountMappingContent = null)
    : IRequest<Result<JournalImportCommitResultDto>>;

public sealed class CommitJournalImportCommandHandler : IRequestHandler<CommitJournalImportCommand, Result<JournalImportCommitResultDto>>
{
    private readonly IJournalImportService _importService;
    private readonly IAuditService _auditService;

    public CommitJournalImportCommandHandler(IJournalImportService importService, IAuditService auditService)
    {
        _importService = importService;
        _auditService = auditService;
    }

    public async Task<Result<JournalImportCommitResultDto>> Handle(CommitJournalImportCommand request, CancellationToken cancellationToken)
    {
        var result = await _importService.CommitAsync(request.Content, request.Format, request.AccountMappingContent, cancellationToken);
        if (result.IsSuccess)
        {
            await _auditService.LogAsync(
                AuditActions.Accounting.DossierImported,
                "JournalEntry",
                newValues: new { request.Format, result.Value.ImportedEntries, result.Value.ImportedLines },
                cancellationToken: cancellationToken);
        }

        return result;
    }
}

// ── Remplacement de compte (traitement de maintenance) ─────────────────────────────

public sealed record PreviewAccountReplacementQuery(string Account) : IRequest<Result<int>>;

public sealed class PreviewAccountReplacementQueryHandler : IRequestHandler<PreviewAccountReplacementQuery, Result<int>>
{
    private readonly IAccountingService _accountingService;

    public PreviewAccountReplacementQueryHandler(IAccountingService accountingService)
    {
        _accountingService = accountingService;
    }

    public async Task<Result<int>> Handle(PreviewAccountReplacementQuery request, CancellationToken cancellationToken)
        => Result.Success(await _accountingService.CountReplaceableLinesAsync(request.Account, cancellationToken));
}

public sealed record ReplaceAccountCommand(string OldAccount, string NewAccount) : IRequest<Result<int>>;

public sealed class ReplaceAccountCommandHandler : IRequestHandler<ReplaceAccountCommand, Result<int>>
{
    private readonly IAccountingService _accountingService;
    private readonly IAuditService _auditService;
    private readonly AccountingSettings _settings;

    public ReplaceAccountCommandHandler(
        IAccountingService accountingService,
        IAuditService auditService,
        IOptions<AccountingSettings> settings)
    {
        _accountingService = accountingService;
        _auditService = auditService;
        _settings = settings.Value;
    }

    public async Task<Result<int>> Handle(ReplaceAccountCommand request, CancellationToken cancellationToken)
    {
        if (!_settings.AccountReplacementEnabled)
            return Result.Failure<int>(Error.Validation("Account", "Le remplacement de compte n'est pas activé."));

        var result = await _accountingService.ReplaceAccountAsync(request.OldAccount, request.NewAccount, cancellationToken);
        if (result.IsSuccess)
        {
            await _auditService.LogAsync(
                AuditActions.Accounting.AccountReplaced,
                "ChartOfAccount",
                newValues: new { request.OldAccount, request.NewAccount, LinesAffected = result.Value },
                cancellationToken: cancellationToken);
        }

        return result;
    }
}

/// <summary>
/// Mapping des lignes de saisie manuelle vers le domaine, avec tiers optionnel (plan tiers) :
/// un ThirdPartyId fourni exige un kind valide (1 = client, 2 = fournisseur) ; sans tiers,
/// comportement historique strict (null / ThirdPartyKind.None).
/// </summary>
public static class ManualJournalLineMapper
{
    public static Result<List<JournalLineInput>> Map(IReadOnlyList<ManualJournalLineRequest> lines)
    {
        var mapped = new List<JournalLineInput>(lines.Count);
        foreach (var l in lines)
        {
            Guid? thirdPartyId = null;
            var kind = ThirdPartyKind.None;
            if (l.ThirdPartyId is { } id && id != Guid.Empty)
            {
                if (l.ThirdPartyKind is not ((int)ThirdPartyKind.Client or (int)ThirdPartyKind.Supplier))
                    return Result.Failure<List<JournalLineInput>>(Error.Validation(
                        "ThirdPartyKind", "Type de tiers obligatoire (1 = client, 2 = fournisseur) quand un tiers est fourni."));
                thirdPartyId = id;
                kind = (ThirdPartyKind)l.ThirdPartyKind!.Value;
            }
            mapped.Add(new JournalLineInput(l.AccountNumber, l.LineLabel, l.Debit, l.Credit, thirdPartyId, kind));
        }
        return Result.Success(mapped);
    }
}
