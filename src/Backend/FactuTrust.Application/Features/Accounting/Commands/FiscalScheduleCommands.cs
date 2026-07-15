using System.Text.Json;
using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.Accounting.FiscalSchedule;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using MediatR;
using AuditActions = FactuTrust.Domain.Entities.AuditActions;

namespace FactuTrust.Application.Features.Accounting.Commands;

public sealed record EnsureFiscalScheduleCommand(int FiscalYear) : IRequest<Result<int>>;

public sealed class EnsureFiscalScheduleCommandHandler : IRequestHandler<EnsureFiscalScheduleCommand, Result<int>>
{
    private readonly IFiscalScheduleGenerator _generator;
    private readonly IAuditService _auditService;

    public EnsureFiscalScheduleCommandHandler(IFiscalScheduleGenerator generator, IAuditService auditService)
    {
        _generator = generator;
        _auditService = auditService;
    }

    public async Task<Result<int>> Handle(EnsureFiscalScheduleCommand request, CancellationToken cancellationToken)
    {
        var result = await _generator.EnsureFiscalYearAsync(request.FiscalYear, cancellationToken);
        if (result.IsFailure)
            return result;

        await _auditService.LogAsync(
            AuditActions.Accounting.FiscalScheduleGenerated,
            "FiscalScheduleEntry",
            newValues: new { request.FiscalYear, CreatedCount = result.Value },
            cancellationToken: cancellationToken);

        return result;
    }
}

public sealed record CreateFiscalScheduleEntryCommand(CreateFiscalScheduleEntryRequest Request) : IRequest<Result<FiscalScheduleEntryDto>>;

public sealed class CreateFiscalScheduleEntryCommandHandler : IRequestHandler<CreateFiscalScheduleEntryCommand, Result<FiscalScheduleEntryDto>>
{
    private readonly IFiscalScheduleRepository _repository;
    private readonly IAuditService _auditService;
    private readonly ICurrentUser _currentUser;
    private readonly TimeProvider _timeProvider;

    public CreateFiscalScheduleEntryCommandHandler(
        IFiscalScheduleRepository repository,
        IAuditService auditService,
        ICurrentUser currentUser,
        TimeProvider timeProvider)
    {
        _repository = repository;
        _auditService = auditService;
        _currentUser = currentUser;
        _timeProvider = timeProvider;
    }

    public async Task<Result<FiscalScheduleEntryDto>> Handle(CreateFiscalScheduleEntryCommand request, CancellationToken cancellationToken)
    {
        var obligation = ParseObligationType(request.Request.ObligationType);
        if (obligation.IsFailure)
            return Result.Failure<FiscalScheduleEntryDto>(obligation.Error);

        var label = string.IsNullOrWhiteSpace(request.Request.ObligationLabel)
            ? FiscalScheduleMappings.GetObligationDisplay(obligation.Value)
            : request.Request.ObligationLabel;

        var periodValidation = FiscalScheduleMappings.ValidatePeriodConsistency(
            obligation.Value,
            request.Request.PeriodMonth,
            request.Request.PeriodQuarter);
        if (periodValidation.IsFailure)
            return Result.Failure<FiscalScheduleEntryDto>(periodValidation.Error);

        var exists = await _repository.ExistsAsync(
            obligation.Value,
            request.Request.FiscalYear,
            request.Request.PeriodMonth,
            request.Request.PeriodQuarter,
            FiscalScheduleSourceType.Manual,
            cancellationToken);
        if (exists)
            return Result.Failure<FiscalScheduleEntryDto>(Error.Conflict("Une echeance manuelle identique existe deja pour cette periode."));

        var create = FiscalScheduleEntry.Create(
            obligation.Value,
            label,
            request.Request.FiscalYear,
            request.Request.DueDate,
            request.Request.EstimatedAmount,
            request.Request.Currency,
            request.Request.PeriodMonth,
            request.Request.PeriodQuarter,
            request.Request.PeriodStart,
            request.Request.PeriodEnd,
            FiscalScheduleSourceType.Manual,
            responsibleUserId: request.Request.ResponsibleUserId,
            responsibleName: request.Request.ResponsibleName,
            observations: request.Request.Observations);
        if (create.IsFailure)
            return Result.Failure<FiscalScheduleEntryDto>(create.Error);

        var entry = create.Value;
        entry.SetAuditInfo(_currentUser.Email ?? "system", false);
        await _repository.AddAsync(
            entry,
            FiscalScheduleHistoryEntry.Create(entry.Id, "Created", "Echeance fiscale creee manuellement.", newValuesJson: Serialize(Snapshot(entry))),
            cancellationToken);

        await _auditService.LogAsync(
            AuditActions.Accounting.FiscalScheduleCreated,
            "FiscalScheduleEntry",
            entry.Id,
            newValues: Snapshot(entry),
            cancellationToken: cancellationToken);

        return Result.Success(FiscalScheduleMappings.ToDto(entry, _timeProvider.GetLocalNow().DateTime.Date));
    }

