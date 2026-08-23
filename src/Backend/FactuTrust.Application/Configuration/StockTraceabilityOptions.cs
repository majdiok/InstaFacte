namespace FactuTrust.Application.Configuration;

/// <summary>Feature flags for stock traceability, variants and FIFO/LIFO. All default OFF.</summary>
public sealed class StockTraceabilityOptions
{
    public const string SectionName = "Features:Stock";

    public bool LotTrackingEnabled { get; set; }
    public bool SerialTrackingEnabled { get; set; }
    public bool ExpiryTrackingEnabled { get; set; }
    public bool ProductVariantsEnabled { get; set; }
    public bool FifoLifoValuationEnabled { get; set; }
    public bool BlockExpiredLotsOnExit { get; set; } = true;
    public bool StrictTrackedAllocation { get; set; } = true;
}
