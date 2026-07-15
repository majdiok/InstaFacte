using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.ValueObjects;

namespace FactuTrust.Domain.Services;

public static class NumberingFormatRenderer
{
    public const int MaxRenderedLength = 50;

    public sealed record RenderContext(
        int Sequence,
        DateTime ReferenceDate,
        string? FreeTextOverride = null);

    public static Result<string> Render(
        IReadOnlyList<NumberingFormatBlock> blocks,
        RenderContext context)
    {
        if (blocks.Count == 0)
            return Result.Failure<string>(Error.Validation("Format", "Le format de numerotation est vide."));

        if (!blocks.Any(b => b.Type.IsDocumentNumberBlock()))
            return Result.Failure<string>(Error.Validation("Format",
                "Le format doit contenir au moins un bloc numero de document."));

        var ordered = blocks.OrderBy(b => b.Order).ToList();
        var parts = new List<string>(ordered.Count);

        foreach (var block in ordered)
        {
            var partResult = RenderBlock(block, context);
            if (partResult.IsFailure)
                return partResult;
            parts.Add(partResult.Value);
        }

        var rendered = string.Concat(parts);
        if (rendered.Length > MaxRenderedLength)
            return Result.Failure<string>(Error.Validation("Format",
                $"Le numero genere depasse {MaxRenderedLength} caracteres."));

        return Result.Success(rendered);
    }

    public static Result ValidateBlocks(IReadOnlyList<NumberingFormatBlock> blocks)
    {
        if (blocks.Count == 0)
            return Result.Failure(Error.Validation("Format", "Le format de numerotation est vide."));

        if (!blocks.Any(b => b.Type.IsDocumentNumberBlock()))
            return Result.Failure(Error.Validation("Format",
                "Le format doit contenir au moins un bloc numero de document."));

        foreach (var block in blocks)
        {
            switch (block.Type)
            {
                case NumberingBlockType.FreeText:
                    if (string.IsNullOrWhiteSpace(block.Value))
                        return Result.Failure(Error.Validation("FreeText", "Le texte libre est obligatoire."));
                    if (block.Value.Trim().Length > 20)
                        return Result.Failure(Error.Validation("FreeText",
                            "Le texte libre ne peut pas depasser 20 caracteres."));
                    break;
                case NumberingBlockType.Separator:
                    if (block.Value is not ("-" or "/"))
                        return Result.Failure(Error.Validation("Separator",
                            "Le separateur doit etre '-' ou '/'."));
                    break;
            }
        }

        return Result.Success();
    }

    private static Result<string> RenderBlock(NumberingFormatBlock block, RenderContext context)
    {
        return block.Type switch
        {
            NumberingBlockType.FreeText => Result.Success(
                (context.FreeTextOverride ?? block.Value ?? string.Empty).Trim().ToUpperInvariant()),
            NumberingBlockType.Separator => Result.Success(block.Value ?? "-"),
            NumberingBlockType.DocumentNumber => Result.Success(context.Sequence.ToString()),
            NumberingBlockType.DocumentNumberPadded3 => Result.Success(context.Sequence.ToString("D3")),
            NumberingBlockType.DocumentNumberPadded4 => Result.Success(context.Sequence.ToString("D4")),
            NumberingBlockType.DocumentNumberPadded5 => Result.Success(context.Sequence.ToString("D5")),
            NumberingBlockType.DocumentNumberPadded6 => Result.Success(context.Sequence.ToString("D6")),
            NumberingBlockType.Day => Result.Success(context.ReferenceDate.Day.ToString("D2")),
            NumberingBlockType.Month => Result.Success(context.ReferenceDate.Month.ToString("D2")),
            NumberingBlockType.Year4 => Result.Success(context.ReferenceDate.Year.ToString("D4")),
            NumberingBlockType.Year2 => Result.Success((context.ReferenceDate.Year % 100).ToString("D2")),
            _ => Result.Failure<string>(Error.Validation("BlockType", $"Type de bloc inconnu: {block.Type}"))
        };
    }
}