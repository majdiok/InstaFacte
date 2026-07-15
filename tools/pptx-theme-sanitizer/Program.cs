using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;

if (args.Length == 0)
{
    Console.Error.WriteLine("Usage: pptx-theme-sanitizer <file-or-directory> [--fix-layout-names]");
    return 1;
}

var fixLayoutNames = args.Contains("--fix-layout-names", StringComparer.OrdinalIgnoreCase);
var targets = args.Where(a => !a.StartsWith('-')).ToList();
if (targets.Count == 0)
{
    Console.Error.WriteLine("No input path provided.");
    return 1;
}

var files = targets
    .SelectMany(ResolvePptxFiles)
    .Distinct(StringComparer.OrdinalIgnoreCase)
    .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
    .ToList();

if (files.Count == 0)
{
    Console.Error.WriteLine("No .pptx files found.");
    return 1;
}

var hadErrors = false;
foreach (var file in files)
{
    Console.WriteLine($"=== {file} ===");
    try
    {
        if (fixLayoutNames)
            NormalizeLayoutNames(file);

        var errors = Validate(file);
        if (errors.Count == 0)
        {
            Console.WriteLine("OK — OpenXml validation passed.");
        }
        else
        {
            hadErrors = true;
            Console.WriteLine($"FAIL — {errors.Count} validation issue(s):");
            foreach (var error in errors.Take(10))
                Console.WriteLine($"  {error.ErrorType} {error.Id}: {error.Description}");
        }

        ReportLayouts(file);
    }
    catch (Exception ex)
    {
        hadErrors = true;
        Console.WriteLine($"ERROR — {ex.Message}");
    }

    Console.WriteLine();
}

return hadErrors ? 2 : 0;

static IEnumerable<string> ResolvePptxFiles(string path)
{
    if (File.Exists(path) && path.EndsWith(".pptx", StringComparison.OrdinalIgnoreCase))
        return new[] { Path.GetFullPath(path) };

    if (!Directory.Exists(path))
    {
        Console.Error.WriteLine($"Path not found: {path}");
        return Array.Empty<string>();
    }

    return Directory.EnumerateFiles(path, "*.pptx", SearchOption.AllDirectories)
        .Where(f => !Path.GetFileName(f).StartsWith("~$", StringComparison.Ordinal));
}

static List<ValidationErrorInfo> Validate(string filePath)
{
    using var stream = File.OpenRead(filePath);
    using var document = PresentationDocument.Open(stream, false);
    var validator = new OpenXmlValidator(DocumentFormat.OpenXml.FileFormatVersions.Office2019);
    return validator.Validate(document).ToList();
}

static void ReportLayouts(string filePath)
{
    using var stream = File.OpenRead(filePath);
    using var document = PresentationDocument.Open(stream, false);
    var master = document.PresentationPart?.SlideMasterParts.FirstOrDefault();
    if (master is null)
    {
        Console.WriteLine("  (no slide master)");
        return;
    }

    var layouts = master.SlideLayoutParts.ToList();
    Console.WriteLine($"  Layouts ({layouts.Count}):");
    foreach (var layout in layouts)
    {
        var name = layout.SlideLayout?.CommonSlideData?.ShapeTree?
            .GetFirstChild<DocumentFormat.OpenXml.Presentation.NonVisualGroupShapeProperties>()?
            .NonVisualDrawingProperties?.Name?.Value ?? "(unnamed)";
        Console.WriteLine($"    - {name}");
    }
}

static void NormalizeLayoutNames(string filePath)
{
    var expected = new[]
    {
        "Layout_Cover",
        "Layout_Section",
        "Layout_TitleAndContent",
        "Layout_TwoColumn",
        "Layout_Credits",
        "Layout_Blank"
    };

    using var stream = new FileStream(filePath, FileMode.Open, FileAccess.ReadWrite);
    using var document = PresentationDocument.Open(stream, true);
    var master = document.PresentationPart?.SlideMasterParts.FirstOrDefault()
        ?? throw new InvalidOperationException("Missing slide master.");

    var layouts = master.SlideLayoutParts.ToList();
    for (var i = 0; i < layouts.Count && i < expected.Length; i++)
    {
        var nv = layouts[i].SlideLayout?.CommonSlideData?.ShapeTree?
            .GetFirstChild<DocumentFormat.OpenXml.Presentation.NonVisualGroupShapeProperties>()?
            .NonVisualDrawingProperties;
        if (nv is not null)
            nv.Name = expected[i];
    }

    document.Save();
    Console.WriteLine("  Applied standard layout names.");
}