    private static Result<FiscalObligationType> ParseObligationType(int value)
    {
        if (!Enum.IsDefined(typeof(FiscalObligationType), value))
            return Result.Failure<FiscalObligationType>(Error.Validation("ObligationType", "Type d'obligation fiscale invalide."));
        return Result.Success((FiscalObligationType)value);
    }

    private static object Snapshot(FiscalScheduleEntry entry) => FiscalScheduleCommandHelpers.Snapshot(entry);
    private static string Serialize(object value) => FiscalScheduleCommandHelpers.Serialize(value);
}

public sealed record UpdateFiscalScheduleEntryCommand(Guid Id, UpdateFiscalScheduleEntryRequest Request) : IRequest<Result<FiscalScheduleEntryDto>>;

public sealed class UpdateFiscalScheduleEntryCommandHandler : IRequestHandler<UpdateFiscalScheduleEntryCommand, Result<FiscalScheduleEntryDto>>
{
    private readonly IFiscalScheduleRepository _repository;
    private readonly IAuditService _auditService;
    private readonly ICurrentUser _currentUser;
    private readonly TimeProvider _timeProvider;

    public UpdateFiscalScheduleEntryCommandHandler(
        IFiscalScheduleRepository repository,
        IAuditService auditService,
        ICurrentUser currentUser,
        TimeProvider timeProvider)
    {
        _repository = repository;
        _auditService = auditService;
        _currentUser = currentUser;
        _timeProvider = timeProvider;
    }

    public async Task<Result<FiscalScheduleEntryDto>> Handle(UpdateFiscalScheduleEntryCommand request, CancellationToken cancellationToken)
    {
        var entry = await _repository.GetByIdAsync(request.Id, cancellationToken);
        if (entry is null)
            return Result.Failure<FiscalScheduleEntryDto>(Error.NotFound("FiscalScheduleEntry", request.Id));

        if (!Enum.IsDefined(typeof(FiscalObligationType), request.Request.ObligationType))
            return Result.Failure<FiscalScheduleEntryDto>(Error.Validation("ObligationType", "Type d'obligation fiscale invalide."));

        var oldValues = FiscalScheduleCommandHelpers.Snapshot(entry);
        var obligation = (FiscalObligationType)request.Request.ObligationType;
        var label = string.IsNullOrWhiteSpace(request.Request.ObligationLabel)
            ? FiscalScheduleMappings.GetObligationDisplay(obligation)
            : request.Request.ObligationLabel;

        var periodValidation = FiscalScheduleMappings.ValidatePeriodConsistency(
            obligation,
            request.Request.PeriodMonth,
            request.Request.PeriodQuarter);
        if (periodValidation.IsFailure)
            return Result.Failure<FiscalScheduleEntryDto>(periodValidation.Error);

        var update = entry.Update(
            obligation,
            label,
            request.Request.FiscalYear,
            request.Request.DueDate,
            request.Request.EstimatedAmount,
            request.Request.Currency,
            request.Request.PeriodMonth,
            request.Request.PeriodQuarter,
            request.Request.PeriodStart,
            request.Request.PeriodEnd,
            request.Request.ResponsibleUserId,
            request.Request.ResponsibleName,
            request.Request.Observations);
        if (update.IsFailure)
            return Result.Failure<FiscalScheduleEntryDto>(update.Error);

        entry.SetAuditInfo(_currentUser.Email ?? "system", true);
        var newValues = FiscalScheduleCommandHelpers.Snapshot(entry);
        await _repository.UpdateAsync(
            entry,
            FiscalScheduleHistoryEntry.Create(entry.Id, "Updated", "Echeance fiscale modifiee.", Serialize(oldValues), Serialize(newValues)),
            cancellationToken);

        await _auditService.LogAsync(
            AuditActions.Accounting.FiscalScheduleUpdated,
            "FiscalScheduleEntry",
            entry.Id,
            oldValues,
            newValues,
            cancellationToken);

        return Result.Success(FiscalScheduleMappings.ToDto(entry, _timeProvider.GetLocalNow().DateTime.Date));
    }

