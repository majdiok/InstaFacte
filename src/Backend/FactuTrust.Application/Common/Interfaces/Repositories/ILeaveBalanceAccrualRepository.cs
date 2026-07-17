using FactuTrust.Domain.Entities.Payroll;



namespace FactuTrust.Application.Common.Interfaces.Repositories;



public interface ILeaveBalanceAccrualRepository

{

    Task<LeaveBalanceAccrual?> GetByEmployeePeriodAsync(

        Guid employeeId,

        int year,

        int month,

        CancellationToken cancellationToken = default);



    Task<IReadOnlyList<LeaveBalanceAccrual>> ListByEmployeeAndYearAsync(

        Guid employeeId,

        int year,

        CancellationToken cancellationToken = default);



    Task<IReadOnlyList<LeaveBalanceAccrual>> ListByPayrollRunIdAsync(

        Guid payrollRunId,

        CancellationToken cancellationToken = default);



    Task<IReadOnlyList<LeaveBalanceAccrual>> GetByEmployeePeriodsAsync(
        IReadOnlyCollection<Guid> employeeIds,
        int year,
        int month,
        CancellationToken cancellationToken = default);

    Task<LeaveBalanceAccrual> AddAsync(LeaveBalanceAccrual entity, CancellationToken cancellationToken = default);

    Task AddRangeAsync(IReadOnlyList<LeaveBalanceAccrual> entities, CancellationToken cancellationToken = default);



    Task DeleteByPayrollRunIdAsync(Guid payrollRunId, CancellationToken cancellationToken = default);

}


