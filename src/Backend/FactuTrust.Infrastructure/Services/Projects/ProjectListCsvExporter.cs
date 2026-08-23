using System.Globalization;
using System.Text;
using FactuTrust.Application.DTOs;

namespace FactuTrust.Infrastructure.Services.Projects;

/// <summary>CSV UTF-8 BOM export for project list (Excel FR).</summary>
public static class ProjectListCsvExporter
{
    public const int MaxRows = 1000;

    private static readonly byte[] Utf8Bom = [0xEF, 0xBB, 0xBF];

    public static byte[] ToCsvBytes(IReadOnlyList<ProjectListItemDto> items)
    {
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(';', new[]
        {
            "Projet", "Client", "Chef de projet", "Type", "Statut", "Échéance",
            "Avancement %", "Tâches ouvertes", "Tâches en retard", "Budget HT"
        }));

        foreach (var p in items)
        {
            sb.AppendLine(string.Join(';', new[]
            {
                Escape(p.Name),
                Escape(p.ClientName),
                Escape(p.OwnerUserName ?? "—"),
                Escape(p.KindDisplay),
                Escape(p.StatusDisplay),
                Escape(p.EndDate?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) ?? "—"),
                p.ProgressPercent.ToString(CultureInfo.InvariantCulture),
                p.OpenTaskCount.ToString(CultureInfo.InvariantCulture),
                p.OverdueTaskCount.ToString(CultureInfo.InvariantCulture),
                p.BudgetHt.ToString("0.###", CultureInfo.InvariantCulture)
            }));
        }

        var csv = Encoding.UTF8.GetBytes(sb.ToString());
        var result = new byte[Utf8Bom.Length + csv.Length];
        Utf8Bom.CopyTo(result, 0);
        csv.CopyTo(result, Utf8Bom.Length);
        return result;
    }

    internal static string Escape(string? value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        if (value.Contains(';') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        return value;
    }
}
