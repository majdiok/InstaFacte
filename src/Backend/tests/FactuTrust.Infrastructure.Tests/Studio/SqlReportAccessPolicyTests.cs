using FactuTrust.Application.Features.Studio.Common;
using FactuTrust.Application.Features.Studio.Common.SqlReport;
using FactuTrust.Domain.Enums;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Studio;

/// <summary>
/// Garde de sécurité du moteur d'états : une table non classée est refusée, chaque domaine exige sa
/// permission, et les colonnes sensibles ne sont jamais projetées.
/// </summary>
public sealed class SqlReportAccessPolicyTests
{
    private static Func<string, bool> Grants(params string[] permissions)
    {
        var set = new HashSet<string>(permissions, StringComparer.Ordinal);
        return p => set.Contains(p);
    }

    [Fact]
    public void An_unclassified_table_is_denied()
    {
        Assert.Null(SqlReportAccessPolicy.Describe("UneTableQuiNExistePas"));
        Assert.False(SqlReportAccessPolicy.TryAuthorize(
            "UneTableQuiNExistePas", _ => true, out var access, out var error));
        Assert.Null(access);
        Assert.NotNull(error);
    }

    [Theory]
    [InlineData("AuditLogs")]
    [InlineData("CustomRecords")]
    [InlineData("StudioAiBuildPlans")]
    [InlineData("CustomSystemDefinitions")]
    [InlineData("__EFMigrationsHistory")]
    public void Denylisted_tables_are_never_classified(string table)
    {
        Assert.Null(SqlReportAccessPolicy.Describe(table));
    }

    [Fact]
    public void Base_reporting_permission_is_required_even_with_the_domain_permission()
    {
        Assert.False(SqlReportAccessPolicy.TryAuthorize(
            "InvoiceLines", Grants(Permissions.Invoices.Read), out _, out var error));
        Assert.Contains("états", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Sales_lines_need_the_invoices_permission()
    {
        Assert.False(SqlReportAccessPolicy.TryAuthorize(
            "InvoiceLines", Grants(SqlReportAccessPolicy.BasePermission), out _, out _));

        Assert.True(SqlReportAccessPolicy.TryAuthorize(
            "InvoiceLines",
            Grants(SqlReportAccessPolicy.BasePermission, Permissions.Invoices.Read),
            out var access,
            out _));
        Assert.Equal(ReportDomain.Ventes, access!.Domain);
    }

    [Fact]
    public void Payroll_tables_stay_closed_without_the_payroll_permission()
    {
        var salesUser = Grants(
            SqlReportAccessPolicy.BasePermission,
            Permissions.Invoices.Read,
            Permissions.Clients.Read,
            Permissions.Products.Read);

        Assert.False(SqlReportAccessPolicy.TryAuthorize("Payslips", salesUser, out _, out var error));
        Assert.Contains("Paie", error, StringComparison.OrdinalIgnoreCase);

        Assert.DoesNotContain(
            SqlReportAccessPolicy.AllowedFor(salesUser),
            a => a.Domain == ReportDomain.Paie);
    }

    [Fact]
    public void Accounting_tables_require_the_accounting_permission()
    {
        Assert.False(SqlReportAccessPolicy.TryAuthorize(
            "JournalEntryLines", Grants(SqlReportAccessPolicy.BasePermission), out _, out _));

        Assert.True(SqlReportAccessPolicy.TryAuthorize(
            "JournalEntryLines",
            Grants(SqlReportAccessPolicy.BasePermission, Permissions.Accounting.Read),
            out var access,
            out _));
        Assert.Equal(ReportDomain.Comptabilite, access!.Domain);
    }

    [Fact]
    public void Without_the_base_permission_nothing_is_listed()
    {
        Assert.Empty(SqlReportAccessPolicy.AllowedFor(Grants(Permissions.Payroll.Read)));
    }

    [Fact]
    public void Allowed_sources_are_grouped_by_domain()
    {
        var everything = SqlReportAccessPolicy.AllowedFor(_ => true);
        Assert.NotEmpty(everything);
        Assert.DoesNotContain(everything, a => a.Domain == ReportDomain.Inconnu);
        // Trié par domaine : l'ordre alimente directement le sélecteur de source et le prompt.
        Assert.Equal(everything.OrderBy(a => a.Domain).Select(a => a.Domain), everything.Select(a => a.Domain));
    }

    [Theory]
    [InlineData("PasswordHash")]
    [InlineData("SecurityStamp")]
    [InlineData("RowVersion")]
    [InlineData("ApiKeyEncrypted")]
    [InlineData("RefreshToken")]
    public void Sensitive_columns_are_filtered_out(string column)
    {
        var columns = new[]
        {
            new SqlColumnInfo("Name", "nvarchar", false, false),
            new SqlColumnInfo(column, "nvarchar", true, false)
        };

        var kept = SqlReportAccessPolicy.FilterColumns(columns);
        Assert.Single(kept);
        Assert.Equal("Name", kept[0].Name);
    }

    [Fact]
    public void Ordinary_columns_survive_filtering()
    {
        var columns = new[]
        {
            new SqlColumnInfo("Quantity", "decimal", false, true),
            new SqlColumnInfo("IssueDate", "datetime2", false, false)
        };

        Assert.Equal(2, SqlReportAccessPolicy.FilterColumns(columns).Count);
    }

    [Fact]
    public void Project_tables_require_project_permissions()
    {
        Assert.False(SqlReportAccessPolicy.TryAuthorize(
            "Projects", Grants(SqlReportAccessPolicy.BasePermission), out _, out _));

        Assert.True(SqlReportAccessPolicy.TryAuthorize(
            "Projects",
            Grants(SqlReportAccessPolicy.BasePermission, Permissions.Projects.Read),
            out var access,
            out _));
        Assert.Equal(ReportDomain.Projets, access!.Domain);

        Assert.True(SqlReportAccessPolicy.TryAuthorize(
            "ProjectTimeEntries",
            Grants(SqlReportAccessPolicy.BasePermission, Permissions.ProjectTime.Read),
            out var timeAccess,
            out _));
        Assert.Equal(ReportDomain.Projets, timeAccess!.Domain);
    }

    [Fact]
    public void Unknown_project_like_table_is_denied()
    {
        Assert.Null(SqlReportAccessPolicy.Describe("ProjectSecretStuff"));
        Assert.False(SqlReportAccessPolicy.TryAuthorize("ProjectSecretStuff", _ => true, out _, out var error));
        Assert.NotNull(error);
    }
}
