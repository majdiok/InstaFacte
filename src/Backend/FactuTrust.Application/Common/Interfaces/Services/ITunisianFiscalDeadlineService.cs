using FactuTrust.Domain.Entities;

namespace FactuTrust.Application.Common.Interfaces.Services;

public interface ITunisianFiscalDeadlineService
{
    DateTime ComputeVatFilingDeadline(int periodYear, int periodMonth, TaxRegime? taxRegime = null);
    DateTime AdjustForWeekendsAndHolidays(DateTime dueDate);
}
