namespace FactuTrust.Application.Configuration;

/// <summary>
/// Runtime flags and constants for the Public 3D Virtual Street module.
/// Bound to the <c>Features:Storefront</c> section.
/// </summary>
public sealed class StorefrontOptions
{
    public const string SectionName = "Features:Storefront";

    /// <summary>Global kill-switch. When <c>false</c>, all storefront endpoints return 404/410.</summary>
    public bool Enabled { get; set; }

    /// <summary>Latest accepted version of the CGU publication terms.</summary>
    public string CurrentTermsVersion { get; set; } = "1.0.0";

    /// <summary>Root folder under <c>wwwroot</c> where public product images are stored.</summary>
    public string PublicMediaFolder { get; set; } = "public-storefronts";

    /// <summary>Hard upper bound on the number of simultaneously published storefronts (safety cap).</summary>
    public int MaxPublishedStorefronts { get; set; } = 5_000;

    /// <summary>Polling interval of the background projection sync service.</summary>
    public int ProjectionPollIntervalSeconds { get; set; } = 10;

    /// <summary>When <c>true</c>, public order submissions require a valid Turnstile / reCAPTCHA token.</summary>
    public bool OrderSubmissionRequiresCaptcha { get; set; } = true;
}
