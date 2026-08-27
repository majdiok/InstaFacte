namespace FactuTrust.Application.Features.RecurringContracts;

/// <summary>
/// Clé d'idempotence stable par run : un double-clic « Valider » ne réserve pas un second FAC.
/// Doit rester ≥ 32 caractères (contrat <c>SubmitInvoiceCommand</c> / wizard).
/// </summary>
public static class RecurringBillingIssueKeys
{
    public static string ForRun(Guid billingRunId) => $"recurring-issue-{billingRunId:N}";
}