    private static string Serialize(object value) => FiscalScheduleCommandHelpers.Serialize(value);
}

public sealed record DeleteFiscalScheduleEntryCommand(Guid Id) : IRequest<Result>;

public sealed class DeleteFiscalScheduleEntryCommandHandler : IRequestHandler<DeleteFiscalScheduleEntryCommand, Result>
{
    private readonly IFiscalScheduleRepository _repository;
    private readonly IAuditService _auditService;
    private readonly ICurrentUser _currentUser;

    public DeleteFiscalScheduleEntryCommandHandler(IFiscalScheduleRepository repository, IAuditService auditService, ICurrentUser currentUser)
    {
        _repository = repository;
        _auditService = auditService;
        _currentUser = currentUser;
    }

    public async Task<Result> Handle(DeleteFiscalScheduleEntryCommand request, CancellationToken cancellationToken)
    {
        var entry = await _repository.GetByIdAsync(request.Id, cancellationToken);
        if (entry is null)
            return Result.Failure(Error.NotFound("FiscalScheduleEntry", request.Id));

        var oldValues = FiscalScheduleCommandHelpers.Snapshot(entry);
        entry.Cancel();
        entry.SetAuditInfo(_currentUser.Email ?? "system", true);
        await _repository.UpdateAsync(
            entry,
            FiscalScheduleHistoryEntry.Create(entry.Id, "Cancelled", "Echeance fiscale annulee.", FiscalScheduleCommandHelpers.Serialize(oldValues), FiscalScheduleCommandHelpers.Serialize(FiscalScheduleCommandHelpers.Snapshot(entry))),
            cancellationToken);

        await _auditService.LogAsync(
            AuditActions.Accounting.FiscalScheduleDeleted,
            "FiscalScheduleEntry",
            entry.Id,
            oldValues: oldValues,
            newValues: FiscalScheduleCommandHelpers.Snapshot(entry),
            cancellationToken: cancellationToken);

        return Result.Success();
    }
}

public sealed record MarkFiscalScheduleDepositedCommand(Guid Id, MarkFiscalScheduleDepositedRequest Request) : IRequest<Result<FiscalScheduleEntryDto>>;

public sealed class MarkFiscalScheduleDepositedCommandHandler : IRequestHandler<MarkFiscalScheduleDepositedCommand, Result<FiscalScheduleEntryDto>>
{
    private readonly IFiscalScheduleRepository _repository;
    private readonly IAuditService _auditService;
    private readonly ICurrentUser _currentUser;
    private readonly TimeProvider _timeProvider;

    public MarkFiscalScheduleDepositedCommandHandler(
        IFiscalScheduleRepository repository,
        IAuditService auditService,
        ICurrentUser currentUser,
        TimeProvider timeProvider)
    {
        _repository = repository;
        _auditService = auditService;
        _currentUser = currentUser;
        _timeProvider = timeProvider;
    }

