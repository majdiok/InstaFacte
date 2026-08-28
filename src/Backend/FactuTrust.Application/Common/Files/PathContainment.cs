namespace FactuTrust.Application.Common.Files;

/// <summary>
/// Path-traversal containment check (CWE-22 hardening) shared by services that write user-named
/// attachments to disk under a per-tenant base directory.
///
/// A naive <c>candidate.StartsWith(basePath)</c> string check is NOT sufficient: a sibling directory
/// that happens to share the same string prefix (e.g. <c>/data/safe-other</c> starts with
/// <c>/data/safe</c>) would wrongly be treated as contained. <see cref="IsContained"/> instead uses
/// <see cref="Path.GetRelativePath(string, string)"/> and rejects the result if it escapes upward
/// (starts with <c>..</c>) or is itself rooted (which happens when the two paths share no common
/// root, e.g. different drives on Windows).
/// </summary>
public static class PathContainment
{
    public static bool IsContained(string basePath, string candidateFullPath)
    {
        var relative = Path.GetRelativePath(basePath, candidateFullPath);
        if (relative.StartsWith("..", StringComparison.Ordinal))
            return false;
        if (Path.IsPathRooted(relative))
            return false;
        return true;
    }
}
