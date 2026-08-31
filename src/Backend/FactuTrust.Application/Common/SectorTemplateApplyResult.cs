namespace FactuTrust.Application.Common;

/// <summary>One template already applied (or newly applied) to the tenant during this run.</summary>
public sealed record AppliedTemplateInfo(string Code, int Version);

/// <summary>
/// Outcome of a single template item evaluation — "created" (row inserted), "existing" (row
/// already present, left untouched) or "skipped" (unknown item kind, or dry-run).
/// </summary>
public sealed record TemplateItemOutcome(string TemplateCode, string ItemKind, string Outcome);

/// <summary>
/// Result of <c>ISectorDataTemplateApplier.ApplyAsync</c> (plan §WP-B6). Additive-only: nothing in
/// here ever implies a row was updated or deleted.
/// </summary>
public sealed record SectorTemplateApplyResult
{
    public required IReadOnlyList<AppliedTemplateInfo> Applied { get; init; }
    public required IReadOnlyList<AppliedTemplateInfo> Skipped { get; init; }
    public required IReadOnlyList<TemplateItemOutcome> ItemOutcomes { get; init; }
    public required IReadOnlyList<string> Warnings { get; init; }

    public static SectorTemplateApplyResult Empty { get; } = new()
    {
        Applied = Array.Empty<AppliedTemplateInfo>(),
        Skipped = Array.Empty<AppliedTemplateInfo>(),
        ItemOutcomes = Array.Empty<TemplateItemOutcome>(),
        Warnings = Array.Empty<string>()
    };
}