    public async Task<Result<FiscalScheduleEntryDto>> Handle(MarkFiscalScheduleDepositedCommand request, CancellationToken cancellationToken)
    {
        var entry = await _repository.GetByIdAsync(request.Id, cancellationToken);
        if (entry is null)
            return Result.Failure<FiscalScheduleEntryDto>(Error.NotFound("FiscalScheduleEntry", request.Id));

        var oldValues = FiscalScheduleCommandHelpers.Snapshot(entry);
        var result = entry.MarkDeposited(request.Request.DepositDate);
        if (result.IsFailure)
            return Result.Failure<FiscalScheduleEntryDto>(result.Error);

        if (!string.IsNullOrWhiteSpace(request.Request.Observations))
        {
            var update = entry.Update(
                entry.ObligationType,
                entry.ObligationLabel,
                entry.FiscalYear,
                entry.DueDate,
                entry.EstimatedAmount,
                entry.Currency,
                entry.PeriodMonth,
                entry.PeriodQuarter,
                entry.PeriodStart,
                entry.PeriodEnd,
                entry.ResponsibleUserId,
                entry.ResponsibleName,
                request.Request.Observations);
            if (update.IsFailure)
                return Result.Failure<FiscalScheduleEntryDto>(update.Error);
        }

        entry.SetAuditInfo(_currentUser.Email ?? "system", true);
        var newValues = FiscalScheduleCommandHelpers.Snapshot(entry);
        await _repository.UpdateAsync(
            entry,
            FiscalScheduleHistoryEntry.Create(entry.Id, "Deposited", "Echeance fiscale marquee comme deposee.", FiscalScheduleCommandHelpers.Serialize(oldValues), FiscalScheduleCommandHelpers.Serialize(newValues)),
            cancellationToken);

        await _auditService.LogAsync(
            AuditActions.Accounting.FiscalScheduleDeposited,
            "FiscalScheduleEntry",
            entry.Id,
            oldValues,
            newValues,
            cancellationToken);

        return Result.Success(FiscalScheduleMappings.ToDto(entry, _timeProvider.GetLocalNow().DateTime.Date));
    }
}

public sealed record CaptureFiscalSchedulePaymentCommand(Guid Id, CaptureFiscalSchedulePaymentRequest Request) : IRequest<Result<FiscalScheduleEntryDto>>;

public sealed class CaptureFiscalSchedulePaymentCommandHandler : IRequestHandler<CaptureFiscalSchedulePaymentCommand, Result<FiscalScheduleEntryDto>>
{
    private readonly IFiscalScheduleRepository _repository;
    private readonly IAuditService _auditService;
    private readonly ICurrentUser _currentUser;
    private readonly TimeProvider _timeProvider;

    public CaptureFiscalSchedulePaymentCommandHandler(
        IFiscalScheduleRepository repository,
        IAuditService auditService,
        ICurrentUser currentUser,
        TimeProvider timeProvider)
    {
        _repository = repository;
        _auditService = auditService;
        _currentUser = currentUser;
        _timeProvider = timeProvider;
    }

