using System.Globalization;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Application.Features.Accounting.Queries;

/// <summary>
/// Fabriques partagées pour les exports d'états comptables (libellés, en-tête PDF).
/// Centralise la construction pour garantir des sorties homogènes entre états.
/// </summary>
internal static class AccountingExportHelpers
{
    public static string KindLabel(ThirdPartyKind kind) =>
        kind == ThirdPartyKind.Supplier ? "Fournisseurs" : "Clients";

    public static string PeriodRange(DateTime from, DateTime to) =>
        $"Du {from.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)} au {to.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)}";

    public static AccountingReportHeader Header(Company? company, string title, string periodLabel) =>
        new(company?.Name ?? "Société", company?.VatCode, title, periodLabel);
}
