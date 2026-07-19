namespace FactuTrust.Application.Common.Enums;

/// <summary>
/// Format d'export d'un état comptable. Lié aux endpoints <c>*/export?format=</c>.
/// Le défaut est <see cref="Csv"/> pour préserver le comportement historique des endpoints existants.
/// </summary>
public enum AccountingExportFormat
{
    Csv = 0,
    Excel = 1,
    Pdf = 2
}
