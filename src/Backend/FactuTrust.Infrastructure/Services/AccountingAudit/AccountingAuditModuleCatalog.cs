namespace FactuTrust.Infrastructure.Services.AccountingAudit;

public sealed record AccountingAuditModuleDefinition(string Code, string Label, string Icon);

public static class AccountingAuditModuleCatalog
{
    public static readonly IReadOnlyList<AccountingAuditModuleDefinition> All =
    [
        new("integrity", "Contrôle d'intégrité", "fa-shield-halved"),
        new("reconciliation", "Rapprochements", "fa-arrows-rotate"),
        new("balance-analysis", "Analyses des soldes", "fa-scale-balanced"),
        new("vat", "Contrôle de TVA", "fa-percent"),
        new("lettering", "Lettrage automatique", "fa-link"),
        new("audit-trail", "Piste d'audit", "fa-clipboard-list"),
        new("suspense", "Comptes d'attente", "fa-hourglass-half"),
        new("analytic", "Analytique", "fa-chart-pie"),
        new("fiscal", "Fiscalité", "fa-landmark"),
        new("fixed-assets", "Immobilisations", "fa-building"),
        new("budget", "Budgétaire", "fa-coins"),
        new("liasse", "Liasse fiscale", "fa-file-contract"),
        new("bank", "Banque", "fa-building-columns"),
        new("purchases", "Achats", "fa-cart-shopping"),
        new("sales", "Ventes", "fa-receipt"),
        new("payroll", "Paie & RH", "fa-users"),
        new("treasury", "Trésorerie", "fa-wallet"),
        new("documents", "Documents", "fa-file-circle-exclamation")
    ];

    public static string? LabelFor(string moduleCode) =>
        All.FirstOrDefault(m => m.Code == moduleCode)?.Label;
}
