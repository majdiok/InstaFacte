using FactuTrust.Application.Common.Interfaces.Services;

namespace FactuTrust.Infrastructure.Services.AccountingAudit;

public sealed class AccountingAuditRuleRegistry
{
    private readonly IReadOnlyList<IAccountingAuditRule> _rules;

    public AccountingAuditRuleRegistry(IEnumerable<IAccountingAuditRule> rules) =>
        _rules = rules.ToList();

    public IReadOnlyList<IAccountingAuditRule> GetAll() => _rules;

    public IAccountingAuditRule? GetByCode(string code) =>
        _rules.FirstOrDefault(r => string.Equals(r.Code, code, StringComparison.Ordinal));
}
