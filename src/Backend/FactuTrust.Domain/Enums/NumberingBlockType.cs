namespace FactuTrust.Domain.Enums;

public enum NumberingBlockType
{
    FreeText = 0,
    Separator = 1,
    DocumentNumber = 2,
    DocumentNumberPadded3 = 3,
    DocumentNumberPadded4 = 4,
    DocumentNumberPadded5 = 5,
    DocumentNumberPadded6 = 6,
    Day = 7,
    Month = 8,
    Year4 = 9,
    Year2 = 10
}

public static class NumberingBlockTypeExtensions
{
    public static bool IsDocumentNumberBlock(this NumberingBlockType type) =>
        type is NumberingBlockType.DocumentNumber
            or NumberingBlockType.DocumentNumberPadded3
            or NumberingBlockType.DocumentNumberPadded4
            or NumberingBlockType.DocumentNumberPadded5
            or NumberingBlockType.DocumentNumberPadded6;

    public static string ToDisplayString(this NumberingBlockType type) => type switch
    {
        NumberingBlockType.FreeText => "Texte libre",
        NumberingBlockType.Separator => "Separateur",
        NumberingBlockType.DocumentNumber => "Numero de document",
        NumberingBlockType.DocumentNumberPadded3 => "Numero de document a 3 chiffres",
        NumberingBlockType.DocumentNumberPadded4 => "Numero de document a 4 chiffres",
        NumberingBlockType.DocumentNumberPadded5 => "Numero de document a 5 chiffres",
        NumberingBlockType.DocumentNumberPadded6 => "Numero de document a 6 chiffres",
        NumberingBlockType.Day => "Jour (JJ)",
        NumberingBlockType.Month => "Mois (MM)",
        NumberingBlockType.Year4 => "Annee (AAAA)",
        NumberingBlockType.Year2 => "Annee (AA)",
        _ => type.ToString()
    };
}