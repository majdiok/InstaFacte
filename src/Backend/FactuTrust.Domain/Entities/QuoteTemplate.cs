using FactuTrust.Domain.Common;
using FactuTrust.Domain.ValueObjects;

namespace FactuTrust.Domain.Entities;

public sealed class QuoteTemplate : AggregateRoot
{
    public string Name { get; private set; } = null!;
    public string? Description { get; private set; }
    public string? DefaultNotes { get; private set; }
    public string? DefaultTermsAndConditions { get; private set; }
    public int DefaultValidityDays { get; private set; }
    public bool IsActive { get; private set; }
    public int UsageCount { get; private set; }

    private readonly List<QuoteTemplateLine> _lines = new();
    public IReadOnlyCollection<QuoteTemplateLine> Lines => _lines.AsReadOnly();

    private QuoteTemplate() { }

    public static Result<QuoteTemplate> Create(
        string name,
        string? description = null,
        string? defaultNotes = null,
        string? defaultTermsAndConditions = null,
        int defaultValidityDays = 30)
    {
        name = name?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(name))
            return Result.Failure<QuoteTemplate>(Error.Validation("Name", "Le nom du modèle est obligatoire"));

        if (defaultValidityDays < 1)
            return Result.Failure<QuoteTemplate>(Error.Validation("DefaultValidityDays", "La validité doit être d'au moins 1 jour"));

        return Result.Success(new QuoteTemplate
        {
            Name = name,
            Description = description?.Trim(),
            DefaultNotes = defaultNotes?.Trim(),
            DefaultTermsAndConditions = defaultTermsAndConditions?.Trim(),
            DefaultValidityDays = defaultValidityDays,
            IsActive = true,
            UsageCount = 0
        });
    }

    public Result Update(string name, string? description, string? defaultNotes, string? defaultTermsAndConditions, int defaultValidityDays)
    {
        name = name?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(name))
            return Result.Failure(Error.Validation("Name", "Le nom du modèle est obligatoire"));

        Name = name;
        Description = description?.Trim();
        DefaultNotes = defaultNotes?.Trim();
        DefaultTermsAndConditions = defaultTermsAndConditions?.Trim();
        DefaultValidityDays = defaultValidityDays < 1 ? 30 : defaultValidityDays;
        return Result.Success();
    }

    public void AddLine(QuoteTemplateLine line)
    {
        _lines.Add(line);
    }

    public void ClearLines() => _lines.Clear();

    public void IncrementUsage() => UsageCount++;

    public void Deactivate() => IsActive = false;
    public void Reactivate() => IsActive = true;
}
