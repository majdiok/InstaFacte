using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.Payroll;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities.Payroll;
using FactuTrust.Domain.Services.Payroll;
using MediatR;

namespace FactuTrust.Application.Features.Payroll.PublicHolidays;

public sealed record ListPublicHolidaysQuery(int Year) : IRequest<IReadOnlyList<PayrollPublicHolidayDto>>;

public sealed class ListPublicHolidaysQueryHandler : IRequestHandler<ListPublicHolidaysQuery, IReadOnlyList<PayrollPublicHolidayDto>>
{
    private readonly IPayrollPublicHolidayRepository _holidays;

    public ListPublicHolidaysQueryHandler(IPayrollPublicHolidayRepository holidays) => _holidays = holidays;

    public async Task<IReadOnlyList<PayrollPublicHolidayDto>> Handle(ListPublicHolidaysQuery request, CancellationToken cancellationToken)
    {
        var items = await _holidays.ListByYearAsync(request.Year, cancellationToken);
        return items.Select(PayrollMappings.ToPublicHolidayDto).ToList();
    }
}

public sealed record CreatePublicHolidayCommand(UpsertPayrollPublicHolidayDto Dto) : IRequest<Result<Guid>>;

public sealed class CreatePublicHolidayCommandHandler : IRequestHandler<CreatePublicHolidayCommand, Result<Guid>>
{
    private readonly IPayrollPublicHolidayRepository _holidays;

    public CreatePublicHolidayCommandHandler(IPayrollPublicHolidayRepository holidays) => _holidays = holidays;

    public async Task<Result<Guid>> Handle(CreatePublicHolidayCommand request, CancellationToken cancellationToken)
    {
        var dto = request.Dto;
        if (await _holidays.ExistsForDateAsync(dto.Year, dto.Date, cancellationToken: cancellationToken))
            return Result.Failure<Guid>(Error.Validation("Date", "Un jour férié existe déjà à cette date pour l'exercice."));

        var result = PayrollPublicHoliday.Create(
            dto.Year, dto.Date, dto.Label, dto.Kind, dto.IsPaid, dto.IsEstimated, dto.DecreeReference);
        if (result.IsFailure)
            return Result.Failure<Guid>(result.Error);

        await _holidays.AddAsync(result.Value, cancellationToken);
        return Result.Success(result.Value.Id);
    }
}

public sealed record UpdatePublicHolidayCommand(Guid Id, UpsertPayrollPublicHolidayDto Dto) : IRequest<Result>;

public sealed class UpdatePublicHolidayCommandHandler : IRequestHandler<UpdatePublicHolidayCommand, Result>
{
    private readonly IPayrollPublicHolidayRepository _holidays;

    public UpdatePublicHolidayCommandHandler(IPayrollPublicHolidayRepository holidays) => _holidays = holidays;

    public async Task<Result> Handle(UpdatePublicHolidayCommand request, CancellationToken cancellationToken)
    {
        var holiday = await _holidays.GetByIdAsync(request.Id, cancellationToken);
        if (holiday is null)
            return Result.Failure(Error.NotFound("PayrollPublicHoliday", request.Id));

        var dto = request.Dto;
        if (dto.Year != holiday.Year)
            return Result.Failure(Error.Validation("Year", "L'exercice d'un jour férié ne peut pas être modifié."));

        if (await _holidays.ExistsForDateAsync(dto.Year, dto.Date, request.Id, cancellationToken))
            return Result.Failure(Error.Validation("Date", "Un jour férié existe déjà à cette date pour l'exercice."));

        var update = holiday.Update(dto.Date, dto.Label, dto.Kind, dto.IsPaid, dto.IsEstimated, dto.DecreeReference);
        if (update.IsFailure)
            return update;

        await _holidays.UpdateAsync(holiday, cancellationToken);
        return Result.Success();
    }
}

public sealed record DeletePublicHolidayCommand(Guid Id) : IRequest<Result>;

public sealed class DeletePublicHolidayCommandHandler : IRequestHandler<DeletePublicHolidayCommand, Result>
{
    private readonly IPayrollPublicHolidayRepository _holidays;

    public DeletePublicHolidayCommandHandler(IPayrollPublicHolidayRepository holidays) => _holidays = holidays;

    public async Task<Result> Handle(DeletePublicHolidayCommand request, CancellationToken cancellationToken)
    {
        var holiday = await _holidays.GetByIdAsync(request.Id, cancellationToken);
        if (holiday is null)
            return Result.Failure(Error.NotFound("PayrollPublicHoliday", request.Id));

        await _holidays.DeleteAsync(holiday, cancellationToken);
        return Result.Success();
    }
}

public sealed record SeedPublicHolidaysCommand(SeedPayrollPublicHolidaysDto Dto) : IRequest<Result<SeedPayrollPublicHolidaysResultDto>>;

public sealed class SeedPublicHolidaysCommandHandler : IRequestHandler<SeedPublicHolidaysCommand, Result<SeedPayrollPublicHolidaysResultDto>>
{
    private readonly IPayrollPublicHolidayRepository _holidays;

    public SeedPublicHolidaysCommandHandler(IPayrollPublicHolidayRepository holidays) => _holidays = holidays;

    public async Task<Result<SeedPayrollPublicHolidaysResultDto>> Handle(SeedPublicHolidaysCommand request, CancellationToken cancellationToken)
    {
        var years = request.Dto.Years.Count > 0
            ? request.Dto.Years
            : PayrollPublicHolidaySeed.SupportedSeedYears;

        var unsupported = years.Where(y => !PayrollPublicHolidaySeed.SupportedSeedYears.Contains(y)).ToList();
        if (unsupported.Count > 0)
            return Result.Failure<SeedPayrollPublicHolidaysResultDto>(
                Error.Validation("Years", $"Aucune donnée d'amorçage pour les exercices : {string.Join(", ", unsupported)}."));

        var toInsert = new List<PayrollPublicHoliday>();
        var skipped = 0;

        foreach (var year in years)
        {
            var existingDates = (await _holidays.ListByYearAsync(year, cancellationToken))
                .Select(h => h.Date.Date)
                .ToHashSet();

            foreach (var seed in PayrollPublicHolidaySeed.GetDefaultsForYear(year))
            {
                if (existingDates.Contains(seed.Date.Date))
                {
                    skipped++;
                    continue;
                }

                toInsert.Add(seed);
            }
        }

        if (toInsert.Count > 0)
            await _holidays.AddRangeAsync(toInsert, cancellationToken);

        return Result.Success(new SeedPayrollPublicHolidaysResultDto
        {
            InsertedCount = toInsert.Count,
            SkippedCount = skipped
        });
    }
}
