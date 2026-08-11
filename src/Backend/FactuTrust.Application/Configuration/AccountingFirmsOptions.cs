namespace FactuTrust.Application.Configuration;

public sealed class AccountingFirmsOptions
{
    public const string SectionName = "Features:AccountingFirms";

    public bool Enabled { get; set; }

    /// <summary>
    /// When true, firm users in delegated client context receive <c>ai:chat</c> and
    /// <see cref="Enums.AppModule.AI"/> for accounting assistant and document import.
    /// </summary>
    public bool AiAccountingEnabled { get; set; } = true;
}
