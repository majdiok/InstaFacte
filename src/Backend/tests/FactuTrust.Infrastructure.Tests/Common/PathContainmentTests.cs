using FactuTrust.Application.Common.Files;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Common;

/// <summary>
/// CWE-22 hardening: <see cref="PathContainment.IsContained"/> backs the Honoraires attachment
/// storage path check. A naive <c>StartsWith(basePath)</c> would wrongly accept a sibling directory
/// that shares the same string prefix — these tests pin down both the traversal-neutralized case and
/// the sibling-directory-prefix case explicitly called out in the remediation plan.
/// </summary>
public sealed class PathContainmentTests
{
    [Fact]
    public void Rejects_traversal_outside_base_directory()
    {
        var basePath = Path.Combine(Path.GetTempPath(), "ft-attachments-base");
        var candidate = Path.GetFullPath(Path.Combine(basePath, "..", "..", "etc", "passwd"));

        Assert.False(PathContainment.IsContained(basePath, candidate));
    }

    [Fact]
    public void Rejects_sibling_directory_sharing_a_string_prefix()
    {
        // "/tmp/safe-other" starts with "/tmp/safe" as a STRING, but is not contained in it as a PATH.
        var basePath = Path.Combine(Path.GetTempPath(), "safe");
        var candidate = Path.Combine(Path.GetTempPath(), "safe-other", "secret.txt");

        Assert.False(PathContainment.IsContained(basePath, candidate));
    }

    [Fact]
    public void Accepts_a_path_genuinely_nested_under_base()
    {
        var basePath = Path.Combine(Path.GetTempPath(), "ft-attachments-base");
        var candidate = Path.Combine(basePath, "tenants", "abc", "file.pdf");

        Assert.True(PathContainment.IsContained(basePath, candidate));
    }

    [Fact]
    public void Accepts_base_path_itself()
    {
        var basePath = Path.Combine(Path.GetTempPath(), "ft-attachments-base");

        Assert.True(PathContainment.IsContained(basePath, basePath));
    }
}
