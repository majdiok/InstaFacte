using ClosedXML.Excel;
using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Forecasting;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities.FirmGovernance;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.Services.FirmGovernance;
using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FactuTrust.Infrastructure.Services;

public sealed class FirmLeaveService : IFirmLeaveService
{
    private readonly MasterDbContext _db;
    private readonly ITunisianCalendarService _calendar;

    public FirmLeaveService(MasterDbContext db, ITunisianCalendarService calendar)
    {
        _db = db;
        _calendar = calendar;
    }

    public async Task EnsureDefaultsAsync(Guid firmTenantId, int year, CancellationToken cancellationToken = default)
    {
        var hasTypes = await _db.FirmLeaveTypes.AnyAsync(t => t.FirmTenantId == firmTenantId, cancellationToken);
        if (!hasTypes)
        {
            var sort = 0;
            foreach (var (code, label, color, deducts, requiresApproval) in FirmLeaveType.DefaultCatalog)
            {
                var created = FirmLeaveType.Create(firmTenantId, code, label, color, deducts, requiresApproval, isSystem: true, sortOrder: sort++);
                if (created.IsSuccess)
                    _db.FirmLeaveTypes.Add(created.Value);
            }
            await _db.SaveChangesAsync(cancellationToken);
        }

        var hasSettings = await _db.FirmLeaveSettings.AnyAsync(s => s.FirmTenantId == firmTenantId && s.Year == year, cancellationToken);
        if (!hasSettings)
        {
            var defaultDays = 30m;
            var ts = await _db.FirmTimeSheetYearSettings.AsNoTracking()
                .FirstOrDefaultAsync(s => s.FirmTenantId == firmTenantId && s.Year == year, cancellationToken);
            if (ts is not null && ts.PaidLeaveDaysPerYear > 0)
                defaultDays = ts.PaidLeaveDaysPerYear;

            var settings = FirmLeaveSettings.Create(firmTenantId, year, defaultDays);
            if (settings.IsSuccess)
            {
                _db.FirmLeaveSettings.Add(settings.Value);
                await _db.SaveChangesAsync(cancellationToken);
            }
        }
    }

    public async Task<FirmLeaveOverviewDto> GetOverviewAsync(
        Guid firmTenantId, int year, bool isManager, Guid actorUserId, CancellationToken cancellationToken = default)
    {
        await EnsureDefaultsAsync(firmTenantId, year, cancellationToken);

        var balances = await ListBalancesAsync(firmTenantId, year, isManager, actorUserId, cancellationToken);
        var requests = await ListRequestsAsync(firmTenantId, isManager, actorUserId, null, null, null, null, null, year, cancellationToken);

        var taken = requests.Where(r => r.Status == (int)FirmLeaveRequestStatus.Approved).Sum(r => r.Days);
        var pending = requests.Where(r => r.Status == (int)FirmLeaveRequestStatus.Submitted).Sum(r => r.Days);
        var collabCount = Math.Max(1, balances.Count);
        var absenteeism = Math.Round(taken / (collabCount * 220m) * 100m, 2, MidpointRounding.AwayFromZero);

        var types = await ListTypesAsync(firmTenantId, activeOnly: true, cancellationToken);
        var typeSummaries = types.Select(t =>
        {
            var takenType = requests.Where(r => r.LeaveTypeId == t.Id && r.Status == (int)FirmLeaveRequestStatus.Approved).Sum(r => r.Days);
            return new FirmLeaveTypeSummaryDto
            {
                LeaveTypeId = t.Id,
                Label = t.Label,
                ColorHex = t.ColorHex,
                TakenDays = takenType,
                BalanceDays = t.DeductsBalance ? balances.Sum(b => b.RemainingDays) : 0m
            };
        }).ToList();

        return new FirmLeaveOverviewDto
        {
            Year = year,
            TotalPaidBalanceDays = balances.Sum(b => b.RemainingDays),
            TakenDays = taken,
            PendingDays = pending,
            AbsenteeismRatePercent = absenteeism,
            PendingRequests = requests
                .Where(r => r.Status == (int)FirmLeaveRequestStatus.Submitted)
                .OrderByDescending(r => r.SubmittedAt)
                .Take(10)
                .ToList(),
            TopBalances = balances.OrderBy(b => b.RemainingDays).Take(8).ToList(),
            TypeSummaries = typeSummaries
        };
    }

