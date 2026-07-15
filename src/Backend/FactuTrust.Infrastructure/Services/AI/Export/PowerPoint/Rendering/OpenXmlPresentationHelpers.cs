using A = DocumentFormat.OpenXml.Drawing;
using P = DocumentFormat.OpenXml.Presentation;

namespace FactuTrust.Infrastructure.Services.AI.Export.PowerPoint.Rendering;

/// <summary>
/// Low-level OpenXml helpers shared by every slide builder. Centralising creation of
/// shapes/text-frames/runs ensures consistent formatting and prevents subtle XML mistakes that
/// would corrupt the .pptx file.
/// </summary>
/// <remarks>
/// All builders should go through these helpers rather than instantiating OpenXml classes
/// directly — this keeps the visual style consistent and gives us a single place to harden
/// against XML injection (e.g. text content always passed through the typed API, never as raw XML).
/// </remarks>
internal static class OpenXmlPresentationHelpers
{
    /// <summary>Converts pixels (96 DPI assumption) to EMU.</summary>
    public static long PxToEmu(double px) => (long)(px * 9525.0);

    /// <summary>Converts points (typographic) to EMU.</summary>
    public static long PtToEmu(double pt) => (long)(pt * 12700.0);

    /// <summary>Converts a percentage (0-100) to a per-1000 value, the unit OpenXml uses for many props.</summary>
    public static int PercentToPerMille(double percent) => (int)Math.Round(percent * 1000.0);

    /// <summary>Builds a solid colour fill from a hex string (no leading <c>#</c>).</summary>
    public static A.SolidFill SolidFill(string hex) =>
        new(new A.RgbColorModelHex { Val = NormalizeHex(hex) });

    /// <summary>Builds a no-fill brush (transparent).</summary>
    public static A.NoFill NoFill() => new();

    /// <summary>
    /// Creates a typed run with the supplied text. Uses the OpenXml object model so the resulting
    /// XML is correctly escaped regardless of <paramref name="text"/> content.
    /// </summary>
    public static A.Run TextRun(string text, A.RunProperties? properties = null)
    {
        var run = new A.Run();
        if (properties is not null)
            run.Append(properties);
        run.Append(new A.Text(text ?? string.Empty));
        return run;
    }

    /// <summary>Builds a paragraph that wraps a single styled run with optional bullet styling.</summary>
    public static A.Paragraph Paragraph(string text, A.RunProperties? runProperties = null, A.ParagraphProperties? paragraphProperties = null)
    {
        var paragraph = new A.Paragraph();
        if (paragraphProperties is not null)
            paragraph.Append(paragraphProperties);
        paragraph.Append(TextRun(text, runProperties));
        return paragraph;
    }

    /// <summary>Builds an empty paragraph used as a layout spacer.</summary>
    public static A.Paragraph EmptyParagraph() => new(new A.EndParagraphRunProperties { Language = "fr-FR" });

    public static A.RunProperties RunProperties(
        int fontSizeHundredths,
        string colorHex,
        bool bold = false,
        bool italic = false,
        string? fontFamily = null)
    {
        var rp = new A.RunProperties
        {
            FontSize = fontSizeHundredths,
            Bold = bold,
            Italic = italic,
            Language = "fr-FR"
        };

        rp.Append(SolidFill(colorHex));

        if (!string.IsNullOrWhiteSpace(fontFamily))
        {
            rp.Append(new A.LatinFont { Typeface = fontFamily });
            rp.Append(new A.EastAsianFont { Typeface = fontFamily });
            rp.Append(new A.ComplexScriptFont { Typeface = fontFamily });
        }

        return rp;
    }