    public async Task<Result<FiscalScheduleEntryDto>> Handle(CaptureFiscalSchedulePaymentCommand request, CancellationToken cancellationToken)
    {
        var entry = await _repository.GetByIdAsync(request.Id, cancellationToken);
        if (entry is null)
            return Result.Failure<FiscalScheduleEntryDto>(Error.NotFound("FiscalScheduleEntry", request.Id));

        var oldValues = FiscalScheduleCommandHelpers.Snapshot(entry);
        var result = entry.CapturePayment(request.Request.PaymentDate);
        if (result.IsFailure)
            return Result.Failure<FiscalScheduleEntryDto>(result.Error);

        if (!string.IsNullOrWhiteSpace(request.Request.Observations))
        {
            var update = entry.Update(
                entry.ObligationType,
                entry.ObligationLabel,
                entry.FiscalYear,
                entry.DueDate,
                entry.EstimatedAmount,
                entry.Currency,
                entry.PeriodMonth,
                entry.PeriodQuarter,
                entry.PeriodStart,
                entry.PeriodEnd,
                entry.ResponsibleUserId,
                entry.ResponsibleName,
                request.Request.Observations);
            if (update.IsFailure)
                return Result.Failure<FiscalScheduleEntryDto>(update.Error);
        }

        entry.SetAuditInfo(_currentUser.Email ?? "system", true);
        var newValues = FiscalScheduleCommandHelpers.Snapshot(entry);
        await _repository.UpdateAsync(
            entry,
            FiscalScheduleHistoryEntry.Create(entry.Id, "PaymentCaptured", "Paiement de l'echeance fiscale saisi.", FiscalScheduleCommandHelpers.Serialize(oldValues), FiscalScheduleCommandHelpers.Serialize(newValues)),
            cancellationToken);

        await _auditService.LogAsync(
            AuditActions.Accounting.FiscalSchedulePaymentCaptured,
            "FiscalScheduleEntry",
            entry.Id,
            oldValues,
            newValues,
            cancellationToken);

        return Result.Success(FiscalScheduleMappings.ToDto(entry, _timeProvider.GetLocalNow().DateTime.Date));
    }
}

public sealed record ScheduleFiscalReminderCommand(Guid Id, ScheduleFiscalReminderRequest Request) : IRequest<Result<FiscalScheduleEntryDto>>;

public sealed class ScheduleFiscalReminderCommandHandler : IRequestHandler<ScheduleFiscalReminderCommand, Result<FiscalScheduleEntryDto>>
{
    private readonly IFiscalScheduleRepository _repository;
    private readonly IAuditService _auditService;
    private readonly ICurrentUser _currentUser;
    private readonly TimeProvider _timeProvider;

    public ScheduleFiscalReminderCommandHandler(
        IFiscalScheduleRepository repository,
        IAuditService auditService,
        ICurrentUser currentUser,
        TimeProvider timeProvider)
    {
        _repository = repository;
        _auditService = auditService;
        _currentUser = currentUser;
        _timeProvider = timeProvider;
    }

    public async Task<Result<FiscalScheduleEntryDto>> Handle(ScheduleFiscalReminderCommand request, CancellationToken cancellationToken)
    {
        var entry = await _repository.GetByIdAsync(request.Id, cancellationToken);
        if (entry is null)
            return Result.Failure<FiscalScheduleEntryDto>(Error.NotFound("FiscalScheduleEntry", request.Id));
        if (!Enum.IsDefined(typeof(FiscalReminderChannel), request.Request.Channel))
            return Result.Failure<FiscalScheduleEntryDto>(Error.Validation("Channel", "Canal de rappel invalide."));

        var oldValues = FiscalScheduleCommandHelpers.Snapshot(entry);
        var reminderAt = request.Request.ReminderAt ?? _timeProvider.GetUtcNow().DateTime;
        var result = entry.MarkReminder((FiscalReminderChannel)request.Request.Channel, reminderAt);
        if (result.IsFailure)
            return Result.Failure<FiscalScheduleEntryDto>(result.Error);

        entry.SetAuditInfo(_currentUser.Email ?? "system", true);
        var newValues = FiscalScheduleCommandHelpers.Snapshot(entry);
        await _repository.UpdateAsync(
            entry,
            FiscalScheduleHistoryEntry.Create(entry.Id, "ReminderScheduled", "Rappel fiscal planifie.", FiscalScheduleCommandHelpers.Serialize(oldValues), FiscalScheduleCommandHelpers.Serialize(newValues)),
            cancellationToken);

        await _auditService.LogAsync(
            AuditActions.Accounting.FiscalScheduleReminderScheduled,
            "FiscalScheduleEntry",
            entry.Id,
            oldValues,
            newValues,
            cancellationToken);

        return Result.Success(FiscalScheduleMappings.ToDto(entry, _timeProvider.GetLocalNow().DateTime.Date));
    }
}