    public async Task<IReadOnlyList<FirmLeaveCalendarEntryDto>> GetCalendarAsync(
        Guid firmTenantId, DateTime from, DateTime to, bool isManager, Guid actorUserId, CancellationToken cancellationToken = default)
    {
        await EnsureDefaultsAsync(firmTenantId, from.Year, cancellationToken);

        var fromD = from.Date;
        var toD = to.Date;
        var statuses = new[] { FirmLeaveRequestStatus.Approved, FirmLeaveRequestStatus.Submitted };

        var query = from r in _db.FirmLeaveRequests.AsNoTracking()
                    join t in _db.FirmLeaveTypes.AsNoTracking() on r.LeaveTypeId equals t.Id
                    join u in _db.Users.AsNoTracking() on r.UserId equals u.Id
                    where r.FirmTenantId == firmTenantId
                          && statuses.Contains(r.Status)
                          && r.StartDate <= toD && r.EndDate >= fromD
                    select new { r, t, u };

        var rows = await query.ToListAsync(cancellationToken);
        var profiles = await _db.FirmCollaboratorProfiles.AsNoTracking()
            .Where(p => rows.Select(x => x.u.Id).Contains(p.UserId))
            .ToDictionaryAsync(p => p.UserId, cancellationToken);

        return rows
            .OrderBy(x => x.u.LastName)
            .ThenBy(x => x.r.StartDate)
            .Select(x => new FirmLeaveCalendarEntryDto
            {
                RequestId = x.r.Id,
                UserId = x.r.UserId,
                CollaboratorName = DisplayName(x.u),
                Qualification = profiles.TryGetValue(x.u.Id, out var p) ? p.Qualification : null,
                LeaveTypeId = x.t.Id,
                LeaveTypeLabel = x.t.Label,
                ColorHex = x.t.ColorHex,
                StartDate = x.r.StartDate,
                EndDate = x.r.EndDate,
                StartUnit = (int)x.r.StartUnit,
                EndUnit = (int)x.r.EndUnit,
                Days = x.r.Days,
                Status = (int)x.r.Status
            })
            .ToList();
    }

    public async Task<IReadOnlyList<FirmLeaveRequestDto>> ListRequestsAsync(
        Guid firmTenantId,
        bool isManager,
        Guid actorUserId,
        Guid? userId,
        int? status,
        Guid? typeId,
        DateTime? from,
        DateTime? to,
        int? year,
        CancellationToken cancellationToken = default)
    {
        await EnsureDefaultsAsync(firmTenantId, year ?? DateTime.UtcNow.Year, cancellationToken);

        var query = from r in _db.FirmLeaveRequests.AsNoTracking()
                    join t in _db.FirmLeaveTypes.AsNoTracking() on r.LeaveTypeId equals t.Id
                    join u in _db.Users.AsNoTracking() on r.UserId equals u.Id
                    where r.FirmTenantId == firmTenantId
                    select new { r, t, u };

        if (!isManager)
            query = query.Where(x => x.r.UserId == actorUserId);
        else if (userId.HasValue)
            query = query.Where(x => x.r.UserId == userId.Value);

        if (status.HasValue)
            query = query.Where(x => (int)x.r.Status == status.Value);
        if (typeId.HasValue)
            query = query.Where(x => x.r.LeaveTypeId == typeId.Value);
        if (from.HasValue)
            query = query.Where(x => x.r.EndDate >= from.Value.Date);
        if (to.HasValue)
            query = query.Where(x => x.r.StartDate <= to.Value.Date);
        if (year.HasValue)
            query = query.Where(x => x.r.StartDate.Year == year.Value);

        var rows = await query
            .OrderByDescending(x => x.r.StartDate)
            .ThenByDescending(x => x.r.CreatedAt)
            .ToListAsync(cancellationToken);

        return rows.Select(x => MapRequest(x.r, x.t, DisplayName(x.u))).ToList();
    }

    public async Task<FirmLeaveRequestDto?> GetRequestAsync(
        Guid firmTenantId, Guid id, bool isManager, Guid actorUserId, CancellationToken cancellationToken = default)
    {
        var row = await (from r in _db.FirmLeaveRequests.AsNoTracking()
                         join t in _db.FirmLeaveTypes.AsNoTracking() on r.LeaveTypeId equals t.Id
                         join u in _db.Users.AsNoTracking() on r.UserId equals u.Id
                         where r.FirmTenantId == firmTenantId && r.Id == id
                         select new { r, t, u }).FirstOrDefaultAsync(cancellationToken);

        if (row is null) return null;
        if (!isManager && row.r.UserId != actorUserId) return null;
        return MapRequest(row.r, row.t, DisplayName(row.u));
    }

