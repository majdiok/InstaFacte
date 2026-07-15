using FactuTrust.Domain.Entities.Payroll;



namespace FactuTrust.Application.Common.Interfaces.Repositories;



public interface IPayrollOvertimeRepository

{

    Task<PayrollOvertimeLine?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);



    Task<IReadOnlyList<PayrollOvertimeLine>> ListForMonthAsync(int year, int month, CancellationToken cancellationToken = default);



    Task<IReadOnlyList<PayrollOvertimeLine>> ListByEmployeeAndMonthAsync(

        Guid employeeId,

        int year,

        int month,

        CancellationToken cancellationToken = default);



    Task<PayrollOvertimeLine> AddAsync(PayrollOvertimeLine entity, CancellationToken cancellationToken = default);



    Task UpdateAsync(PayrollOvertimeLine entity, CancellationToken cancellationToken = default);



    Task DeleteAsync(PayrollOvertimeLine entity, CancellationToken cancellationToken = default);

}