    public static A.ParagraphProperties ParagraphProperties(
        A.TextAlignmentTypeValues alignment = A.TextAlignmentTypeValues.Left,
        int indentLevel = 0,
        bool isBullet = false)
    {
        var pp = new A.ParagraphProperties
        {
            Alignment = alignment,
            Level = indentLevel
        };

        if (isBullet)
        {
            pp.Append(new A.BulletFont { Typeface = "Arial" });
            pp.Append(new A.CharacterBullet { Char = "•" });
        }
        else
        {
            pp.Append(new A.NoBullet());
        }

        return pp;
    }

    /// <summary>
    /// Creates a shape positioned at <paramref name="offsetX"/>/<paramref name="offsetY"/> with
    /// dimensions <paramref name="width"/>/<paramref name="height"/> (all in EMU), filled with the
    /// supplied background colour and containing the provided text body.
    /// </summary>
    public static P.Shape Shape(
        uint shapeId,
        string shapeName,
        long offsetX,
        long offsetY,
        long width,
        long height,
        A.SolidFill? fill,
        IEnumerable<A.Paragraph> paragraphs,
        bool wrapText = true,
        A.TextAnchoringTypeValues verticalAnchor = A.TextAnchoringTypeValues.Top,
        long? leftInsetEmu = null,
        long? rightInsetEmu = null,
        long? topInsetEmu = null,
        long? bottomInsetEmu = null)
    {
        var shape = new P.Shape();

        shape.Append(new P.NonVisualShapeProperties(
            new P.NonVisualDrawingProperties { Id = shapeId, Name = shapeName },
            new P.NonVisualShapeDrawingProperties(new A.ShapeLocks { NoGrouping = true }),
            new P.ApplicationNonVisualDrawingProperties()));

        var sp = new P.ShapeProperties();
        sp.Append(new A.Transform2D(
            new A.Offset { X = offsetX, Y = offsetY },
            new A.Extents { Cx = width, Cy = height }));
        sp.Append(new A.PresetGeometry { Preset = A.ShapeTypeValues.Rectangle });
        if (fill is not null)
            sp.Append(fill);
        else
            sp.Append(NoFill());
        sp.Append(new A.Outline(NoFill()) { Width = 0 });
        shape.Append(sp);

        var bodyProperties = new A.BodyProperties
        {
            Anchor = verticalAnchor,
            Wrap = wrapText ? A.TextWrappingValues.Square : A.TextWrappingValues.None,
            LeftInset = (int?)leftInsetEmu,
            RightInset = (int?)rightInsetEmu,
            TopInset = (int?)topInsetEmu,
            BottomInset = (int?)bottomInsetEmu
        };

        var txBody = new P.TextBody(bodyProperties, new A.ListStyle());
        foreach (var paragraph in paragraphs)
            txBody.Append(paragraph);

        shape.Append(txBody);
        return shape;
    }

    /// <summary>
    /// Returns a normalised 6-character hex code (no leading <c>#</c>). Falls back to
    /// <see cref="DefaultDark"/> when the input cannot be parsed, guaranteeing valid OpenXml.
    /// </summary>
    public static string NormalizeHex(string? hex)
    {
        if (string.IsNullOrWhiteSpace(hex))
            return DefaultDark;

        var trimmed = hex.Trim();
        if (trimmed.StartsWith('#'))
            trimmed = trimmed[1..];

        if (trimmed.Length == 3)
            trimmed = $"{trimmed[0]}{trimmed[0]}{trimmed[1]}{trimmed[1]}{trimmed[2]}{trimmed[2]}";

        if (trimmed.Length != 6 || !IsHex(trimmed))
            return DefaultDark;

        return trimmed.ToUpperInvariant();
    }

    private const string DefaultDark = "0F172A";

    private static bool IsHex(ReadOnlySpan<char> s)
    {
        foreach (var c in s)
        {
            var isDigit = c is >= '0' and <= '9';
            var isUpper = c is >= 'A' and <= 'F';
            var isLower = c is >= 'a' and <= 'f';
            if (!isDigit && !isUpper && !isLower)
                return false;
        }
        return true;
    }
}