    public async Task<Result<ComputeFirmLeaveDaysResultDto>> ComputeDaysAsync(
        Guid firmTenantId, int year, ComputeFirmLeaveDaysDto dto, CancellationToken cancellationToken = default)
    {
        await EnsureDefaultsAsync(firmTenantId, year, cancellationToken);
        var settings = await GetOrCreateSettingsAsync(firmTenantId, year, cancellationToken);
        var days = FirmLeaveWorkingDaysCalculator.ComputeDays(
            dto.StartDate,
            dto.EndDate,
            (FirmLeaveDayUnit)dto.StartUnit,
            (FirmLeaveDayUnit)dto.EndUnit,
            settings.AllowHalfDays,
            _calendar.IsHoliday);

        return Result.Success(new ComputeFirmLeaveDaysResultDto { Days = days });
    }

    public async Task<Result<FirmLeaveRequestDto>> CreateAsync(
        Guid firmTenantId, Guid actorUserId, bool isManager, CreateFirmLeaveRequestDto dto, CancellationToken cancellationToken = default)
    {
        await EnsureDefaultsAsync(firmTenantId, dto.StartDate.Year, cancellationToken);

        var targetUserId = actorUserId;
        if (dto.UserId.HasValue && dto.UserId.Value != Guid.Empty)
        {
            if (!isManager && dto.UserId.Value != actorUserId)
                return Result.Failure<FirmLeaveRequestDto>(Error.Validation("UserId", "Vous ne pouvez créer une demande que pour vous-même."));
            targetUserId = dto.UserId.Value;
        }

        var userOk = await _db.Users.AnyAsync(u => u.Id == targetUserId && u.TenantId == firmTenantId, cancellationToken);
        if (!userOk)
            return Result.Failure<FirmLeaveRequestDto>(Error.Validation("UserId", "Collaborateur introuvable dans ce cabinet."));

        var leaveType = await _db.FirmLeaveTypes.FirstOrDefaultAsync(
            t => t.Id == dto.LeaveTypeId && t.FirmTenantId == firmTenantId && t.IsActive, cancellationToken);
        if (leaveType is null)
            return Result.Failure<FirmLeaveRequestDto>(Error.Validation("LeaveTypeId", "Type d'absence introuvable ou inactif."));

        var settings = await GetOrCreateSettingsAsync(firmTenantId, dto.StartDate.Year, cancellationToken);
        var days = FirmLeaveWorkingDaysCalculator.ComputeDays(
            dto.StartDate, dto.EndDate,
            (FirmLeaveDayUnit)dto.StartUnit, (FirmLeaveDayUnit)dto.EndUnit,
            settings.AllowHalfDays, _calendar.IsHoliday);

        if (days <= 0)
            return Result.Failure<FirmLeaveRequestDto>(Error.Validation("Days", "Aucun jour ouvré sur la période sélectionnée."));

        if (settings.BlockOverlap)
        {
            var overlap = await HasOverlapAsync(firmTenantId, targetUserId, dto.StartDate, dto.EndDate, excludeId: null, cancellationToken);
            if (overlap)
                return Result.Failure<FirmLeaveRequestDto>(Error.Validation("Overlap", "Une demande existe déjà sur cette période."));
        }

        var created = FirmLeaveRequest.Create(
            firmTenantId, targetUserId, leaveType.Id,
            dto.StartDate, dto.EndDate,
            (FirmLeaveDayUnit)dto.StartUnit, (FirmLeaveDayUnit)dto.EndUnit,
            days, dto.Reason);
        if (created.IsFailure)
            return Result.Failure<FirmLeaveRequestDto>(created.Error);

        var entity = created.Value;
        entity.SetAuditInfo(actorUserId.ToString());

        if (dto.SubmitImmediately)
        {
            var notice = CheckMinNotice(settings, entity.StartDate);
            if (notice is not null) return Result.Failure<FirmLeaveRequestDto>(notice);
            var submit = entity.Submit();
            if (submit.IsFailure) return Result.Failure<FirmLeaveRequestDto>(submit.Error);
        }

        _db.FirmLeaveRequests.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);