public sealed record MarkFiscalScheduleValidatedCommand(Guid Id, MarkFiscalScheduleValidatedRequest Request) : IRequest<Result<FiscalScheduleEntryDto>>;

public sealed class MarkFiscalScheduleValidatedCommandHandler : IRequestHandler<MarkFiscalScheduleValidatedCommand, Result<FiscalScheduleEntryDto>>
{
    private readonly IFiscalScheduleRepository _repository;
    private readonly IAuditService _auditService;
    private readonly ICurrentUser _currentUser;
    private readonly TimeProvider _timeProvider;

    public MarkFiscalScheduleValidatedCommandHandler(
        IFiscalScheduleRepository repository,
        IAuditService auditService,
        ICurrentUser currentUser,
        TimeProvider timeProvider)
    {
        _repository = repository;
        _auditService = auditService;
        _currentUser = currentUser;
        _timeProvider = timeProvider;
    }

    public async Task<Result<FiscalScheduleEntryDto>> Handle(MarkFiscalScheduleValidatedCommand request, CancellationToken cancellationToken)
    {
        var entry = await _repository.GetByIdAsync(request.Id, cancellationToken);
        if (entry is null)
            return Result.Failure<FiscalScheduleEntryDto>(Error.NotFound("FiscalScheduleEntry", request.Id));

        var oldValues = FiscalScheduleCommandHelpers.Snapshot(entry);
        var validatedBy = _currentUser.Email ?? "system";
        var result = entry.MarkValidated(request.Request.ValidatedDate, validatedBy);
        if (result.IsFailure)
            return Result.Failure<FiscalScheduleEntryDto>(result.Error);

        if (!string.IsNullOrWhiteSpace(request.Request.Observations))
        {
            var update = entry.Update(
                entry.ObligationType,
                entry.ObligationLabel,
                entry.FiscalYear,
                entry.DueDate,
                entry.EstimatedAmount,
                entry.Currency,
                entry.PeriodMonth,
                entry.PeriodQuarter,
                entry.PeriodStart,
                entry.PeriodEnd,
                entry.ResponsibleUserId,
                entry.ResponsibleName,
                request.Request.Observations);
            if (update.IsFailure)
                return Result.Failure<FiscalScheduleEntryDto>(update.Error);
        }

        entry.SetAuditInfo(validatedBy, true);
        var newValues = FiscalScheduleCommandHelpers.Snapshot(entry);
        await _repository.UpdateAsync(
            entry,
            FiscalScheduleHistoryEntry.Create(entry.Id, "Validated", "Echeance fiscale marquee comme validee.", FiscalScheduleCommandHelpers.Serialize(oldValues), FiscalScheduleCommandHelpers.Serialize(newValues)),
            cancellationToken);

        await _auditService.LogAsync(
            AuditActions.Accounting.FiscalScheduleValidated,
            "FiscalScheduleEntry",
            entry.Id,
            oldValues,
            newValues,
            cancellationToken);

        return Result.Success(FiscalScheduleMappings.ToDto(entry, _timeProvider.GetLocalNow().DateTime.Date));
    }
}

internal static class FiscalScheduleCommandHelpers
{
    public static object Snapshot(FiscalScheduleEntry entry) => new
    {
        entry.Id,
        entry.ObligationType,
        entry.ObligationLabel,
        entry.FiscalYear,
        entry.PeriodMonth,
        entry.PeriodQuarter,
        entry.PeriodStart,
        entry.PeriodEnd,
        entry.DueDate,
        entry.EstimatedAmount,
        entry.Currency,
        Status = entry.ResolveStatus(DateTime.UtcNow.Date),
        entry.SourceType,
        entry.SourceId,
        entry.DepositDate,
        entry.PaymentDate,
        entry.ValidatedAt,
        entry.ValidatedBy,
        entry.ResponsibleUserId,
        entry.ResponsibleName,
        entry.Observations,
        entry.LastReminderAt,
        entry.LastReminderChannel,
        entry.IsCancelled,
        entry.Version
    };

    public static string Serialize(object value) => JsonSerializer.Serialize(value);
}
