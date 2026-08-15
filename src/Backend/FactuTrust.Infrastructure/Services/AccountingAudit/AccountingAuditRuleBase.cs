using System.Security.Cryptography;
using System.Text;
using FactuTrust.Application.Configuration;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.Features.Accounting.Audit;
using FactuTrust.Domain.Entities.AccountingAudit;
using FactuTrust.Domain.Enums;
using FactuTrust.Infrastructure.Persistence;

namespace FactuTrust.Infrastructure.Services.AccountingAudit;

public sealed class AuditEvaluationContextImpl : IAuditEvaluationContext
{
    private readonly TenantDbContext _db;

    public AuditEvaluationContextImpl(
        TenantDbContext db,
        AccountingSettings settings,
        int fiscalYear,
        DateOnly periodFrom,
        DateOnly periodTo,
        IReadOnlyList<AccountingControlRuleSetting> ruleSettings,
        IReadOnlySet<string>? moduleCodesFilter)
    {
        _db = db;
        Settings = settings;
        FiscalYear = fiscalYear;
        PeriodFrom = periodFrom;
        PeriodTo = periodTo;
        RuleSettings = ruleSettings;
        ModuleCodesFilter = moduleCodesFilter;
    }

    public int FiscalYear { get; }
    public DateOnly PeriodFrom { get; }
    public DateOnly PeriodTo { get; }
    public DateTime YearEnd => new(FiscalYear, 12, 31);
    public AccountingSettings Settings { get; }
    public IReadOnlyList<AccountingControlRuleSetting> RuleSettings { get; }
    public IReadOnlySet<string>? ModuleCodesFilter { get; }

    public TenantDbContext Db => _db;

    public AccountingControlRuleSetting? GetRuleSetting(string code) =>
        RuleSettings.FirstOrDefault(s => s.RuleCode == code);

    public bool IsModuleInScope(string moduleCode) =>
        ModuleCodesFilter is null || ModuleCodesFilter.Count == 0 || ModuleCodesFilter.Contains(moduleCode);
}

public static class AuditFingerprint
{
    public static string Build(string ruleCode, params string?[] parts)
    {
        var raw = ruleCode + "|" + string.Join("|", parts.Select(p => p ?? ""));
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(hash);
    }
}

public abstract class AccountingAuditRuleBase : IAccountingAuditRule
{
    public abstract string Code { get; }
    public abstract string ModuleCode { get; }
    public abstract int Category { get; }
    public abstract int DefaultSeverity { get; }

    public virtual bool IsEnabled(AccountingSettings settings, AccountingControlRuleSetting? tenantOverride)
    {
        if (tenantOverride is { IsEnabled: false })
            return false;
        return true;
    }

    public abstract Task<IReadOnlyList<AnomalyCandidate>> EvaluateAsync(
        IAuditEvaluationContext ctx,
        CancellationToken cancellationToken);

    protected static AnomalyCandidate SingleGroup(
        string ruleCode,
        string moduleCode,
        int category,
        int severity,
        string title,
        string description,
        string impact,
        string? accountRef,
        decimal amount,
        DateOnly? periodFrom,
        DateOnly? periodTo,
        IReadOnlyList<AnomalyLineCandidate> lines,
        IReadOnlyList<string> recommendations,
        string? deepLinkRoute) =>
        SingleGroup(ruleCode, moduleCode, category, severity, title, description, impact,
            accountRef, amount, periodFrom, periodTo, lines, recommendations, deepLinkRoute,
            discriminator: null);

    /// <summary>
    /// Variante avec <paramref name="discriminator"/> : identifiant métier qui distingue deux
    /// anomalies de la même règle sur la même période.
    ///
    /// <para><b>À utiliser dès qu'une règle émet plus d'une anomalie par compte et par période.</b>
    /// <c>AccountingAnomaly.Fingerprint</c> porte un index UNIQUE et l'empreinte de base ne combine
    /// que la règle, le compte et la période : deux fournisseurs en doublon, ou deux salariés hors
    /// régime, produiraient la même empreinte et la seconde anomalie écraserait la première.
    /// Passer ici l'identifiant du fournisseur, le matricule, le numéro de pièce ou la date
    /// concernée.</para>
    ///
    /// <para>Le discriminant doit être <b>stable d'un run à l'autre</b> : c'est ce qui permet de
    /// reconnaître la même anomalie et de conserver son statut (assignée, ignorée) au lieu d'en
    /// recréer une neuve à chaque contrôle. Ne jamais y mettre une date d'exécution ni un GUID
    /// nouvellement tiré.</para>
    /// </summary>
    protected static AnomalyCandidate SingleGroup(
        string ruleCode,
        string moduleCode,
        int category,
        int severity,
        string title,
        string description,
        string impact,
        string? accountRef,
        decimal amount,
        DateOnly? periodFrom,
        DateOnly? periodTo,
        IReadOnlyList<AnomalyLineCandidate> lines,
        IReadOnlyList<string> recommendations,
        string? deepLinkRoute,
        string? discriminator)
    {
        // Sans discriminant, l'empreinte doit rester STRICTEMENT identique à l'historique : ajouter
        // un séparateur vide changerait le hachage de toutes les anomalies déjà en base, qui
        // seraient alors auto-résolues à tort puis recréées à neuf, perdant statut et affectation.
        var fp = string.IsNullOrEmpty(discriminator)
            ? AuditFingerprint.Build(ruleCode, accountRef, periodFrom?.ToString(), periodTo?.ToString())
            : AuditFingerprint.Build(ruleCode, accountRef, periodFrom?.ToString(), periodTo?.ToString(), discriminator);

        return new AnomalyCandidate(
            fp, ruleCode, moduleCode, category, severity,
            title, description, impact, accountRef, amount,
            periodFrom, periodTo, lines, recommendations, deepLinkRoute);
    }
}
