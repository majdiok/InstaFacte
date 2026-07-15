namespace FactuTrust.Application.Features.AI.Export.PowerPoint.Models;

/// <summary>Cover slide visual style metadata for the UI catalogue.</summary>
public enum CoverLayoutStyle
{
    ClassicBar = 0,
    MinimalSerif = 1,
    DarkCinematic = 2,
    GradientHero = 3,
    SplitPhoto = 4,
    BoldEditorial = 5,
    CorporateBlue = 6,
    MedicalClean = 7,

    /// <summary>Twin neon bars (top + bottom) plus a corner glow — cyber/tech feel.</summary>
    NeonAccent = 8,

    /// <summary>Narrow side band plus two thin horizontal rules — editorial magazine.</summary>
    EditorialMagazine = 9,

    /// <summary>40% accent block on the right plus a thick accent bar — asymmetric corporate.</summary>
    AsymmetricSplit = 10,

    /// <summary>Stacked color stripes simulating a multi-stop gradient hero.</summary>
    GradientWaveHero = 11
}
