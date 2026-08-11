namespace FactuTrust.Application.Common.Interfaces;

public interface IFirmGovernanceFeature
{
    bool IsEnabled { get; }
    bool IsInternalPayrollEnabled { get; }
}
