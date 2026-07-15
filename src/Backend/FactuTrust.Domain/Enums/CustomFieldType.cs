namespace FactuTrust.Domain.Enums;

/// <summary>
/// Field types available when designing a custom table (entity) in the low-code Studio.
/// Stored as an int on <c>CustomFieldDefinition</c>; values are append-only to stay migration-safe.
/// </summary>
public enum CustomFieldType
{
    /// <summary>Single-line text.</summary>
    Text = 0,

    /// <summary>Multi-line text.</summary>
    MultilineText = 1,

    /// <summary>Whole number.</summary>
    Number = 2,

    /// <summary>Decimal number.</summary>
    Decimal = 3,

    /// <summary>Boolean (yes/no).</summary>
    Boolean = 4,

    /// <summary>Date only.</summary>
    Date = 5,

    /// <summary>Date and time.</summary>
    DateTime = 6,

    /// <summary>Single choice from a fixed option list (stored in OptionsJson).</summary>
    Select = 7,

    /// <summary>Multiple choices from a fixed option list (stored in OptionsJson).</summary>
    MultiSelect = 8,

    /// <summary>Reference to a record of another custom entity (target key in OptionsJson).</summary>
    RelationCustom = 9,

    /// <summary>Read-only reference into a whitelisted existing source (e.g. Client). Source key in OptionsJson.</summary>
    RelationExisting = 10,

    /// <summary>Monetary amount (decimal) with a currency code (OptionsJson <c>money.currency</c>).</summary>
    Money = 11,

    /// <summary>Percentage (decimal).</summary>
    Percentage = 12,

    /// <summary>Star rating (integer 0..max; OptionsJson <c>rating.max</c>).</summary>
    Rating = 13,

    /// <summary>Text value rendered as a QR code (OptionsJson <c>render</c>).</summary>
    QrCode = 14,

    /// <summary>Text value rendered as a 1D barcode (OptionsJson <c>render.format</c>).</summary>
    Barcode = 15,

    /// <summary>
    /// Sequential auto-generated reference, computed on record creation and immutable thereafter
    /// (OptionsJson <c>number.{prefix,padding,suffix}</c>). Read-only; user input is ignored.
    /// </summary>
    AutoNumber = 16,

    /// <summary>
    /// Value computed from a sandboxed expression over other fields (OptionsJson <c>formula.expr</c>).
    /// Re-evaluated on every write (compute-on-write); read-only, user input is ignored.
    /// </summary>
    Formula = 17,

    /// <summary>
    /// Shows a field from a related record/source via a relation field on this entity
    /// (OptionsJson <c>lookup.{via,target}</c>). Resolved fresh on read (compute-on-read); not stored.
    /// </summary>
    Lookup = 18,

    /// <summary>
    /// Aggregate (count/sum/avg/min/max) over child records that reference this record
    /// (OptionsJson <c>rollup.{entity,relationField,agg,field}</c>). Computed on read; not stored.
    /// </summary>
    Rollup = 19,

    /// <summary>Uploaded file; the field stores the relative URL of the stored file (tenant-scoped local storage).</summary>
    Attachment = 20,

    /// <summary>Hand-drawn signature captured as a PNG; the field stores the relative URL of the image.</summary>
    Signature = 21
}
