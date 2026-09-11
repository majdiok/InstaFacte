using FactuTrust.Domain.Common;

namespace FactuTrust.Domain.Entities.Projects;

/// <summary>Daily motivational tip shown on the billing rate leaderboard (Odoo Tips).</summary>
public sealed class TimesheetTip : Entity
{
    public string Text { get; private set; } = null!;
    public bool IsActive { get; private set; }

    private TimesheetTip() { }

    public static Result<TimesheetTip> Create(string text)
    {
        text = text?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(text))
            return Result.Failure<TimesheetTip>(Error.Validation("Text", "Le texte est obligatoire"));
        if (text.Length > 500)
            text = text[..500];

        return Result.Success(new TimesheetTip { Text = text, IsActive = true });
    }

    public Result Update(string text, bool isActive)
    {
        text = text?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(text))
            return Result.Failure(Error.Validation("Text", "Le texte est obligatoire"));
        Text = text.Length > 500 ? text[..500] : text;
        IsActive = isActive;
        return Result.Success();
    }
}
