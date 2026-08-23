namespace FactuTrust.Infrastructure.Accounting;

public sealed record SceChartAccount(
    string Number,
    string Label,
    int AccountClass,
    string? Parent,
    int NatureType,
    int Level,
    bool IsSystem,
    string Layer);