        var user = await _db.Users.AsNoTracking().FirstAsync(u => u.Id == targetUserId, cancellationToken);
        return Result.Success(MapRequest(entity, leaveType, DisplayName(user)));
    }

    public async Task<Result<FirmLeaveRequestDto>> UpdateAsync(
        Guid firmTenantId, Guid actorUserId, bool isManager, Guid id, UpdateFirmLeaveRequestDto dto, CancellationToken cancellationToken = default)
    {
        var entity = await _db.FirmLeaveRequests.FirstOrDefaultAsync(r => r.Id == id && r.FirmTenantId == firmTenantId, cancellationToken);
        if (entity is null)
            return Result.Failure<FirmLeaveRequestDto>(Error.NotFound("Leave", id));
        if (!isManager && entity.UserId != actorUserId)
            return Result.Failure<FirmLeaveRequestDto>(Error.Validation("Access", "Accès refusé."));

        var leaveType = await _db.FirmLeaveTypes.FirstOrDefaultAsync(
            t => t.Id == dto.LeaveTypeId && t.FirmTenantId == firmTenantId && t.IsActive, cancellationToken);
        if (leaveType is null)
            return Result.Failure<FirmLeaveRequestDto>(Error.Validation("LeaveTypeId", "Type d'absence introuvable ou inactif."));

        var settings = await GetOrCreateSettingsAsync(firmTenantId, dto.StartDate.Year, cancellationToken);
        var days = FirmLeaveWorkingDaysCalculator.ComputeDays(
            dto.StartDate, dto.EndDate,
            (FirmLeaveDayUnit)dto.StartUnit, (FirmLeaveDayUnit)dto.EndUnit,
            settings.AllowHalfDays, _calendar.IsHoliday);
        if (days <= 0)
            return Result.Failure<FirmLeaveRequestDto>(Error.Validation("Days", "Aucun jour ouvré sur la période sélectionnée."));

        if (settings.BlockOverlap)
        {
            var overlap = await HasOverlapAsync(firmTenantId, entity.UserId, dto.StartDate, dto.EndDate, entity.Id, cancellationToken);
            if (overlap)
                return Result.Failure<FirmLeaveRequestDto>(Error.Validation("Overlap", "Une demande existe déjà sur cette période."));
        }

        var update = entity.Update(leaveType.Id, dto.StartDate, dto.EndDate,
            (FirmLeaveDayUnit)dto.StartUnit, (FirmLeaveDayUnit)dto.EndUnit, days, dto.Reason);
        if (update.IsFailure)
            return Result.Failure<FirmLeaveRequestDto>(update.Error);

        entity.SetAuditInfo(actorUserId.ToString(), isUpdate: true);
        await _db.SaveChangesAsync(cancellationToken);

        var user = await _db.Users.AsNoTracking().FirstAsync(u => u.Id == entity.UserId, cancellationToken);
        return Result.Success(MapRequest(entity, leaveType, DisplayName(user)));
    }

    public async Task<Result<FirmLeaveRequestDto>> SubmitAsync(
        Guid firmTenantId, Guid actorUserId, bool isManager, Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _db.FirmLeaveRequests.FirstOrDefaultAsync(r => r.Id == id && r.FirmTenantId == firmTenantId, cancellationToken);
        if (entity is null)
            return Result.Failure<FirmLeaveRequestDto>(Error.NotFound("Leave", id));
        if (!isManager && entity.UserId != actorUserId)
            return Result.Failure<FirmLeaveRequestDto>(Error.Validation("Access", "Accès refusé."));

        var settings = await GetOrCreateSettingsAsync(firmTenantId, entity.StartDate.Year, cancellationToken);
        var notice = CheckMinNotice(settings, entity.StartDate);
        if (notice is not null) return Result.Failure<FirmLeaveRequestDto>(notice);

        if (settings.BlockOverlap)
        {
            var overlap = await HasOverlapAsync(firmTenantId, entity.UserId, entity.StartDate, entity.EndDate, entity.Id, cancellationToken);
            if (overlap)
                return Result.Failure<FirmLeaveRequestDto>(Error.Validation("Overlap", "Une demande existe déjà sur cette période."));
        }

        var result = entity.Submit();
        if (result.IsFailure) return Result.Failure<FirmLeaveRequestDto>(result.Error);

        entity.SetAuditInfo(actorUserId.ToString(), isUpdate: true);
        await _db.SaveChangesAsync(cancellationToken);
        return Result.Success(await MapRequestByIdAsync(entity.Id, cancellationToken));
    }

    public async Task<Result<FirmLeaveRequestDto>> CancelAsync(
        Guid firmTenantId, Guid actorUserId, bool isManager, Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _db.FirmLeaveRequests.FirstOrDefaultAsync(r => r.Id == id && r.FirmTenantId == firmTenantId, cancellationToken);
        if (entity is null)
            return Result.Failure<FirmLeaveRequestDto>(Error.NotFound("Leave", id));
        if (!isManager && entity.UserId != actorUserId)
            return Result.Failure<FirmLeaveRequestDto>(Error.Validation("Access", "Accès refusé."));

        var result = entity.Cancel();
        if (result.IsFailure) return Result.Failure<FirmLeaveRequestDto>(result.Error);

        entity.SetAuditInfo(actorUserId.ToString(), isUpdate: true);
        await _db.SaveChangesAsync(cancellationToken);
        return Result.Success(await MapRequestByIdAsync(entity.Id, cancellationToken));
    }

    public async Task<Result<FirmLeaveRequestDto>> ProcessAsync(
        Guid firmTenantId, Guid processorUserId, string processorName, Guid id, ProcessFirmLeaveDto dto, CancellationToken cancellationToken = default)
    {
        var entity = await _db.FirmLeaveRequests.FirstOrDefaultAsync(r => r.Id == id && r.FirmTenantId == firmTenantId, cancellationToken);
        if (entity is null)
            return Result.Failure<FirmLeaveRequestDto>(Error.NotFound("Leave", id));

        var leaveType = await _db.FirmLeaveTypes.FirstAsync(t => t.Id == entity.LeaveTypeId, cancellationToken);

        if (dto.Approve)
        {
            if (leaveType.DeductsBalance)
            {
                var year = entity.StartDate.Year;
                var remaining = await ComputeRemainingAsync(firmTenantId, entity.UserId, year, cancellationToken);
                if (entity.Days > remaining)
                    return Result.Failure<FirmLeaveRequestDto>(Error.Validation("Balance",
                        $"Solde insuffisant ({remaining:0.##} j. restants pour {entity.Days:0.##} j. demandés)."));
            }

            var approve = entity.Approve(processorUserId, processorName);
            if (approve.IsFailure) return Result.Failure<FirmLeaveRequestDto>(approve.Error);
        }
        else
        {
            var reject = entity.Reject(processorUserId, processorName, dto.RejectionReason);
            if (reject.IsFailure) return Result.Failure<FirmLeaveRequestDto>(reject.Error);
        }

        entity.SetAuditInfo(processorUserId.ToString(), isUpdate: true);
        await _db.SaveChangesAsync(cancellationToken);
        return Result.Success(await MapRequestByIdAsync(entity.Id, cancellationToken));
    }

    public async Task<IReadOnlyList<FirmLeaveBalanceDto>> ListBalancesAsync(
        Guid firmTenantId, int year, bool isManager, Guid actorUserId, CancellationToken cancellationToken = default)
    {
        await EnsureDefaultsAsync(firmTenantId, year, cancellationToken);
        var settings = await GetOrCreateSettingsAsync(firmTenantId, year, cancellationToken);

        var users = await _db.Users.AsNoTracking()
            .Where(u => u.TenantId == firmTenantId && u.IsActive)
            .OrderBy(u => u.LastName).ThenBy(u => u.FirstName)
            .ToListAsync(cancellationToken);

        if (!isManager)
            users = users.Where(u => u.Id == actorUserId).ToList();

        var balanceRows = await _db.FirmLeaveBalances.AsNoTracking()
            .Where(b => b.FirmTenantId == firmTenantId && b.Year == year)
            .ToListAsync(cancellationToken);
        var balanceMap = balanceRows.ToDictionary(b => b.UserId);

        var consumedMap = await GetConsumedByUserAsync(firmTenantId, year, cancellationToken);

        return users.Select(u =>
        {
            balanceMap.TryGetValue(u.Id, out var bal);
            var opening = bal?.OpeningBalanceDays ?? settings.DefaultAnnualPaidDays;
            var adjustment = bal?.AdjustmentDays ?? 0m;
            var consumed = consumedMap.GetValueOrDefault(u.Id);
            return new FirmLeaveBalanceDto
            {
                Id = bal?.Id,
                UserId = u.Id,
                CollaboratorName = DisplayName(u),
                Year = year,
                OpeningBalanceDays = opening,
                AdjustmentDays = adjustment,
                EntitlementDays = settings.DefaultAnnualPaidDays,
                ConsumedDays = consumed,
                RemainingDays = FirmLeaveBalance.ComputeRemaining(opening, adjustment, consumed),
                Notes = bal?.Notes
            };
        }).ToList();
    }

    public async Task<FirmLeaveBalanceDto?> GetMyBalanceAsync(
        Guid firmTenantId, Guid userId, int year, CancellationToken cancellationToken = default)
    {
        var list = await ListBalancesAsync(firmTenantId, year, isManager: false, userId, cancellationToken);
        return list.FirstOrDefault();
    }

    public async Task<Result<FirmLeaveBalanceDto>> SetBalanceAsync(
        Guid firmTenantId, Guid userId, int year, SetFirmLeaveBalanceDto dto, CancellationToken cancellationToken = default)
    {
        await EnsureDefaultsAsync(firmTenantId, year, cancellationToken);
        var userOk = await _db.Users.AnyAsync(u => u.Id == userId && u.TenantId == firmTenantId, cancellationToken);
        if (!userOk)
            return Result.Failure<FirmLeaveBalanceDto>(Error.Validation("UserId", "Collaborateur introuvable."));

        var bal = await _db.FirmLeaveBalances.FirstOrDefaultAsync(
            b => b.FirmTenantId == firmTenantId && b.UserId == userId && b.Year == year, cancellationToken);

        if (bal is null)
        {
            var created = FirmLeaveBalance.Create(firmTenantId, userId, year, dto.OpeningBalanceDays, dto.AdjustmentDays, dto.Notes);
            if (created.IsFailure) return Result.Failure<FirmLeaveBalanceDto>(created.Error);
            _db.FirmLeaveBalances.Add(created.Value);
        }
        else
        {
            bal.Set(dto.OpeningBalanceDays, dto.AdjustmentDays, dto.Notes);
        }

        await _db.SaveChangesAsync(cancellationToken);
        var list = await ListBalancesAsync(firmTenantId, year, isManager: true, userId, cancellationToken);
        return Result.Success(list.First(b => b.UserId == userId));
    }

    public async Task<IReadOnlyList<FirmLeaveTypeDto>> ListTypesAsync(
        Guid firmTenantId, bool activeOnly, CancellationToken cancellationToken = default)
    {
        await EnsureDefaultsAsync(firmTenantId, DateTime.UtcNow.Year, cancellationToken);
        var q = _db.FirmLeaveTypes.AsNoTracking().Where(t => t.FirmTenantId == firmTenantId);
        if (activeOnly) q = q.Where(t => t.IsActive);
        var list = await q.OrderBy(t => t.SortOrder).ThenBy(t => t.Label).ToListAsync(cancellationToken);
        return list.Select(MapType).ToList();
    }

    public async Task<Result<FirmLeaveTypeDto>> UpsertTypeAsync(
        Guid firmTenantId, UpsertFirmLeaveTypeDto dto, CancellationToken cancellationToken = default)
    {
        await EnsureDefaultsAsync(firmTenantId, DateTime.UtcNow.Year, cancellationToken);

        if (dto.Id.HasValue && dto.Id.Value != Guid.Empty)
        {
            var existing = await _db.FirmLeaveTypes.FirstOrDefaultAsync(
                t => t.Id == dto.Id.Value && t.FirmTenantId == firmTenantId, cancellationToken);
            if (existing is null)
                return Result.Failure<FirmLeaveTypeDto>(Error.NotFound("LeaveType", dto.Id.Value));

            var upd = existing.Update(dto.Label, dto.ColorHex, dto.DeductsBalance, dto.RequiresApproval, dto.SortOrder);
            if (upd.IsFailure) return Result.Failure<FirmLeaveTypeDto>(upd.Error);
            if (dto.IsActive) existing.Activate(); else existing.Deactivate();
            await _db.SaveChangesAsync(cancellationToken);
            return Result.Success(MapType(existing));
        }

        var created = FirmLeaveType.Create(
            firmTenantId, dto.Code, dto.Label, dto.ColorHex, dto.DeductsBalance, dto.RequiresApproval, isSystem: false, dto.SortOrder);
        if (created.IsFailure) return Result.Failure<FirmLeaveTypeDto>(created.Error);

        var codeExists = await _db.FirmLeaveTypes.AnyAsync(
            t => t.FirmTenantId == firmTenantId && t.Code == created.Value.Code, cancellationToken);
        if (codeExists)
            return Result.Failure<FirmLeaveTypeDto>(Error.Validation("Code", "Ce code existe déjà."));

        if (!dto.IsActive) created.Value.Deactivate();
        _db.FirmLeaveTypes.Add(created.Value);
        await _db.SaveChangesAsync(cancellationToken);
        return Result.Success(MapType(created.Value));
    }

    public async Task<FirmLeaveSettingsDto> GetSettingsAsync(Guid firmTenantId, int year, CancellationToken cancellationToken = default)
    {
        await EnsureDefaultsAsync(firmTenantId, year, cancellationToken);
        var s = await GetOrCreateSettingsAsync(firmTenantId, year, cancellationToken);
        return MapSettings(s);
    }

    public async Task<Result<FirmLeaveSettingsDto>> UpdateSettingsAsync(
        Guid firmTenantId, int year, UpdateFirmLeaveSettingsDto dto, CancellationToken cancellationToken = default)
    {
        var s = await GetOrCreateSettingsAsync(firmTenantId, year, cancellationToken);
        var upd = s.Update(dto.DefaultAnnualPaidDays, dto.AllowHalfDays, dto.MinNoticeDays, dto.BlockOverlap, dto.CarryOverEnabled, dto.MaxCarryOverDays);
        if (upd.IsFailure) return Result.Failure<FirmLeaveSettingsDto>(upd.Error);
        await _db.SaveChangesAsync(cancellationToken);
        return Result.Success(MapSettings(s));
    }

    public async Task<Result<(byte[] Content, string FileName)>> ExportListAsync(
        Guid firmTenantId, int? year, int? status, Guid? typeId, CancellationToken cancellationToken = default)
    {
        var list = await ListRequestsAsync(firmTenantId, isManager: true, Guid.Empty, null, status, typeId, null, null, year, cancellationToken);
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Demandes");
        ws.Cell(1, 1).Value = "Collaborateur";
        ws.Cell(1, 2).Value = "Date début";
        ws.Cell(1, 3).Value = "Date fin";
        ws.Cell(1, 4).Value = "Nb jours";
        ws.Cell(1, 5).Value = "Date demande";
        ws.Cell(1, 6).Value = "Type";
        ws.Cell(1, 7).Value = "Traité par";
        ws.Cell(1, 8).Value = "Statut";
        ws.Cell(1, 9).Value = "Motif";
        ws.Range(1, 1, 1, 9).Style.Font.Bold = true;

        var row = 2;
        foreach (var r in list)
        {
            ws.Cell(row, 1).Value = r.CollaboratorName;
            ws.Cell(row, 2).Value = r.StartDate;
            ws.Cell(row, 3).Value = r.EndDate;
            ws.Cell(row, 4).Value = r.Days;
            ws.Cell(row, 5).Value = r.SubmittedAt ?? r.CreatedAt;
            ws.Cell(row, 6).Value = r.LeaveTypeLabel;
            ws.Cell(row, 7).Value = r.ProcessedByName ?? "";
            ws.Cell(row, 8).Value = r.StatusDisplay;
            ws.Cell(row, 9).Value = r.Reason ?? "";
            row++;
        }
        ws.Columns().AdjustToContents();
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return Result.Success((ms.ToArray(), $"conges_liste_{DateTime.UtcNow:yyyyMMdd_HHmmss}.xlsx"));
    }

    public async Task<Result<(byte[] Content, string FileName)>> ExportSynthesisAsync(
        Guid firmTenantId, int year, CancellationToken cancellationToken = default)
    {
        var balances = await ListBalancesAsync(firmTenantId, year, isManager: true, Guid.Empty, cancellationToken);
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Synthèse");
        ws.Cell(1, 1).Value = "Collaborateur";
        ws.Cell(1, 2).Value = "Ouverture";
        ws.Cell(1, 3).Value = "Ajustement";
        ws.Cell(1, 4).Value = "Consommé";
        ws.Cell(1, 5).Value = "Restant";
        ws.Range(1, 1, 1, 5).Style.Font.Bold = true;
        var row = 2;
        foreach (var b in balances)
        {
            ws.Cell(row, 1).Value = b.CollaboratorName;
            ws.Cell(row, 2).Value = b.OpeningBalanceDays;
            ws.Cell(row, 3).Value = b.AdjustmentDays;
            ws.Cell(row, 4).Value = b.ConsumedDays;
            ws.Cell(row, 5).Value = b.RemainingDays;
            row++;
        }
        ws.Columns().AdjustToContents();
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return Result.Success((ms.ToArray(), $"conges_synthese_{year}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.xlsx"));
    }

    private async Task<FirmLeaveSettings> GetOrCreateSettingsAsync(Guid firmTenantId, int year, CancellationToken ct)
    {
        var s = await _db.FirmLeaveSettings.FirstOrDefaultAsync(x => x.FirmTenantId == firmTenantId && x.Year == year, ct);
        if (s is not null) return s;
        await EnsureDefaultsAsync(firmTenantId, year, ct);
        return await _db.FirmLeaveSettings.FirstAsync(x => x.FirmTenantId == firmTenantId && x.Year == year, ct);
    }

    private async Task<bool> HasOverlapAsync(
        Guid firmTenantId, Guid userId, DateTime start, DateTime end, Guid? excludeId, CancellationToken ct)
    {
        var startD = start.Date;
        var endD = end.Date;
        var blocking = new[] { FirmLeaveRequestStatus.Draft, FirmLeaveRequestStatus.Submitted, FirmLeaveRequestStatus.Approved };
        var q = _db.FirmLeaveRequests.AsNoTracking()
            .Where(r => r.FirmTenantId == firmTenantId
                        && r.UserId == userId
                        && blocking.Contains(r.Status)
                        && r.StartDate <= endD && r.EndDate >= startD);
        if (excludeId.HasValue)
            q = q.Where(r => r.Id != excludeId.Value);
        return await q.AnyAsync(ct);
    }

    private async Task<decimal> ComputeRemainingAsync(Guid firmTenantId, Guid userId, int year, CancellationToken ct)
    {
        var settings = await GetOrCreateSettingsAsync(firmTenantId, year, ct);
        var bal = await _db.FirmLeaveBalances.AsNoTracking()
            .FirstOrDefaultAsync(b => b.FirmTenantId == firmTenantId && b.UserId == userId && b.Year == year, ct);
        var opening = bal?.OpeningBalanceDays ?? settings.DefaultAnnualPaidDays;
        var adjustment = bal?.AdjustmentDays ?? 0m;
        var consumedMap = await GetConsumedByUserAsync(firmTenantId, year, ct);
        var consumed = consumedMap.GetValueOrDefault(userId);
        return FirmLeaveBalance.ComputeRemaining(opening, adjustment, consumed);
    }

    private async Task<Dictionary<Guid, decimal>> GetConsumedByUserAsync(Guid firmTenantId, int year, CancellationToken ct)
    {
        var rows = await (from r in _db.FirmLeaveRequests.AsNoTracking()
                          join t in _db.FirmLeaveTypes.AsNoTracking() on r.LeaveTypeId equals t.Id
                          where r.FirmTenantId == firmTenantId
                                && r.Status == FirmLeaveRequestStatus.Approved
                                && t.DeductsBalance
                                && r.StartDate.Year == year
                          select new { r.UserId, r.Days }).ToListAsync(ct);

        return rows.GroupBy(x => x.UserId).ToDictionary(g => g.Key, g => Math.Round(g.Sum(x => x.Days), 2));
    }

    private static Error? CheckMinNotice(FirmLeaveSettings settings, DateTime startDate)
    {
        if (settings.MinNoticeDays <= 0) return null;
        var minStart = DateTime.UtcNow.Date.AddDays(settings.MinNoticeDays);
        if (startDate.Date < minStart)
            return Error.Validation("MinNotice", $"Un préavis de {settings.MinNoticeDays} jour(s) est requis.");
        return null;
    }

    private async Task<FirmLeaveRequestDto> MapRequestByIdAsync(Guid id, CancellationToken ct)
    {
        var row = await (from r in _db.FirmLeaveRequests.AsNoTracking()
                         join t in _db.FirmLeaveTypes.AsNoTracking() on r.LeaveTypeId equals t.Id
                         join u in _db.Users.AsNoTracking() on r.UserId equals u.Id
                         where r.Id == id
                         select new { r, t, u }).FirstAsync(ct);
        return MapRequest(row.r, row.t, DisplayName(row.u));
    }

    private static FirmLeaveRequestDto MapRequest(FirmLeaveRequest r, FirmLeaveType t, string name) => new()
    {
        Id = r.Id,
        UserId = r.UserId,
        CollaboratorName = name,
        LeaveTypeId = t.Id,
        LeaveTypeCode = t.Code,
        LeaveTypeLabel = t.Label,
        LeaveTypeColorHex = t.ColorHex,
        DeductsBalance = t.DeductsBalance,
        StartDate = r.StartDate,
        EndDate = r.EndDate,
        StartUnit = (int)r.StartUnit,
        EndUnit = (int)r.EndUnit,
        Days = r.Days,
        Reason = r.Reason,
        Status = (int)r.Status,
        StatusDisplay = StatusLabel(r.Status),
        CreatedAt = r.CreatedAt,
        SubmittedAt = r.SubmittedAt,
        ProcessedAt = r.ProcessedAt,
        ProcessedByUserId = r.ProcessedByUserId,
        ProcessedByName = r.ProcessedByName,
        RejectionReason = r.RejectionReason
    };

    private static FirmLeaveTypeDto MapType(FirmLeaveType t) => new()
    {
        Id = t.Id,
        Code = t.Code,
        Label = t.Label,
        ColorHex = t.ColorHex,
        DeductsBalance = t.DeductsBalance,
        RequiresApproval = t.RequiresApproval,
        IsSystem = t.IsSystem,
        IsActive = t.IsActive,
        SortOrder = t.SortOrder
    };

    private static FirmLeaveSettingsDto MapSettings(FirmLeaveSettings s) => new()
    {
        Id = s.Id,
        Year = s.Year,
        DefaultAnnualPaidDays = s.DefaultAnnualPaidDays,
        AllowHalfDays = s.AllowHalfDays,
        MinNoticeDays = s.MinNoticeDays,
        BlockOverlap = s.BlockOverlap,
        CarryOverEnabled = s.CarryOverEnabled,
        MaxCarryOverDays = s.MaxCarryOverDays
    };

    private static string DisplayName(ApplicationUser u) => $"{u.FirstName} {u.LastName}".Trim();

    private static string StatusLabel(FirmLeaveRequestStatus s) => s switch
    {
        FirmLeaveRequestStatus.Draft => "Brouillon",
        FirmLeaveRequestStatus.Submitted => "En attente",
        FirmLeaveRequestStatus.Approved => "Acceptée",
        FirmLeaveRequestStatus.Rejected => "Refusée",
        FirmLeaveRequestStatus.Cancelled => "Annulée",
        _ => s.ToString()
    };
}
