using FactuTrust.Application.Configuration;
using FactuTrust.Application.Features.Accounting.Audit;
using FactuTrust.Domain.Entities.AccountingAudit;

namespace FactuTrust.Application.Common.Interfaces.Services;

/// <summary>Contrat d'une règle de contrôle comptable.</summary>
public interface IAccountingAuditRule
{
    string Code { get; }
    string ModuleCode { get; }
    int Category { get; }
    int DefaultSeverity { get; }

    bool IsEnabled(AccountingSettings settings, AccountingControlRuleSetting? tenantOverride);

    Task<IReadOnlyList<AnomalyCandidate>> EvaluateAsync(
        IAuditEvaluationContext ctx,
        CancellationToken cancellationToken);
}

/// <summary>Contexte d'évaluation (abstraction pour respecter les couches).</summary>
public interface IAuditEvaluationContext
{
    int FiscalYear { get; }
    DateOnly PeriodFrom { get; }
    DateOnly PeriodTo { get; }
    DateTime YearEnd { get; }
    AccountingSettings Settings { get; }
    AccountingControlRuleSetting? GetRuleSetting(string code);
    bool IsModuleInScope(string moduleCode);
}
